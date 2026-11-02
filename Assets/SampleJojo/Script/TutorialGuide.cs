using System.Collections;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Tuto pas à pas de la première partie (après JOUER) :
/// 1. réviser : tourner une page du livre
/// 2. le sac apparaît : sortir la machine et la poser sur la table
/// 3. on reçoit 6 pièces dans le bocal, la calculatrice les compte
/// 4. en mettre une dans la machine, puis tirer le levier
/// 5. la dopamine : ce que c'est (bords roses, gains multipliés, elle baisse avec le temps)
/// 6. la prof apparaît : ranger la machine dans le sac
/// 5. « Tu as bien compris ? Tu as 3 minutes » → C'EST PARTI : le premier cours commence.
/// </summary>
public class TutorialGuide : MonoBehaviour
{
    [Header("Éléments du jeu (trouvés tout seuls si vides)")]
    public JackpotBook book;
    public JackpotBag bag;
    public JackpotItem machine;
    public JackpotLever lever;
    public CoinSlot slot;
    public CoinJar jar;
    public ProfAI prof;
    public CoinCounterDisplay calculator;

    [Header("Réglages")]
    public int tutorialCoins = 6;
    public TMP_FontAsset font;
    public AudioClip messageClip;
    public AudioClip popClip;
    public AudioClip clickClip;

    enum Step { Off, Book, TakeOut, Calc, Coin, Pull, Dopamine, Store, Ready, Done }
    Step step = Step.Off;
    GameMenu menu;
    Transform cam, card, arrow, arrowTarget;
    TMP_Text stepText, bodyText;
    GameObject button;
    AudioSource audioSource;
    bool pageTurned, spinDone;
    float calcTimer;
    [Tooltip("Temps de l'explication de la calculatrice (s).")]
    public float calcSeconds = 7f;
    [Tooltip("Temps de l'explication de la dopamine (s).")]
    public float dopamineSeconds = 11f;
    float dopTimer;

    Vector3? CalcPoint() => calculator != null ? Top(calculator.gameObject) : null;
    float arrowLift = 0.12f;

    public bool Running => step != Step.Off && step != Step.Done;

    void Awake()
    {
        if (book == null) book = FindFirstObjectByType<JackpotBook>(FindObjectsInactive.Include);
        if (bag == null) bag = FindFirstObjectByType<JackpotBag>(FindObjectsInactive.Include);
        if (machine == null) machine = FindFirstObjectByType<JackpotItem>(FindObjectsInactive.Include);
        if (lever == null) lever = FindFirstObjectByType<JackpotLever>(FindObjectsInactive.Include);
        if (slot == null) slot = FindFirstObjectByType<CoinSlot>(FindObjectsInactive.Include);
        if (jar == null) jar = FindFirstObjectByType<CoinJar>(FindObjectsInactive.Include);
        if (prof == null) prof = FindFirstObjectByType<ProfAI>(FindObjectsInactive.Include);
        if (calculator == null) calculator = FindFirstObjectByType<CoinCounterDisplay>(FindObjectsInactive.Include);
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false; audioSource.spatialBlend = 0f;
    }

    // ================= Déroulé =================
    public void Begin(GameMenu from)
    {
        menu = from;
        tutorialCoins = GameBalance.StartCoins;
        var origin = FindFirstObjectByType<XROrigin>();
        cam = origin != null && origin.Camera != null ? origin.Camera.transform : Camera.main != null ? Camera.main.transform : null;
        BuildCard();
        BuildArrow();

        // Au début : pas de sac, pas de machine, pas de prof, bocal vide.
        if (bag != null) bag.gameObject.SetActive(false);
        if (machine != null) machine.gameObject.SetActive(false);
        if (prof != null) prof.gameObject.SetActive(false);
        if (jar != null) jar.Clear();
        if (bag != null) bag.SetCoins(0);

        if (book != null) book.onPageTurned.AddListener(_ => pageTurned = true);
        if (lever != null) lever.onFinished.AddListener(() => spinDone = true);
        Go(Step.Book);
    }

