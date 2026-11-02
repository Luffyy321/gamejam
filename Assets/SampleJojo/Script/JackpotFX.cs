using UnityEngine;

/// <summary>
/// Lampes qui clignotent et sons de la machine à sous.
/// - Au repos : chenillard lent autour de la vitre.
/// - Pendant que les rouleaux tournent : chenillard rapide + cliquetis.
/// - Jackpot : toutes les lampes flashent en couleurs + jingle.
/// </summary>
public class JackpotFX : MonoBehaviour
{
    public JackpotLever lever;

    [Header("Lampes")]
    [Tooltip("Lampes autour de la vitre, dans l'ordre du tour.")]
    public Renderer[] bulbs;
    [Tooltip("Corps de la machine (contient aussi des lampes).")]
    public Renderer bodyRenderer;
    public int bodyLightsSlot = -1;
    public Color onColor = new Color(1f, 0.82f, 0.25f);
    public Color offColor = new Color(0.22f, 0.2f, 0.17f);
    public Color[] winColors =
    {
        new Color(1f, 0.82f, 0.25f), Color.white, new Color(1f, 0.25f, 0.3f), new Color(0.3f, 0.8f, 1f)
    };
    public float emission = 3f;
    public float idleStep = 0.3f;
    public float spinStep = 0.06f;
    public float winStep = 0.12f;
    public float winDuration = 4f;

    [Header("Sons")]
    public AudioSource sfx;
    public AudioSource loopSource;
    public AudioClip leverClip;
    public AudioClip spinLoopClip;
    public AudioClip reelStopClip;
    public AudioClip winClip;

    enum Mode { Idle, Spin, Win }
    Mode mode = Mode.Idle;
    float timer, winEnd;
    int step;
    Material[] bulbMats = new Material[0];
    Material bodyMat;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    void Awake()
    {
        if (bulbs == null) bulbs = new Renderer[0];
        bulbMats = new Material[bulbs.Length];
        for (int i = 0; i < bulbs.Length; i++)
        {
            if (bulbs[i] == null) continue;
            bulbMats[i] = bulbs[i].material;        // copie propre à chaque lampe
            Prep(bulbMats[i]);
        }
        if (bodyRenderer != null && bodyLightsSlot >= 0)
        {
            var mats = bodyRenderer.materials;
            if (bodyLightsSlot < mats.Length)
            {
                bodyMat = mats[bodyLightsSlot];
                Prep(bodyMat);
            }
        }
    }

    static void Prep(Material m)
    {
        m.EnableKeyword("_EMISSION");
        // Pas de mise à jour de l'éclairage global à chaque clignotement (coûteux).
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
    }

    void OnEnable()
    {
        mode = Mode.Idle;
        if (lever == null) return;
        lever.onPulled.AddListener(OnPulled);
        lever.onReelStopped.AddListener(OnReelStopped);
        lever.onWin.AddListener(OnWin);
        lever.onFinished.AddListener(OnFinished);
    }

    void OnDisable()
    {
        if (lever == null) return;
        lever.onPulled.RemoveListener(OnPulled);
        lever.onReelStopped.RemoveListener(OnReelStopped);
        lever.onWin.RemoveListener(OnWin);
        lever.onFinished.RemoveListener(OnFinished);
    }

    void OnPulled()
    {
        mode = Mode.Spin;
        Play(leverClip);
        if (loopSource != null && spinLoopClip != null)
        {
            loopSource.clip = spinLoopClip;
            loopSource.loop = true;
            loopSource.Play();
        }
    }

    void OnReelStopped(int index) => Play(reelStopClip);

    void OnWin()
    {
        mode = Mode.Win;
        winEnd = Time.time + winDuration;
        Play(winClip);
    }

    void OnFinished()
    {
        if (loopSource != null) loopSource.Stop();
        if (mode != Mode.Win) mode = Mode.Idle;
    }

    void Play(AudioClip clip)
    {
        if (sfx != null && clip != null) sfx.PlayOneShot(clip);
    }

    bool wasOff;

    void Update()
    {
        if (mode == Mode.Win && Time.time > winEnd) mode = Mode.Idle;

        // Pas de pièce insérée : machine éteinte (toutes les lampes éteintes).
        if (lever != null && !lever.Powered && mode == Mode.Idle)
        {
            if (!wasOff)
            {
                wasOff = true;
                foreach (var m in bulbMats) if (m != null) Set(m, false, onColor);
                if (bodyMat != null) Set(bodyMat, false, onColor);
            }
            return;
        }
        if (wasOff) { wasOff = false; mode = Mode.Win; winEnd = Time.time + 0.8f; }   // petit flash à l'allumage

        float stepTime = mode == Mode.Spin ? spinStep : mode == Mode.Win ? winStep : idleStep;
        timer += Time.deltaTime;
        if (timer < stepTime) return;
        timer = 0f;
        step++;

        for (int i = 0; i < bulbMats.Length; i++)
        {
            if (bulbMats[i] == null) continue;
            if (mode == Mode.Win)
                Set(bulbMats[i], step % 2 == 0, winColors[(step / 2 + i) % winColors.Length]);
            else
                Set(bulbMats[i], (i + step) % 3 == 0, onColor);    // une lampe sur trois, qui tourne
        }

        if (bodyMat != null)
        {
            bool on = mode == Mode.Win ? step % 2 == 1 : mode == Mode.Spin ? step % 2 == 0 : (step / 2) % 2 == 0;
            Set(bodyMat, on, mode == Mode.Win ? winColors[step % winColors.Length] : onColor);
        }
    }

    void Set(Material m, bool on, Color c)
    {
        m.SetColor(BaseColorId, on ? c : offColor);
        m.SetColor(EmissionId, on ? c * emission : Color.black);
    }
}
