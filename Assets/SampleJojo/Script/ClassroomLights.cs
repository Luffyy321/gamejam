using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Lampes de la classe : allume / éteint toutes les lumières et la partie lumineuse des panneaux.
/// L'interrupteur (XRSimpleInteractable) bascule l'état quand on appuie dessus avec la main.
/// </summary>
public class ClassroomLights : MonoBehaviour
{
    public bool on = true;
    public Light[] lights;
    public Renderer[] panels;
    [ColorUsage(false, true)] public Color emission = new Color(1f, 0.95f, 0.85f) * 2.2f;

    [Header("Interrupteur")]
    public XRSimpleInteractable switchButton;
    public Transform switchToggle;          // la petite bascule qui pivote
    public AudioSource audioSource;
    public AudioClip clickClip;

    MaterialPropertyBlock mpb;
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    void Awake() { mpb = new MaterialPropertyBlock(); Apply(); }

    void OnEnable() { if (switchButton != null) switchButton.selectEntered.AddListener(OnPress); }
    void OnDisable() { if (switchButton != null) switchButton.selectEntered.RemoveListener(OnPress); }

    void OnPress(SelectEnterEventArgs args) => Toggle();

    [ContextMenu("Allumer / éteindre")]
    public void Toggle()
    {
        on = !on;
        Apply();
        if (audioSource != null && clickClip != null) audioSource.PlayOneShot(clickClip);
    }

    public void Apply()
    {
        if (mpb == null) mpb = new MaterialPropertyBlock();
        if (lights != null) foreach (var l in lights) if (l != null) l.enabled = on;
        if (panels != null)
            foreach (var r in panels)
            {
                if (r == null) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetColor(EmissionId, on ? emission : Color.black);
                r.SetPropertyBlock(mpb);
            }
        if (switchToggle != null) switchToggle.localRotation = Quaternion.Euler(on ? -12f : 12f, 0f, 0f);
    }
}
