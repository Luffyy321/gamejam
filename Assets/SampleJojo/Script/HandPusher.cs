using UnityEngine;

/// <summary>
/// Petite zone invisible dans la main : elle pousse les pièces (voir JackpotCoin).
/// Créée automatiquement sur chaque manette par JackpotCoins.
/// </summary>
public class HandPusher : MonoBehaviour
{
    Vector3 last;
    bool init;
    public Vector3 Velocity { get; private set; }

    void FixedUpdate()
    {
        Vector3 p = transform.position;
        if (init) Velocity = (p - last) / Time.fixedDeltaTime;
        last = p;
        init = true;
    }

    /// <summary>Ajoute une zone qui pousse les pièces sous <paramref name="hand"/>.</summary>
    public static HandPusher AddTo(Transform hand)
    {
        var existing = hand.GetComponentInChildren<HandPusher>();
        if (existing != null) return existing;
        var go = new GameObject("Pousse-pièces");
        go.layer = 2;                                   // Ignore Raycast : invisible pour les rayons
        go.transform.SetParent(hand, false);
        go.transform.localPosition = new Vector3(0f, -0.03f, -0.04f);
        var rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        var sc = go.AddComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius = 0.06f;
        return go.AddComponent<HandPusher>();
    }
}
