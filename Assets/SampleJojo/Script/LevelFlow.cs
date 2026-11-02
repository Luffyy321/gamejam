using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Fin du chrono : fondu, on se retrouve dans le couloir de la boutique.
/// « Niveau suivant » : on revient en classe avec nos pièces, et les objets achetés sont sur notre table.
/// </summary>
public class LevelFlow : MonoBehaviour
{
    [System.Serializable]
    public class ItemPrefab
    {
        public string itemId;
        public GameObject prefab;
    }

    public ClassroomCountdown countdown;
    public ProfAI prof;
    public GameOverRestart fader;
    public GameMenu menu;

    [Header("Boutique")]
    public Transform shopSpawn;

    [Header("Objets achetés")]
    public ItemPrefab[] itemPrefabs;
    [Tooltip("Où poser les objets achetés sur notre table (alignés vers la droite).")]
    public Transform tableSpot;
    public float itemSpacing = 0.12f;

    [Header("Examen (fin du 3e cours)")]
    public int examLevel = 3;
    [Tooltip("Durée de l'examen (secondes) ; ensuite c'est la fin du niveau.")]
    public float examSeconds = 60f;
    public QcmSheet qcm;
    public JackpotBook book;

    [Header("Sons")]
    [Tooltip("Sonnerie de fin du cours.")]
    public AudioClip bellClip;
    [Tooltip("Alarme de fin de l'examen.")]
    public AudioClip examEndClip;

    void Play2D(AudioClip c, float v = 1f)
    {
        if (c == null) return;
        var cam = Camera.main;
        AudioSource.PlayClipAtPoint(c, cam != null ? cam.transform.position : transform.position, v);
    }

    bool inShop, leaving, examStarted, examDone;
    public bool ExamStarted => examStarted;
    public bool InShop => inShop;

    void Awake()
    {
        if (countdown == null) countdown = FindFirstObjectByType<ClassroomCountdown>();
        if (prof == null) prof = FindFirstObjectByType<ProfAI>();
        if (fader == null) fader = FindFirstObjectByType<GameOverRestart>();
        if (menu == null) menu = FindFirstObjectByType<GameMenu>();
    }

    void OnEnable() { if (countdown != null) countdown.onTimeUp.AddListener(GoToShop); }
    void OnDisable() { if (countdown != null) countdown.onTimeUp.RemoveListener(GoToShop); }

    void Start() => SpawnPurchased();

    // ---------------- Fin du chrono → boutique ----------------
    public void GoToShop()
    {
        if (examStarted && !examDone) { Play2D(examEndClip); StartCoroutine(EndExam()); return; }   // fin de la minute d'examen
        if (inShop) return;
        Play2D(bellClip);
        if (GameProgress.Level >= examLevel && qcm != null) { StartExam(); return; }
        inShop = true;
        StartCoroutine(ShopRoutine());
    }

    IEnumerator ShopRoutine()
    {
        Debug.Log("[Niveau] Fin du chrono : direction la boutique.");
        if (fader != null) yield return fader.FadeTo(0f, 1f, 0.7f);
        if (prof != null) prof.enabled = false;               // la prof ne peut plus nous attraper
        if (menu != null) menu.TeleportTo(shopSpawn);
        yield return new WaitForSecondsRealtime(0.3f);
        if (fader != null) yield return fader.FadeTo(1f, 0f, 0.8f);
    }

    // ---------------- Examen : le livre s'en va, la feuille de QCM arrive ----------------
    public void StartExam()
    {
        if (examStarted || qcm == null) return;
        examStarted = true;
        StartCoroutine(ExamRoutine());
    }

    IEnumerator ExamRoutine()
    {
        Debug.Log("[Niveau] Fin du 3e cours : interro surprise !");
        // La prof continue sa ronde pendant l'examen (plus tard : antisèche à cacher).
        // La machine range dans le sac : la feuille prend sa place sur la table.
        var machine = FindFirstObjectByType<JackpotItem>();
        if (machine != null && !machine.IsStored && JackpotBag.Active != null && !machine.Grab.isSelected) JackpotBag.Active.Store(machine);
        if (book == null) book = FindFirstObjectByType<JackpotBook>();
        if (book != null)
        {
            var t = book.transform;
            Vector3 s0 = t.localScale, p0 = t.position;
            for (float k = 0f; k < 1f; k += Time.deltaTime / 0.45f)
            {
                float e = Mathf.SmoothStep(0f, 1f, k);
                t.localScale = s0 * (1f - e);
                t.position = p0 + Vector3.up * 0.2f * e;
                yield return null;
            }
            book.gameObject.SetActive(false);
        }
        yield return new WaitForSeconds(0.15f);
        qcm.Show();
        if (countdown != null) countdown.StartCountdown(examSeconds);       // 1 minute sur les horloges
    }

    [Tooltip("Temps pour voir la note sur la copie avant le choix du niveau (s).")]
    public float showResultSeconds = 3.5f;

