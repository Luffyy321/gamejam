using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Menu : Tools > Jackpot > 12 - Mains avec avant-bras (à la place des manettes).</summary>
public static class HandsSetup
{
    const string ModelsDir = "Assets/Samples/XR Hands/1.8.1/HandVisualizer/Models";
    const string MatDir = "Assets/SampleJojo/model";

    [MenuItem("Tools/Jackpot/12 - Mains avec avant-bras", false, 123)]
    static void Setup()
    {
        if (Application.isPlaying) { Debug.LogWarning("[Mains] Quitte le mode Play avant."); return; }
        var origin = Object.FindFirstObjectByType<XROrigin>();
        if (origin == null) { Debug.LogError("[Mains] Pas de XR Origin dans la scène."); return; }

        var left = AssetDatabase.LoadAssetAtPath<GameObject>(ModelsDir + "/LeftHand.fbx");
        var right = AssetDatabase.LoadAssetAtPath<GameObject>(ModelsDir + "/RightHand.fbx");
        if (left == null || right == null)
        {
            // Autre version du sample XR Hands ?
            foreach (var guid in AssetDatabase.FindAssets("LeftHand t:Model"))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (p.EndsWith("/LeftHand.fbx")) { left = AssetDatabase.LoadAssetAtPath<GameObject>(p); right = AssetDatabase.LoadAssetAtPath<GameObject>(p.Replace("LeftHand.fbx", "RightHand.fbx")); break; }
            }
        }
        if (left == null || right == null) { Debug.LogError("[Mains] Modèles LeftHand/RightHand (sample XR Hands) introuvables."); return; }

        var hands = origin.GetComponent<VRHands>();
        if (hands == null) hands = Undo.AddComponent<VRHands>(origin.gameObject);
        Undo.RecordObject(hands, "Mains");
        hands.leftHandModel = left;
        hands.rightHandModel = right;
        hands.skinMaterial = Mat("Main_Peau", new Color(0.86f, 0.66f, 0.54f), 0.35f);
        hands.sleeveMaterial = Mat("Main_Manche", new Color(0.13f, 0.17f, 0.28f), 0.15f);
        EditorUtility.SetDirty(hands);
        PrefabUtility.RecordPrefabInstancePropertyModifications(hands);
        EditorSceneManager.MarkSceneDirty(origin.gameObject.scene);
        Debug.Log("[Mains] OK : mains avec avant-bras ajoutées sur « " + origin.name + " » (visibles en Play).");
    }

    [MenuItem("Tools/Jackpot/13 - Test : voir les mains (en Play)", false, 124)]
    static void TestOpen() => Show(0.1f);

    [MenuItem("Tools/Jackpot/14 - Test : mains fermées (en Play)", false, 125)]
    static void TestClosed() => Show(1f);

    static void Show(float curl)
    {
        var h = Object.FindFirstObjectByType<VRHands>(FindObjectsInactive.Include);
        if (h == null || !Application.isPlaying) { Debug.LogWarning("[Mains] À lancer en Play."); return; }
        h.TestShowInFront(curl);
    }

    static Material Mat(string name, Color c, float smooth)
    {
        string path = MatDir + "/" + name + ".mat";
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        m.shader = shader;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        m.color = c;
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        return m;
    }
}
