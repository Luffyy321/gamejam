using System.Linq;
using UnityEditor;
using UnityEngine;

public static class BagScan
{
    [MenuItem("Tools/Jackpot/Rapport orientation sac", false, 307)]
    static void Scan()
    {
        var bag = Object.FindFirstObjectByType<JackpotBag>();
        var pack = bag != null ? bag.transform.Find("Sac à dos") : null;
        if (pack == null) { Debug.LogError("[Sac] introuvable"); return; }
        string s = "";
        foreach (var n in new[] { "Lock2", "Lock0", "LeftHandleMiddle", "RightHandleMiddle", "UpperHandle", "Pocket", "Zipper" })
        {
            var t = pack.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == n);
            if (t != null) s += $"{n}={bag.transform.InverseTransformPoint(t.position):F3} ";
        }
        var r = pack.GetComponentInChildren<Renderer>();
        s += $" centre={bag.transform.InverseTransformPoint(r.bounds.center):F3} bagFwd={bag.transform.forward:F2}";
        Debug.Log("[Sac] " + s);
    }

    [MenuItem("Tools/Jackpot/Rapport positions (calculatrice)", false, 308)]
    static void Positions()
    {
        string s = "";
        foreach (var n in new[] { "Calculatrice", "Post-it objectif", "Objectif", "Ecran pièces", "Chaise du jeu", "Jackpot", "Sac Jackpot", "Props bureau" })
        {
            var go = GameObject.Find(n);
            if (go == null) { s += n + "=? | "; continue; }
            var rs = go.GetComponentsInChildren<Renderer>();
            Bounds b = rs.Length > 0 ? rs[0].bounds : new Bounds(go.transform.position, Vector3.zero);
            foreach (var r in rs) b.Encapsulate(r.bounds);
            s += $"{n} pos={go.transform.position:F2} fwd={go.transform.forward:F2} up={go.transform.up:F2} bc={b.center:F2} bs={b.size:F2} | ";
        }
        var sock = Object.FindFirstObjectByType<JackpotSocket>();
        if (sock != null) s += $"socket={sock.transform.position:F2}";
        System.IO.File.WriteAllText("Assets/SampleJojo/Script/Editor/positions.txt", s.Replace(" | ", "\n"));
        Debug.Log("[Positions] écrit");
    }

    [MenuItem("Tools/Jackpot/Photo calculatrice (depuis la chaise)", false, 309)]
    static void Photo()
    {
        var calc = GameObject.Find("Calculatrice");
        var chair = GameObject.Find("Chaise du jeu");
        if (calc == null || chair == null) { Debug.LogError("[Photo] calculatrice ou chaise introuvable"); return; }
        Vector3 target = calc.GetComponentInChildren<Renderer>().bounds.center;
        Vector3 eye = chair.transform.position + Vector3.up * 1.2f;
        Shoot(eye, target, 25f, "photo_calc.png");
        Vector3 close = target + (eye - target).normalized * 0.45f;
        Shoot(close, target, 35f, "photo_calc_proche.png");
    }

    static void Shoot(Vector3 eye, Vector3 target, float fov, string file)
    {
        var go = new GameObject("PhotoCam");
        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = fov;
        cam.nearClipPlane = 0.01f;
        go.transform.position = eye;
        go.transform.LookAt(target);
        var rt = new RenderTexture(1280, 800, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1280, 800, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        System.IO.File.WriteAllBytes(file, tex.EncodeToPNG());
        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(go);
        Debug.Log("[Photo] " + file);
    }
}
