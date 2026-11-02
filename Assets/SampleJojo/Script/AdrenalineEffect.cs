using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Adrénaline (0 → 1) : plus elle est haute, plus le bord de la vue devient rose, avec des traînées
/// qui filent vers l'extérieur (impression de vision élargie) et un battement de cœur qui accélère.
/// Elle monte à chaque tirage (beaucoup si on gagne, un peu si on perd) ou quand on consomme un objet,
/// et redescend petit à petit avec le temps.
/// La pilule resserre la vision (tunnel noir) pendant 20 s.
/// </summary>
public class AdrenalineEffect : MonoBehaviour
{
    public static AdrenalineEffect Active;

    [Range(0f, 1f)] public float value = 0f;
    [Tooltip("Baisse toute seule par seconde (0 = elle ne bouge qu'avec la machine et les objets).")]
    public float decayPerSecond = 0f;
    [Tooltip("Temps avant que ça redescende après une prise (s).")]
    public float holdTime = 4f;

    [Header("Rendu")]
    public Shader vignetteShader;
    public Color color = new Color(1f, 0.2f, 0.62f, 1f);
    [Tooltip("Sur PC (sans casque) le champ de vision s'élargit vraiment de ce nombre de degrés au max.")]
    public float extraFovPc = 25f;

    [Header("Dopamine et machine")]
    [Tooltip("Dopamine gagnée quand un tirage rapporte des pièces.")]
    public float winGain = 0.12f;
    [Tooltip("Dopamine GAGNÉE quand un tirage ne rapporte rien (petite).")]
    public float loseGain = 0.01f;
    [Tooltip("Bonus ajouté aux gains ET aux pertes de dopamine (la seringue fait +0.1).")]
    public float boost = 0f;
    [Tooltip("Multiplicateur des gains de pièces quand la dopamine est au max (x1 à vide).")]
    public float maxMultiplier = 2f;

    /// <summary>Les gains de la machine sont multipliés par ça (x1 → x2 selon la dopamine).</summary>
    public float WinMultiplier => Mathf.Lerp(1f, maxMultiplier, value);

    /// <summary>Résultat d'un tirage : gagné → +0,1 de dopamine, perdu → +0,01 (la seringue ajoute son bonus aux deux).
    /// Avec le temps, la dopamine redescend.</summary>
    public void OnSpinResult(bool won) => Add(won ? winGain + boost : loseGain + boost, false);

    [Header("Pilule : vision resserrée")]
    [Tooltip("Force du tunnel noir (0 à 1).")]
    public float tunnelStrength = 0.85f;
    Material tunnelMat;
    Renderer tunnelVeil;
    float tunnelTime, tunnelShown;

    /// <summary>La vision se resserre (bords noirs) pendant <paramref name="seconds"/> secondes.</summary>
    public void Tunnel(float seconds) { tunnelTime = Mathf.Max(tunnelTime, seconds); }

    /// <summary>Garde la dopamine et le bonus de la seringue pour le cours suivant.</summary>
    public void SaveToProgress() { GameProgress.Dopamine = value; GameProgress.DopamineBoost = boost; }

    [Header("Sons")]
    public AudioClip heartbeatClip;
    public AudioClip consumeClip;

    Camera cam;
    Material mat;
    Renderer veil;
    AudioSource heart, sfx;
    float shown, hold, beatTimer, pulse, baseFov, flash;

    public float Value => value;

    void Awake()
    {
        Active = this;
        // Équilibrage (GameBalance) : remplace les valeurs de l'Inspector.
        winGain = GameBalance.DopamineOnWin;
        loseGain = GameBalance.DopamineOnLose;
        decayPerSecond = GameBalance.DopamineDecayPerSecond;
        holdTime = GameBalance.DopamineHold;
    }
    void OnDestroy() { if (Active == this) Active = null; }

    void Start()
    {
        var origin = FindFirstObjectByType<XROrigin>();
        cam = origin != null ? origin.Camera : Camera.main;
        if (cam == null) { Debug.LogWarning("[Adrénaline] pas de caméra"); return; }
        baseFov = cam.fieldOfView;
        if (vignetteShader == null) vignetteShader = Shader.Find("SampleJojo/AdrenalineVignette");
        if (vignetteShader != null)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "Vignette adrénaline";
            Destroy(q.GetComponent<Collider>());
            q.transform.SetParent(cam.transform, false);
            float d = Mathf.Max(cam.nearClipPlane * 1.6f, 0.025f);
            q.transform.localPosition = new Vector3(0f, 0f, d);
            q.transform.localRotation = Quaternion.identity;
            q.transform.localScale = Vector3.one * d * 8f;      // couvre tout le champ de vision
            mat = new Material(vignetteShader);
            veil = q.GetComponent<Renderer>();
            veil.sharedMaterial = mat;
            veil.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            veil.receiveShadows = false;
        }
        else Debug.LogWarning("[Adrénaline] shader SampleJojo/AdrenalineVignette introuvable");
        if (vignetteShader != null)
        {
            // Deuxième voile, noir, pour la vision resserrée de la pilule.
            var q2 = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q2.name = "Vision resserrée";
            Destroy(q2.GetComponent<Collider>());
            q2.transform.SetParent(cam.transform, false);
            float d2 = Mathf.Max(cam.nearClipPlane * 1.5f, 0.024f);
            q2.transform.localPosition = new Vector3(0f, 0f, d2);
            q2.transform.localScale = Vector3.one * d2 * 8f;
            tunnelMat = new Material(vignetteShader);
            tunnelMat.renderQueue = mat != null ? mat.renderQueue + 1 : 4051;
            tunnelVeil = q2.GetComponent<Renderer>();
            tunnelVeil.sharedMaterial = tunnelMat;
            tunnelVeil.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tunnelVeil.enabled = false;
        }
        // Dopamine gardée du cours précédent.
        if (GameProgress.CarryCoins) { value = GameProgress.Dopamine; boost = GameProgress.DopamineBoost; }

