using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Une pièce gagnée à la machine : elle a une valeur, on peut l'attraper, la lancer, la pousser avec la main.
/// Lâchée (ou tombée) dans le sac, elle est récoltée.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class JackpotCoin : MonoBehaviour
{
    public int value = 1;
    public AudioClip clinkClip;
    [Tooltip("Force avec laquelle la main pousse la pièce (0 = pas de poussée).")]
    public float pushStrength = 0.6f;
    [Tooltip("Délai avant de pouvoir être récoltée (pour ne pas la ranger en sortant de la machine).")]
    public float collectDelay = 0.6f;

    JackpotCoins keepSpawner;
    float keepY, keepUntil;

    Rigidbody rb;
    XRGrabInteractable grab;
    float spawnTime, lastClink;
    bool collected;
    static float lastGlobalClink;

    public bool IsHeld => grab != null && grab.isSelected;
    public bool IsCollected => collected;
    public float Age => Time.time - spawnTime;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        grab = GetComponent<XRGrabInteractable>();
        spawnTime = Time.time;
    }

    void FixedUpdate()
    {
        if (collected) return;
        if (transform.position.y < -20f) { Destroy(gameObject); return; }
        if (keepSpawner != null) KeepOnTableCheck();
        var bag = JackpotBag.Active;
        if (bag != null && !IsHeld && Time.time - spawnTime > collectDelay && bag.InCollectZone(transform.position))
            bag.Collect(this);
    }

    /// <summary>Pendant <paramref name="seconds"/> s, si la pièce tombe de la table, elle revient dessus.</summary>
    public void KeepOnTable(JackpotCoins spawner, float tableY, float seconds)
    {
        keepSpawner = spawner;
        keepY = tableY;
        keepUntil = Time.time + seconds;
    }

    void KeepOnTableCheck()
    {
        if (IsHeld || Time.time > keepUntil) { keepSpawner = null; return; }      // attrapée : elle est libre
        bool fell = transform.position.y < keepY - 0.1f;
        // Arrêtée ailleurs que sur la table (chaise, objet voisin…) : on la remet aussi.
        bool strayed = !fell && rb.linearVelocity.sqrMagnitude < 0.01f && Time.time > keepUntil - 4.5f && !keepSpawner.IsOverTable(transform.position);
        if (!fell && !strayed) return;
        // Tombée : on la repose sur la table.
        if (!keepSpawner.TryLandingPoint(out Vector3 p)) { keepSpawner = null; return; }
        p += Vector3.up * 0.05f;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        var rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        rb.position = p;
        rb.rotation = rot;
        transform.SetPositionAndRotation(p, rot);
    }

    void OnCollisionEnter(Collision c)
    {
        if (collected || clinkClip == null) return;
        float v = c.relativeVelocity.magnitude;
        if (v < 1f || Time.time - lastClink < 0.2f || Time.time - lastGlobalClink < 0.05f) return;
        lastClink = lastGlobalClink = Time.time;
        AudioSource.PlayClipAtPoint(clinkClip, transform.position, Mathf.Clamp01(v / 6f) * 0.5f);
    }

    // La main (sans rien tenir) pousse les pièces.
    void OnTriggerStay(Collider other)
    {
        if (collected || IsHeld || pushStrength <= 0f) return;
        var hand = other.GetComponent<HandPusher>();
        if (hand == null) return;
        Vector3 v = hand.Velocity;
        if (v.sqrMagnitude < 0.04f) return;
        rb.WakeUp();
        Vector3 rel = v - rb.linearVelocity;
        if (Vector3.Dot(rel, v) <= 0f) return;            // la pièce va déjà plus vite que la main
        rb.AddForce(rel * pushStrength, ForceMode.VelocityChange);
    }

    /// <summary>La pièce file dans le sac puis disparaît.</summary>
    public void CollectInto(Vector3 target)
    {
        if (collected) return;
        collected = true;
        if (grab != null) grab.enabled = false;
        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
        rb.isKinematic = true;
        StartCoroutine(Suck(target));
    }

    IEnumerator Suck(Vector3 target)
    {
        Vector3 p0 = transform.position, s0 = transform.localScale;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.25f)
        {
            transform.position = Vector3.Lerp(p0, target, t * t);
            transform.localScale = s0 * (1f - t * 0.8f);
            yield return null;
        }
        Destroy(gameObject);
    }
}
