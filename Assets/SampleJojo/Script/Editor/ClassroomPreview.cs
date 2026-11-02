using UnityEditor;
using UnityEngine;

public static class ClassroomPreview
{
    const string Blend = "Assets/SampleJojo/model/Props/clasroom/Salle de Classe WALAD .blend";

    [MenuItem("Tools/Jackpot/Aperçu salle (.blend) à l'écart", false, 302)]
    static void Preview()
    {
        var old = GameObject.Find("_Apercu salle");
        if (old != null) { Object.DestroyImmediate(old); return; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Blend));
        go.name = "_Apercu salle";
        go.transform.position = new Vector3(60f, 0f, 60f);
        var plane = go.transform.Find("Plane");
        Selection.activeGameObject = plane != null ? plane.gameObject : go;
        SceneView.lastActiveSceneView?.FrameSelected();
    }
}
