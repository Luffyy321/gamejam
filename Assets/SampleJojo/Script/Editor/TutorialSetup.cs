using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using UnityEngine;

/// <summary>
/// Menu : Tools > Boutique > 19 - Tuto guidé.
/// Après JOUER : réviser (page) → sac → machine sur la table → pièce → levier → la prof arrive → ranger → C'EST PARTI.
/// </summary>
public static class TutorialSetup
{
    [MenuItem("Tools/Boutique/19 - Tuto guidé", false, 19)]
    static void Setup()
    {
        if (Application.isPlaying) { Debug.LogWarning("[Tuto] Quitte le mode Play avant."); return; }
        var menu = Object.FindFirstObjectByType<GameMenu>(FindObjectsInactive.Include);
        if (menu == null) { Debug.LogError("[Tuto] Menu introuvable."); return; }
        var guide = Object.FindFirstObjectByType<TutorialGuide>(FindObjectsInactive.Include);
        if (guide == null)
        {
            var go = new GameObject("Tuto guidé");
            Undo.RegisterCreatedObjectUndo(go, "Tuto");
            guide = go.AddComponent<TutorialGuide>();
        }
        Undo.RecordObject(guide, "Tuto");
        guide.book = Object.FindFirstObjectByType<JackpotBook>(FindObjectsInactive.Include);
        guide.bag = Object.FindFirstObjectByType<JackpotBag>(FindObjectsInactive.Include);
        guide.machine = Object.FindFirstObjectByType<JackpotItem>(FindObjectsInactive.Include);
        guide.lever = Object.FindFirstObjectByType<JackpotLever>(FindObjectsInactive.Include);
        guide.slot = Object.FindFirstObjectByType<CoinSlot>(FindObjectsInactive.Include);
        guide.jar = Object.FindFirstObjectByType<CoinJar>(FindObjectsInactive.Include);
        guide.prof = Object.FindFirstObjectByType<ProfAI>(FindObjectsInactive.Include);
        guide.tutorialCoins = 6;
        guide.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/SampleJojo/Fonts/Cartoon SDF.asset");
        guide.messageClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/SampleJojo/Audio/page_flip.wav");
        guide.clickClip = guide.messageClip;
        guide.popClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/SampleJojo/Audio/pop.wav");
        EditorUtility.SetDirty(guide);

        Undo.RecordObject(menu, "Tuto");
        menu.guide = guide;
        EditorUtility.SetDirty(menu);
        EditorSceneManager.MarkSceneDirty(guide.gameObject.scene);
        Debug.Log("[Tuto] Tuto guidé branché sur JOUER. Livre " + (guide.book ? "OK" : "?") + ", sac " + (guide.bag ? "OK" : "?") +
                  ", machine " + (guide.machine ? "OK" : "?") + ", fente " + (guide.slot ? "OK" : "?") + ", bocal " + (guide.jar ? "OK" : "?") +
                  ", prof " + (guide.prof ? "OK" : "?") + ", police " + (guide.font ? "OK" : "?") + ", sons " + (guide.popClip && guide.messageClip ? "OK" : "?") + ".");
    }

    [MenuItem("Tools/Boutique/20 - Test : JOUER (en Play)", false, 20)]
    static void TestPlay() { var m = Object.FindFirstObjectByType<GameMenu>(); if (Application.isPlaying && m != null) m.Jouer(); }

    [MenuItem("Tools/Boutique/21 - Test : tuto, faire l'étape (en Play)", false, 21)]
    static void TestStep() { var g = Object.FindFirstObjectByType<TutorialGuide>(); if (Application.isPlaying && g != null) g.TestDoStep(); }

    [MenuItem("Tools/Boutique/22 - Photo vue du joueur (en Play)", false, 22)]
    static void Photo()
    {
        var cam = Camera.main;
        if (cam == null) return;
        var go = new GameObject("PhotoCam");
        var c = go.AddComponent<Camera>();
        c.fieldOfView = 75f; c.nearClipPlane = 0.02f;
        go.transform.SetPositionAndRotation(cam.transform.position, cam.transform.rotation);
        var rt = new RenderTexture(1280, 800, 24);
        c.targetTexture = rt; c.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1280, 800, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); tex.Apply();
        RenderTexture.active = null;
        System.IO.File.WriteAllBytes("Assets/SampleJojo/Preview~/tuto.png", tex.EncodeToPNG());
        c.targetTexture = null;
        Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); Object.DestroyImmediate(go);
        Debug.Log("[Tuto] photo");
    }
}
