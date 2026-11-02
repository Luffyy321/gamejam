using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Emplacement en pointillés sur une table : n'accepte que la machine à sous (sortie du sac).
/// Les pointillés disparaissent quand la machine est posée, et clignotent en vert quand on l'approche.
/// Optimisé : les pointillés sont un seul mesh, et la couleur ne change que si nécessaire.
/// </summary>
public class JackpotSocket : XRSocketInteractor
{
    [Tooltip("Les pointillés (un seul objet avec un seul mesh).")]
    public GameObject outline;
    public Color idleColor = Color.white;
    public Color hoverColor = new Color(0.3f, 1f, 0.4f, 1f);

    Material outlineMat;
    bool wasHover;
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    protected override void Awake()
    {
        base.Awake();
        if (outline != null)
        {
            var r = outline.GetComponentInChildren<Renderer>();
            if (r != null) outlineMat = r.material;
        }
    }

    static bool IsJackpot(IXRInteractable interactable) =>
        interactable is Component c && c.TryGetComponent(out JackpotItem item) && !item.IsStored;

    public override bool CanHover(IXRHoverInteractable interactable) =>
        base.CanHover(interactable) && IsJackpot(interactable);

    public override bool CanSelect(IXRSelectInteractable interactable) =>
        base.CanSelect(interactable) && IsJackpot(interactable);

    void LateUpdate()
    {
        if (outline == null) return;
        bool occupied = hasSelection;
        if (outline.activeSelf == occupied) outline.SetActive(!occupied);
        if (occupied || outlineMat == null) return;

        bool hover = hasHover;
        if (hover)
        {
            outlineMat.SetColor(BaseColorId, Color.Lerp(idleColor, hoverColor, 0.5f + 0.5f * Mathf.Sin(Time.time * 10f)));
        }
        else if (wasHover)
        {
            outlineMat.SetColor(BaseColorId, idleColor);
        }
        wasHover = hover;
    }
}
