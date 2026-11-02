using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Remet un objet à sa place de départ s'il tombe hors du monde (sous le sol)
/// ou s'il est resté loin et immobile trop longtemps (optionnel).
/// </summary>
public class PropRespawn : MonoBehaviour
{
    [Tooltip("En dessous de cette hauteur (par rapport au départ), l'objet revient à sa place.")]
    public float fallLimit = 3f;
    [Tooltip("Si > 0 : revient à sa place après ce temps posé ailleurs (secondes). 0 = jamais.")]
    public float returnAfter = 0f;

    Vector3 startPos;
    Quaternion startRot;
    Rigidbody rb;
    XRGrabInteractable grab;
    float idle;

    void Start()
    {
        startPos = transform.position;
        startRot = transform.rotation;
        rb = GetComponent<Rigidbody>();
        grab = GetComponent<XRGrabInteractable>();
    }

    void Update()
    {
        if (grab != null && grab.isSelected) { idle = 0f; return; }
        if (transform.position.y < startPos.y - fallLimit) { Respawn(); return; }
        if (returnAfter > 0f && (transform.position - startPos).sqrMagnitude > 0.04f)
        {
            idle += Time.deltaTime;
            if (idle > returnAfter) Respawn();
        }
        else idle = 0f;
    }

    public void Respawn()
    {
        idle = 0f;
        if (rb != null && !rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        transform.SetPositionAndRotation(startPos, startRot);
    }
}