    void Go(Step s)
    {
        step = s;
        Debug.Log("[Tuto] Étape : " + s);
        switch (s)
        {
            case Step.Book:
                Say("1 / 7", "C'est l'heure de <b>réviser</b> !\nTourne une page du livre :\npince la page de droite et tire-la vers la gauche.", Top(book != null ? book.gameObject : null));
                break;
            case Step.TakeOut:
                StartCoroutine(Pop(bag != null ? bag.gameObject : null));
                if (machine != null) machine.gameObject.SetActive(true);
                Say("2 / 7", "Ton <b>sac</b> est là !\nDedans il y a ta machine à sous.\nPlonge la main dans le sac, sors-la\net pose-la sur ta table.", bag != null ? bag.HandlePoint : (Vector3?)null);
                break;
            case Step.Calc:
                // Les pièces apparaissent et tombent dans le bocal.
                if (jar != null) jar.Deposit(new System.Collections.Generic.List<int>(System.Linq.Enumerable.Repeat(1, tutorialCoins)));
                calcTimer = calcSeconds;
                Say("3 / 7", $"Tiens, voilà <b>{tutorialCoins} pièces</b> dans ton bocal !\nTa <b>calculatrice</b> compte toutes tes pièces :\nregarde, elle affiche {tutorialCoins}.\nQuand tu gagnes, le total monte.", CalcPoint());
                break;
            case Step.Coin:
                Say("4 / 7", "La machine marche avec des <b>pièces</b>.\nPrends une pièce dans le bocal\net mets-la dans la <b>fente</b>\nsur le devant de la machine.", slot != null ? slot.transform.position : (Vector3?)null);
                break;
            case Step.Pull:
                spinDone = false;
                Say("4 / 7", "Bien joué ! La machine est allumée.\nMaintenant <b>tire le levier</b> !", lever != null ? (lever.lever != null ? lever.lever : lever.transform).position : (Vector3?)null);
                break;
            case Step.Dopamine:
                dopTimer = dopamineSeconds;
                Say("5 / 7", "Tu sens ton cœur battre ? C'est la <b><color=#E0368F>dopamine</color></b> !\n" +
                    "Chaque tirage en donne (beaucoup quand tu gagnes, un peu quand tu perds)\n" +
                    "et les bords de ta vue deviennent roses.\n" +
                    "Plus tu en as, plus tes gains sont <b>multipliés</b> (jusqu'à x2).\n" +
                    "Mais elle <b>baisse avec le temps</b> : continue de jouer !", null);
                break;
            case Step.Store:
                StartCoroutine(Pop(prof != null ? prof.gameObject : null));
                Say("6 / 7", "<color=#D8204A><b>Attention !</b></color> La prof surveille...\nPense à <b>ranger ta machine dans le sac</b>\ndès qu'elle regarde vers toi !\nRange-la maintenant.", bag != null ? bag.HandlePoint : (Vector3?)null);
                break;
            case Step.Ready:
                Say("7 / 7", $"Tu as bien compris ?\nObjectif du niveau (le <b>post-it</b>) : <b>{GameBalance.GoalFor(GameProgress.Chapter)} pièces</b>\nà la fin de l'examen, sans te faire attraper par la prof !", null);
                if (button != null) button.SetActive(true);
                break;
        }
    }

    void Update()
    {
        if (step == Step.Off || step == Step.Done) return;
        switch (step)
        {
            case Step.Book: if (pageTurned) Go(Step.TakeOut); break;
            case Step.TakeOut: if (machine != null && machine.IsPlaced) Go(Step.Calc); break;
            case Step.Calc: calcTimer -= Time.deltaTime; if (calcTimer <= 0f) Go(Step.Coin); break;
            case Step.Coin: if (lever != null && (lever.credits > 0 || lever.IsSpinning)) Go(Step.Pull); break;
            case Step.Pull: if (spinDone && lever != null && !lever.IsSpinning) Go(Step.Dopamine); break;
            case Step.Dopamine: dopTimer -= Time.deltaTime; if (dopTimer <= 0f) Go(Step.Store); break;
            case Step.Store: if (machine != null && machine.IsStored) Go(Step.Ready); break;
        }
        // Flèche qui suit sa cible et rebondit.
        if (arrow != null && arrow.gameObject.activeSelf)
        {
            if (step == Step.Pull && lever != null) arrowTarget.position = (lever.lever != null ? lever.lever : lever.transform).position;
            if ((step == Step.TakeOut || step == Step.Store) && bag != null) arrowTarget.position = bag.HandlePoint;
            if (step == Step.Coin && slot != null) arrowTarget.position = slot.transform.position;
            if (step == Step.Calc) { var c = CalcPoint(); if (c.HasValue) arrowTarget.position = c.Value; }
            arrow.position = arrowTarget.position + Vector3.up * (arrowLift + 0.025f * Mathf.Sin(Time.time * 5f));
            if (cam != null) { Vector3 d = cam.position - arrow.position; d.y = 0f; if (d.sqrMagnitude > 1e-4f) arrow.rotation = Quaternion.LookRotation(-d, Vector3.up); }
        }
        // La bulle reste lisible : elle se tourne vers le joueur.
        if (card != null && cam != null)
        {
            Vector3 d = card.position - cam.position; d.y = 0f;
            if (d.sqrMagnitude > 1e-4f) card.rotation = Quaternion.Slerp(card.rotation, Quaternion.LookRotation(d, Vector3.up) * Quaternion.Euler(12f, 0f, 0f), Time.deltaTime * 4f);
        }
    }

