using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.XR.CoreUtils;
using UnityEngine;

/// <summary>
/// Quand on gagne à la machine, les pièces explosent de la machine vers le joueur.
/// Le gain (valeur) est découpé en pièces de 100, 50, 25, 10, 5 et 1 (plus la valeur est grande, plus la pièce est grosse).
/// À mettre sur la machine (avec JackpotItem).
/// </summary>
public class JackpotCoins : MonoBehaviour
{
    [System.Serializable]
    public class CoinType
    {
        public int value;
        public GameObject prefab;
    }

    public JackpotLever lever;
    public JackpotItem item;
    [Tooltip("Une pièce par valeur (1, 5, 10, 25, 50, 100).")]
    public CoinType[] coins;

    [Header("Table")]
    [Tooltip("Les pièces retombent sur la table, dans ce rayon autour de la machine (m).")]
    public float landRadius = 0.5f;
    [Tooltip("Marge au bord de la table (m).")]
    public float tableMargin = 0.08f;
    [Tooltip("Durée du vol d'une pièce (s).")]
    public Vector2 flightTime = new Vector2(0.55f, 0.8f);
    [Tooltip("Une pièce qui tombe de la table pendant ce temps revient dessus.")]
    public float keepOnTableFor = 6f;

    [Header("Explosion")]
    [Tooltip("Nombre max de pièces pour un gain (au-delà, on prend de plus grosses pièces).")]
    public int maxCoins = 25;
    public float burstDuration = 0.7f;
    public Vector2 upSpeed = new Vector2(2.2f, 3.4f);
    public Vector2 outSpeed = new Vector2(0.6f, 1.8f);
    [Tooltip("Ouverture de la gerbe de pièces (degrés).")]
    public float spreadAngle = 80f;

    [Header("Sons")]
    public AudioSource audioSource;
    public AudioClip burstClip;

    [Header("Mains")]
    [Tooltip("Les mains poussent les pièces.")]
    public bool addHandPushers = true;

    CharacterController playerBody;
    Transform cam;

    void OnEnable()
    {
        if (lever != null) { lever.onPayout.RemoveListener(Payout); lever.onPayout.AddListener(Payout); }
    }

    void OnDestroy()
    {
        if (lever != null) lever.onPayout.RemoveListener(Payout);
    }

    void Start()
    {
        var origin = FindFirstObjectByType<XROrigin>();
        if (origin != null)
        {
            playerBody = origin.GetComponentInChildren<CharacterController>();
            if (origin.Camera != null) cam = origin.Camera.transform;
            if (addHandPushers)
                foreach (var n in new[] { "Left Controller", "Right Controller" })
                {
                    var t = FindDeep(origin.transform, n);
                    if (t != null) HandPusher.AddTo(t);
                }
        }
        if (cam == null && Camera.main != null) cam = Camera.main.transform;
    }

    public void Payout(int amount)
    {
        if (amount <= 0 || coins == null || coins.Length == 0) return;
        var list = Split(amount);
        Debug.Log($"[Pièces] Gain de {amount} : " + string.Join(" + ", list.GroupBy(v => v).OrderByDescending(g => g.Key).Select(g => g.Count() + "×" + g.Key)));
        // Les pièces gagnées se téléportent directement dans le bocal.
        if (CoinJar.Active != null && CoinJar.Active.isActiveAndEnabled)
        {
            if (burstClip != null) AudioSource.PlayClipAtPoint(burstClip, CoinJar.Active.transform.position, 0.8f);
            CoinJar.Active.Deposit(list);
            return;
        }
        StartCoroutine(Burst(list));
    }

    /// <summary>Découpe le gain avec le moins de pièces possible (les plus grosses d'abord).</summary>
    public List<int> Split(int amount)
    {
        var vals = coins.Where(c => c != null && c.prefab != null && c.value > 0).Select(c => c.value).Distinct().OrderByDescending(v => v).ToArray();
        var list = new List<int>();
        int rem = amount;
        foreach (int v in vals) { while (rem >= v && list.Count < maxCoins) { list.Add(v); rem -= v; } }
        return list;
    }

