using System.IO;
using UnityEditor;
using UnityEngine;

public static class ShopPhoto
{
    const string Dir = "Assets/SampleJojo/Preview~/";

    [MenuItem("Tools/Boutique/Photo du couloir", false, 20)]
    static void Photo()
    {
        var spawn = GameObject.Find("Couloir boutique/Départ boutique");
        var locker = GameObject.Find("Couloir boutique/Casier trésor");
        if (spawn == null || locker == null) { Debug.LogError("[Photo] couloir introuvable"); return; }
        Vector3 eye = spawn.transform.position + Vector3.up * 1.6f;
        Shoot(eye, eye + Vector3.forward * 5f, Vector3.up, "couloir.png");
        var lb = locker.GetComponentInChildren<Renderer>().bounds;
        var shop = locker.GetComponent<LockerShop>();
        Shoot(lb.center + new Vector3(0f, 0.3f, -1.6f), lb.center + new Vector3(0.3f, -0.1f, 0f), Vector3.up, "casier.png");
        if (shop != null && shop.door != null)
        {
            var rest = shop.door.localRotation;
            shop.door.localRotation = Quaternion.AngleAxis(shop.openAngle, shop.hingeAxis) * rest;
            foreach (var it in shop.items) if (it != null && it.priceLabel != null) it.priceLabel.gameObject.SetActive(true);
            Shoot(lb.center + new Vector3(0.15f, 0.15f, -1.0f), lb.center + new Vector3(0f, 0f, 0f), Vector3.up, "casier_ouvert.png");
            Shoot(lb.center + new Vector3(-1.0f, 0.9f, -1.1f), lb.center + new Vector3(-0.3f, 0f, 0f), Vector3.up, "casier_ouvert_haut.png");
            Shoot(lb.center + new Vector3(0f, 2.2f, -0.7f), lb.center + new Vector3(0f, 0f, -0.69f), Vector3.forward, "casier_dessus.png");
            Shoot(lb.center + new Vector3(0f, 0.1f, -2.8f), lb.center, Vector3.up, "casier_face.png");
            shop.door.localRotation = Quaternion.AngleAxis(shop.openAngle * 0.5f, shop.hingeAxis) * rest;
            Shoot(lb.center + new Vector3(-1.0f, 0.9f, -1.1f), lb.center + new Vector3(-0.3f, 0f, 0f), Vector3.up, "casier_mi.png");
            shop.door.localRotation = Quaternion.AngleAxis(shop.openAngle, shop.hingeAxis) * rest;
            string info = "";
            foreach (var it in shop.items) if (it != null) { var rb = it.GetComponentInChildren<Renderer>(); info += it.name + " " + it.transform.position.ToString("F2") + " actif=" + it.gameObject.activeInHierarchy + " taille=" + (rb ? rb.bounds.size.ToString("F2") : "?") + " | "; }
            Debug.Log("[Photo] objets : " + info + " casier=" + lb.ToString("F2"));
            foreach (var it in shop.items) if (it != null && it.priceLabel != null) it.priceLabel.gameObject.SetActive(false);
            shop.door.localRotation = rest;
        }
        var deco = GameObject.Find("Couloir boutique/Casiers décor/Casier D3");
        var ld = deco != null ? deco.GetComponent<LockerDoor>() : null;
        if (ld != null)
        {
            var r0 = ld.hinge.localRotation;
            ld.hinge.localRotation = Quaternion.AngleAxis(ld.openAngle, ld.hingeAxis) * r0;
            var db = deco.GetComponentInChildren<Renderer>().bounds;
            Shoot(db.center + new Vector3(-1.2f, 0.5f, -0.9f), db.center, Vector3.up, "casier_deco.png");
            Shoot(db.center + new Vector3(-0.6f, 2.0f, 0f), db.center + new Vector3(-0.59f, 0f, 0f), Vector3.forward, "casier_deco_dessus.png");
            ld.hinge.localRotation = r0;
        }
        Debug.Log("[Photo] couloir");
    }

    static void Shoot(Vector3 eye, Vector3 target, Vector3 up, string file)
    {
        var go = new GameObject("PhotoCam");
        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = 45f;
        cam.nearClipPlane = 0.01f;
        go.transform.position = eye;
        go.transform.LookAt(target, up);
        var rt = new RenderTexture(1280, 800, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1280, 800, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        Directory.CreateDirectory(Dir);
        File.WriteAllBytes(Dir + file, tex.EncodeToPNG());
        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(go);
    }
}
