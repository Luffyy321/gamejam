using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Roulette de la boutique.
/// 1. On vise la boîte à mises et on appuie : +1 pièce misée (garder appuyé pour miser plus vite).
/// 2. On appuie sur une couleur : noir (x2), rouge (x2) ou vert (x100).
/// 3. La roue tourne, la bille tombe dans une case : si c'est notre couleur, on gagne la mise x2 (ou x100),
///    les pièces vont dans le bocal ; sinon la mise est perdue.
/// Chances (GameBalance) : noir 49,5 %, rouge 49,5 %, vert 1 %.
/// </summary>
public class RouletteTable : MonoBehaviour
{
    public enum Pocket { Black = 0, Red = 1, Green = 2 }

    public static RouletteTable Active { get; private set; }

    [Header("Roue")]
    [Tooltip("La partie qui tourne (cases + chiffres). Elle tourne autour de son axe Z local.")]
    public Transform wheel;
    [Tooltip("Repère fixe de la cuvette (même orientation que la roue au repos).")]
    public Transform bowl;
    public Transform ball;
    [Tooltip("Angle de chaque case (degrés, repère de la cuvette, roue au repos).")]
    public float[] pocketAngles = new float[0];
    public Pocket[] pocketColors = new Pocket[0];
    [Tooltip("Rayon et hauteur (repère local de la cuvette) : piste extérieure de la bille, puis fond des cases.")]
    public float trackRadius = 18f, trackZ = 3f, pocketRadius = 12f, pocketZ = 0f;
    public float spinSeconds = 6.5f, ballSeconds = 5f;

    [Header("Mises")]
    public XRSimpleInteractable betBox;
    [Tooltip("Où s'empilent les pièces misées (dans la boîte).")]
    public Transform betSpot;
    public GameObject coinVisual;
    public TMP_Text betText;
    public TMP_Text resultText;
    public MenuButton blackButton, redButton, greenButton;
    public int maxBet = 999;

    [Header("Sons")]
    public AudioSource audioSource;
    public AudioClip betClip, spinClip, ballClip, winClip, loseClip, refuseClip;

    int bet;
    bool spinning, holding;
    float holdTime, nextAdd;
    Quaternion wheelRest;
    float wheelAngle;
    readonly List<GameObject> stack = new List<GameObject>();
    JackpotCoins coinSplitter;

    public bool Spinning => spinning;
    public int Bet => bet;

    void Awake()
    {
        Active = this;
        if (wheel != null) wheelRest = wheel.localRotation;
        if (betBox != null)
        {
            betBox.selectEntered.AddListener(_ => { holding = true; holdTime = 0f; AddBet(); nextAdd = Time.time + 0.45f; });
            betBox.selectExited.AddListener(_ => holding = false);
        }
        if (blackButton != null) blackButton.onClick.AddListener(() => Play(Pocket.Black));
        if (redButton != null) redButton.onClick.AddListener(() => Play(Pocket.Red));
        if (greenButton != null) greenButton.onClick.AddListener(() => Play(Pocket.Green));
        if (coinVisual != null) coinVisual.SetActive(false);
        if (ball != null) PlaceBall(ballRestAngle, trackRadius, trackZ);
        ShowBet();
        Say("Fais tes jeux !", new Color(1f, 0.85f, 0.35f));
    }

    void OnDestroy() { if (Active == this) Active = null; }

    float ballRestAngle = 90f;

    // ---------------- Mises ----------------
    void Update()
    {
        if (!holding || spinning) return;
        holdTime += Time.deltaTime;
        if (Time.time < nextAdd) return;
        // Plus on garde appuyé, plus ça va vite (1, puis 5 par 5 après 3 s).
        AddBet(holdTime > 3f ? 5 : 1);
        nextAdd = Time.time + Mathf.Lerp(0.28f, 0.08f, Mathf.Clamp01(holdTime / 2.5f));
    }