    /// <summary>Bouton « OUI, C'EST PARTI ! » : fin du tuto, le premier cours commence.</summary>
    public void Finish()
    {
        if (step != Step.Ready) return;
        step = Step.Done;
        if (clickClip != null) audioSource.PlayOneShot(clickClip);
        StartCoroutine(CloseCard());
        if (arrow != null) arrow.gameObject.SetActive(false);
        if (menu != null) menu.StartChrono();
    }

    // ================= Bulle =================
    void Say(string stepLabel, string text, Vector3? target)
    {
        if (stepText != null) stepText.text = "TUTO  " + stepLabel;
        if (bodyText != null) bodyText.text = text;
        if (button != null) button.SetActive(false);
        PlaceCard();
        StartCoroutine(PopCard());
        if (messageClip != null) audioSource.PlayOneShot(messageClip, 0.8f);
        if (arrow != null)
        {
            arrow.gameObject.SetActive(target.HasValue);
            if (target.HasValue) arrowTarget.position = target.Value;
        }
    }

    void PlaceCard()
    {
        if (card == null || cam == null) return;
        Vector3 fwd = cam.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
        fwd.Normalize();
        card.position = cam.position + fwd * 0.8f + Vector3.up * 0.2f;
        card.rotation = Quaternion.LookRotation(fwd, Vector3.up) * Quaternion.Euler(12f, 0f, 0f);
    }

