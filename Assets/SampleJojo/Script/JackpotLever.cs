using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Levier de machine à sous pour la VR (XR Interaction Toolkit 3).
/// - Le levier suit la main petit à petit, dans les deux sens, autour de l'axe du cylindre bleu.
/// - Quand on le lâche, il remonte tout seul.
/// - Arrivé presque en bas, il lance les rouleaux (une seule fois par tirage).
/// À mettre sur le levier (Cylinder.007), dont l'origine est sur l'axe du cylindre bleu.
/// </summary>
[DisallowMultipleComponent]
public class JackpotLever : XRBaseInteractable
{
    [Header("Levier")]
    [Tooltip("Le levier qui tourne. Vide = cet objet.")]
    public Transform lever;
    [Tooltip("Course maximale du levier, en degrés.")]
    public float maxAngle = 70f;
    [Tooltip("Avant de la machine (côté joueur), dans le repère du parent du levier.")]
    public Vector3 frontLocal = Vector3.forward;
    [Tooltip("Coche si le levier part dans le mauvais sens.")]
    public bool invertPull = false;
    [Range(0.3f, 1f)] public float triggerRatio = 0.85f;
    [Range(0f, 0.6f)] public float rearmRatio = 0.25f;
    [Tooltip("Vitesse de retour quand on lâche le levier (degrés/seconde).")]
    public float returnSpeed = 220f;

    [Header("Rouleaux (gauche, milieu, droite)")]
    public Transform[] reels;
    [Tooltip("Coche si les rouleaux tournent dans le mauvais sens.")]
    public bool invertReels = false;
    public float[] stopTimes = { 2.4f, 3.1f, 3.8f };
    public float maxSpeed = 900f;
    public float rampUp = 0.5f;
    public float rampDown = 1.0f;
    [Tooltip("Nombre de symboles sur chaque rouleau (les rouleaux s'arrêtent sur l'un d'eux au hasard).")]
    public int symbolsPerReel = 6;

    [System.Serializable]
    public class ReelStrip
    {
        [Tooltip("Symboles du rouleau, dans l'ordre des crans (le cran 0 est celui de départ).")]
        public string[] symbols;
    }

    [System.Serializable]
    public class PayRule
    {
        public string symbol;
        [Tooltip("Gain (en valeur de pièces) pour 3 fois ce symbole.")]
        public int triple;
        [Tooltip("Gain pour 2 fois ce symbole.")]
        public int pair;
        [Tooltip("Poids quand un triple est forcé (plus petit = plus rare).")]
        public float weight = 1f;
    }

    [Header("Gain")]
    [Tooltip("Symboles de chaque rouleau (même ordre que Reels).")]
    public ReelStrip[] strips;
    [Tooltip("Symbole du gros jackpot.")]
    public string jackpotSymbol = "mmi";
    [Tooltip("Chance de forcer un triple (symbole tiré selon les poids) à chaque tirage.")]
    [Range(0f, 1f)] public float winChance = 0.1f;
    [Tooltip("Table des gains : 3 identiques / 2 identiques.")]
    public PayRule[] payTable =
    {
        new PayRule { symbol = "mmi",     triple = 200, pair = 10, weight = 1f },
        new PayRule { symbol = "claude",  triple = 100, pair = 5, weight = 2f },
        new PayRule { symbol = "izly",    triple = 60,  pair = 5, weight = 3f },
        new PayRule { symbol = "crous",   triple = 40,  pair = 5, weight = 4f },
        new PayRule { symbol = "couteau", triple = 25,  pair = 5, weight = 5f },
    };
    [Tooltip("Petit gain si un seul symbole du jackpot est sorti.")]
    public int singleJackpotSymbol = 5;

    [Header("Résultat du dernier tirage")]
    public int[] lastResult;
    public bool lastWasWin;
    public int lastPayout;

    [Header("Événements (son, lumières, gain...)")]
    public UnityEvent onPulled;
    public UnityEvent<int> onReelStopped;
    [Tooltip("3 symboles identiques (lumières + jingle).")]
    public UnityEvent onWin;
    [Tooltip("Gain en valeur de pièces (appelé dès qu'on gagne quelque chose).")]
    public UnityEvent<int> onPayout;
    public UnityEvent onLose;
    public UnityEvent onFinished;

    [Header("État (lecture seule)")]
    [SerializeField, Range(0f, 1f)] float pull01;

