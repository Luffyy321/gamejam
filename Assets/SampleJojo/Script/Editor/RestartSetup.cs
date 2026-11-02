using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menu : Tools > Jackpot > 29 - Attrapé par la prof = fondu au noir + on recommence (5 pièces au départ).
/// </summary>
public static class RestartSetup
{
    const int StartCoins = 5;

    [MenuItem("Tools/Jackpot/29 - Attrapé = fondu au noir + recommencer (5 pièces)", false, 140)]
    static void Setup()
    {
        if (Application.isPlaying) { Debug.LogWarning("[Partie] Quitte le mode Play avant."); return; }
        var prof = Object.FindFirstObjectByType<ProfAI>();
        if (prof == null) { Debug.LogError("[Partie] Prof (ProfAI) introuvable."); return; }

        var go = GameObject.Find("Partie");
        if (go == null) { go = new GameObject("Partie"); Undo.RegisterCreatedObjectUndo(go, "Partie"); }
        var g = go.GetComponent<GameOverRestart>();
        if (g == null) g = Undo.AddComponent<GameOverRestart>(go);
        Undo.RecordObject(g, "Partie");
        g.prof = prof;
        g.fadeShader = Shader.Find("SampleJojo/ScreenFade");
        if (g.fadeShader == null) Debug.LogWarning("[Partie] Shader SampleJojo/ScreenFade introuvable (attends la fin de la compilation et relance).");
        g.caughtClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/SampleJojo/Audio/prof_alert.wav");
        EditorUtility.SetDirty(g);

        // 5 pièces dans le sac au départ.
        var bag = Object.FindFirstObjectByType<JackpotBag>();
        if (bag != null)
        {
            Undo.RecordObject(bag, "Sac");
            bag.coinValue = StartCoins;
            EditorUtility.SetDirty(bag);
        }
        foreach (var d in Object.FindObjectsByType<CoinCounterDisplay>(FindObjectsSortMode.None))
        {
            var t = d.GetComponent<TMP_Text>();
            if (t != null) { Undo.RecordObject(t, "Écran"); t.text = StartCoins.ToString(); }
        }

        // La scène doit être dans la liste du build pour être rechargée.
        var scene = go.scene;
        var list = EditorBuildSettings.scenes.ToList();
        if (!string.IsNullOrEmpty(scene.path) && !list.Any(s => s.path == scene.path))
        {
            list.Insert(0, new EditorBuildSettingsScene(scene.path, true));
            EditorBuildSettings.scenes = list.ToArray();
            Debug.Log("[Partie] Scène ajoutée au build : " + scene.path);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[Partie] Attrapé par la prof → fondu au noir puis on recommence avec {StartCoins} pièces.");
    }

    [MenuItem("Tools/Jackpot/30 - Test : se faire attraper (en Play)", false, 141)]
    static void TestCaught()
    {
        var g = Object.FindFirstObjectByType<GameOverRestart>();
        if (Application.isPlaying && g != null) g.TestCaught();
    }
}