        heart = cam.gameObject.AddComponent<AudioSource>();
        heart.playOnAwake = false; heart.spatialBlend = 0f;
        sfx = cam.gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false; sfx.spatialBlend = 0f;
        shown = value;
        Apply();
    }

    /// <summary>Ajoute de l'adrénaline (une prise d'objet).</summary>
    public void Add(float amount) => Add(amount, true);

    public void Add(float amount, bool withSound)
    {
        float before = value;
        value = Mathf.Clamp01(value + amount);
        hold = holdTime;
        if (withSound && sfx != null && consumeClip != null) sfx.PlayOneShot(consumeClip);
        if (value >= 0.999f && before < 0.999f) flash = 1f;      // on vient d'atteindre le max
        Debug.Log($"[Dopamine] {(amount >= 0 ? "+" : "")}{amount:P0} → {value:P0} (gains x{WinMultiplier:F2})");
    }

    public void SetValue(float v) { value = Mathf.Clamp01(v); }

    void Update()
    {
        if (hold > 0f) hold -= Time.deltaTime;
        else value = Mathf.Max(0f, value - decayPerSecond * Time.deltaTime);

        // Monte assez vite pour qu'on sente la prise, redescend en suivant la valeur.
        shown = Mathf.MoveTowards(shown, value, Time.deltaTime * (value > shown ? 0.6f : 0.3f));

        // Battement de cœur : 60 → 150 bpm.
        if (shown > 0.02f)
        {
            float bpm = Mathf.Lerp(60f, 150f, shown);
            beatTimer += Time.deltaTime * bpm / 60f;
            if (beatTimer >= 1f)
            {
                beatTimer -= 1f;
                if (heart != null && heartbeatClip != null) heart.PlayOneShot(heartbeatClip, Mathf.Lerp(0.15f, 0.9f, shown));
            }
            // Double battement « boum-boum ».
            float t = beatTimer;
            pulse = Mathf.Exp(-t * 14f) + 0.6f * Mathf.Exp(-Mathf.Max(0f, t - 0.18f) * 14f) * (t > 0.18f ? 1f : 0f);
            pulse = Mathf.Clamp01(pulse);
        }
        else { pulse = 0f; beatTimer = 0f; }

        flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 1.2f);
        // Tunnel de la pilule : se ferme en 1 s, se rouvre pendant les 2 dernières secondes.
        if (tunnelTime > 0f) tunnelTime -= Time.deltaTime;
        float tunnelTarget = tunnelTime > 0f ? Mathf.Clamp01(tunnelTime / 2f) : 0f;
        tunnelShown = Mathf.MoveTowards(tunnelShown, tunnelTarget, Time.deltaTime * (tunnelTarget > tunnelShown ? 1f : 0.6f));
        Apply();
    }

    void Apply()
    {
        if (mat != null)
        {
            float k = Mathf.Clamp01(shown + flash * 0.3f);
            mat.SetColor("_Color", color);
            mat.SetFloat("_Intensity", k);
            mat.SetFloat("_Pulse", pulse);
            mat.SetFloat("_Streaks", Mathf.Clamp01((shown - 0.15f) / 0.85f));
            veil.enabled = k > 0.003f;
        }
        if (tunnelMat != null)
        {
            float k2 = tunnelShown * tunnelStrength;
            tunnelMat.SetColor("_Color", Color.black);
            tunnelMat.SetFloat("_Intensity", k2);
            tunnelMat.SetFloat("_Pulse", 0f);
            tunnelMat.SetFloat("_Streaks", 0f);
            tunnelVeil.enabled = k2 > 0.003f;
        }
        // Dans le casque, le champ de vision est fixé par le casque : on ne le change que sur PC.
        if (cam != null && !XRSettings.isDeviceActive)
            cam.fieldOfView = baseFov + extraFovPc * shown;
    }

    [ContextMenu("Test : +25 %")] void TestAdd() => Add(0.25f);
    [ContextMenu("Test : vider")] void TestEmpty() { value = 0f; }
    [ContextMenu("Test : vision resserrée 20 s")] void TestTunnel() => Tunnel(20f);
}
