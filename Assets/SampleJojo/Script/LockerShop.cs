using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Le casier trésor au bout du couloir : on interagit avec la porte pour l'ouvrir, puis on achète les objets.
/// </summary>
public class LockerShop : MonoBehaviour
{
    [Header("Porte")]
    public Transform door;
    [Tooltip("Axe de la charnière dans le repère du parent de la porte.")]
    public Vector3 hingeAxis = Vector3.up;
    public float openAngle = -105f;
    public XRSimpleInteractable doorInteractable;

    [Header("Objets")]
    public ShopItem[] items;
    public ParticleSystem treasureFx;

    [Header("Fiche d'infos (quand on vise un objet)")]
    public ShopInfoCard infoCard;

    [Header("Sons")]
    public AudioSource audioSource;
    public AudioClip openClip;
    public AudioClip buyClip;
    public AudioClip refuseClip;

    JackpotBag bag;
    bool open;
    Quaternion doorRest;

    public int Coins => bag != null ? bag.Total : 0;

    void Awake()
    {
        if (door != null) doorRest = door.localRotation;
        foreach (var it in items) if (it != null) { it.shop = this; it.GetComponent<XRSimpleInteractable>().enabled = false; }
        // Lunettes déjà achetées : on les a déjà, elles ne sont plus en vente.
        foreach (var it in items)
            if (it != null && Consumable.Permanent(it.itemId) && GameProgress.Purchased.Contains(it.itemId)) it.gameObject.SetActive(false);
        if (doorInteractable != null) doorInteractable.selectEntered.AddListener(_ => Open());
    }

    void Start() => bag = JackpotBag.Active != null ? JackpotBag.Active : FindFirstObjectByType<JackpotBag>();

    [ContextMenu("Ouvrir le casier")]
    public void Open()
    {
        if (open) return;
        open = true;
        if (doorInteractable != null) doorInteractable.enabled = false;
        if (audioSource != null && openClip != null) audioSource.PlayOneShot(openClip);
        StartCoroutine(OpenDoor());
        foreach (var it in items) if (it != null && it.gameObject.activeSelf) it.GetComponent<XRSimpleInteractable>().enabled = true;
        if (treasureFx != null) treasureFx.Emit(60);
    }

    IEnumerator OpenDoor()
    {
        if (door == null) yield break;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 1.1f)
        {
            float e = LockerDoor.Curve(t, true);
            door.localRotation = Quaternion.AngleAxis(openAngle * e, hingeAxis) * doorRest;
            yield return null;
        }
        door.localRotation = Quaternion.AngleAxis(openAngle, hingeAxis) * doorRest;
    }

    ShopItem shown;

    public void ShowInfo(ShopItem item)
    {
        shown = item;
        if (infoCard != null) infoCard.Show(item.displayName, item.description, item.price, item.CanPay);
    }

    public void HideInfo(ShopItem item)
    {
        if (shown != item) return;
        shown = null;
        if (infoCard != null) infoCard.Hide();
    }

    public void TryBuy(ShopItem item)
    {
        if (item == null || item.Sold || bag == null) return;
        if (bag.Spend(item.price))
        {
            GameProgress.Purchased.Add(item.itemId);
            item.OnBought();
            if (audioSource != null && buyClip != null) audioSource.PlayOneShot(buyClip);
            if (treasureFx != null) treasureFx.Emit(25);
            foreach (var it in items) if (it != null && !it.Sold) it.RefreshLabel();
            Debug.Log($"[Boutique] Acheté : {item.displayName} ({item.price} pièces). Reste {bag.Total}.");
        }
        else
        {
            item.Refuse();
            if (audioSource != null && refuseClip != null) audioSource.PlayOneShot(refuseClip);
        }
    }

    // ---------------- Tests sans casque ----------------
    [ContextMenu("Test : acheter le premier objet")]
    void TestBuy() { Open(); if (items.Length > 0) TryBuy(items[0]); }
}
