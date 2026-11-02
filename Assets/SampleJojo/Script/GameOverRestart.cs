using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Quand la prof arrive devant le joueur : fondu au noir, puis la partie recommence (la scène est rechargée).
/// Au début de chaque partie, l'image sort du noir.
/// </summary>
public class GameOverRestart : MonoBehaviour
{
    public ProfAI prof;
    [Tooltip("Shader du voile noir (SampleJojo/ScreenFade).")]
    public Shader fadeShader;
    public Color fadeColor = Color.black;
    [Tooltip("Temps où la prof nous montre du doigt avant le fondu (s).")]
    public float pointTime = 1.6f;
    [Tooltip("Durée du fondu au noir (s).")]
    public float fadeOutTime = 0.8f;
    [Tooltip("Temps passé dans le noir avant de recommencer (s).")]
    public float holdBlack = 0.6f;
    [Tooltip("Durée du retour à l'image au début de la partie (s).")]
    public float fadeInTime = 0.8f;
    public AudioClip caughtClip;
    [Tooltip("Joué au début du cours qui recommence.")]
    public AudioClip restartClip;

    Material mat;
    Renderer veil;
    bool restarting;
    static readonly int ColorId = Shader.PropertyToID("_BaseColor");

    void Awake()
    {
        if (prof == null) prof = FindFirstObjectByType<ProfAI>();
    }

    void OnEnable()
    {
        if (prof != null) prof.onCaught.AddListener(OnCaught);
        if (restartClip != null) ClassBanner.RestartClip = restartClip;
    }
    void OnDisable() { if (prof != null) prof.onCaught.RemoveListener(OnCaught); }

    void Start()
    {
        BuildVeil();
        StartCoroutine(Fade(1f, 0f, fadeInTime));      // la partie commence en sortant du noir
    }

    void BuildVeil()
    {
        Camera cam = null;
        var origin = FindFirstObjectByType<XROrigin>();
        if (origin != null) cam = origin.Camera;
        if (cam == null) cam = Camera.main;
        if (cam == null) return;
        if (fadeShader == null) fadeShader = Shader.Find("SampleJojo/ScreenFade");
        if (fadeShader == null) { Debug.LogWarning("[Partie] Shader SampleJojo/ScreenFade introuvable : pas de fondu."); return; }

        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = "Fondu noir";
        Destroy(q.GetComponent<Collider>());
        q.transform.SetParent(cam.transform, false);
        float d = Mathf.Max(cam.nearClipPlane * 1.5f, 0.02f);
        q.transform.localPosition = new Vector3(0f, 0f, d);
        q.transform.localRotation = Quaternion.identity;
        q.transform.localScale = Vector3.one * d * 8f;          // couvre tout le champ de vision
        mat = new Material(fadeShader);
        veil = q.GetComponent<Renderer>();
        veil.sharedMaterial = mat;
        veil.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        veil.receiveShadows = false;
        SetAlpha(1f);
    }

    void SetAlpha(float a)
    {
        if (mat == null) return;
        var c = fadeColor; c.a = a;
        mat.SetColor(ColorId, c);
        veil.enabled = a > 0.001f;
    }

    IEnumerator Fade(float from, float to, float time)
    {
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / Mathf.Max(0.01f, time))
        {
            SetAlpha(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t)));
            yield return null;
        }
        SetAlpha(to);
    }

    /// <summary>Fondu (0 = image visible, 1 = noir), utilisable par d'autres scripts (menu).</summary>
    public IEnumerator FadeTo(float from, float to, float time) => Fade(from, to, time);

    void OnCaught()
    {
        if (restarting) return;
        restarting = true;
        StopAllCoroutines();
        StartCoroutine(RestartRoutine());
    }

    IEnumerator RestartRoutine()
    {
        Debug.Log($"[Partie] La prof t'a attrapé ({GameProgress.Strikes + 1}/{GameProgress.MaxStrikes}).");
        if (caughtClip != null && prof != null) AudioSource.PlayClipAtPoint(caughtClip, prof.transform.position);
        yield return new WaitForSecondsRealtime(pointTime);     // elle nous montre du doigt
        yield return Fade(0f, 1f, fadeOutTime);
        yield return new WaitForSecondsRealtime(holdBlack);
        GameMenu.SkipNextMenu = true;          // on recommence directement, sans le menu
        GameProgress.Caught();                 // 1re et 2e fois : on recommence le cours ; 3e fois : tout le niveau
        PlayerSafety.ReleaseAll();             // on lâche ce qu'on tient avant de recharger
        var scene = SceneManager.GetActiveScene();
        if (scene.buildIndex >= 0) SceneManager.LoadScene(scene.buildIndex);
        else SceneManager.LoadScene(scene.name);
    }

    [ContextMenu("Test : se faire attraper (en Play)")]
    public void TestCaught() { if (Application.isPlaying) OnCaught(); }
}