    Transform space;
    Vector3 pivot, axis, restDir;
    Quaternion leverRest;
    Quaternion[] reelRest;
    Vector3[] reelAxis;
    float[] reelOffset;
    float sign = 1f, reelSign = 1f, amount, grabOffset;
    IXRSelectInteractor grabber;
    bool armed = true, spinning, simulating, initialized;

    public float Pull01 => pull01;
    public bool IsSpinning => spinning;

    // Le levier ne se tire que si la machine est posée. Un tirage déjà lancé va toujours jusqu'au bout.
    bool usable = true;
    public bool Usable => usable;

    // ---------------- Crédits (pièces insérées) ----------------
    [Header("Pièces")]
    [Tooltip("Il faut insérer une pièce pour jouer (1 crédit par unité de valeur, 1 tirage = 1 crédit).")]
    public bool needsCoins = true;
    public int credits;
    public UnityEvent<int> onCreditsChanged = new UnityEvent<int>();

    /// <summary>Machine allumée : elle a des crédits (ou un tirage est en cours).</summary>
    public bool Powered => !needsCoins || credits > 0 || spinning;

    public void AddCredits(int n)
    {
        credits = Mathf.Max(0, credits + n);
        onCreditsChanged?.Invoke(credits);
    }

    public void SetUsable(bool value)
    {
        if (usable == value) return;
        usable = value;
        if (!value && isSelected && interactionManager != null)
            foreach (var s in new List<IXRSelectInteractor>(interactorsSelecting))
                interactionManager.SelectExit(s, (IXRSelectInteractable)this);
        if (!value) grabber = null;      // le levier remonte tout seul
    }

    public override bool IsSelectableBy(IXRSelectInteractor interactor) => usable && Powered && base.IsSelectableBy(interactor);
    public override bool IsHoverableBy(IXRHoverInteractor interactor) => usable && Powered && base.IsHoverableBy(interactor);

    void Start()
    {
        // Crédits gardés du cours précédent.
        if (needsCoins && GameProgress.CarryCoins && GameProgress.Credits > 0) AddCredits(GameProgress.Credits);
    }

    protected override void Awake()
    {
        if (lever == null) lever = transform;
        if (GetComponentInChildren<Collider>() == null)
        {
            var bc = gameObject.AddComponent<BoxCollider>();
            bc.size *= 1.3f;
        }
        base.Awake();
        Init();
    }

    Vector3 ToSpace(Vector3 world) => space != null ? space.InverseTransformPoint(world) : world;

    void Init()
    {
        space = lever.parent;
        leverRest = lever.localRotation;
        pivot = lever.localPosition;

        if (reels == null || reels.Length == 0) reels = FindReels();

        // Axe de rotation = alignement des rouleaux (même axe que le cylindre bleu).
        axis = Vector3.right;
        if (reels.Length >= 2)
        {
            Vector3 d = ToSpace(reels[reels.Length - 1].position) - ToSpace(reels[0].position);
            if (d.sqrMagnitude > 1e-10f) axis = d.normalized;
        }

        // Direction de repos du levier = de l'axe vers le centre de la tige.
        restDir = Vector3.up;
        var mf = lever.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            Vector3 c = ToSpace(lever.TransformPoint(mf.sharedMesh.bounds.center)) - pivot;
            c = Vector3.ProjectOnPlane(c, axis);
            if (c.sqrMagnitude > 1e-10f) restDir = c;
        }
        restDir = Vector3.ProjectOnPlane(restDir, axis).normalized;

        // Sens du tirage : le bout du levier va vers l'avant de la machine.
        Vector3 front = Vector3.ProjectOnPlane(frontLocal, axis).normalized;
        Vector3 moved = Quaternion.AngleAxis(maxAngle, axis) * restDir;
        sign = Vector3.Dot(moved - restDir, front) >= 0f ? 1f : -1f;
        if (invertPull) sign = -sign;

        // Sens des rouleaux : la face avant descend, comme sur une vraie machine.
        Vector3 f2 = Quaternion.AngleAxis(5f, axis) * front;
        reelSign = f2.y < front.y ? 1f : -1f;
        if (invertReels) reelSign = -reelSign;

        reelRest = new Quaternion[reels.Length];
        reelAxis = new Vector3[reels.Length];
        reelOffset = new float[reels.Length];
        lastResult = new int[reels.Length];
        Vector3 axisWorld = space != null ? space.TransformDirection(axis) : axis;
        for (int i = 0; i < reels.Length; i++)
        {
            reelRest[i] = reels[i].localRotation;
            Transform p = reels[i].parent;
            reelAxis[i] = (p != null ? p.InverseTransformDirection(axisWorld) : axisWorld).normalized;
        }

