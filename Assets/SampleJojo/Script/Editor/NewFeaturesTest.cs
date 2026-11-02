using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Test en Play des nouveautés : objets (café, lunettes), livre, roulette. Écrit des photos dans Preview~.</summary>
public static class NewFeaturesTest
{
    [MenuItem("Tools/Jeu/7 - Test en Play : objets + roulette", false, 7)]
    static void Run()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[Test] Lance le mode Play d'abord."); return; }
        new GameObject("Test nouveautés").AddComponent<Runner>();
    }

    [MenuItem("Tools/Jeu/8 - Test en Play : fin du niveau + post-it", false, 8)]
    static void RunEnd()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[Test] Lance le mode Play d'abord."); return; }
        new GameObject("Test fin").AddComponent<EndRunner>();
    }

    [MenuItem("Tools/Jeu/9 - Diagnostic machine + sac (en Play)", false, 9)]
    static void Diag()
    {
        var bag = Object.FindFirstObjectByType<JackpotBag>(FindObjectsInactive.Include);
        var m = Object.FindFirstObjectByType<JackpotItem>(FindObjectsInactive.Include);
        var cam = Camera.main;
        string B(Transform t) { var s2 = ""; for (var x = t; x != null; x = x.parent) s2 = x.name + (x.gameObject.activeSelf ? "" : "[OFF]") + "/" + s2; return s2; }
        Debug.Log($"[Diag] cours {GameProgress.Level} niveau {GameProgress.Chapter} | sac : {(bag == null ? "AUCUN" : B(bag.transform) + $" pos={bag.transform.position:F2} actif={bag.gameObject.activeInHierarchy} hasItem={bag.HasItem}")}" +
                  $" | machine : {(m == null ? "AUCUNE" : B(m.transform) + $" pos={m.transform.position:F2} actif={m.gameObject.activeInHierarchy} rangée={m.IsStored} posée={m.IsPlaced}")}" +
                  $" | caméra {(cam != null ? cam.transform.position.ToString("F2") : "-")}");
        var cr = Object.FindFirstObjectByType<ClassroomRooms>(FindObjectsInactive.Include);
        if (cr != null && bag != null) Debug.Log($"[Diag] sac dans core={cr.core.Contains(bag.transform)} movers={cr.movers.Contains(bag.transform)} ; machine core={(m != null && cr.core.Contains(m.transform))} movers={(m != null && cr.movers.Contains(m.transform))}");
    }

    class EndRunner : MonoBehaviour
    {
        IEnumerator Start()
        {
            var post = FindObjectsByType<TMPro.TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.name == "Objectif" && t.transform.parent != null && t.transform.parent.name.StartsWith("Post-it") && t.gameObject.activeInHierarchy);
            if (post != null) Runner.Photo(post.transform.position + post.transform.up * 0.0f - post.transform.forward * 0.18f, post.transform.position, "test_postit.png");
            var flow = FindFirstObjectByType<LevelFlow>();
            flow.TestEndExam();
            yield return new WaitForSeconds(6.5f);
            var cam = Camera.main;
            Runner.Photo(cam.transform.position, cam.transform.position + cam.transform.forward, "test_fin_niveau.png", true);
            Destroy(gameObject);
        }
    }

    class Runner : MonoBehaviour
    {
        IEnumerator Start()
        {
            var flow = FindFirstObjectByType<LevelFlow>(FindObjectsInactive.Include);
            var cam = Camera.main;
            var book = FindFirstObjectByType<JackpotBook>();
            if (book != null) { book.TestTurn(1); yield return new WaitForSeconds(1f); Photo(book.transform.position + Vector3.up * 0.45f, book.transform.position, "test_livre_normal.png"); }
            foreach (var id in new[] { "lunettes3d", "doflamingo", "cafe" })
            {
                var def = flow.itemPrefabs.FirstOrDefault(p => p != null && p.itemId == id);
                if (def == null || def.prefab == null) { Debug.LogWarning("[Test] prefab manquant " + id); continue; }
                var go = Instantiate(def.prefab, cam.transform.position + cam.transform.forward * 0.4f, Quaternion.identity);
                go.GetComponent<Rigidbody>().isKinematic = true;
                yield return null;
                go.GetComponent<Consumable>().Consume();
                yield return new WaitForSeconds(0.5f);
            }
            Debug.Log($"[Test] Après les objets : temps x{SlowMo.Factor:F2}, filtre rose {ItemEffects.PinkOn}, mots clés {JackpotBook.ShowKeywords}.");
            if (book != null) Photo(book.transform.position + Vector3.up * 0.45f, book.transform.position, "test_livre_lunettes.png");
            Photo(cam.transform.position, cam.transform.position + cam.transform.forward, "test_vue_rose.png", true);

            flow.TestShop();
            yield return new WaitForSeconds(3f);
            var rt = FindFirstObjectByType<RouletteTable>();
            if (rt == null) { Debug.LogWarning("[Test] pas de roulette"); yield break; }
            var bag = JackpotBag.Active;
            Debug.Log($"[Test] Pièces avant roulette : {(bag != null ? bag.Total : -1)}");
            rt.SendMessage("TestBet");
            Debug.Log($"[Test] Mise : {rt.Bet}, reste {(bag != null ? bag.Total : -1)}");
            rt.Play(RouletteTable.Pocket.Red);
            yield return new WaitForSeconds(2.5f);
            Vector3 c = rt.transform.TransformPoint(new Vector3(0f, 0.86f, -0.2f));
            Photo(rt.transform.TransformPoint(new Vector3(0f, 1.5f, 0.35f)), c, "test_roulette_tourne.png");
            yield return new WaitForSeconds(5f);
            Photo(rt.transform.TransformPoint(new Vector3(0f, 1.5f, 0.35f)), c, "test_roulette_fin.png");
            Photo(rt.transform.TransformPoint(new Vector3(0f, 1.4f, 1.2f)), rt.transform.TransformPoint(new Vector3(0f, 1.2f, -0.3f)), "test_roulette_panneau.png");
            yield return new WaitForSeconds(2f);
            Debug.Log($"[Test] Pièces après roulette : {(bag != null ? bag.Total : -1)}");
            Destroy(gameObject);
        }

        public static void Photo(Vector3 eye, Vector3 target, string file, bool useMain = false)
        {
            Camera cam;
            GameObject go = null;
            if (useMain) cam = Camera.main;
            else
            {
                go = new GameObject("PhotoCam");
                cam = go.AddComponent<Camera>();
                cam.fieldOfView = 60f; cam.nearClipPlane = 0.02f;
                go.transform.position = eye; go.transform.LookAt(target, Vector3.up);
            }
            var rt = new RenderTexture(1024, 640, 24);
            var prev = cam.targetTexture;
            cam.targetTexture = rt; cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1024, 640, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1024, 640), 0, 0); tex.Apply();
            RenderTexture.active = null;
            System.IO.File.WriteAllBytes("Assets/SampleJojo/Preview~/" + file, tex.EncodeToPNG());
            cam.targetTexture = prev;
            Destroy(rt); Destroy(tex); if (go != null) Destroy(go);
        }
    }
}
