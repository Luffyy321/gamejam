using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Bouton 3D du menu : il grossit quand on le vise, et déclenche onClick quand on appuie (gâchette ou doigt).
/// </summary>
[RequireComponent(typeof(XRSimpleInteractable))]
public class MenuButton : MonoBehaviour
{
    [Tooltip("La partie visible qui grossit au survol (vide = cet objet).")]
    public Transform visual;
    public float hoverScale = 1.07f;
    public float hoverLift = 0.006f;
    public AudioClip clickClip;
    public UnityEvent onClick = new UnityEvent();

    XRSimpleInteractable interactable;
    Vector3 baseScale, basePos;
    float hover, target;
    float lastClick = -1f;

    void Awake()
    {
        if (visual == null) visual = transform;
        baseScale = visual.localScale;
        basePos = visual.localPosition;
        interactable = GetComponent<XRSimpleInteractable>();
        interactable.hoverEntered.AddListener(_ => target = 1f);
        interactable.hoverExited.AddListener(_ => { if (!interactable.isHovered) target = 0f; });
        interactable.selectEntered.AddListener(_ => Click());
    }

    void OnDisable() { hover = target = 0f; Apply(); }

    void Update()
    {
        if (Mathf.Approximately(hover, target)) return;
        hover = Mathf.MoveTowards(hover, target, Time.unscaledDeltaTime * 8f);
        Apply();
    }

    void Apply()
    {
        if (visual == null) return;
        float k = Mathf.SmoothStep(0f, 1f, hover);
        visual.localScale = baseScale * Mathf.Lerp(1f, hoverScale, k);
        visual.localPosition = basePos + Vector3.back * hoverLift * k;      // vers le joueur
    }

    public void Click()
    {
        if (Time.unscaledTime - lastClick < 0.3f) return;
        lastClick = Time.unscaledTime;
        if (clickClip != null) AudioSource.PlayClipAtPoint(clickClip, transform.position, 0.7f);
        onClick?.Invoke();
    }
}