        initialized = true;
        Debug.Log($"[Jackpot] Levier prêt : axe {axis}, repos {restDir}, sens {sign}, {reels.Length} rouleaux.");
    }

    Transform[] FindReels()
    {
        string[] names = { "Cylinder.005", "Cylinder.008", "Cylinder.004" };
        var list = new List<Transform>();
        Transform root = lever.root;
        foreach (var n in names)
        {
            var t = FindDeep(root, n);
            if (t != null) list.Add(t);
        }
        return list.ToArray();
    }

    static Transform FindDeep(Transform r, string n)
    {
        if (r.name == n) return r;
        foreach (Transform c in r)
        {
            var f = FindDeep(c, n);
            if (f != null) return f;
        }
        return null;
    }

    // Machine rangée dans le sac pendant un tirage : on remet tout au propre.
    protected override void OnDisable()
    {
        base.OnDisable();
        StopAllCoroutines();
        if (!initialized) return;
        if (spinning)
        {
            for (int i = 0; i < reels.Length; i++)
                reels[i].localRotation = Quaternion.AngleAxis(reelSign * reelOffset[i], reelAxis[i]) * reelRest[i];
            spinning = false;
            // Rangée avant la fin du tirage : pas de gain, mais on récupère le crédit.
            if (needsCoins && Application.isPlaying && gameObject.scene.isLoaded) { AddCredits(1); Debug.Log("[Jackpot] Machine rangée pendant le tirage : crédit rendu."); }
        }
        simulating = false;
        grabber = null;
        amount = 0f;
        pull01 = 0f;
        armed = true;
        lever.localRotation = leverRest;
    }

    // ---------------- Interaction VR ----------------
    protected override void OnSelectEntered(SelectEnterEventArgs args)
    {
        base.OnSelectEntered(args);
        grabber = args.interactorObject;
        grabOffset = amount - HandAmount();   // pas de saut quand on attrape
    }

    protected override void OnSelectExited(SelectExitEventArgs args)
    {
        base.OnSelectExited(args);
        if (args.interactorObject == grabber) grabber = null;
    }

    float HandAmount()
    {
        if (grabber == null) return amount;
        Vector3 hand = ToSpace(grabber.GetAttachTransform(this).position) - pivot;
        hand = Vector3.ProjectOnPlane(hand, axis);
        if (hand.sqrMagnitude < 1e-12f) return amount;
        return Vector3.SignedAngle(restDir, hand, axis) * sign;
    }

    public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase updatePhase)
    {
        base.ProcessInteractable(updatePhase);
        if (!initialized || simulating || updatePhase != XRInteractionUpdateOrder.UpdatePhase.Dynamic) return;

        if (grabber != null) SetAmount(HandAmount() + grabOffset);
        else if (amount > 0f) SetAmount(Mathf.MoveTowards(amount, 0f, returnSpeed * Time.deltaTime));
    }

    void SetAmount(float a)
    {
        amount = Mathf.Clamp(a, 0f, maxAngle);
        pull01 = maxAngle > 0f ? amount / maxAngle : 0f;
        lever.localRotation = Quaternion.AngleAxis(sign * amount, axis) * leverRest;

        if (usable && armed && !spinning && pull01 >= triggerRatio && (!needsCoins || credits > 0))
        {
            armed = false;
            StartSpin();
        }
        else if (!armed && !spinning && pull01 <= rearmRatio)
        {
            armed = true;
        }
    }

    // ---------------- Rouleaux ----------------
    [ContextMenu("Lancer les rouleaux")]
    public void StartSpin()
    {
        if (spinning || !Application.isPlaying) return;
        if (needsCoins)
        {
            if (credits <= 0) { Debug.Log("[Jackpot] Pas de crédit : insère une pièce."); return; }
            AddCredits(-1);
        }
        Debug.Log("[Jackpot] Levier en bas : rouleaux lancés !");
        onPulled?.Invoke();
        StartCoroutine(SpinRoutine());
    }

    IEnumerator SpinRoutine()
    {
        spinning = true;
        float K = 2f / Mathf.PI;
        float t0 = Time.time;
        int n = reels.Length;
        float maxStop = 0f;
        float[] w = new float[n], a1 = new float[n], a2 = new float[n], total = new float[n], stop = new float[n];
        int steps = Mathf.Max(1, symbolsPerReel);
        float step = 360f / steps;

        // Tirage selon l'équilibrage (GameBalance) : on décide d'abord le résultat, puis les rouleaux s'arrêtent dessus.
        int[] target = null;
        bool force = forceWinOnce;
        forceWinOnce = false;
        bool stripsOk = strips != null && strips.Length == n && n > 0 && strips[0].symbols != null && strips[0].symbols.Length > 0;
        if (stripsOk)
        {
            string sym = useBalance ? RollBalance(force) : (force || Random.value < winChance) ? PickTripleSymbol() : null;
            if (sym != null)
            {
                target = new int[n];
                for (int i = 0; i < n; i++)
                {
                    target[i] = strips[i].symbols != null ? System.Array.IndexOf(strips[i].symbols, sym) : -1;
                    if (target[i] < 0) { target = null; break; }
                }
            }
            else if (useBalance) target = LosingCombo(n);
        }

        for (int i = 0; i < n; i++)
        {
            stop[i] = stopTimes[Mathf.Min(i, stopTimes.Length - 1)];
            maxStop = Mathf.Max(maxStop, stop[i]);
            float tLin = Mathf.Max(0.2f, stop[i] - rampUp - rampDown);
            float span = K * (rampUp + rampDown) + tLin;
            float turns = Mathf.Max(1f, Mathf.Round(maxSpeed * span / 360f));
            int cur = ((Mathf.RoundToInt(reelOffset[i] / step) % steps) + steps) % steps;
            int extra = target != null
                ? ((target[i] - cur) % steps + steps) % steps          // vise le symbole du jackpot
                : Random.Range(0, steps);                             // symbole d'arrivée au hasard
            total[i] = turns * 360f + extra * step;             // s'arrête pile sur un symbole
            w[i] = total[i] / span;
            a1[i] = K * w[i] * rampUp;
            a2[i] = a1[i] + w[i] * tLin;
            lastResult[i] = (cur + extra) % steps;
        }

        bool[] stopped = new bool[n];
        while (Time.time - t0 < maxStop)
        {
            float t = Time.time - t0;
            for (int i = 0; i < n; i++)
            {
                if (!stopped[i] && t > stop[i])
                {
                    stopped[i] = true;
                    onReelStopped?.Invoke(i);
                }
                float tDown0 = stop[i] - rampDown;
                float ang;
                if (t <= rampUp) ang = a1[i] * (1f - Mathf.Cos(Mathf.PI * t / rampUp / 2f));
                else if (t <= tDown0) ang = a1[i] + w[i] * (t - rampUp);
                else if (t <= stop[i]) ang = a2[i] + K * w[i] * rampDown * Mathf.Sin(Mathf.PI * (t - tDown0) / rampDown / 2f);
                else ang = total[i];
                reels[i].localRotation = Quaternion.AngleAxis(reelSign * (reelOffset[i] + ang), reelAxis[i]) * reelRest[i];
            }
            yield return null;
        }

        for (int i = 0; i < n; i++)
        {
            reelOffset[i] = Mathf.Repeat(reelOffset[i] + total[i], 360f);
            reels[i].localRotation = Quaternion.AngleAxis(reelSign * reelOffset[i], reelAxis[i]) * reelRest[i];
        }
        for (int i = 0; i < n; i++)
            if (!stopped[i]) onReelStopped?.Invoke(i);

        var names = new string[n];
        for (int i = 0; i < n; i++) names[i] = SymbolAt(i);
        lastPayout = useBalance ? BalancePayout(names, out lastWasWin) : ComputePayout(names, out lastWasWin);
        // La dopamine multiplie les gains (x1 à x2).
        if (lastPayout > 0 && AdrenalineEffect.Active != null) lastPayout = Mathf.RoundToInt(lastPayout * AdrenalineEffect.Active.WinMultiplier);
        // Gagné : la dopamine monte ; perdu : elle baisse.
        if (AdrenalineEffect.Active != null) AdrenalineEffect.Active.OnSpinResult(lastPayout > 0);

        spinning = false;
        if (pull01 <= rearmRatio) armed = true;
        for (int i = 0; i < n; i++) names[i] = names[i] ?? lastResult[i].ToString();
        Debug.Log("[Jackpot] Rouleaux arrêtés : " + string.Join(" | ", names) +
                  (lastPayout > 0 ? $"  => gain {lastPayout}" + (lastWasWin ? " (3 identiques !)" : "") : ""));
        if (lastWasWin) onWin?.Invoke();
        if (lastPayout > 0) onPayout?.Invoke(lastPayout);
        else onLose?.Invoke();
        onFinished?.Invoke();
    }

    [Header("Équilibrage")]
    [Tooltip("Utilise les chances et gains de GameBalance (3 symboles identiques = gain, sinon perdu).")]
    public bool useBalance = true;

    /// <summary>Résultat du tirage : le symbole gagnant (3 fois), ou null = perdu.</summary>
    string RollBalance(bool forceWin)
    {
        var odds = GameBalance.Slot;
        float total = 0f; foreach (var o in odds) total += o.chance;
        float x = Random.value * (forceWin ? total : 1f);
        foreach (var o in odds) { if (x < o.chance) return o.symbol; x -= o.chance; }
        return null;
    }

    /// <summary>Combinaison perdante : jamais 3 symboles identiques.</summary>
    int[] LosingCombo(int n)
    {
        var t = new int[n];
        for (int tries = 0; tries < 50; tries++)
        {
            for (int i = 0; i < n; i++) t[i] = Random.Range(0, strips[i].symbols.Length);
            bool same = true;
            for (int i = 1; i < n; i++) if (strips[i].symbols[t[i]] != strips[0].symbols[t[0]]) same = false;
            if (!same || n < 2) return t;
        }
        return t;
    }

    int BalancePayout(string[] syms, out bool triple)
    {
        triple = syms.Length > 0 && syms[0] != null;
        for (int i = 1; i < syms.Length; i++) if (syms[i] != syms[0]) triple = false;
        if (!triple) return 0;
        foreach (var o in GameBalance.Slot) if (o.symbol == syms[0]) return o.payout;
        return 0;
    }

    PayRule Rule(string s)
    {
        if (payTable == null || s == null) return null;
        foreach (var r in payTable) if (r != null && r.symbol == s) return r;
        return null;
    }

    /// <summary>Gain du tirage : 3 identiques, sinon 2 identiques, sinon un symbole du jackpot.</summary>
    int ComputePayout(string[] syms, out bool triple)
    {
        triple = false;
        var count = new Dictionary<string, int>();
        foreach (var s in syms) if (s != null) count[s] = count.TryGetValue(s, out int c) ? c + 1 : 1;
        int best = 0;
        foreach (var kv in count)
        {
            var r = Rule(kv.Key);
            if (r == null) continue;
            if (kv.Value >= 3 && r.triple > best) { best = r.triple; triple = true; }
            else if (kv.Value == 2 && r.pair > best) best = r.pair;
        }
        if (best == 0 && !string.IsNullOrEmpty(jackpotSymbol) && count.ContainsKey(jackpotSymbol)) best = singleJackpotSymbol;
        return best;
    }

    string PickTripleSymbol()
    {
        float total = 0f;
        if (payTable != null) foreach (var r in payTable) if (r != null && r.weight > 0f) total += r.weight;
        if (total <= 0f) return !string.IsNullOrEmpty(jackpotSymbol) ? jackpotSymbol : strips[0].symbols[Random.Range(0, strips[0].symbols.Length)];
        float x = Random.value * total;
        foreach (var r in payTable)
        {
            if (r == null || r.weight <= 0f) continue;
            x -= r.weight;
            if (x <= 0f) return r.symbol;
        }
        return payTable[payTable.Length - 1].symbol;
    }

    string SymbolAt(int i)
    {
        if (strips == null || i >= strips.Length || strips[i] == null || strips[i].symbols == null) return null;
        int k = lastResult[i];
        return k >= 0 && k < strips[i].symbols.Length ? strips[i].symbols[k] : null;
    }

    // ---------------- Test sans casque ----------------
    [ContextMenu("Test : tirer le levier (en Play)")]
    public void TestPull()
    {
        if (Application.isPlaying && (!isActiveAndEnabled || !usable))
        {
            Debug.Log("[Jackpot] Levier inactif : la machine doit être posée sur son emplacement.");
            return;
        }
        if (Application.isPlaying && initialized && !simulating) StartCoroutine(SimulatePull());
    }

    bool forceWinOnce;

    [ContextMenu("Test : jackpot garanti (en Play)")]
    public void TestWin()
    {
        if (!Application.isPlaying || !initialized || spinning) return;
        forceWinOnce = true;
        TestPull();
    }

    IEnumerator SimulatePull()
    {
        simulating = true;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.8f)
        {
            SetAmount(Mathf.SmoothStep(0f, maxAngle, t));
            yield return null;
        }
        SetAmount(maxAngle);
        yield return new WaitForSeconds(0.3f);
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.8f)
        {
            SetAmount(Mathf.SmoothStep(maxAngle, 0f, t));
            yield return null;
        }
        SetAmount(0f);
        simulating = false;
    }
}