    /// <summary>Fin de la minute : la copie est ramassée, le pourcentage s'affiche, puis on passe au choix du niveau.</summary>
    IEnumerator EndExam()
    {
        examDone = true;
        int score = qcm != null ? qcm.Collect() : 0;
        GameProgress.LastExamScore = score;
        GameProgress.LastExamQuestions = qcm != null ? qcm.QuestionCount : 0;
        GameProgress.LastExamPassed = qcm != null && qcm.Passed(score);
        // Objectif de pièces du niveau (post-it) : toutes nos pièces à la fin de l'examen.
        GameProgress.LastGoal = GameBalance.GoalFor(GameProgress.Chapter);
        GameProgress.LastGoalCoins = CurrentCoins();
        GameProgress.LastGoalReached = GameProgress.LastGoalCoins >= GameProgress.LastGoal;
        // Niveau validé = examen réussi ET objectif de pièces atteint : le niveau suivant se débloque.
        GameProgress.LastLevelValidated = GameProgress.LastExamPassed && GameProgress.LastGoalReached;
        if (GameProgress.LastLevelValidated)
            GameProgress.UnlockedChapter = Mathf.Max(GameProgress.UnlockedChapter, Mathf.Min(GameProgress.Chapter + 1, GameProgress.MaxChapter));
        Debug.Log($"[Niveau] Fin du niveau {GameProgress.Chapter} : examen {score} / {GameProgress.LastExamQuestions} ({GameProgress.LastExamPercent} %) {(GameProgress.LastExamPassed ? "RÉUSSI" : "RATÉ")}, " +
                  $"objectif {GameProgress.LastGoalCoins} / {GameProgress.LastGoal} pièces {(GameProgress.LastGoalReached ? "ATTEINT" : "RATÉ")} → niveau {(GameProgress.LastLevelValidated ? "VALIDÉ" : "RATÉ")}.");
        if (prof != null) prof.enabled = false;               // plus de danger pendant qu'on regarde la note
        yield return new WaitForSeconds(showResultSeconds);
        inShop = true;
        yield return LevelSelectRoutine();
    }

    /// <summary>Fin du niveau : fondu, on se retrouve dans l'espace gris devant le choix du niveau.</summary>
    IEnumerator LevelSelectRoutine()
    {
        int coins = CurrentCoins(), credits = CurrentCredits();
        if (fader != null) yield return fader.FadeTo(0f, 1f, 0.7f);
        if (prof != null) prof.enabled = false;
        if (menu != null) menu.TeleportTo(menu.menuSpawn != null ? menu.menuSpawn : shopSpawn);
        yield return new WaitForSecondsRealtime(0.3f);
        var ls = new GameObject("Choix du niveau").AddComponent<LevelSelect>();
        ls.Open(coins, credits, fader);
        if (fader != null) yield return fader.FadeTo(1f, 0f, 0.8f);
    }

    /// <summary>Toutes nos pièces : sac + bocal + celles qui traînent sur la table.</summary>
    int CurrentCoins() { var bag = JackpotBag.Active; return bag != null ? bag.TotalWithLoose : GameProgress.Coins; }
    int CurrentCredits() { var l = FindFirstObjectByType<JackpotLever>(FindObjectsInactive.Include); return l != null ? l.credits : 0; }

    // ---------------- Boutique → cours suivant ----------------
    public void NextLevel()
    {
        if (leaving) return;
        leaving = true;
        StartCoroutine(NextLevelRoutine());
    }

    IEnumerator NextLevelRoutine()
    {
        if (RouletteTable.Active != null) RouletteTable.Active.Refund();      // mise pas jouée : rendue
        int coins = CurrentCoins(), credits = CurrentCredits();
        if (AdrenalineEffect.Active != null) AdrenalineEffect.Active.SaveToProgress();
        if (fader != null) yield return fader.FadeTo(0f, 1f, 0.7f);
        GameProgress.NextLevel(coins, credits);
        GameMenu.SkipNextMenu = true;
        Debug.Log($"[Niveau] Niveau {GameProgress.Chapter}, cours {GameProgress.Level} ({GameProgress.Coins} pièces, {GameProgress.Credits} crédit(s), {GameProgress.Purchased.Count} objet(s)).");
        PlayerSafety.ReleaseAll();             // on lâche ce qu'on tient avant de recharger
        var scene = SceneManager.GetActiveScene();
        if (scene.buildIndex >= 0) SceneManager.LoadScene(scene.buildIndex);
        else SceneManager.LoadScene(scene.name);
    }

    // ---------------- Objets achetés sur la table ----------------
    void SpawnPurchased()
    {
        if (tableSpot == null || GameProgress.Purchased.Count == 0) return;
        int i = 0;
        foreach (var id in GameProgress.Purchased)
        {
            ItemPrefab def = null;
            foreach (var d in itemPrefabs) if (d != null && d.itemId == id) { def = d; break; }
            if (def == null || def.prefab == null) continue;
            Vector3 pos = tableSpot.position + tableSpot.right * (i * itemSpacing) + Vector3.up * 0.05f;
            var go = Instantiate(def.prefab, pos, tableSpot.rotation);
            go.name = def.prefab.name;
            i++;
        }
    }

    // ---------------- Tests sans casque ----------------
    [ContextMenu("Test : interro (QCM)")] public void TestExam() => StartExam();
    [ContextMenu("Test : fin du chrono (boutique)")] public void TestShop() => GoToShop();
    [ContextMenu("Test : niveau suivant")] public void TestNext() => NextLevel();
    [ContextMenu("Test : fin de l'examen (choix du niveau)")] public void TestEndExam() { if (!examStarted) { examStarted = true; if (qcm != null && !qcm.gameObject.activeInHierarchy) qcm.Show(); } if (!examDone) StartCoroutine(EndExam()); }
}
