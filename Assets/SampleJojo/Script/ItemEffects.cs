using System.Collections;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Vitesse du temps pour la classe (prof, chrono, élèves). La machine à sous n'est jamais ralentie.</summary>
public static class SlowMo
{
    public static float Factor = 1f;
}

/// <summary>
/// Effets des objets de la boutique, pour le cours en cours (tout revient à la normale au cours suivant) :
/// - lunettes de Doflamingo : filtre rose ;
/// - lunettes 3D : les mots clés du livre en gras surlignés ;
/// - café gourmand : le temps ralentit 30 s (pas la machine à sous).
/// </summary>
public class ItemEffects : MonoBehaviour
{
    static ItemEffects inst;
    Transform cam;
    Renderer pink, slowTint;
    Material pinkMat, slowMat;
    float slowLeft, slowShown;

    public static bool PinkOn { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnScene;
        SceneManager.sceneLoaded += OnScene;
        ResetAll();
    }

    static void OnScene(Scene s, LoadSceneMode m) => ResetAll();

    static void ResetAll()
    {
        SlowMo.Factor = 1f;
        PinkOn = false;
        JackpotBook.SetKeywords(false);
    }

    static ItemEffects Get()
    {
        if (inst == null) inst = new GameObject("Effets des objets").AddComponent<ItemEffects>();
        return inst;
    }

    void Awake()
    {
        var origin = FindFirstObjectByType<XROrigin>();
        cam = origin != null && origin.Camera != null ? origin.Camera.transform : Camera.main != null ? Camera.main.transform : null;
    }

    void OnDestroy() { if (inst == this) { inst = null; SlowMo.Factor = 1f; } }

    // ---------------- Lunettes de Doflamingo ----------------
    public static void PinkFilter()
    {
        var e = Get();
        PinkOn = true;
        if (e.pink == null) e.pink = e.Veil("Filtre rose", 3400, out e.pinkMat);
        if (e.pinkMat != null) e.pinkMat.SetColor("_BaseColor", new Color(1f, 0.32f, 0.72f, 0.24f));
        if (e.pink != null) e.pink.enabled = true;
        Debug.Log("[Objets] Lunettes de Doflamingo : tout est rose.");
    }

    // ---------------- Lunettes 3D ----------------
    public static void Keywords()
    {
        JackpotBook.SetKeywords(true);
        Debug.Log("[Objets] Lunettes 3D : les mots clés du livre sont surlignés.");
    }

    // ---------------- Café gourmand ----------------
    public static void SlowTime(float seconds)
    {
        var e = Get();
        e.slowLeft = Mathf.Max(e.slowLeft, seconds);
        if (e.slowTint == null) e.slowTint = e.Veil("Temps ralenti", 3390, out e.slowMat);
        Debug.Log($"[Objets] Café gourmand : le temps ralentit pendant {seconds:0} s.");
    }

    void Update()
    {
        if (slowLeft > 0f) slowLeft -= Time.deltaTime;
        float target = slowLeft > 0f ? 1f : 0f;
        slowShown = Mathf.MoveTowards(slowShown, target, Time.deltaTime * 1.5f);
        SlowMo.Factor = Mathf.Lerp(1f, GameBalance.CoffeeTimeScale, slowShown);
        if (slowTint != null)
        {
            slowTint.enabled = slowShown > 0.001f;
            // Léger voile bleuté qui « respire » pendant que le temps est ralenti.
            float a = 0.13f * slowShown * (0.85f + 0.15f * Mathf.Sin(Time.time * 2f));
            slowMat.SetColor("_BaseColor", new Color(0.35f, 0.55f, 1f, a));
        }
    }

    Renderer Veil(string name, int queue, out Material mat)
    {
        mat = null;
        if (cam == null) return null;
        var sh = Shader.Find("SampleJojo/ScreenFade");
        if (sh == null) { Debug.LogWarning("[Objets] shader SampleJojo/ScreenFade introuvable"); return null; }
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = name;
        Destroy(q.GetComponent<Collider>());
        q.transform.SetParent(cam, false);
        var c = cam.GetComponent<Camera>();
        float d = Mathf.Max((c != null ? c.nearClipPlane : 0.01f) * 1.7f, 0.026f);
        q.transform.localPosition = new Vector3(0f, 0f, d);
        q.transform.localRotation = Quaternion.identity;
        q.transform.localScale = Vector3.one * d * 8f;
        mat = new Material(sh) { renderQueue = queue };
        var r = q.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return r;
    }

    // ---------------- Petit message flottant ----------------
    public static void Toast(string text, Vector3 pos, Color color)
    {
        var go = new GameObject("Message objet");
        go.transform.position = pos + Vector3.up * 0.12f;
        var t = go.AddComponent<TextMeshPro>();
        t.text = text;
        t.fontSize = 0.35f;
        t.alignment = TextAlignmentOptions.Center;
        t.color = color;
        t.outlineWidth = 0.25f; t.outlineColor = new Color32(20, 10, 30, 255);
        t.rectTransform.sizeDelta = new Vector2(0.6f, 0.2f);
        Get().StartCoroutine(Get().FloatAway(go.transform, t));
    }

    IEnumerator FloatAway(Transform tr, TextMeshPro t)
    {
        Vector3 p0 = tr.position;
        for (float k = 0f; k < 1f; k += Time.deltaTime / 2.5f)
        {
            if (tr == null) yield break;
            tr.position = p0 + Vector3.up * 0.12f * k;
            if (cam != null) tr.rotation = Quaternion.LookRotation(tr.position - cam.position, Vector3.up);
            t.alpha = k < 0.75f ? 1f : 1f - (k - 0.75f) / 0.25f;
            yield return null;
        }
        if (tr != null) Destroy(tr.gameObject);
    }
}
