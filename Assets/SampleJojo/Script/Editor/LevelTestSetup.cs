using UnityEditor;
using UnityEngine;

/// <summary>Tests sans casque : fin d'examen / choix du niveau, gain dans le bocal.</summary>
public static class LevelTestSetup
{
    [MenuItem("Tools/Boutique/27 - Test : fin de l'examen + choix du niveau (en Play)", false, 27)]
    static void EndExam()
    {
        if (!Application.isPlaying) return;
        var flow = Object.FindFirstObjectByType<LevelFlow>();
        var qcm = Object.FindFirstObjectByType<QcmSheet>(FindObjectsInactive.Include);
        if (flow == null) { Debug.Log("[Test] LevelFlow introuvable"); return; }
        if (qcm != null && !qcm.gameObject.activeInHierarchy) qcm.Show();
        // Coche la bonne réponse aux 3 premières questions (75 %).
        if (qcm != null)
            for (int i = 0; i < qcm.correctAnswers.Length && i < 9; i++)
                if (qcm.correctAnswers[i] && !qcm.IsChecked(i)) qcm.Toggle(i);
        flow.TestEndExam();
    }

    [MenuItem("Tools/Boutique/28 - Test : gain de 37 dans le bocal (en Play)", false, 28)]
    static void Win()
    {
        if (!Application.isPlaying) return;
        var c = Object.FindFirstObjectByType<JackpotCoins>(FindObjectsInactive.Include);
        if (c != null) c.Payout(37);
    }

    [MenuItem("Tools/Boutique/29 - Test : choisir le niveau 2 (en Play)", false, 29)]
    static void Choose2()
    {
        if (!Application.isPlaying) return;
        var ls = Object.FindFirstObjectByType<LevelSelect>();
        if (ls != null) ls.Choose(2);
    }
}
