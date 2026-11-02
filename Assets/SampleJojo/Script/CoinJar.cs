using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Bocal à pièces posé sur notre table. Les pièces dedans sont de vraies pièces (on peut les reprendre
/// pour les mettre dans la machine) et elles sont comptées sur la calculatrice.
/// Au début du niveau, nos pièces sont dans le bocal (5 pièces de 1 au départ).
/// </summary>
public class CoinJar : MonoBehaviour
{
    [System.Serializable]
    public class CoinPrefab
    {
        public int value;
        public GameObject prefab;
    }

    public static CoinJar Active;

    public CoinPrefab[] coinPrefabs;
    [Header("Intérieur (repère local du bocal, en mètres)")]
    public float innerRadius = 0.095f;
    public float innerBottom = 0.02f;
    public float innerTop = 0.26f;
    [Tooltip("Nombre max de pièces posées dans le bocal au début d'un niveau.")]
    public int maxCoins = 30;

    readonly HashSet<JackpotCoin> inside = new HashSet<JackpotCoin>();
    int value;
    float nextScan;

    public int Value => value;
    public int Count => inside.Count;

    void Awake() { Active = this; }

    void Start()
    {
        // Les mains (rayons) passent à travers le verre : on peut viser et prendre les pièces dedans.
        var cols = GetComponentsInChildren<Collider>();
        if (cols.Length == 0) return;
        int bit = 1 << cols[0].gameObject.layer;
        if (cols[0].gameObject.layer == 0) return;                 // pas de calque dédié : on ne touche à rien
        foreach (var ray in FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            ray.raycastMask &= ~bit;
        foreach (var caster in FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Interactors.Casters.CurveInteractionCaster>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            caster.raycastMask &= ~bit;
        foreach (var caster in FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Interactors.Casters.SphereInteractionCaster>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            caster.physicsLayerMask &= ~bit;
    }
    void OnDestroy() { if (Active == this) Active = null; }

    public bool IsInside(Vector3 world)
    {
        Vector3 p = transform.InverseTransformPoint(world);
        return new Vector2(p.x, p.z).magnitude < innerRadius + 0.01f && p.y > innerBottom - 0.03f && p.y < innerTop + 0.02f;
    }

    void Update()
    {
        if (Time.time < nextScan) return;
        nextScan = Time.time + 0.2f;
        Rescan();
    }

    void Rescan()
    {
        inside.Clear();
        int v = 0;
        foreach (var c in FindObjectsByType<JackpotCoin>(FindObjectsSortMode.None))
        {
            if (c == null || c.IsCollected || c.IsHeld || !IsInside(c.transform.position)) continue;
            inside.Add(c);
            v += c.value;
        }
        if (v != value)
        {
            value = v;
            if (JackpotBag.Active != null) JackpotBag.Active.NotifyChanged();
        }
    }

    /// <summary>Met des pièces dans le bocal pour un total donné. Renvoie ce qui n'a pas pu être mis (reste dans le sac).</summary>
    public int Fill(int total, bool smallCoins)
    {
        var types = coinPrefabs.Where(c => c != null && c.prefab != null && c.value > 0).OrderByDescending(c => c.value).ToArray();
        if (types.Length == 0 || total <= 0) return total;
        var list = new List<CoinPrefab>();
        int rem = total;
        var one = types.Last();
        if (smallCoins && total <= 10) { while (rem >= one.value && list.Count < maxCoins) { list.Add(one); rem -= one.value; } }
        else foreach (var t in types) while (rem >= t.value && list.Count < maxCoins) { list.Add(t); rem -= t.value; }

        // Les plus grosses au fond.
        for (int i = 0; i < list.Count; i++)
        {
            var t = list[i];
            Vector2 r = Random.insideUnitCircle * innerRadius * 0.25f;
            Vector3 p = transform.TransformPoint(new Vector3(r.x, innerBottom + 0.015f + i * 0.014f, r.y));
            var go = Instantiate(t.prefab, p, Quaternion.Euler(Random.Range(-8f, 8f), Random.Range(0f, 360f), Random.Range(-8f, 8f)));
            go.name = t.prefab.name;
            var coin = go.GetComponent<JackpotCoin>();
            if (coin != null) { inside.Add(coin); value += coin.value; }
        }
        if (JackpotBag.Active != null) JackpotBag.Active.NotifyChanged();
        return rem;
    }

    /// <summary>Valeur des pièces hors du bocal (posées sur la table, par terre…) qui ne sont pas encore rangées.</summary>
    public int LooseValue()
    {
        int v = 0;
        foreach (var c in FindObjectsByType<JackpotCoin>(FindObjectsSortMode.None))
            if (c != null && !c.IsCollected && !IsInside(c.transform.position)) v += c.value;
        return v;
    }

    /// <summary>Gain de la machine : les pièces apparaissent directement dans le bocal (téléportées), une par une.</summary>
    public void Deposit(List<int> values)
    {
        if (values == null || values.Count == 0) return;
        StartCoroutine(DepositRoutine(values));
    }

    [Header("Gain : les pièces apparaissent au-dessus du bocal et tombent dedans")]
    [Tooltip("Hauteur d'apparition au-dessus de l'ouverture (m).")]
    public float popHeight = 0.14f;
    [Tooltip("Temps entre deux pièces (s).")]
    public float popInterval = 0.13f;
    public AudioClip popClip;

    System.Collections.IEnumerator DepositRoutine(List<int> values)
    {
        int total = 0;
        foreach (int v in values) total += v;
        ShowGain(total);
        if (popClip == null && JackpotBag.Active != null) popClip = JackpotBag.Active.coinClip;
        // Les grosses d'abord (elles tombent au fond).
        values.Sort((a, b) => b.CompareTo(a));
        foreach (int v in values)
        {
            var t = coinPrefabs.FirstOrDefault(c => c != null && c.prefab != null && c.value == v);
            if (t == null) continue;
            // Au-dessus de l'ouverture, bien au centre pour qu'elle tombe dedans.
            Vector2 r = Random.insideUnitCircle * innerRadius * 0.2f;
            Vector3 p = transform.TransformPoint(new Vector3(r.x, innerTop, r.y)) + Vector3.up * popHeight;
            var go = Instantiate(t.prefab, p, Quaternion.Euler(Random.Range(-30f, 30f), Random.Range(0f, 360f), Random.Range(-30f, 30f)));
            go.name = t.prefab.name;
            var rb = go.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.down * 0.3f;
                rb.angularVelocity = Random.insideUnitSphere * 8f;
            }
            StartCoroutine(PopIn(go.transform, rb));
            if (popClip != null) AudioSource.PlayClipAtPoint(popClip, p, 0.5f);
            yield return new WaitForSeconds(popInterval);
        }
        yield return new WaitForSeconds(0.8f);
        Rescan();
    }

    /// <summary>« Pop » : la pièce apparaît en grossissant avec un petit rebond, suspendue un instant, puis tombe.</summary>
    System.Collections.IEnumerator PopIn(Transform t, Rigidbody rb)
    {
        Vector3 s1 = t.localScale;
        if (rb != null) { rb.useGravity = false; rb.linearVelocity = Vector3.zero; }
        for (float k = 0f; k < 1f && t != null; k += Time.deltaTime / 0.18f)
        {
            float e = Mathf.SmoothStep(0f, 1f, k) + Mathf.Sin(k * Mathf.PI) * 0.35f;
            t.localScale = s1 * Mathf.Max(0.05f, e);
            yield return null;
        }
        if (t == null) yield break;
        t.localScale = s1;
        yield return new WaitForSeconds(0.08f);
        if (rb != null) { rb.useGravity = true; rb.linearVelocity = Vector3.down * 0.4f; rb.WakeUp(); }
    }

    /// <summary>« +25 » doré qui monte au-dessus du bocal.</summary>
    void ShowGain(int amount)
    {
        var go = new GameObject("Gain bocal");
        go.transform.position = transform.TransformPoint(new Vector3(0f, innerTop + 0.08f, 0f));
        var tmp = go.AddComponent<TMPro.TextMeshPro>();
        var any = FindFirstObjectByType<TMPro.TMP_Text>();
        if (any != null && any.font != null) tmp.font = any.font;
        tmp.text = "+" + amount;
        tmp.fontSize = 0.6f;
        tmp.fontStyle = TMPro.FontStyles.Bold;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.color = new Color(1f, 0.85f, 0.25f);
        tmp.rectTransform.sizeDelta = new Vector2(0.3f, 0.1f);
        StartCoroutine(FloatUp(tmp));
    }

    System.Collections.IEnumerator FloatUp(TMPro.TextMeshPro tmp)
    {
        var cam = Camera.main != null ? Camera.main.transform : null;
        Vector3 p0 = tmp.transform.position;
        for (float k = 0f; k < 1f; k += Time.deltaTime / 1.6f)
        {
            tmp.transform.position = p0 + Vector3.up * 0.12f * k;
            if (cam != null) tmp.transform.rotation = Quaternion.LookRotation(tmp.transform.position - cam.position, Vector3.up);
            tmp.alpha = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
            yield return null;
        }
        Destroy(tmp.gameObject);
    }

    /// <summary>Vide le bocal (début du tuto : les pièces sont données plus tard).</summary>
    public void Clear()
    {
        Rescan();
        foreach (var c in inside) if (c != null) Destroy(c.gameObject);
        inside.Clear();
        value = 0;
        if (JackpotBag.Active != null) JackpotBag.Active.NotifyChanged();
    }

    /// <summary>
    /// Retire du bocal des pièces pour payer <paramref name="amount"/> (achat à la boutique).
    /// Renvoie la monnaie rendue (si on a dû prendre une pièce plus grosse).
    /// </summary>
    public int Take(int amount)
    {
        Rescan();
        var coins = inside.Where(c => c != null).OrderByDescending(c => c.value).ToList();
        var picked = new List<JackpotCoin>();
        int rem = amount;
        foreach (var c in coins) if (c.value <= rem) { picked.Add(c); rem -= c.value; if (rem == 0) break; }
        if (rem > 0)
        {
            var bigger = coins.Where(c => !picked.Contains(c) && c.value >= rem).OrderBy(c => c.value).FirstOrDefault();
            if (bigger != null) { picked.Add(bigger); rem -= bigger.value; }
            else foreach (var c in coins) if (!picked.Contains(c)) { picked.Add(c); rem -= c.value; if (rem <= 0) break; }
        }
        foreach (var c in picked) { inside.Remove(c); value -= c.value; Destroy(c.gameObject); }
        return Mathf.Max(0, -rem);
    }
}
