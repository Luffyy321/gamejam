using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Objet consommable : on le prend en main et on l'approche de la bouche (pilule, coca, seringue, café)
/// ou des yeux (lunettes, antisèche) → il est utilisé et fait son effet.
/// </summary>
public class Consumable : MonoBehaviour
{
    public string itemId = "pilule";
    [Range(-1f, 1f)] public float adrenaline = 0.25f;
    [Tooltip("Distance bouche → objet pour le consommer (m).")]
    public float mouthDistance = 0.17f;

    [Tooltip("Ajouté aux gains ET aux pertes de dopamine de la machine (seringue).")]
    public float boost = 0f;
    [Tooltip("Vision resserrée pendant ce temps (s) (pilule).")]
    public float tunnelSeconds = 0f;
    public AudioClip eatClip;

    XRGrabInteractable grab;

    void Awake()
    {
        // Règles des objets :
        // canette : +0,3 dopamine ;
        // seringue : +0,1 aux gains ET aux pertes de dopamine de la machine ;
        // pilule : +0,6 dopamine mais la vision se resserre pendant 20 s.
        switch (itemId)
        {
            case "coca": adrenaline = 0.3f; boost = 0f; tunnelSeconds = 0f; break;
            case "seringue": adrenaline = 0f; boost = 0.1f; tunnelSeconds = 0f; break;
            case "pilule": adrenaline = 0.6f; boost = 0f; tunnelSeconds = 20f; break;
            // Nouveaux objets : pas de dopamine, un effet à la place.
            case "antiseche": case "lunettes3d": case "doflamingo": case "cafe":
                adrenaline = 0f; boost = 0f; tunnelSeconds = 0f; break;
        }
        wearOnFace = itemId == "lunettes3d" || itemId == "doflamingo" || itemId == "antiseche";
    }
    Transform head;
    bool used;
    [Tooltip("Objet qu'on approche des yeux (lunettes, antisèche) au lieu de la bouche.")]
    public bool wearOnFace;
    float nextHint;

    void Start()
    {
        grab = GetComponent<XRGrabInteractable>();
        var origin = FindFirstObjectByType<XROrigin>();
        head = origin != null && origin.Camera != null ? origin.Camera.transform : Camera.main != null ? Camera.main.transform : null;
    }

    void Update()
    {
        if (used || grab == null || head == null || !grab.isSelected) return;
        Vector3 mouth = wearOnFace ? head.position + head.forward * 0.07f : head.position - head.up * 0.08f + head.forward * 0.04f;
        var col = GetComponent<Collider>();
        Vector3 p = col != null ? col.ClosestPoint(mouth) : transform.position;
        if ((p - mouth).sqrMagnitude < mouthDistance * mouthDistance) Consume();
    }

    /// <summary>Objets qu'on garde (on les remet à chaque cours) : les lunettes.</summary>
    public static bool Permanent(string id) => id == "lunettes3d" || id == "doflamingo";

    [ContextMenu("Consommer")]
    public void Consume()
    {
        if (used) return;
        // L'antisèche ne sert que pendant l'examen : avant, on la garde.
        if (itemId == "antiseche" && (QcmSheet.Active == null || !QcmSheet.Active.InProgress))
        {
            if (Time.time > nextHint)
            {
                nextHint = Time.time + 3f;
                ItemEffects.Toast("Garde-la pour l'examen !", transform.position, new Color(1f, 0.9f, 0.4f));
            }
            return;
        }
        used = true;
        // Consommé : il ne revient pas au cours suivant. Les lunettes, elles, se gardent : on les remet à chaque cours.
        if (!Permanent(itemId)) GameProgress.Purchased.Remove(itemId);
        if (AdrenalineEffect.Active != null)
        {
            if (adrenaline != 0f) AdrenalineEffect.Active.Add(adrenaline);
            AdrenalineEffect.Active.boost += boost;
            if (tunnelSeconds > 0f) AdrenalineEffect.Active.Tunnel(tunnelSeconds);
        }
        switch (itemId)
        {
            case "antiseche": QcmSheet.Active.RevealAnswers(); ItemEffects.Toast("Antisèche !", transform.position, new Color(0.5f, 0.75f, 1f)); break;
            case "lunettes3d": ItemEffects.Keywords(); ItemEffects.Toast("Lunettes 3D !", transform.position, new Color(0.5f, 0.9f, 1f)); break;
            case "doflamingo": ItemEffects.PinkFilter(); ItemEffects.Toast("Woaaa c'est beau", transform.position, new Color(1f, 0.45f, 0.8f)); break;
            case "cafe": ItemEffects.SlowTime(GameBalance.CoffeeSeconds); ItemEffects.Toast("Le temps ralentit...", transform.position, new Color(0.6f, 0.8f, 1f)); break;
        }
        if (eatClip != null) AudioSource.PlayClipAtPoint(eatClip, transform.position, 1f);
        if (grab != null)
        {
            if (grab.isSelected && grab.interactionManager != null) grab.interactionManager.CancelInteractableSelection((UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)grab);
            grab.enabled = false;
        }
        var rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;
        StartCoroutine(Vanish());
    }

    IEnumerator Vanish()
    {
        Vector3 s0 = transform.localScale;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.3f)
        {
            transform.localScale = s0 * (1f - t);
            if (head != null) transform.position = Vector3.Lerp(transform.position, head.position - head.up * 0.08f, t * 0.5f);
            yield return null;
        }
        Destroy(gameObject);
    }
}
