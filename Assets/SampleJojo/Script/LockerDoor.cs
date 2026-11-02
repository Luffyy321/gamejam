using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Porte de casier sur charnière : on clique dessus pour l'ouvrir / la fermer.
/// La charnière est placée sur l'arête arrière de la porte, contre le corps du casier,
/// donc la porte reste collée au casier pendant toute l'animation.
/// </summary>
public class LockerDoor : MonoBehaviour
{
    [Tooltip("Objet charnière (parent de la porte).")]
    public Transform hinge;
    [Tooltip("Axe de rotation dans le repère du parent de la charnière.")]
    public Vector3 hingeAxis = Vector3.up;
    public float openAngle = 100f;
    public float openTime = 0.9f;
    public XRSimpleInteractable interactable;
    public AudioSource audioSource;
    public AudioClip openClip;

    Quaternion rest;
    float angle;
    bool isOpen;
    Coroutine anim;

    public bool IsOpen => isOpen;

    void Awake()
    {
        if (hinge == null) hinge = transform;
        rest = hinge.localRotation;
        if (interactable != null) interactable.selectEntered.AddListener(_ => Toggle());
    }

    public void Toggle() { if (isOpen) Close(); else Open(); }

    [ContextMenu("Ouvrir")]
    public void Open()
    {
        if (isOpen) return;
        isOpen = true;
        if (audioSource != null && openClip != null) audioSource.PlayOneShot(openClip);
        Play(openAngle);
    }

    [ContextMenu("Fermer")]
    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;
        Play(0f);
    }

    void Play(float target)
    {
        if (anim != null) StopCoroutine(anim);
        anim = StartCoroutine(Swing(angle, target, openTime, target > angle));
    }

    /// <summary>Ouverture : petit à-coup du loquet, puis la porte pivote et dépasse un peu avant de se poser.</summary>
    public IEnumerator Swing(float from, float to, float time, bool opening)
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / time)
        {
            angle = from + (to - from) * Curve(t, opening);
            Apply();
            yield return null;
        }
        angle = to;
        Apply();
        anim = null;
    }

    public static float Curve(float t, bool opening)
    {
        t = Mathf.Clamp01(t);
        if (!opening)
        {
            // Fermeture : accélère et claque doucement (petit rebond).
            if (t < 0.8f) { float u = t / 0.8f; return u * u; }
            return 1f - Mathf.Sin((t - 0.8f) / 0.2f * Mathf.PI) * 0.04f;
        }
        // Loquet : la porte se décolle de 3 % puis s'ouvre en « ease-out-back ».
        const float latch = 0.1f;
        if (t < latch) return 0.03f * Mathf.SmoothStep(0f, 1f, t / latch);
        float x = (t - latch) / (1f - latch);
        const float c1 = 1.3f, c3 = c1 + 1f;
        float back = 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        return Mathf.Lerp(0.03f, 1f, back);
    }

    void Apply() => hinge.localRotation = Quaternion.AngleAxis(angle, hingeAxis) * rest;
}
