using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Le post-it à côté de la calculatrice (en classe et dans le casier) affiche l'objectif de pièces du niveau :
/// 100 au niveau 1, 200 au niveau 2, 400 au niveau 3 (GameBalance.Goals).
/// </summary>
public static class GoalPostIt
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnScene;
        SceneManager.sceneLoaded += OnScene;
        Refresh();
    }

    static void OnScene(Scene s, LoadSceneMode m) => Refresh();

    public static void Refresh()
    {
        int goal = GameBalance.GoalFor(GameProgress.Chapter);
        int n = 0;
        foreach (var t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.name != "Objectif" || t.transform.parent == null || !t.transform.parent.name.StartsWith("Post-it")) continue;
            t.text = $"Objectif\ndu jour :\n<size=170%>{goal}</size>\npièces";      // seulement des lettres de la police du crayon
            n++;
        }
        if (n > 0) Debug.Log($"[Objectif] Niveau {GameProgress.Chapter} : {goal} pièces (post-it).");
    }
}
