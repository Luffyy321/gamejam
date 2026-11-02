using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ShopDebug
{
    [MenuItem("Tools/Boutique/Debug objets", false, 30)]
    static void Run()
    {
        string s = "";
        foreach (var it in Object.FindObjectsByType<ShopItem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var rs = it.GetComponentsInChildren<Renderer>(true);
            s += $"\n{it.name} pos={it.transform.position:F3} parent={(it.transform.parent ? it.transform.parent.name : "-")} actif={it.gameObject.activeInHierarchy} layer={it.gameObject.layer} scale={it.transform.lossyScale:F3} rends={rs.Length} " +
                 string.Join(",", rs.Select(r => $"{r.name}:{r.enabled}/{r.bounds.center:F2}/{r.bounds.size:F3}/{(r.sharedMaterial ? r.sharedMaterial.name : "null")}"));
        }
        var cam = Camera.main;
        if (cam != null) s += $"\ncam={cam.transform.position:F2} fwd={cam.transform.forward:F2} mask={cam.cullingMask}";
        var body = GameObject.Find("Couloir boutique/Casier trésor");
        if (body != null) s += $"\ncasier pos={body.transform.position:F2} rot={body.transform.eulerAngles:F0} scale={body.transform.lossyScale:F2}";
        System.IO.File.WriteAllText("Assets/SampleJojo/Preview~/shop_debug.txt", s);
        Debug.Log("[Debug] " + s);
    }

    [MenuItem("Tools/Boutique/Debug : objets devant les yeux", false, 31)]
    static void Bring()
    {
        var cam = Camera.main;
        int i = 0;
        foreach (var it in Object.FindObjectsByType<ShopItem>(FindObjectsSortMode.None))
        {
            it.transform.position = cam.transform.position + cam.transform.forward * 0.6f + cam.transform.right * (i - 1) * 0.2f;
            i++;
        }
    }
}
