using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Un objet à vendre sur l'étagère du casier : on le vise → il grossit, s'avance et sa fiche s'affiche ; on clique pour l'acheter.
/// </summary>
[RequireComponent(typeof(XRSimpleInteractable))]
public class ShopItem : MonoBehaviour
{
    public string itemId = "coca";
    public string displayName = "Coca";
    [TextArea] public string description = "";
    public int price = 20;
    [Tooltip("Ancienne étiquette de prix (plus utilisée : la fiche d'infos la remplace).")]
    public TMP_Text priceLabel;
    public LockerShop shop;

    [Header("Survol")]
    [Tooltip("La partie visible qui grossit et s'avance (vide = l'enfant « Visuel »).")]
    public Transform visual;
    public float hoverScale = 1.45f;
    [Tooltip("De combien il s'avance vers nous quand on le vise (m).")]
    public float hoverForward = 0.16f;

    XRSimpleInteractable interactable;
    Vector3 baseScale, basePos;
    bool sold, hovered;
    float spin, k;
    Transform cam;

    public bool Sold => sold;

    void Awake()
    {
        if (visual == null) visual = transform.Find("Visuel");
        if (visual == null) visual = transform;
        baseScale = visual.localScale;
        basePos = visual.localPosition;
        interactable = GetComponent<XRSimpleInteractable>();
        interactable.hoverEntered.AddListener(_ => SetHover(true));
        interactable.hoverExited.AddListener(_ => { if (!interactable.isHovered) SetHover(false); });
        interactable.selectEntered.AddListener(_ => { if (shop != null) shop.TryBuy(this); });
        if (priceLabel != null) priceLabel.gameObject.SetActive(false);
    }

    void Start()
    {
        var origin = FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
        cam = origin != null && origin.Camera != null ? origin.Camera.transform : Camera.main != null ? Camera.main.transform : null;
    }

    void SetHover(bool on)
    {
        hovered = on && !sold;
        if (shop != null) { if (hovered) shop.ShowInfo(this); else shop.HideInfo(this); }
    }

    public bool CanPay => shop == null || shop.Coins >= price;

    public void RefreshLabel()
    {
        if (hovered && shop != null) shop.ShowInfo(this);
    }

    void Update()
    {
        if (sold) return;
        // Tourne doucement sur lui-même, plus vite quand on le vise ; il grossit et s'avance vers nous.
        spin = Mathf.MoveTowards(spin, hovered ? 70f : 20f, Time.deltaTime * 200f);
        visual.Rotate(Vector3.up, spin * Time.deltaTime, Space.World);
        k = Mathf.MoveTowards(k, hovered ? 1f : 0f, Time.deltaTime * 5f);
        float e = Mathf.SmoothStep(0f, 1f, k);
        visual.localScale = baseScale * Mathf.Lerp(1f, hoverScale, e);
        Vector3 toCam = Vector3.zero;
        if (cam != null)
        {
            Vector3 d = cam.position - transform.position; d.y = 0f;
            if (d.sqrMagnitude > 1e-4f) toCam = transform.InverseTransformDirection(d.normalized);
        }
        visual.localPosition = basePos + toCam * (hoverForward / Mathf.Max(1e-4f, transform.lossyScale.x)) * e;
    }

    /// <summary>Acheté : il disparaît (il sera sur la table au prochain cours).</summary>
    public void OnBought()
    {
        sold = true;
        hovered = false;
        interactable.enabled = false;
        if (shop != null) shop.HideInfo(this);
        StartCoroutine(Vanish());
    }

    IEnumerator Vanish()
    {
        Vector3 s0 = transform.localScale, p0 = transform.position;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.5f)
        {
            transform.localScale = s0 * (1f + t * 0.3f) * (1f - t);
            transform.position = p0 + Vector3.up * 0.25f * t;
            yield return null;
        }
        gameObject.SetActive(false);
    }

    /// <summary>Pas assez de pièces : il tremble.</summary>
    public void Refuse() => StartCoroutine(Shake());

    IEnumerator Shake()
    {
        Vector3 p0 = transform.localPosition;
        for (float t = 0f; t < 0.35f; t += Time.deltaTime)
        {
            transform.localPosition = p0 + Vector3.right * Mathf.Sin(t * 60f) * 0.01f;
            yield return null;
        }
        transform.localPosition = p0;
    }
}
