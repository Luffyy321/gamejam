using System.Collections;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;

/// <summary>
/// Menu principal en forme de carnet, dans un espace gris à part.
/// JOUER (ou « C'EST PARTI ! ») : fondu au noir, on apparaît sur la chaise à côté du sac et la partie commence.
/// PARAMÈTRES : volume. QUITTER : ferme le jeu.
/// Après « attrapé par la prof », la partie recommence directement sur la chaise, sans le menu.
/// </summary>
public class GameMenu : MonoBehaviour
{
    [Header("Panneaux")]
    public Transform root;
    public GameObject tutorialPanel;
    public GameObject settingsPanel;
    public TMP_Text volumeText;

    [Header("Lieux")]
    [Tooltip("Où se tient le joueur pendant le menu (espace gris).")]
    public Transform menuSpawn;
    [Tooltip("Où le joueur apparaît quand la partie commence (la chaise à côté du sac).")]
    public Transform gameSpawn;

    [Header("Jeu")]
    public ClassroomCountdown countdown;
    public ProfAI prof;
    public GameOverRestart fader;

    [Header("Tuto guidé (première partie)")]
    [Tooltip("Si présent : après JOUER, le tuto pas à pas remplace la fiche tutoriel.")]
    public TutorialGuide guide;

    [Header("Placement du menu")]
    [Tooltip("Distance devant les yeux (m).")]
    public float distance = 0.8f;
    [Tooltip("Hauteur par rapport aux yeux (m).")]
    public float heightOffset = -0.15f;

    [Header("Placement de la fiche tutoriel (sur la chaise)")]
    [Tooltip("Distance devant les yeux (m).")]
    public float tutorialDistance = 0.65f;
    [Tooltip("Hauteur par rapport aux yeux (m). Plus grand = plus haut.")]
    public float tutorialHeight = 0.05f;

    /// <summary>Mis à vrai juste avant de recharger la scène (attrapé) : on saute le menu.</summary>
    public static bool SkipNextMenu;

    bool skip, gameStarted;
    XROrigin origin;
    Transform cam;

    void Awake()
    {
        if (root == null) root = transform;
        if (countdown == null) countdown = FindFirstObjectByType<ClassroomCountdown>();
        if (prof == null) prof = FindFirstObjectByType<ProfAI>();
        if (fader == null) fader = FindFirstObjectByType<GameOverRestart>();

        // La fiche tutoriel n'est plus dans le menu : elle s'affiche devant la chaise.
        if (tutorialPanel != null) { tutorialPanel.transform.SetParent(null, true); tutorialPanel.SetActive(false); }

        if (SkipNextMenu)
        {
            SkipNextMenu = false;
            skip = gameStarted = true;
            HideMenu();
            return;                           // la partie démarre normalement (chrono + prof)
        }
        GameProgress.Reset();                 // nouvelle partie depuis le menu
        // En attente : chrono arrêté, prof immobile.
        if (countdown != null) countdown.startOnPlay = false;
        if (prof != null) prof.enabled = false;
        if (settingsPanel != null) settingsPanel.SetActive(false);
        root.localScale = Vector3.one * 0.001f;
    }

    void Start()
    {
        origin = FindFirstObjectByType<XROrigin>();
        if (origin != null && origin.Camera != null) cam = origin.Camera.transform;
        if (cam == null && Camera.main != null) cam = Camera.main.transform;
        UpdateVolume();
        if (skip) { StartCoroutine(TeleportLater(gameSpawn)); StartCoroutine(BannerLater()); return; }
        StartCoroutine(Open());
    }

    /// <summary>« COURS n » devant nous au début du cours (après le retour de l'image).</summary>
    IEnumerator BannerLater()
    {
        yield return new WaitForSecondsRealtime(0.7f);
        ClassBanner.ShowCurrent();
    }

    IEnumerator TeleportLater(Transform p)
    {
        yield return null;                    // le casque a donné la position de la tête
        TeleportTo(p);
    }

    IEnumerator Open()
    {
        yield return null;
        TeleportTo(menuSpawn);                // dans l'espace gris
        // Sur le casque, la position de la tête arrive parfois après quelques images : on l'attend (3 s max).
        float t0 = Time.realtimeSinceStartup;
        while (cam != null && cam.localPosition.sqrMagnitude < 0.0004f && Time.realtimeSinceStartup - t0 < 3f) yield return null;
        yield return new WaitForSecondsRealtime(0.4f);
        TeleportTo(menuSpawn);                // une fois la tête suivie par le casque
        PlaceInFront();
        menuOpen = true;
        yield return Scale(0.001f, 1f, 0.35f);
    }

    bool menuOpen;

    // Tant que le menu est affiché, il reste à hauteur des yeux (si la hauteur du casque change, il suit doucement).
    void Update()
    {
        if (!menuOpen || gameStarted || cam == null) return;
        float wantY = cam.position.y + heightOffset;
        float dy = wantY - root.position.y;
        if (Mathf.Abs(dy) > 0.08f) root.position += Vector3.up * dy * Mathf.Min(1f, Time.unscaledDeltaTime * 3f);
    }

    /// <summary>Cache le carnet et les fiches (ce script reste actif).</summary>
    void HideMenu()
    {
        foreach (Transform c in root) c.gameObject.SetActive(false);
    }