    IEnumerator PopCard()
    {
        if (card == null) yield break;
        card.gameObject.SetActive(true);
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.25f)
        {
            float e = 1f + Mathf.Sin(t * Mathf.PI) * 0.12f;
            card.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, Mathf.SmoothStep(0f, 1f, t)) * e;
            yield return null;
        }
        card.localScale = Vector3.one;
    }

    IEnumerator CloseCard()
    {
        if (card == null) yield break;
        Vector3 s0 = card.localScale, p0 = card.position;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.35f)
        {
            float e = Mathf.SmoothStep(0f, 1f, t);
            card.localScale = s0 * (1f - e);
            card.position = p0 + Vector3.up * 0.15f * e;
            yield return null;
        }
        card.gameObject.SetActive(false);
    }

    /// <summary>« Bam » : l'objet apparaît en grossissant, avec un petit rebond.</summary>
    IEnumerator Pop(GameObject go)
    {
        if (go == null) yield break;
        Vector3 s1 = go.transform.localScale;
        go.transform.localScale = s1 * 0.01f;
        go.SetActive(true);
        if (popClip != null) AudioSource.PlayClipAtPoint(popClip, go.transform.position, 1f);
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.35f)
        {
            float e = Mathf.SmoothStep(0f, 1f, t) + Mathf.Sin(t * Mathf.PI) * 0.18f;
            go.transform.localScale = s1 * e;
            yield return null;
        }
        go.transform.localScale = s1;
    }

    Vector3? Top(GameObject go)
    {
        if (go == null) return null;
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return go.transform.position;
        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        return new Vector3(b.center.x, b.max.y, b.center.z);
    }

    // ================= Construction (au lancement du tuto) =================
    void BuildCard()
    {
        if (card != null) return;
        card = new GameObject("Bulle tuto").transform;
        Box(card, "Bord", new Vector3(0f, 0f, 0.004f), new Vector3(0.56f, 0.30f, 0.008f), new Color(0.2f, 0.42f, 0.86f), true, 0.5f);
        Box(card, "Papier", Vector3.zero, new Vector3(0.53f, 0.27f, 0.006f), new Color(0.99f, 0.97f, 0.9f), true, 0.55f);
        stepText = Label(card, "Étape", new Vector3(0f, 0.105f, -0.004f), new Vector2(0.5f, 0.04f), 0.22f, new Color(0.2f, 0.42f, 0.86f));
        bodyText = Label(card, "Texte", new Vector3(0f, -0.005f, -0.004f), new Vector2(0.5f, 0.18f), 0.2f, new Color(0.12f, 0.12f, 0.18f));
        bodyText.enableAutoSizing = true; bodyText.fontSizeMin = 0.1f; bodyText.fontSizeMax = 0.2f;

        // Bouton final.
        button = new GameObject("Bouton C'est parti");
        button.transform.SetParent(card, false);
        button.transform.localPosition = new Vector3(0f, -0.185f, -0.01f);
        var vis = Box(button.transform, "Fond", Vector3.zero, new Vector3(0.3f, 0.07f, 0.02f), new Color(0.25f, 0.75f, 0.35f), true, 0.4f);
        Label(vis, "Texte", new Vector3(0f, 0f, -0.6f), new Vector2(0.28f, 0.06f), 0.2f, Color.white, true).text = "OUI, C'EST PARTI !";
        var col = button.AddComponent<BoxCollider>();
        col.size = new Vector3(0.3f, 0.07f, 0.04f);
        var mb = button.AddComponent<MenuButton>();
        mb.onClick.AddListener(Finish);
        button.SetActive(false);
        card.gameObject.SetActive(false);
    }

    void BuildArrow()
    {
        if (arrow != null) return;
        arrowTarget = new GameObject("Cible flèche tuto").transform;
        arrow = new GameObject("Flèche tuto").transform;
        var c = new Color(1f, 0.82f, 0.2f);
        // Chevron pointant vers le bas.
        var a = Box(arrow, "Trait 1", new Vector3(-0.017f, 0.017f, 0f), new Vector3(0.012f, 0.055f, 0.012f), c, true);
        a.localRotation = Quaternion.Euler(0f, 0f, 45f);
        var b = Box(arrow, "Trait 2", new Vector3(0.017f, 0.017f, 0f), new Vector3(0.012f, 0.055f, 0.012f), c, true);
        b.localRotation = Quaternion.Euler(0f, 0f, -45f);
        Box(arrow, "Tige", new Vector3(0f, 0.05f, 0f), new Vector3(0.012f, 0.06f, 0.012f), c, true);
        arrow.gameObject.SetActive(false);
    }

    Transform Box(Transform parent, string name, Vector3 pos, Vector3 size, Color color, bool glow = false, float glowAmount = 1.5f)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        var r = go.GetComponent<Renderer>();
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", 0.2f);
        if (glow) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", color * glowAmount); }
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go.transform;
    }

    TMP_Text Label(Transform parent, string name, Vector3 pos, Vector2 size, float fontSize, Color color, bool inScaledParent = false)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        if (inScaledParent)
        {
            // Parent mis à l'échelle (fond du bouton) : on annule l'échelle pour le texte.
            var ps = parent.localScale;
            go.transform.localScale = new Vector3(1f / ps.x, 1f / ps.y, 1f / ps.z);
        }
        var t = go.AddComponent<TextMeshPro>();
        if (font != null) t.font = font;
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.rectTransform.sizeDelta = size;
        return t;
    }

    // ================= Tests sans casque =================
    /// <summary>Fait l'action demandée à l'étape en cours (pour tester sans casque).</summary>
    public void TestDoStep()
    {
        switch (step)
        {
            case Step.Book: if (book != null) book.TestTurn(1); break;
            case Step.Calc: calcTimer = 0f; break;
            case Step.TakeOut:
                var socket = FindFirstObjectByType<JackpotSocket>();
                if (bag != null && socket != null) bag.TestPlaceOn(socket);
                break;
            case Step.Coin: if (lever != null) lever.AddCredits(1); break;
            case Step.Pull: if (lever != null) lever.StartSpin(); break;
            case Step.Store: if (bag != null) bag.TestStore(); break;
            case Step.Ready: Finish(); break;
        }
    }
}
