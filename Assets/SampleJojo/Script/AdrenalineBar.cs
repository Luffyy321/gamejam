using UnityEngine;

/// <summary>
/// Barre d'adrénaline : fine barre rose verticale, collée au bord gauche de la vue,
/// sur toute la hauteur de l'écran. Valeur entre 0 et 1 (Value, Add, SetValue).
/// </summary>
public class AdrenalineBar : MonoBehaviour
{
    public static AdrenalineBar Active;

    [Range(0f, 1f)] public float value = 1f;
    [Tooltip("Shader dessiné par-dessus tout (SampleJojo/OverlayUnlit).")]
    public Shader overlayShader;

    [Header("Apparence")]
    public Color fillColor = new Color(1f, 0.25f, 0.65f, 1f);
    public Color trackColor = new Color(0.16f, 0.05f, 0.11f, 1f);
    [Tooltip("Distance devant les yeux (m).")]
    public float distance = 0.5f;
    [Tooltip("Largeur de la barre en fraction de la largeur de l'écran.")]
    public float width = 0.012f;
    [Tooltip("Hauteur en fraction de la hauteur de l'écran (1 = tout l'écran).")]
    public float height = 0.96f;
    [Tooltip("Position horizontale : 0 = centre, 1 = bord gauche exact. Un peu moins de 1 pour qu'on la voie bien dans le casque.")]
    public float edge = 0.82f;

    Transform cam, holder, fill;
    Material fillMat;
    float shown;

    public float Value => value;
    public void SetValue(float v) => value = Mathf.Clamp01(v);
    public void Add(float delta) => SetValue(value + delta);

    void Awake() { Active = this; shown = value; }

    void Start() => Build();

    void Build()
    {
        var c = Camera.main;
        if (c == null) c = FindFirstObjectByType<Camera>();
        if (c == null) { Debug.LogWarning("[Adrénaline] pas de caméra"); return; }
        cam = c.transform;
        if (overlayShader == null) overlayShader = Shader.Find("SampleJojo/OverlayUnlit");

        holder = new GameObject("Barre d'adrénaline (HUD)").transform;
        holder.SetParent(cam, false);

        float h = 2f * distance * Mathf.Tan(c.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float aspect = c.aspect > 0.1f ? c.aspect : 1f;
        float w = h * aspect;
        float barH = h * height;
        float barW = Mathf.Max(0.004f, w * width);
        holder.localPosition = new Vector3(-w * 0.5f * edge, 0f, distance);
        holder.localRotation = Quaternion.identity;

        // Fond sombre + bord, puis remplissage rose qui monte depuis le bas.
        Quad("Contour", Vector3.zero, new Vector2(barW + 0.003f, barH + 0.003f), new Color(1f, 0.85f, 0.93f, 1f), 3500);
        Quad("Fond", Vector3.zero, new Vector2(barW, barH), trackColor, 3501);
        var pivot = new GameObject("Pivot").transform;
        pivot.SetParent(holder, false);
        pivot.localPosition = new Vector3(0f, -barH * 0.5f, 0f);
        var q = Quad("Remplissage", new Vector3(0f, 0.5f, 0f), Vector2.one, fillColor, 3502);
        q.SetParent(pivot, false);
        q.localPosition = new Vector3(0f, 0.5f, 0f);
        fill = pivot;
        fill.localScale = new Vector3(barW, barH * shown, 1f);
        fillMat = q.GetComponent<Renderer>().sharedMaterial;
    }

    Transform Quad(string name, Vector3 pos, Vector2 size, Color color, int queue)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(holder, false);
        go.transform.localPosition = pos;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var r = go.GetComponent<Renderer>();
        var m = new Material(overlayShader != null ? overlayShader : Shader.Find("Universal Render Pipeline/Unlit"));
        m.SetColor("_BaseColor", color);
        m.renderQueue = queue;
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return go.transform;
    }

    void Update()
    {
        if (fill == null) return;
        shown = Mathf.MoveTowards(shown, value, Time.deltaTime * 1.5f);
        var s = fill.localScale;
        float barH = holder.Find("Fond").localScale.y;
        fill.localScale = new Vector3(s.x, Mathf.Max(0.0001f, barH * shown), 1f);
        // Petite pulsation quand la barre est presque pleine.
        if (fillMat != null)
        {
            float p = shown > 0.85f ? 0.15f * (0.5f + 0.5f * Mathf.Sin(Time.time * 6f)) : 0f;
            fillMat.SetColor("_BaseColor", Color.Lerp(fillColor, Color.white, p));
        }
    }

    void OnDestroy() { if (Active == this) Active = null; }

    [ContextMenu("Test : vider")] void TestEmpty() => SetValue(0f);
    [ContextMenu("Test : moitié")] void TestHalf() => SetValue(0.5f);
    [ContextMenu("Test : remplir")] void TestFull() => SetValue(1f);
}
