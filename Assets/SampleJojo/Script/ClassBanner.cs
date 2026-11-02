using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Au début de chaque cours : « COURS 1 » s'affiche devant le joueur puis disparaît petit à petit.
/// Après un « attrapé », une ligne en rouge dit combien de fois on s'est fait prendre.
/// </summary>
public class ClassBanner : MonoBehaviour
{
    public float distance = 1.6f;
    /// <summary>Son « recommence le cours » (réglé par GameOverRestart).</summary>
    public static AudioClip RestartClip;
    public float fadeIn = 0.4f, hold = 1.6f, fadeOut = 1.8f;

    TextMeshPro title, sub;
    Transform cam;

    /// <summary>Affiche le titre du cours en cours (niveau, cours, attrapé).</summary>
    public static void ShowCurrent()
    {
        string s = $"Niveau {GameProgress.Chapter}";
        if (GameProgress.JustCaught)
        {
            if (RestartClip != null && Camera.main != null) AudioSource.PlayClipAtPoint(RestartClip, Camera.main.transform.position, 1f);
            s = GameProgress.LastCaughtRestartedLevel
                ? $"<color=#FF4A4A>Attrapé {GameProgress.MaxStrikes} fois : le niveau recommence !</color>"
                : $"<color=#FF4A4A>Attrapé {GameProgress.Strikes} / {GameProgress.MaxStrikes}</color>  ·  Niveau {GameProgress.Chapter}";
            GameProgress.JustCaught = false;
        }
        Show($"COURS {GameProgress.Level}", s);
    }

    public static void Show(string text, string subText)
    {
        var go = new GameObject("Titre du cours");
        var b = go.AddComponent<ClassBanner>();
        b.Build(text, subText);
    }

    void Build(string text, string subText)
    {
        TMP_FontAsset font = null;
        var guide = FindFirstObjectByType<TutorialGuide>(FindObjectsInactive.Include);
        if (guide != null) font = guide.font;
        var origin = FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
        cam = origin != null && origin.Camera != null ? origin.Camera.transform : Camera.main != null ? Camera.main.transform : null;

        title = Label("Titre", text, font, 1.6f, new Vector3(0f, 0.06f, 0f), new Color(1f, 0.85f, 0.25f));
        title.fontStyle = FontStyles.Bold;
        title.outlineWidth = 0.18f;
        title.outlineColor = new Color32(60, 20, 70, 255);
        sub = Label("Sous-titre", subText, font, 0.55f, new Vector3(0f, -0.13f, 0f), Color.white);
        sub.outlineWidth = 0.15f;
        sub.outlineColor = new Color32(30, 30, 40, 255);
        Place(1f);
        StartCoroutine(Run());
    }

    TextMeshPro Label(string name, string text, TMP_FontAsset font, float size, Vector3 pos, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = pos;
        var t = go.AddComponent<TextMeshPro>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.rectTransform.sizeDelta = new Vector2(2f, 0.4f);
        // Toujours visible, même devant une table ou la prof.
        var m = t.fontMaterial;
        if (m.HasProperty("unity_GUIZTestMode")) m.SetFloat("unity_GUIZTestMode", (float)UnityEngine.Rendering.CompareFunction.Always);
        m.renderQueue = 4000;
        return t;
    }

    /// <summary>Devant les yeux, à hauteur des yeux ; suit doucement le regard.</summary>
    void Place(float k)
    {
        if (cam == null) return;
        Vector3 fwd = cam.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
        fwd.Normalize();
        Vector3 pos = cam.position + fwd * distance + Vector3.up * 0.05f;
        Quaternion rot = Quaternion.LookRotation(fwd, Vector3.up);
        transform.SetPositionAndRotation(Vector3.Lerp(transform.position, pos, k), Quaternion.Slerp(transform.rotation, rot, k));
    }

    IEnumerator Run()
    {
        float total = fadeIn + hold + fadeOut;
        for (float t = 0f; t < total; t += Time.deltaTime)
        {
            float a = t < fadeIn ? t / fadeIn : t < fadeIn + hold ? 1f : 1f - (t - fadeIn - hold) / fadeOut;
            a = Mathf.Clamp01(a);
            title.alpha = a;
            sub.alpha = a;
            // Arrive en grossissant un peu, puis monte doucement en disparaissant.
            float s = t < fadeIn ? Mathf.Lerp(0.8f, 1f, Mathf.SmoothStep(0f, 1f, t / fadeIn)) : 1f + 0.04f * (t - fadeIn);
            transform.localScale = Vector3.one * s;
            Place(1f - Mathf.Exp(-3f * Time.deltaTime));
            if (t > fadeIn + hold) transform.position += Vector3.up * 0.04f * Time.deltaTime;
            yield return null;
        }
        Destroy(gameObject);
    }
}