    IEnumerator Burst(List<int> values)
    {
        if (audioSource != null && burstClip != null) audioSource.PlayOneShot(burstClip);

        // Sortie : dessus de la machine, vers le joueur.
        Bounds b = MachineBounds();
        Vector3 toPlayer = cam != null ? cam.position - b.center : -transform.forward;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 1e-4f) toPlayer = transform.forward;
        toPlayer.Normalize();
        Vector3 spawn = new Vector3(b.center.x, b.max.y + 0.06f, b.center.z) + toPlayer * Mathf.Min(0.1f, b.extents.magnitude * 0.3f);
        machineBounds = b;
        lastToPlayer = toPlayer;
        bool onTable = FindTableHeight(b, toPlayer, out tableY);

        // Les grosses pièces d'abord, réparties sur la durée.
        var order = values.OrderBy(_ => Random.value).OrderByDescending(v => v >= 50 ? 1 : 0).ToList();
        float dt = order.Count > 1 ? burstDuration / order.Count : 0f;
        foreach (int v in order)
        {
            var type = coins.FirstOrDefault(c => c != null && c.value == v && c.prefab != null);
            if (type != null) SpawnCoin(type.prefab, spawn, toPlayer, onTable);
            if (dt > 0f) yield return new WaitForSeconds(dt);
        }
    }

    Bounds machineBounds;
    Vector3 lastToPlayer;
    float tableY;
    readonly HashSet<Collider> tableColliders = new HashSet<Collider>();

    void SpawnCoin(GameObject prefab, Vector3 spawn, Vector3 toPlayer, bool onTable)
    {
        Vector3 pos = spawn + new Vector3(Random.Range(-0.04f, 0.04f), Random.Range(0f, 0.03f), Random.Range(-0.04f, 0.04f));
        var go = Instantiate(prefab, pos, Random.rotation);
        go.name = prefab.name;
        var rb = go.GetComponent<Rigidbody>();
        if (rb != null)
        {
            if (onTable && TryLandingPoint(out Vector3 target))
            {
                // Tir calculé pour retomber sur un point libre de la table.
                float t = Random.Range(flightTime.x, flightTime.y);
                rb.linearVelocity = (target + Vector3.up * 0.03f - pos - 0.5f * Physics.gravity * t * t) / t;
            }
            else
            {
                Vector3 dir = Quaternion.AngleAxis(Random.Range(-spreadAngle, spreadAngle) * 0.5f, Vector3.up) * toPlayer;
                rb.linearVelocity = Vector3.up * Random.Range(upSpeed.x, upSpeed.y) + dir * Random.Range(outSpeed.x, outSpeed.y);
            }
            rb.angularVelocity = Random.insideUnitSphere * 12f;
        }
        var coin = go.GetComponent<JackpotCoin>();
        if (coin != null && onTable) coin.KeepOnTable(this, tableY, keepOnTableFor);
        // Les pièces ne bloquent pas le joueur.
        if (playerBody != null)
            foreach (var c in go.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(c, playerBody);
    }

    /// <summary>Premier obstacle solide sous un point (on ignore la machine, les pièces et les zones invisibles).</summary>
    bool SurfaceBelow(Vector3 from, float maxDist, out RaycastHit surface)
    {
        surface = default;
        var hits = Physics.RaycastAll(from, Vector3.down, maxDist, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
        foreach (var h in hits)
        {
            if (h.collider.transform.IsChildOf(transform)) continue;
            if (h.collider.GetComponentInParent<JackpotCoin>() != null) continue;
            surface = h;
            return true;
        }
        return false;
    }

    /// <summary>Hauteur de la table : mesurée juste à côté de la machine.</summary>
    bool FindTableHeight(Bounds machine, Vector3 toPlayer, out float y)
    {
        y = machine.min.y;
        var hits = new List<RaycastHit>();
        for (int i = 0; i < 8; i++)
        {
            Vector3 dir = Quaternion.AngleAxis(i * 45f, Vector3.up) * toPlayer;
            Vector3 p = machine.center + dir * (Mathf.Max(machine.extents.x, machine.extents.z) + 0.08f);
            p.y = machine.max.y;
            if (SurfaceBelow(p, machine.size.y + 0.4f, out var h) && h.point.y > machine.min.y - 0.15f) hits.Add(h);
        }
        if (hits.Count == 0) return false;
        hits.Sort((a, b) => a.point.y.CompareTo(b.point.y));
        y = hits[hits.Count / 2].point.y;
        // La table = le ou les objets touchés à cette hauteur (pas une chaise à côté).
        tableColliders.Clear();
        foreach (var h in hits) if (Mathf.Abs(h.point.y - y) < 0.02f) tableColliders.Add(h.collider);
        return true;
    }

    /// <summary>Point libre sur la table (pas sur la machine), plutôt du côté du joueur.</summary>
    public bool TryLandingPoint(out Vector3 point)
    {
        Bounds m = machineBounds;
        float inner = Mathf.Max(m.extents.x, m.extents.z) + 0.06f;
        point = Vector3.zero;
        float bestScore = float.MinValue;
        for (int i = 0; i < 20; i++)
        {
            Vector2 r = Random.insideUnitCircle.normalized * Random.Range(inner, Mathf.Max(inner + 0.05f, landRadius));
            Vector3 p = new Vector3(m.center.x + r.x, tableY + 0.4f, m.center.z + r.y);
            if (!SurfaceBelow(p, 0.6f, out var h)) continue;
            if (!OnTable(h)) continue;                                        // pas la table (vide, chaise, autre objet)
            // Marge : le bord de la table doit être assez loin.
            bool edge = false;
            for (int k = 0; k < 4 && !edge; k++)
            {
                Vector3 q = h.point + Quaternion.AngleAxis(k * 90f, Vector3.up) * Vector3.forward * tableMargin + Vector3.up * 0.3f;
                if (!SurfaceBelow(q, 0.45f, out var hq) || !OnTable(hq)) edge = true;
            }
            if (edge) continue;
            Vector3 d = h.point - m.center; d.y = 0f;
            float score = Vector3.Dot(d.normalized, lastToPlayer) * 0.5f + Random.value;
            if (score > bestScore) { bestScore = score; point = h.point; }
        }
        return bestScore > float.MinValue;
    }

    bool OnTable(RaycastHit h) => Mathf.Abs(h.point.y - tableY) < 0.05f && (tableColliders.Count == 0 || tableColliders.Contains(h.collider));

    /// <summary>Ce point est-il posé sur la table (et pas sur une chaise ou un autre meuble) ?</summary>
    public bool IsOverTable(Vector3 p) => SurfaceBelow(p + Vector3.up * 0.05f, 0.3f, out var h) && OnTable(h);

    /// <summary>Hauteur de la table utilisée pour le dernier gain.</summary>
    public float TableY => tableY;

    Bounds MachineBounds()
    {
        bool first = true;
        Bounds b = new Bounds(transform.position, Vector3.zero);
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled || r is ParticleSystemRenderer) continue;
            if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
        }
        return b;
    }

    static Transform FindDeep(Transform r, string n)
    {
        if (r.name == n) return r;
        foreach (Transform c in r)
        {
            var f = FindDeep(c, n);
            if (f != null) return f;
        }
        return null;
    }

    // ---------------- Tests sans casque ----------------
    [ContextMenu("Test : faire sortir 500 (en Play)")]
    public void TestBigWin() { if (Application.isPlaying) Payout(500); }

    [ContextMenu("Test : faire sortir 25 (en Play)")]
    public void TestSmallWin() { if (Application.isPlaying) Payout(25); }
}
