using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Photos de près des objets de la boutique (prefabs), pour vérifier leur rendu.</summary>
public static class ItemsPhoto
{
    [MenuItem("Tools/Jeu/6 - Photos des objets", false, 6)]
    static void Photo()
    {
        string[] ids = { "antiseche", "lunettes3d", "doflamingo", "cafe" };
        var info = new System.Text.StringBuilder();
        foreach (var id in ids)
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SampleJojo/model/Props/boutique/Objet_" + id + ".prefab");
            if (p == null) continue;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(p);
            go.transform.position = new Vector3(0f, -300f, 0f);
            var l = new GameObject("L").AddComponent<Light>(); l.type = LightType.Directional; l.transform.rotation = Quaternion.Euler(40f, 30f, 0f);
            var b = go.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, c) => { a.Encapsulate(c); return a; });
            info.AppendLine($"{id} taille={b.size:F3}");
            float d = b.size.magnitude * 1.6f;
            Shoot(b.center + new Vector3(0f, 0f, -d), b.center, id + "_face.png");
            Shoot(b.center + new Vector3(d, d * 0.3f, 0f), b.center, id + "_cote.png");
            Shoot(b.center + new Vector3(0f, d, -0.01f), b.center, id + "_dessus.png");
            Object.DestroyImmediate(go); Object.DestroyImmediate(l.gameObject);
        }
        System.IO.File.WriteAllText("Assets/SampleJojo/Preview~/objets.txt", info.ToString());
        Debug.Log("[Jeu] photos des objets");
    }

    static void Shoot(Vector3 eye, Vector3 target, string file)
    {
        var go = new GameObject("PhotoCam");
        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = 40f; cam.nearClipPlane = 0.005f; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.55f, 0.6f, 0.65f);
        go.transform.position = eye; go.transform.LookAt(target, Vector3.up);
        var rt = new RenderTexture(512, 512, 24);
        cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(512, 512, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); tex.Apply();
        RenderTexture.active = null;
        System.IO.File.WriteAllBytes("Assets/SampleJojo/Preview~/obj_" + file, tex.EncodeToPNG());
        cam.targetTexture = null;
        Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); Object.DestroyImmediate(go);
    }
}
