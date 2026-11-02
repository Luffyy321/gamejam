using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Fente à pièces de la machine : une pièce qui y entre donne des crédits (1 crédit par unité de valeur,
/// 1 crédit = 1 tirage). Sans crédit la machine est éteinte et le levier ne se prend pas.
/// </summary>
[RequireComponent(typeof(Collider))]
public class CoinSlot : MonoBehaviour
{
    public JackpotLever lever;
    [Tooltip("Point où la pièce disparaît (fond de la fente).")]
    public Transform slotPoint;
    public TMP_Text creditText;
    public AudioSource audioSource;
    public AudioClip insertClip;
    public AudioClip powerOnClip;
    [Tooltip("Une pièce qui vient de sortir de la machine ne rentre pas tout de suite dans la fente.")]
    public float minCoinAge = 1.5f;

    void Start()
    {
        if (lever == null) lever = GetComponentInParent<JackpotItem>()?.GetComponentInChildren<JackpotLever>(true);
        if (lever == null) lever = FindFirstObjectByType<JackpotLever>();
        if (lever != null) lever.onCreditsChanged.AddListener(_ => Refresh());
        Refresh();
    }

    void OnTriggerEnter(Collider other) => TryInsert(other);
    void OnTriggerStay(Collider other) => TryInsert(other);

    void TryInsert(Collider other)
    {
        if (lever == null) return;
        var coin = other.GetComponentInParent<JackpotCoin>();
        if (coin == null || coin.IsCollected || coin.Age < minCoinAge) return;
        if (CoinJar.Active != null && CoinJar.Active.IsInside(coin.transform.position)) return;
        // Une pièce posée à côté de la machine ne rentre pas toute seule : il faut la tenir ou la lancer dedans.
        var rb = coin.GetComponent<Rigidbody>();
        if (!coin.IsHeld && (rb == null || rb.linearVelocity.magnitude < 0.25f)) return;
        var grab = coin.GetComponent<XRGrabInteractable>();
        if (grab != null && grab.isSelected && grab.interactionManager != null)
            grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grab);
        bool wasOff = lever.credits <= 0;
        lever.AddCredits(coin.value);
        coin.CollectInto(slotPoint != null ? slotPoint.position : transform.position);
        if (audioSource != null)
        {
            if (insertClip != null) audioSource.PlayOneShot(insertClip);
            if (wasOff && powerOnClip != null) audioSource.PlayOneShot(powerOnClip, 0.6f);
        }
        Debug.Log($"[Machine] Pièce de {coin.value} insérée : {lever.credits} crédit(s).");
    }

    float nextRefresh;

    float brokeTime;

    void Update()
    {
        if (Time.time > nextRefresh) { nextRefresh = Time.time + 0.25f; Refresh(); }
        CheckBroke();
        // « INSÉREZ UNE PIÈCE » clignote quand la machine est éteinte.
        if (creditText != null && lever != null && lever.credits <= 0 && !lever.IsSpinning)
            creditText.alpha = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(Time.time * 2.5f));
    }

    /// <summary>Plus aucune pièce (sac, bocal, table) et plus de crédit pendant 2 s : on redonne 1 pièce dans le bocal.</summary>
    void CheckBroke()
    {
        var bag = JackpotBag.Active;
        bool broke = lever != null && bag != null && CoinJar.Active != null && lever.credits <= 0 && !lever.IsSpinning
                     && bag.TotalWithLoose <= 0 && !TutorialRunning();
        if (!broke) { brokeTime = 0f; return; }
        brokeTime += Time.deltaTime;
        if (brokeTime < 2f) return;
        brokeTime = -3f;      // le temps que la pièce arrive dans le bocal
        var list = new System.Collections.Generic.List<int>();
        for (int i = 0; i < GameBalance.RescueCoins; i++) list.Add(1);
        CoinJar.Active.Deposit(list);
        Debug.Log($"[Machine] Plus de pièce : on t'en redonne {GameBalance.RescueCoins}.");
    }

    static TutorialGuide tuto;
    static bool TutorialRunning()
    {
        if (tuto == null) tuto = FindFirstObjectByType<TutorialGuide>();
        return tuto != null && tuto.Running;
    }

    void Refresh()
    {
        if (creditText == null || lever == null) return;
        if (lever.credits > 0)
        {
            float m = AdrenalineEffect.Active != null ? AdrenalineEffect.Active.WinMultiplier : 1f;
            creditText.text = "CRÉDITS : " + lever.credits + (m > 1.05f ? $"  <color=#FF5FB0>x{m:0.0}</color>" : "");
            creditText.color = new Color(0.4f, 1f, 0.45f);
            creditText.alpha = 1f;
        }
        else
        {
            creditText.text = "INSÉREZ UNE PIÈCE";
            creditText.color = new Color(1f, 0.82f, 0.3f);
        }
    }

    [ContextMenu("Test : +1 crédit")] void TestCredit() { if (lever != null) lever.AddCredits(1); }
}
