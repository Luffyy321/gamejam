using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Espace « choix du niveau » (dans l'espace gris du menu), après l'examen de fin de niveau.
/// On y voit le résultat de l'examen (pourcentage), l'objectif de pièces (atteint ou non), si le niveau est validé,
/// et on choisit le niveau à jouer. Un niveau se débloque en validant le niveau d'avant (examen réussi ET objectif atteint). Les pièces et crédits sont gardés.
/// Construit tout seul au moment où il s'ouvre (créé par LevelFlow).
/// </summary>
public class LevelSelect : MonoBehaviour
{
    public TMP_FontAsset font;
    public float distance = 0.9f;

    GameOverRestart fader;
    Transform cam, board;
    int coins, credits;
    bool leaving;

    static readonly Color Blue = new Color(0.2f, 0.42f, 0.86f);
    static readonly Color Paper = new Color(0.99f, 0.97f, 0.9f);
    static readonly Color Ink = new Color(0.12f, 0.12f, 0.18f);
    static readonly Color Green = new Color(0.2f, 0.68f, 0.32f);
    static readonly Color Red = new Color(0.85f, 0.15f, 0.15f);
    static readonly Color Locked = new Color(0.62f, 0.62f, 0.66f);

    /// <summary>Ouvre l'écran (le joueur est déjà dans l'espace gris).</summary>
    public void Open(int coins, int credits, GameOverRestart fader)
    {
        this.coins = coins;
        this.credits = credits;
        this.fader = fader;
        if (font == null)
        {
            var guide = FindFirstObjectByType<TutorialGuide>(FindObjectsInactive.Include);
            if (guide != null) font = guide.font;
        }
        if (font == null) { var any = FindFirstObjectByType<TMP_Text>(FindObjectsInactive.Include); if (any != null) font = any.font; }
        var origin = FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
        if (origin != null && origin.Camera != null) cam = origin.Camera.transform;
        if (cam == null && Camera.main != null) cam = Camera.main.transform;
        Build();
        Place();
        StartCoroutine(Pop());
    }

    void Build()
    {
        board = new GameObject("Tableau choix du niveau").transform;
        board.SetParent(transform, false);
        Box(board, "Bord", new Vector3(0f, 0f, 0.005f), new Vector3(0.74f, 0.56f, 0.01f), Blue, 0.5f);
        Box(board, "Papier", Vector3.zero, new Vector3(0.71f, 0.53f, 0.008f), Paper, 0.5f);

        int ch = GameProgress.Chapter;
        Label(board, "Titre", new Vector3(0f, 0.22f, -0.006f), new Vector2(0.66f, 0.06f), 0.42f, Blue, FontStyles.Bold).text = $"FIN DU NIVEAU {ch}";

        // Résultat : examen + objectif de pièces → niveau validé ou non.
        bool passed = GameProgress.LastExamPassed, goal = GameProgress.LastGoalReached, ok = GameProgress.LastLevelValidated;
        var ex = Label(board, "Examen", new Vector3(0f, 0.16f, -0.006f), new Vector2(0.66f, 0.05f), 0.28f, passed ? Green : Red, FontStyles.Bold);
        ex.text = $"Examen : {GameProgress.LastExamPercent} %  ({GameProgress.LastExamScore}/{GameProgress.LastExamQuestions})  —  {(passed ? "RÉUSSI" : "RATÉ")}";
        var go = Label(board, "Objectif", new Vector3(0f, 0.11f, -0.006f), new Vector2(0.66f, 0.05f), 0.28f, goal ? Green : Red, FontStyles.Bold);
        go.text = $"Objectif : {GameProgress.LastGoalCoins} / {GameProgress.LastGoal} pièces  —  {(goal ? "ATTEINT" : "RATÉ")}";
        var verdict = Label(board, "Verdict", new Vector3(0f, 0.058f, -0.006f), new Vector2(0.66f, 0.05f), 0.3f, ok ? Green : Red, FontStyles.Bold);
        verdict.text = ok ? "NIVEAU VALIDÉ !" : "NIVEAU RATÉ <size=60%>(il faut l'examen ET l'objectif)</size>";
        if (credits > 0) Label(board, "Crédits", new Vector3(0.27f, 0.215f, -0.006f), new Vector2(0.14f, 0.03f), 0.14f, Ink, FontStyles.Normal).text = $"Crédits : {credits}";

        Label(board, "Choix", new Vector3(0f, 0.012f, -0.006f), new Vector2(0.66f, 0.04f), 0.24f, Ink, FontStyles.Bold).text = "CHOIX DU NIVEAU";

        int n = GameProgress.MaxChapter;
        float w = 0.2f, gap = 0.04f;
        float x0 = -(n - 1) * (w + gap) * 0.5f;
        for (int i = 1; i <= n; i++)
        {
            bool open = i <= GameProgress.UnlockedChapter;
            string sub = !open ? $"Valide le niveau {i - 1}" : i == ch ? "Rejouer" : i > ch ? "Nouveau !" : "Rejouer";
            LevelButton(board, i, new Vector3(x0 + (i - 1) * (w + gap), -0.12f, -0.012f), new Vector2(w, 0.16f), open, sub);
        }
    }

    void LevelButton(Transform parent, int level, Vector3 pos, Vector2 size, bool open, string sub)
    {
        var go = new GameObject("Bouton niveau " + level);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        Color c = open ? (level > GameProgress.Chapter ? Green : Blue) : Locked;
        Box(go.transform, "Fond", Vector3.zero, new Vector3(size.x, size.y, 0.02f), c, open ? 0.45f : 0.1f);
        Label(go.transform, "Niveau", new Vector3(0f, 0.025f, -0.012f), new Vector2(size.x - 0.02f, 0.06f), 0.36f, Color.white, FontStyles.Bold).text = "NIVEAU " + level;
        var s = Label(go.transform, "Sous-titre", new Vector3(0f, -0.042f, -0.012f), new Vector2(size.x - 0.02f, 0.07f), 0.18f, Color.white, FontStyles.Normal);
        s.text = open ? sub : "VERROUILLÉ\n<size=75%>" + sub + "</size>";
        if (!open) return;
        var col = go.AddComponent<BoxCollider>();
        col.size = new Vector3(size.x, size.y, 0.04f);
        var mb = go.AddComponent<MenuButton>();
        int lv = level;
        mb.onClick.AddListener(() => Choose(lv));
    }

    void Place()
    {
        if (cam == null) return;
        Vector3 fwd = cam.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
        fwd.Normalize();
        transform.position = cam.position + fwd * distance - Vector3.up * 0.06f;
        transform.rotation = Quaternion.LookRotation(fwd, Vector3.up) * Quaternion.Euler(10f, 0f, 0f);
    }

    IEnumerator Pop()
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.3f)
        {
            float e = Mathf.SmoothStep(0f, 1f, t) + Mathf.Sin(t * Mathf.PI) * 0.08f;
            transform.localScale = Vector3.one * Mathf.Max(0.01f, e);
            yield return null;
        }
        transform.localScale = Vector3.one;
    }

    /// <summary>Lance le niveau choisi (au 1er cours) en gardant nos pièces et nos crédits.</summary>
    public void Choose(int level)
    {
        if (leaving || level > GameProgress.UnlockedChapter) return;
        leaving = true;
        StartCoroutine(ChooseRoutine(level));
    }

    IEnumerator ChooseRoutine(int level)
    {
        Debug.Log($"[Niveau] Choix : niveau {level} ({coins} pièces, {credits} crédit(s)).");
        if (fader != null) yield return fader.FadeTo(0f, 1f, 0.6f);
        // Pièces recomptées au moment du choix (bocal + sac + pièces qui traînent).
        if (JackpotBag.Active != null) coins = JackpotBag.Active.TotalWithLoose;
        GameProgress.StartChapter(level, coins, credits);
        GameMenu.SkipNextMenu = true;
        PlayerSafety.ReleaseAll();             // on lâche ce qu'on tient avant de recharger
        var scene = SceneManager.GetActiveScene();
        if (scene.buildIndex >= 0) SceneManager.LoadScene(scene.buildIndex);
        else SceneManager.LoadScene(scene.name);
    }

    // ---------------- Construction ----------------
    Transform Box(Transform parent, string name, Vector3 pos, Vector3 size, Color color, float glow)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        var r = go.GetComponent<Renderer>();
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", 0.2f);
        if (glow > 0f) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", color * glow); }
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go.transform;
    }

    TMP_Text Label(Transform parent, string name, Vector3 pos, Vector2 size, float fontSize, Color color, FontStyles style)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        var t = go.AddComponent<TextMeshPro>();
        if (font != null) t.font = font;
        t.fontSize = fontSize;
        t.fontStyle = style;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.enableAutoSizing = true; t.fontSizeMin = fontSize * 0.4f; t.fontSizeMax = fontSize;
        t.rectTransform.sizeDelta = size;
        return t;
    }

    // ---------------- Tests sans casque ----------------
    [ContextMenu("Test : niveau 1")] void Test1() => Choose(1);
    [ContextMenu("Test : niveau 2")] void Test2() => Choose(2);
}
