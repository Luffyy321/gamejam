using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menu : Tools > Boutique > 11 - Bocal + fente à pièces.
/// Bocal en verre au fond à droite de notre table (nos pièces dedans, comptées sur la calculatrice),
/// fente à pièces sur la machine : sans pièce la machine est éteinte.
/// </summary>
public static class JarSetup
{
    const string JarObj = "Assets/SampleJojo/model/Props/Coin/Bocal/Bocal.obj";
    const string CoinDir = "Assets/SampleJojo/model/Props/Coin/";
    const string RootName = "Bocal à pièces";

    [MenuItem("Tools/Boutique/11 - Bocal + fente à pièces", false, 11)]
    static void Setup()
    {
        if (Application.isPlaying) { Debug.LogWarning("[Bocal] Quitte le mode Play avant."); return; }
        var log = new List<string>();
        BuildJar(log);
        BuildSlot(log);
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        File.WriteAllText("Assets/SampleJojo/Script/Editor/bocal_log.txt", string.Join("\n", log));
        Debug.Log("[Bocal] " + string.Join(" | ", log));
    }

    // ================= Bocal =================
    static void BuildJar(List<string> log)
    {
        var old = GameObject.Find(RootName);
        if (old != null) Undo.DestroyObjectImmediate(old);

        var socket = Object.FindFirstObjectByType<JackpotSocket>();
        var chair = GameObject.Find("Chaise du jeu");
        Vector3 center = socket != null ? socket.transform.position : new Vector3(-6.35f, 2.29f, 3.40f);
        Vector3 eye = chair != null ? chair.transform.position : center - Vector3.right;
        Vector3 fwd = center - eye; fwd.y = 0f; fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.right;
        Vector3 right = Vector3.Cross(Vector3.up, fwd);

        // Dessus de notre table : rayons vers le bas autour de l'emplacement de la machine,
        // puis on ne garde que la surface reliée à l'emplacement (pas les tables voisines).
        var machine = Object.FindFirstObjectByType<JackpotItem>();
        const float step = 0.02f; const int R = 45;
        var grid = new float?[2 * R + 1, 2 * R + 1];
        for (int i = -R; i <= R; i++)
            for (int j = -R; j <= R; j++)
            {
                Vector3 o = new Vector3(center.x + i * step, 3.2f, center.z + j * step);
                foreach (var h in Physics.RaycastAll(o, Vector3.down, 2f).OrderBy(h => h.distance))
                {
                    if (machine != null && h.collider.transform.IsChildOf(machine.transform)) continue;
                    if (h.collider.isTrigger || h.collider.GetComponentInParent<JackpotCoin>() != null) continue;
                    if (h.collider.GetComponentInParent<JackpotSocket>() != null) continue;
                    grid[i + R, j + R] = h.point.y;
                    break;
                }
            }
        // Hauteur de notre table = la plus fréquente près de l'emplacement.
        var nearY = new List<float>();
        for (int i = -6; i <= 6; i++) for (int j = -6; j <= 6; j++) if (grid[i + R, j + R] is float yy && Mathf.Abs(yy - center.y) < 0.25f) nearY.Add(yy);
        float topY = nearY.Count > 0 ? nearY.GroupBy(y => Mathf.Round(y * 100f)).OrderByDescending(g => g.Count()).First().Key / 100f : center.y;
        // Remplissage depuis le point le plus proche de l'emplacement.
        var seen = new bool[2 * R + 1, 2 * R + 1];
        var queue = new Queue<Vector2Int>();
        var top = new List<Vector3>();
        System.Func<int, int, bool> ok = (a, b) => a >= 0 && b >= 0 && a <= 2 * R && b <= 2 * R && grid[a, b] is float y2 && Mathf.Abs(y2 - topY) < 0.015f;
        Vector2Int start = new Vector2Int(R, R);
        if (!ok(R, R))
        {
            float best = float.MaxValue;
            for (int i = 0; i <= 2 * R; i++) for (int j = 0; j <= 2 * R; j++) if (ok(i, j)) { float d = (i - R) * (i - R) + (j - R) * (j - R); if (d < best) { best = d; start = new Vector2Int(i, j); } }
        }
        if (ok(start.x, start.y)) { queue.Enqueue(start); seen[start.x, start.y] = true; }
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            top.Add(new Vector3(center.x + (c.x - R) * step, topY, center.z + (c.y - R) * step));
            foreach (var d in new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down })
            {
                var n = c + d;
                if (ok(n.x, n.y) && !seen[n.x, n.y]) { seen[n.x, n.y] = true; queue.Enqueue(n); }
            }
        }
        log.Add($"table : {top.Count} points à y={topY:F2}" + (top.Count > 0 ? $" x {top.Min(p => p.x):F2}→{top.Max(p => p.x):F2} z {top.Min(p => p.z):F2}→{top.Max(p => p.z):F2}" : ""));

        // Si la table a son propre collider (pas un gros bloc commun), on se limite à ses bornes.
        foreach (var h in Physics.RaycastAll(new Vector3(center.x, 3.2f, center.z), Vector3.down, 2f).OrderBy(h => h.distance))
        {
            if (machine != null && h.collider.transform.IsChildOf(machine.transform)) continue;
            if (h.collider.isTrigger || h.collider.GetComponentInParent<JackpotSocket>() != null) continue;
            var cb = h.collider.bounds;
            log.Add($"collider sous l'emplacement : {h.collider.name} {cb.size:F2}");
            if (cb.size.x < 1.6f && cb.size.z < 1.6f)
                top = top.Where(p => p.x > cb.min.x && p.x < cb.max.x && p.z > cb.min.z && p.z < cb.max.z).ToList();
            break;
        }
        // Le meuble visible : le plus petit objet affiché dont le dessus est à la hauteur de la table, sous l'emplacement.
        var deskR = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
            .Where(r => r.enabled && Mathf.Abs(r.bounds.max.y - topY) < 0.04f
                        && center.x > r.bounds.min.x && center.x < r.bounds.max.x && center.z > r.bounds.min.z && center.z < r.bounds.max.z
                        && r.bounds.size.x * r.bounds.size.z > 0.25f && r.bounds.size.x < 2.5f && r.bounds.size.z < 2.5f
                        && r.bounds.size.y > 0.3f                                  // un vrai meuble (pas les pointillés)
                        && (machine == null || !r.transform.IsChildOf(machine.transform)))
            .OrderBy(r => r.bounds.size.x * r.bounds.size.z).FirstOrDefault();
        if (deskR != null)
        {
            var db = deskR.bounds;
            log.Add($"meuble : {deskR.name} x {db.min.x:F2}→{db.max.x:F2} z {db.min.z:F2}→{db.max.z:F2}");
            var inDesk = top.Where(p => p.x > db.min.x + 0.01f && p.x < db.max.x - 0.01f && p.z > db.min.z + 0.01f && p.z < db.max.z - 0.01f).ToList();
            if (inDesk.Count > 20) top = inDesk;
        }
        // Et pas plus de 75 cm de profondeur depuis le bord côté chaise (sinon on déborde sur la table de devant).
        if (top.Count > 0)
        {
            float near0 = top.Min(p => Vector3.Dot(p, fwd));
            top = top.Where(p => Vector3.Dot(p, fwd) < near0 + 0.75f).ToList();
        }

        // Coin du fond à droite (vu depuis la chaise), puis 12 cm vers l'intérieur.
        Vector3 corner = top.Count > 0 ? top.OrderByDescending(p => Vector3.Dot(p - center, fwd) + Vector3.Dot(p - center, right)).First() : center + fwd * 0.3f + right * 0.3f;
        Vector3 pos = corner - fwd * 0.12f - right * 0.12f;
        pos.y = topY;

        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, RootName);
        root.transform.position = pos;
        root.transform.rotation = Quaternion.LookRotation(-fwd, Vector3.up);

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(JarObj);
        if (model != null)
        {
            var vis = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(vis, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            vis.name = "Verre";
            vis.transform.SetParent(root.transform, false);
            vis.transform.localPosition = Vector3.zero;
            vis.transform.localRotation = Quaternion.identity;
            vis.transform.localScale = Vector3.one;
            foreach (var c in vis.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            var glass = Glass();
            foreach (var r in vis.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterials = Enumerable.Repeat(glass, r.sharedMaterials.Length).ToArray();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }
        else log.Add("modèle du bocal introuvable");

        // Collisions : fond + parois du ventre + épaule + goulot (des boîtes, la vraie forme est creuse).
        var cols = new GameObject("Collisions").transform;
        cols.SetParent(root.transform, false);
        Box(cols, "Fond", new Vector3(0f, 0.01f, 0f), new Vector3(0.17f, 0.02f, 0.17f), Quaternion.identity);
        const int N = 16;
        for (int i = 0; i < N; i++)
        {
            float ang = i * 360f / N;
            var q = Quaternion.Euler(0f, ang, 0f);
            Vector3 dir = q * Vector3.forward;
            Box(cols, "Paroi " + i, dir * 0.098f + Vector3.up * 0.11f, new Vector3(0.042f, 0.2f, 0.01f), q);
            Box(cols, "Épaule " + i, dir * 0.083f + Vector3.up * 0.207f, new Vector3(0.036f, 0.01f, 0.04f), q);
            Box(cols, "Goulot " + i, dir * 0.067f + Vector3.up * 0.235f, new Vector3(0.028f, 0.05f, 0.012f), q);
        }

        // Calque « Bocal » : le verre arrête les pièces mais pas les mains.
        int layer = EnsureLayer("Bocal");
        if (layer > 0) foreach (var c in cols.GetComponentsInChildren<Transform>(true)) c.gameObject.layer = layer;
        log.Add(layer > 0 ? $"parois sur le calque Bocal ({layer})" : "pas de calque libre pour le bocal");

        var jar = root.AddComponent<CoinJar>();
        jar.innerRadius = 0.093f;
        jar.innerBottom = 0.02f;
        jar.innerTop = 0.26f;
        jar.coinPrefabs = new[] { 1, 5, 10, 25, 50, 100 }
            .Select(v => new CoinJar.CoinPrefab { value = v, prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CoinDir + "Piece_" + v + ".prefab") })
            .Where(c => c.prefab != null).ToArray();
        log.Add($"bocal en {pos:F2}, {jar.coinPrefabs.Length} sortes de pièces");
    }

    static int EnsureLayer(string name)
    {
        int existing = LayerMask.NameToLayer(name);
        if (existing >= 0) return existing;
        var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tm.FindProperty("layers");
        for (int i = 8; i < layers.arraySize; i++)
        {
            var sp = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(sp.stringValue)) { sp.stringValue = name; tm.ApplyModifiedProperties(); return i; }
        }
        return -1;
    }

    static void Box(Transform parent, string name, Vector3 localPos, Vector3 size, Quaternion rot)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = rot;
        go.AddComponent<BoxCollider>().size = size;
    }

    static Material Glass()
    {
        string path = "Assets/SampleJojo/model/Props/Coin/Bocal/Bocal_Verre.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.shader = Shader.Find("Universal Render Pipeline/Lit");
        m.SetFloat("_Surface", 1f);               // transparent
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_ZWrite", 0f);
        m.SetFloat("_Cull", 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetColor("_BaseColor", new Color(0.82f, 0.95f, 1f, 0.22f));
        m.SetFloat("_Smoothness", 0.95f);
        m.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(m);
        return m;
    }

    // ================= Fente à pièces =================
    // Position sur la face avant : hauteur (0 = milieu, 1 = bas de la machine), décalage côté (0 = centre, 1 = bord), texte au-dessus (m).
    const float SlotHeight = 0.64f;
    const float SlotSide = 0f;
    const float SlotTextY = 0.012f;

    [MenuItem("Tools/Boutique/26 - Fente à pièces sur la face avant", false, 26)]
    static void SlotOnly()
    {
        var log = new List<string>();
        BuildSlot(log);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        Debug.Log("[Bocal] " + string.Join(" | ", log));
        Photo();
    }

    static void BuildSlot(List<string> log)
    {
        var lever = Object.FindFirstObjectByType<JackpotLever>(FindObjectsInactive.Include);
        var item = Object.FindFirstObjectByType<JackpotItem>(FindObjectsInactive.Include);
        if (lever == null || item == null) { log.Add("machine introuvable"); return; }
        var oldSlot = item.transform.Find("Fente à pièces");
        if (oldSlot != null) Undo.DestroyObjectImmediate(oldSlot.gameObject);

        Undo.RecordObject(lever, "Crédits");
        lever.needsCoins = true;
        lever.credits = 0;
        EditorUtility.SetDirty(lever);

        // Repère de la machine : le corps (BoxCollider) et l'avant du levier.
        var body = item.body != null ? item.body : item.GetComponent<BoxCollider>();
        Transform t = item.transform;
        Vector3 bc = body != null ? body.center : Vector3.zero;
        Vector3 bs = body != null ? body.size : Vector3.one * 0.3f;
        Vector3 frontW = lever.lever != null && lever.lever.parent != null ? lever.lever.parent.TransformDirection(lever.frontLocal) : t.forward;
        Vector3 front = t.InverseTransformDirection(frontW); front.y = 0f;
        // Axe local dominant.
        front = Mathf.Abs(front.x) > Mathf.Abs(front.z) ? new Vector3(Mathf.Sign(front.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(front.z));
        Vector3 side = Vector3.Cross(Vector3.up, front);
        float halfF = Mathf.Abs(Vector3.Dot(bs, front)) * 0.5f, halfS = Mathf.Abs(Vector3.Dot(bs, side)) * 0.5f;
        // Côté opposé au levier.
        Vector3 leverLocal = t.InverseTransformPoint((lever.lever != null ? lever.lever : lever.transform).position) - bc;
        // Le moyeu bleu du levier est sur un flanc : on prend celui d'en face (par défaut -side).
        float dl = Vector3.Dot(leverLocal, side);
        float sideSign = dl < -0.005f ? 1f : -1f;
        // Sur la face avant (côté joueur), en bas, pour toujours voir ses crédits.
        float halfY = bs.y * 0.5f;
        float y = bc.y - halfY * SlotHeight;
        float sx = halfS * SlotSide * sideSign;
        // Surface réelle de la face avant à cet endroit (sommets des meshes de la machine).
        float faceF = halfF;
        float best = float.MinValue;
        foreach (var mf in item.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;
            if (lever.lever != null && mf.transform.IsChildOf(lever.lever)) continue;
            foreach (var v in mf.sharedMesh.vertices)
            {
                Vector3 l = t.InverseTransformPoint(mf.transform.TransformPoint(v)) - bc;
                if (Mathf.Abs(l.y - (y - bc.y)) > 0.03f / Mathf.Max(1e-4f, t.lossyScale.y)) continue;
                if (Mathf.Abs(Vector3.Dot(l, side) - sx) > 0.04f / Mathf.Max(1e-4f, t.lossyScale.x)) continue;
                best = Mathf.Max(best, Vector3.Dot(l, front));
            }
        }
        if (best > float.MinValue && best > halfF * 0.3f) faceF = best;
        Vector3 faceLocal = new Vector3(bc.x, y, bc.z) + front * (faceF + 0.002f / Mathf.Max(1e-4f, t.lossyScale.z)) + side * sx;

        var slot = new GameObject("Fente à pièces");
        Undo.RegisterCreatedObjectUndo(slot, "Fente");
        slot.transform.SetParent(t, false);
        slot.transform.localPosition = faceLocal;
        slot.transform.localRotation = Quaternion.LookRotation(front, Vector3.up);      // +Z = vers le joueur, +Y = vers le haut
        Vector3 ls = t.lossyScale;
        slot.transform.localScale = new Vector3(1f / ls.x, 1f / ls.y, 1f / ls.z);       // 1 unité = 1 m

        // Zone de détection devant la fente.
        var trig = slot.AddComponent<BoxCollider>();
        trig.isTrigger = true;
        trig.center = new Vector3(0f, 0f, 0.04f);
        trig.size = new Vector3(0.18f, 0.1f, 0.1f);

        var frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
        frame.name = "Cadre";
        Object.DestroyImmediate(frame.GetComponent<Collider>());
        frame.transform.SetParent(slot.transform, false);
        frame.transform.localPosition = new Vector3(0f, 0f, 0.004f);
        frame.transform.localScale = new Vector3(0.175f, 0.066f, 0.008f);
        frame.GetComponent<Renderer>().sharedMaterial = Mat("Fente_Cadre", new Color(1f, 0.78f, 0.25f), 0.8f, 1f);
        var hole = GameObject.CreatePrimitive(PrimitiveType.Cube);
        hole.name = "Fente";
        Object.DestroyImmediate(hole.GetComponent<Collider>());
        hole.transform.SetParent(slot.transform, false);
        hole.transform.localPosition = new Vector3(0f, -0.016f, 0.0085f);
        hole.transform.localScale = new Vector3(0.08f, 0.011f, 0.002f);
        hole.GetComponent<Renderer>().sharedMaterial = Mat("Fente_Trou", new Color(0.02f, 0.02f, 0.02f), 0.2f, 0f);
        var point = new GameObject("Fond de la fente").transform;
        point.SetParent(slot.transform, false);
        point.localPosition = new Vector3(0f, -0.016f, -0.03f);
        // Petit écran des crédits au-dessus de la fente.
        var screen = GameObject.CreatePrimitive(PrimitiveType.Cube);
        screen.name = "Écran crédits";
        Object.DestroyImmediate(screen.GetComponent<Collider>());
        screen.transform.SetParent(slot.transform, false);
        screen.transform.localPosition = new Vector3(0f, SlotTextY, 0.0085f);
        screen.transform.localScale = new Vector3(0.163f, 0.03f, 0.002f);
        screen.GetComponent<Renderer>().sharedMaterial = Mat("Fente_Ecran", new Color(0.03f, 0.03f, 0.05f), 0.6f, 0f);

        var tmpGo = new GameObject("Crédits");
        tmpGo.transform.SetParent(slot.transform, false);
        tmpGo.transform.localPosition = new Vector3(0f, SlotTextY, 0.0125f);
        tmpGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);            // lisible depuis l'avant
        var tmp = tmpGo.AddComponent<TextMeshPro>();
        tmp.text = "INSÉREZ UNE PIÈCE";
        tmp.fontSize = 0.2f;
        tmp.enableAutoSizing = true; tmp.fontSizeMin = 0.05f; tmp.fontSizeMax = 0.2f;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = new Color(1f, 0.82f, 0.3f);
        tmp.rectTransform.sizeDelta = new Vector2(0.155f, 0.028f);

        var cs = slot.AddComponent<CoinSlot>();
        cs.lever = lever;
        cs.slotPoint = point;
        cs.creditText = tmp;
        var src = slot.AddComponent<AudioSource>();
        src.playOnAwake = false; src.spatialBlend = 1f;
        cs.audioSource = src;
        cs.insertClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/SampleJojo/Audio/coin_collect.wav");
        cs.powerOnClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/SampleJojo/Audio/jackpot_win.wav");
        log.Add($"fente sur la face avant (face {faceF:F3} / boîte {halfF:F3}, avant local {front}, côté {sideSign} (levier {dl:F3}), demi-largeur {halfS:F3}) à {slot.transform.position:F2}");
    }

    static Material Mat(string name, Color c, float smooth, float metal)
    {
        string path = "Assets/SampleJojo/model/Props/Coin/Bocal/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal);
        EditorUtility.SetDirty(m);
        return m;
    }

    // ================= Tests =================
    [MenuItem("Tools/Boutique/12 - Test : insérer une pièce (en Play)", false, 12)]
    static void TestInsert()
    {
        if (!Application.isPlaying) return;
        var item = Object.FindFirstObjectByType<JackpotItem>();
        var socket = Object.FindFirstObjectByType<JackpotSocket>();
        if (item != null && item.IsStored && JackpotBag.Active != null && socket != null)
        {
            JackpotBag.Active.TestPlaceOn(socket);
            Debug.Log("[Bocal] Machine sortie du sac et posée : relance le test pour insérer une pièce.");
            return;
        }
        var jar = CoinJar.Active;
        var slot = Object.FindFirstObjectByType<CoinSlot>();
        var coin = Object.FindObjectsByType<JackpotCoin>(FindObjectsSortMode.None).FirstOrDefault(c => jar != null && jar.IsInside(c.transform.position));
        if (coin != null && slot != null)
        {
            // On « prend » une pièce du bocal et on la pousse dans la fente.
            var rb = coin.GetComponent<Rigidbody>();
            coin.transform.position = slot.transform.position + slot.transform.forward * 0.06f;
            rb.position = coin.transform.position;
            rb.linearVelocity = -slot.transform.forward * 0.6f;
            Physics.SyncTransforms();
        }
        else if (slot != null && slot.lever != null) slot.lever.AddCredits(1);
    }

    [MenuItem("Tools/Boutique/14 - Test : tirer le levier (en Play)", false, 14)]
    static void TestPull()
    {
        if (!Application.isPlaying) return;
        var lever = Object.FindFirstObjectByType<JackpotLever>();
        if (lever == null) return;
        if (lever.credits <= 0) { Debug.Log("[Bocal] Pas de crédit : insère d'abord une pièce (test 12)."); return; }
        lever.StartSpin();
    }

    [MenuItem("Tools/Boutique/13 - Photo bocal + fente", false, 13)]
    static void Photo()
    {
        var jar = GameObject.Find(RootName);
        var slot = Object.FindFirstObjectByType<CoinSlot>(FindObjectsInactive.Include);
        var chair = GameObject.Find("Chaise du jeu");
        Vector3 eye = (chair != null ? chair.transform.position : Vector3.zero) + Vector3.up * 1.15f;
        if (jar != null) Shoot(eye, jar.transform.position + Vector3.up * 0.1f, "bocal.png", 55f);
        if (jar != null) Shoot(jar.transform.position + new Vector3(0f, 0.55f, 0f) - (jar.transform.position - eye).normalized * 0.35f, jar.transform.position + Vector3.up * 0.08f, "bocal_pres.png", 50f);
        if (slot != null) Shoot(slot.transform.position + slot.transform.forward * 0.55f + Vector3.up * 0.2f, slot.transform.position + Vector3.up * 0.08f, "fente.png", 50f);
        if (slot != null) Shoot(eye, slot.transform.position + Vector3.up * 0.1f, "fente_joueur.png", 55f);
        Debug.Log("[Bocal] photos");
    }

    static void Shoot(Vector3 eye, Vector3 target, string file, float fov)
    {
        var go = new GameObject("PhotoCam");
        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = fov; cam.nearClipPlane = 0.02f;
        go.transform.position = eye; go.transform.LookAt(target, Vector3.up);
        var rt = new RenderTexture(1280, 800, 24);
        cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1280, 800, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); tex.Apply();
        RenderTexture.active = null;
        Directory.CreateDirectory("Assets/SampleJojo/Preview~/");
        File.WriteAllBytes("Assets/SampleJojo/Preview~/" + file, tex.EncodeToPNG());
        cam.targetTexture = null;
        Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); Object.DestroyImmediate(go);
    }
}