    void AddBet(int n = 1)
    {
        if (spinning) return;
        var bag = JackpotBag.Active;
        int can = bag != null ? Mathf.Min(n, bag.Total) : 0;
        can = Mathf.Min(can, maxBet - bet);
        if (can <= 0 || !bag.Spend(can))
        {
            holding = false;
            Say(bet == 0 ? "Pas assez de pièces !" : "Plus de pièces !", new Color(1f, 0.4f, 0.35f));
            if (audioSource != null && refuseClip != null) audioSource.PlayOneShot(refuseClip, 0.6f);
            return;
        }
        bet += can;
        for (int i = 0; i < can; i++) StackCoin();
        if (audioSource != null && betClip != null) audioSource.PlayOneShot(betClip, 0.6f);
        ShowBet();
        Say("Choisis ta couleur !", new Color(1f, 0.85f, 0.35f));
    }

    void StackCoin()
    {
        if (coinVisual == null || betSpot == null || stack.Count >= 30) return;
        var c = Instantiate(coinVisual, betSpot);
        c.SetActive(true);
        int k = stack.Count;
        c.transform.localPosition = coinVisual.transform.localPosition + new Vector3((k % 3 - 1) * 0.034f, (k / 3) * 0.004f, ((k / 3) % 2 == 0 ? 0f : 0.01f));
        c.transform.localRotation = coinVisual.transform.localRotation * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        stack.Add(c);
    }

    void ClearStack(bool flyUp)
    {
        foreach (var c in stack) if (c != null) { if (flyUp) StartCoroutine(FlyAway(c.transform)); else Destroy(c); }
        stack.Clear();
    }

    IEnumerator FlyAway(Transform t)
    {
        Vector3 p0 = t.position, s0 = t.localScale;
        float delay = Random.Range(0f, 0.3f);
        yield return new WaitForSeconds(delay);
        for (float k = 0f; k < 1f; k += Time.deltaTime / 0.5f)
        {
            if (t == null) yield break;
            t.position = p0 + Vector3.up * 0.35f * k;
            t.localScale = s0 * (1f - k);
            yield return null;
        }
        if (t != null) Destroy(t.gameObject);
    }

    void ShowBet()
    {
        if (betText != null) betText.text = bet > 0 ? $"MISE : {bet}" : "MISE : 0\n<size=60%>vise la boîte et appuie</size>";
    }

    void Say(string s, Color c)
    {
        if (resultText == null) return;
        resultText.text = s;
        resultText.color = c;
    }

    /// <summary>On quitte la boutique sans avoir joué : la mise est rendue.</summary>
    public void Refund()
    {
        if (bet <= 0) return;
        StopAllCoroutines();
        spinning = false;
        var bag = JackpotBag.Active;
        if (bag != null) { bag.coinValue += bet; bag.NotifyChanged(); }
        Debug.Log($"[Roulette] Mise de {bet} rendue.");
        bet = 0;
        ClearStack(false);
        ShowBet();
    }

    // ---------------- Tirage ----------------
    public void Play(Pocket choice)
    {
        if (spinning) return;
        if (bet <= 0)
        {
            Say("Mets d'abord une mise\ndans la boîte !", new Color(1f, 0.55f, 0.35f));
            if (audioSource != null && refuseClip != null) audioSource.PlayOneShot(refuseClip, 0.6f);
            return;
        }
        StartCoroutine(SpinRoutine(choice));
    }

    Pocket Roll()
    {
        float x = Random.value * (GameBalance.RouletteBlack + GameBalance.RouletteRed + GameBalance.RouletteGreen);
        if (x < GameBalance.RouletteGreen) return Pocket.Green;
        x -= GameBalance.RouletteGreen;
        return x < GameBalance.RouletteRed ? Pocket.Red : Pocket.Black;
    }

    static int Mult(Pocket p) => p == Pocket.Green ? GameBalance.RouletteGreenMult : p == Pocket.Red ? GameBalance.RouletteRedMult : GameBalance.RouletteBlackMult;
    static string Name(Pocket p) => p == Pocket.Green ? "VERT" : p == Pocket.Red ? "ROUGE" : "NOIR";
    static Color Col(Pocket p) => p == Pocket.Green ? new Color(0.25f, 0.95f, 0.4f) : p == Pocket.Red ? new Color(1f, 0.25f, 0.25f) : new Color(0.85f, 0.85f, 0.9f);

