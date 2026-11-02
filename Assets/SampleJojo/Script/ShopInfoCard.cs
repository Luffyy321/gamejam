using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Fiche qui s'affiche quand on vise un objet de la boutique : nom, ce qu'il fait, et son prix (avec une pièce).
/// </summary>
public class ShopInfoCard : MonoBehaviour
{
    public Transform panel;
    public TMP_Text nameText;
    public TMP_Text descText;
    public TMP_Text priceText;
    public Color priceOk = new Color(0.15f, 0.15f, 0.2f);
    public Color priceTooHigh = new Color(0.85f, 0.15f, 0.15f);

    float target, k;

    void Awake()
    {
        if (panel == null) panel = transform;
        panel.localScale = Vector3.one * 0.001f;
        panel.gameObject.SetActive(false);
    }

    public void Show(string title, string desc, int price, bool canPay)
    {
        if (nameText != null) nameText.text = title;
        if (descText != null) descText.text = desc;
        if (priceText != null)
        {
            priceText.text = price.ToString();
            priceText.color = canPay ? priceOk : priceTooHigh;
        }
        panel.gameObject.SetActive(true);
        target = 1f;
    }

    public void Hide() => target = 0f;

    Transform cam;

    void Start()
    {
        var origin = FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
        cam = origin != null && origin.Camera != null ? origin.Camera.transform : Camera.main != null ? Camera.main.transform : null;
    }

    void LateUpdate()
    {
        // La fiche se tourne toujours vers nous (seulement à l'horizontale), légèrement inclinée.
        if (cam == null || panel == null || !panel.gameObject.activeSelf) return;
        Vector3 d = transform.position - cam.position; d.y = 0f;
        if (d.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up) * Quaternion.Euler(8f, 0f, 0f);
    }

    void Update()
    {
        if (panel == null || (!panel.gameObject.activeSelf && target <= 0f)) return;
        k = Mathf.MoveTowards(k, target, Time.deltaTime * 6f);
        float e = Mathf.SmoothStep(0f, 1f, k);     // apparaît en douceur, sans rebond
        panel.localScale = Vector3.one * Mathf.Max(0.001f, e);
        if (k <= 0f && target <= 0f) panel.gameObject.SetActive(false);
    }
}