    /// <summary>Place le joueur (ses yeux) au-dessus du point, regard dans la direction du point.</summary>
    public void TeleportTo(Transform p)
    {
        if (p == null || origin == null) return;
        PlayerSafety.ReleaseAll();            // on ne garde rien dans les mains en changeant de salle
        var cc = origin.GetComponent<CharacterController>();
        bool ccOn = cc != null && cc.enabled;
        if (cc != null) cc.enabled = false;
        Vector3 fwd = Vector3.ProjectOnPlane(p.forward, Vector3.up);
        if (fwd.sqrMagnitude > 1e-4f) origin.MatchOriginUpCameraForward(Vector3.up, fwd.normalized);
        Vector3 camPos = cam != null ? cam.position : origin.transform.position;
        Vector3 pos = origin.transform.position;
        pos.x += p.position.x - camPos.x;
        pos.z += p.position.z - camPos.z;
        pos.y = p.position.y;
        origin.transform.position = pos;
        Physics.SyncTransforms();
        if (cc != null) cc.enabled = ccOn;
    }

    /// <summary>Le menu devant le joueur, face à lui.</summary>
    public void PlaceInFront()
    {
        if (cam == null) return;
        Vector3 fwd = cam.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
        fwd.Normalize();
        // Un peu à gauche pour que le carnet et la fiche soient centrés.
        Vector3 left = Vector3.Cross(fwd, Vector3.up);
        root.position = cam.position + fwd * distance + Vector3.up * heightOffset + left * 0.1f;
        root.rotation = Quaternion.LookRotation(fwd, Vector3.up) * Quaternion.Euler(8f, 0f, 0f);
    }

    IEnumerator Scale(float from, float to, float time)
    {
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / time)
        {
            float k = Mathf.SmoothStep(0f, 1f, t);
            root.localScale = Vector3.one * Mathf.LerpUnclamped(from, to, k);
            yield return null;
        }
        root.localScale = Vector3.one * to;
    }

    // ---------------- Boutons ----------------
    bool inTutorial;

    /// <summary>JOUER : fondu, on apparaît sur la chaise, la fiche tutoriel est devant nous.</summary>
    public void Jouer()
    {
        if (gameStarted) return;
        gameStarted = true;
        StartCoroutine(GoToChair());
    }

    IEnumerator GoToChair()
    {
        if (fader != null) yield return fader.FadeTo(0f, 1f, 0.5f);
        else yield return Scale(root.localScale.x, 0.001f, 0.3f);
        HideMenu();
        TeleportTo(gameSpawn);                // sur la chaise, à côté du sac
        yield return new WaitForSecondsRealtime(0.15f);
        if (guide != null && guide.isActiveAndEnabled)
        {
            guide.Begin(this);                // tuto pas à pas : le chrono démarre à la fin
            if (fader != null) yield return fader.FadeTo(1f, 0f, 0.6f);
            yield break;
        }
        if (tutorialPanel != null)
        {
            PlaceCardInFront(tutorialPanel.transform);
            tutorialPanel.transform.localScale = Vector3.one;
            tutorialPanel.SetActive(true);
            inTutorial = true;
        }
        if (fader != null) yield return fader.FadeTo(1f, 0f, 0.6f);
        if (tutorialPanel == null) StartChrono();
    }

    /// <summary>La fiche devant les yeux, face au joueur.</summary>
    void PlaceCardInFront(Transform card)
    {
        if (cam == null) return;
        Vector3 fwd = cam.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
        fwd.Normalize();
        card.position = cam.position + fwd * tutorialDistance + Vector3.up * tutorialHeight;
        card.rotation = Quaternion.LookRotation(fwd, Vector3.up) * Quaternion.Euler(tutorialHeight < 0f ? 10f : 0f, 0f, 0f);
    }

    /// <summary>« C'EST PARTI ! » : la fiche s'en va et le chrono démarre.</summary>
    public void CestParti()
    {
        if (!inTutorial) return;
        inTutorial = false;
        StartCoroutine(CloseTutorial());
    }

    IEnumerator CloseTutorial()
    {
        var t = tutorialPanel.transform;
        Vector3 s0 = t.localScale, p0 = t.position;
        for (float k = 0f; k < 1f; k += Time.unscaledDeltaTime / 0.35f)
        {
            float e = Mathf.SmoothStep(0f, 1f, k);
            t.localScale = s0 * (1f - e);
            t.position = p0 + Vector3.up * 0.15f * e;
            yield return null;
        }
        tutorialPanel.SetActive(false);
        StartChrono();
    }

    /// <summary>Fin du tuto : le premier cours commence (chrono + prof).</summary>
    public void StartChrono()
    {
        if (countdown != null) countdown.Restart();
        if (prof != null) prof.enabled = true;
        Debug.Log("[Menu] C'est parti : chrono lancé !");
        ClassBanner.ShowCurrent();
    }

    public void Parametres()
    {
        if (settingsPanel != null) settingsPanel.SetActive(!settingsPanel.activeSelf);
    }

    /// <summary>Croix / RETOUR : ferme les paramètres ; sur la fiche tutoriel, ça lance la partie.</summary>
    public void FermerPanneau()
    {
        if (inTutorial) { CestParti(); return; }
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    public void Quitter()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---------------- Paramètres ----------------
    public void VolumePlus() { AudioListener.volume = Mathf.Clamp01(Mathf.Round(AudioListener.volume * 10f + 1f) / 10f); UpdateVolume(); }
    public void VolumeMoins() { AudioListener.volume = Mathf.Clamp01(Mathf.Round(AudioListener.volume * 10f - 1f) / 10f); UpdateVolume(); }

    void UpdateVolume()
    {
        if (volumeText != null) volumeText.text = Mathf.RoundToInt(AudioListener.volume * 100f) + " %";
    }

    // ---------------- Tests sans casque ----------------
    [ContextMenu("Test : JOUER")] void TestJouer() => Jouer();
    [ContextMenu("Test : C'EST PARTI")] void TestGo() => CestParti();
    [ContextMenu("Test : PARAMÈTRES")] void TestParams() => Parametres();
}