    IEnumerator SpinRoutine(Pocket choice)
    {
        spinning = true;
        Pocket result = Roll();
        // Une case de la bonne couleur au hasard.
        var idx = new List<int>();
        for (int i = 0; i < pocketColors.Length; i++) if (pocketColors[i] == result) idx.Add(i);
        float pocketA = idx.Count > 0 ? pocketAngles[idx[Random.Range(0, idx.Count)]] : 0f;
        Say($"Mise sur le {Name(choice)}...\nLa roue tourne !", Col(choice));
        if (audioSource != null && spinClip != null) audioSource.PlayOneShot(spinClip);
        Debug.Log($"[Roulette] {bet} misé(s) sur {Name(choice)} → tirage : {Name(result)}.");

        float w0 = wheelAngle;
        float wTotal = 360f * Random.Range(2.5f, 3.5f);          // la roue fait 2-3 tours en ralentissant
        float ballTurns = 360f * Random.Range(4f, 6f);            // la bille tourne dans l'autre sens
        float t0 = Time.time;
        bool dropped = false;
        while (true)
        {
            float t = Time.time - t0;
            float kw = Mathf.Clamp01(t / spinSeconds);
            wheelAngle = w0 + wTotal * (1f - (1f - kw) * (1f - kw) * (1f - kw));
            if (wheel != null) wheel.localRotation = wheelRest * Quaternion.AngleAxis(wheelAngle, Vector3.forward);

            // Bille : angle relatif à la roue qui revient à 0 (= la case choisie) en ralentissant.
            float kb = Mathf.Clamp01(t / ballSeconds);
            float rel = -ballTurns * (1f - kb) * (1f - kb);
            float a = pocketA + wheelAngle + rel;
            // Elle reste sur la piste extérieure, puis descend vers les cases (petits rebonds).
            float drop = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 0.85f, kb));
            float bounce = kb > 0.55f && kb < 0.95f ? Mathf.Abs(Mathf.Sin(kb * 60f)) * (1f - kb) * 1.2f : 0f;
            PlaceBall(a, Mathf.Lerp(trackRadius, pocketRadius, drop), Mathf.Lerp(trackZ, pocketZ, drop) + bounce);
            if (!dropped && kb > 0.6f) { dropped = true; if (audioSource != null && ballClip != null) audioSource.PlayOneShot(ballClip, 0.8f); }
            if (kw >= 1f && kb >= 1f) break;
            yield return null;
        }
        wheelAngle = Mathf.Repeat(wheelAngle, 360f);
        ballRestAngle = pocketA + wheelAngle;

        bool won = result == choice;
        int gain = won ? bet * Mult(choice) : 0;
        if (won)
        {
            Say($"{Name(result)} !\n+{gain} pièces !", Col(result));
            if (audioSource != null && winClip != null) audioSource.PlayOneShot(winClip);
            ClearStack(true);
            var list = Split(gain);
            if (CoinJar.Active != null) CoinJar.Active.Deposit(list);
            else if (JackpotBag.Active != null) { JackpotBag.Active.coinValue += gain; JackpotBag.Active.NotifyChanged(); }
        }
        else
        {
            Say($"{Name(result)}...\nPerdu !", Col(result));
            if (audioSource != null && loseClip != null) audioSource.PlayOneShot(loseClip);
            ClearStack(false);
        }
        Debug.Log($"[Roulette] {(won ? "Gagné : +" + gain : "Perdu")}.");
        bet = 0;
        ShowBet();
        spinning = false;
    }

    List<int> Split(int amount)
    {
        if (coinSplitter == null) coinSplitter = FindFirstObjectByType<JackpotCoins>(FindObjectsInactive.Include);
        if (coinSplitter != null) return coinSplitter.Split(amount);
        var l = new List<int>(); for (int i = 0; i < amount; i++) l.Add(1); return l;
    }

    void PlaceBall(float angleDeg, float r, float z)
    {
        if (ball == null || bowl == null) return;
        float a = angleDeg * Mathf.Deg2Rad;
        ball.position = bowl.TransformPoint(new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, z));
    }

    // ---------------- Tests sans casque ----------------
    [ContextMenu("Test : miser 5")] void TestBet() { for (int i = 0; i < 5; i++) AddBet(); }
    [ContextMenu("Test : jouer rouge")] void TestRed() => Play(Pocket.Red);
    [ContextMenu("Test : jouer noir")] void TestBlack() => Play(Pocket.Black);
    [ContextMenu("Test : jouer vert")] void TestGreen() => Play(Pocket.Green);
}
