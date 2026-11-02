using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// La machine à sous devient un objet qu'on porte et qu'on peut lancer.
/// - Rangée : elle reste cachée DANS le sac, avec une poignée invisible à l'ouverture.
///   On plonge la main et on serre : c'est directement la machine qu'on attrape.
/// - On la pose sur l'emplacement en pointillés (au-dessus du livre), on la lance,
///   ou on la lâche près du sac pour la ranger (elle se range aussi si elle atterrit à côté du sac).
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class JackpotItem : MonoBehaviour
{
    [Tooltip("Le sac dans lequel la machine se range.")]
    public JackpotBag bag;
    [Tooltip("Collider principal de la machine.")]
    public BoxCollider body;
    [Tooltip("Poignée invisible active seulement quand la machine est dans le sac.")]
    public SphereCollider bagHandle;

    [Header("Lancer (plus léger)")]
    [Tooltip("Multiplie la vitesse de la main au moment du lâcher.")]
    public float throwStrength = 2.2f;
    public float throwSpin = 1f;
    [Tooltip("Part de la gravité pendant le vol (1 = normale, 0.5 = flotte deux fois plus).")]
    [Range(0.2f, 1f)] public float flightGravity = 0.55f;
    public float mass = 1f;

    XRGrabInteractable grab;
    Rigidbody rb;
    Renderer[] renderers;
    Collider[] leverColliders;
    JackpotLever lever;
    JackpotFX fx;
    AudioSource[] sources;
    bool stored;
    float releasedAt = -10f;

    public bool IsStored => stored;
    public XRGrabInteractable Grab => grab != null ? grab : (grab = GetComponent<XRGrabInteractable>());
    public Vector3 Center => body != null ? transform.TransformPoint(body.center) : transform.position;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        rb = GetComponent<Rigidbody>();
        renderers = GetComponentsInChildren<Renderer>(true);
        lever = GetComponentInChildren<JackpotLever>(true);
        leverColliders = lever != null ? lever.GetComponentsInChildren<Collider>(true) : new Collider[0];
        fx = GetComponent<JackpotFX>();
        sources = GetComponentsInChildren<AudioSource>(true);
        if (bagHandle != null) bagHandle.enabled = false;
        grab.throwVelocityScale = throwStrength;
        grab.throwAngularVelocityScale = throwSpin;
        if (rb != null)
        {
            rb.mass = mass;
            rb.linearDamping = 0.05f;
            rb.angularDamping = 0.5f;
        }
        SetLeverActive(false);
    }

    // ---------------- Levier : seulement quand la machine est posée ----------------
    bool placed;
    public bool IsPlaced => placed;

    // Le composant du levier reste actif tant que la machine est sortie, pour qu'un tirage en cours
    // finisse son animation même si on reprend la machine. On bloque seulement le fait de tirer.
    // Il n'est coupé (et remis à zéro) que quand la machine est rangée dans le sac.
    void SetLeverActive(bool on)
    {
        if (lever == null) return;
        bool keepRunning = !stored;
        if (lever.enabled != keepRunning) lever.enabled = keepRunning;
        lever.SetUsable(on && !stored);
    }

    void UpdatePlaced()
    {
        placed = false;
        foreach (var i in Grab.interactorsSelecting)
            if (i is XRSocketInteractor) { placed = true; break; }
        SetLeverActive(placed && !stored);
    }

    // Gravité réduite pendant le vol : la machine paraît plus légère quand on la lance.
    void FixedUpdate()
    {
        if (stored || rb == null || rb.isKinematic || !rb.useGravity || Grab.isSelected) return;
        rb.AddForce(-Physics.gravity * (1f - flightGravity), ForceMode.Acceleration);
    }

    void OnEnable()
    {
        Grab.hoverEntered.AddListener(OnHoverEnter);
        Grab.hoverExited.AddListener(OnHoverExit);
        Grab.selectEntered.AddListener(OnGrabbed);
        Grab.selectExited.AddListener(OnReleased);
        DisableConcaveColliders();
    }

    void OnDisable()
    {
        Grab.hoverEntered.RemoveListener(OnHoverEnter);
        Grab.hoverExited.RemoveListener(OnHoverExit);
        Grab.selectEntered.RemoveListener(OnGrabbed);
        Grab.selectExited.RemoveListener(OnReleased);
    }

    // ---------------- Rangée / sortie ----------------
    /// <summary>Cache la machine dans le sac (true) ou la rend visible et utilisable (false).</summary>
    public void SetStored(bool value)
    {
        stored = value;
        foreach (var r in renderers) if (r != null) r.enabled = !value;
        if (body != null) body.enabled = !value;
        if (bagHandle != null) bagHandle.enabled = value;
        foreach (var c in leverColliders) if (c != null) c.enabled = !value;
        SetLeverActive(false);                          // rangée : levier remis à zéro ; sortie : en main, pas tirable
        if (fx != null) fx.enabled = !value;            // pas de lampes ni de calculs quand elle est rangée
        if (value)
        {
            foreach (var s in sources) if (s != null) s.Stop();
            if (rb != null)
            {
                if (!rb.isKinematic)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                rb.isKinematic = true;
            }
        }
        DisableConcaveColliders();
    }

    /// <summary>Quand elle est rangée, la machine se tient à l'ouverture du sac (sa poignée est là).</summary>
    public void FollowBag(Vector3 handlePoint, Quaternion rotation)
    {
        if (!stored || Grab.isSelected) return;
        PlaceCenterAt(handlePoint, rotation);
    }

    // La main approche du sac : on prépare la physique AVANT la prise, pour pouvoir lancer la machine ensuite.
    void OnHoverEnter(HoverEnterEventArgs args)
    {
        if (!stored || rb == null || args.interactorObject is XRSocketInteractor) return;
        rb.constraints = RigidbodyConstraints.FreezeAll;
        rb.useGravity = false;
        rb.isKinematic = false;
    }

    void OnHoverExit(HoverExitEventArgs args)
    {
        if (!stored || Grab.isSelected || Grab.isHovered || rb == null) return;
        rb.isKinematic = true;
    }

    void OnGrabbed(SelectEnterEventArgs args)
    {
        if (rb != null) rb.constraints = RigidbodyConstraints.None;
        UpdatePlaced();
        if (!stored || args.interactorObject is XRSocketInteractor) return;
        SetStored(false);                                // elle apparaît directement dans la main
        if (bag != null) bag.OnTakenOut();
    }

    void OnReleased(SelectExitEventArgs args)
    {
        UpdatePlaced();
        if (args.isCanceled || !isActiveAndEnabled || stored) return;
        if (args.interactorObject is XRSocketInteractor) return;     // on vient de la retirer de l'emplacement
        if (Grab.isSelected) return;                                 // encore tenue (autre main)
        // Lâchée ou lancée : la gravité reprend tout de suite (la vitesse du lancer est appliquée par XRI).
        DisableConcaveColliders();
        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.None;
            rb.useGravity = true;
            rb.isKinematic = false;
        }
        releasedAt = Time.time;
        StartCoroutine(CheckAfterRelease());
    }

    IEnumerator CheckAfterRelease()
    {
        // On laisse le temps à l'emplacement en pointillés de l'attraper.
        yield return null;
        yield return null;
        if (stored || Grab.isSelected) yield break;
        if (bag != null && bag.IsNear(this)) bag.Store(this);
    }

    void Update()
    {
        if (stored || bag == null || Grab.isSelected) return;
        // Retombée à côté du sac (lancée dedans ou posée par terre près de lui) : elle se range.
        if (Time.time - releasedAt > 0.3f && bag.IsNear(this, 0.45f) && (rb == null || rb.linearVelocity.sqrMagnitude < 0.25f))
        {
            bag.Store(this);
            return;
        }
        // Sécurité : si elle tombe sous le sol, elle retourne dans le sac.
        if (transform.position.y < bag.FloorY - 3f) bag.Store(this);
    }

    /// <summary>Place la machine pour que son centre soit à <paramref name="worldPos"/>.</summary>
    public void PlaceCenterAt(Vector3 worldPos, Quaternion rotation)
    {
        transform.rotation = rotation;
        transform.position += worldPos - Center;
    }

    /// <summary>
    /// Un Mesh Collider concave (ajouté par exemple pour la téléportation) est interdit sur un objet
    /// soumis à la gravité : le Box Collider de la machine suffit.
    /// </summary>
    void DisableConcaveColliders()
    {
        foreach (var mc in GetComponentsInChildren<MeshCollider>(true))
            if (!mc.convex && mc.enabled) mc.enabled = false;
    }
}
