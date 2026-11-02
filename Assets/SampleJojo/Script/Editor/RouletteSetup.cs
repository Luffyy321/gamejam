using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Menu Jeu > 3 : table de roulette dans le couloir de la boutique (comme le dessin) :
/// la roue au fond de la table, 3 boutons de couleur (noir, rouge, vert) au milieu, la boîte à mises devant.
/// </summary>
public static class RouletteSetup
{
    const string Dir = "Assets/SampleJojo/model/Props/boutique/";
    const string Model = Dir + "Roulette/rulet_8.blend";
    const string FontPath = "Assets/SampleJojo/Fonts/Cartoon SDF.asset";
    const string Snd = "Assets/SampleJojo/Audio/Sons Jeu VR/";
    // Dans le couloir (repère « Couloir boutique ») : côté droit, juste avant le casier trésor ; la table regarde le centre du couloir.
    static readonly Vector3 Spot = new Vector3(0.76f, 0f, 7.1f);
    const float TopH = 0.86f, TableW = 1.1f, TableD = 1.0f, WheelDiameter = 0.5f;

    [MenuItem("Tools/Jeu/3 - Roulette dans la boutique", false, 3)]
    static void Setup()
    {
        if (Application.isPlaying) { Debug.LogWarning("[Roulette] Quitte le mode Play avant."); return; }
        var corridor = GameObject.Find("Couloir boutique");
        if (corridor == null) { Debug.LogError("[Roulette] Couloir boutique introuvable."); return; }
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
        if (model == null) { Debug.LogError("[Roulette] modèle introuvable : " + Model); return; }
        var old = corridor.transform.Find("Roulette");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        var root = new GameObject("Roulette");
        Undo.RegisterCreatedObjectUndo(root, "Roulette");
        root.transform.SetParent(corridor.transform, false);
        root.transform.localPosition = Spot;
        root.transform.localRotation = Quaternion.LookRotation(Vector3.left, Vector3.up);   // l'avant de la table (+Z) vers le milieu du couloir
        var T = root.transform;

        // ---------------- Table ----------------
        var felt = Lit("Roulette_Tapis", new Color(0.05f, 0.42f, 0.2f), 0.15f);
        var wood = Lit("Roulette_Bois", new Color(0.42f, 0.22f, 0.1f), 0.45f);
        var dark = Lit("Roulette_Bois_Fonce", new Color(0.22f, 0.11f, 0.05f), 0.4f);
        Box(T, "Plateau", new Vector3(0f, TopH - 0.03f, 0f), new Vector3(TableW, 0.06f, TableD), wood, true);
        Box(T, "Tapis", new Vector3(0f, TopH + 0.0015f, 0f), new Vector3(TableW - 0.08f, 0.003f, TableD - 0.08f), felt, false);
        foreach (var sx in new[] { -1f, 1f }) foreach (var sz in new[] { -1f, 1f })
            Box(T, "Pied", new Vector3(sx * (TableW * 0.5f - 0.07f), (TopH - 0.06f) * 0.5f, sz * (TableD * 0.5f - 0.07f)), new Vector3(0.07f, TopH - 0.06f, 0.07f), dark, false);
        Box(T, "Jupe", new Vector3(0f, TopH - 0.12f, 0f), new Vector3(TableW - 0.1f, 0.12f, TableD - 0.1f), dark, false);

        // ---------------- Roue (modèle) ----------------
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
        PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        inst.name = "Roue";
        inst.transform.SetParent(T, false);
        foreach (var c in inst.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(c.gameObject);
        foreach (var l in inst.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(l.gameObject);
        var bowl = inst.transform.Find("Plane");
        var wheel = inst.transform.Find("Plane.001");
        var ball = inst.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Sphere");
        if (bowl == null || wheel == null || ball == null) { Debug.LogError("[Roulette] pièces du modèle introuvables (Plane, Plane.001, Sphere)."); return; }
        Paint(inst);
        // Taille : la cuvette fait WheelDiameter de large ; posée au fond de la table.
        var bb = WB(bowl.gameObject);
        inst.transform.localScale *= WheelDiameter / Mathf.Max(bb.size.x, bb.size.z);
        bb = WB(inst, ball.gameObject);
        Vector3 want = T.TransformPoint(new Vector3(0f, TopH + 0.003f, -TableD * 0.5f + WheelDiameter * 0.5f + 0.05f));
        inst.transform.position += new Vector3(want.x - bb.center.x, want.y - bb.min.y, want.z - bb.center.z);

        // ---------------- Cases : angle et couleur ----------------
        var rt = root.AddComponent<RouletteTable>();
        rt.wheel = wheel; rt.bowl = bowl; rt.ball = ball;
        string pocketInfo = Pockets(rt, wheel, bowl);
        BallTrack(rt, wheel, bowl, ball);

        // ---------------- Boutons de couleur ----------------
        rt.blackButton = ColorButton(T, "Bouton NOIR", new Vector3(0.3f, 0f, 0.16f), new Color(0.08f, 0.08f, 0.09f), new Color(0.05f, 0.05f, 0.08f), "NOIR\nx2", Color.white);
        rt.redButton = ColorButton(T, "Bouton ROUGE", new Vector3(0f, 0f, 0.16f), new Color(0.85f, 0.08f, 0.08f), new Color(0.5f, 0.02f, 0.02f), "ROUGE\nx2", Color.white);
        rt.greenButton = ColorButton(T, "Bouton VERT", new Vector3(-0.3f, 0f, 0.16f), new Color(0.1f, 0.75f, 0.25f), new Color(0.03f, 0.4f, 0.1f), "VERT\nx100", Color.white);

        // ---------------- Boîte à mises ----------------
        BetBox(rt, T);

        // ---------------- Panneau de résultat ----------------
        var board = Box(T, "Panneau", new Vector3(0f, TopH + 0.62f, -TableD * 0.5f + 0.02f), new Vector3(0.62f, 0.26f, 0.02f), dark, false);
        Box(T, "Montant panneau", new Vector3(0f, TopH + 0.25f, -TableD * 0.5f + 0.02f), new Vector3(0.04f, 0.5f, 0.02f), dark, false);
        var title = Txt(T, "Titre", "ROULETTE", new Vector3(0f, TopH + 0.71f, -TableD * 0.5f + 0.034f), new Vector2(0.55f, 0.06f), 0.6f, new Color(1f, 0.82f, 0.3f));
        title.fontStyle = FontStyles.Bold;
        rt.resultText = Txt(T, "Résultat", "Fais tes jeux !", new Vector3(0f, TopH + 0.6f, -TableD * 0.5f + 0.034f), new Vector2(0.56f, 0.15f), 0.5f, new Color(1f, 0.85f, 0.35f));

        // ---------------- Sons, lumière ----------------
        var src = root.AddComponent<AudioSource>();
        src.playOnAwake = false; src.spatialBlend = 1f; src.minDistance = 1f;
        rt.audioSource = src;
        rt.betClip = AssetDatabase.LoadAssetAtPath<AudioClip>(Snd + "coin dans machine.mp3");
        rt.spinClip = AssetDatabase.LoadAssetAtPath<AudioClip>(Snd + "levier.mp3");
        rt.ballClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/SampleJojo/Audio/coin_clink.wav");
        rt.winClip = AssetDatabase.LoadAssetAtPath<AudioClip>(Snd + "jackpot.mp3");
        rt.loseClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/SampleJojo/Audio/reel_stop.wav");
        rt.refuseClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/SampleJojo/Audio/reel_stop.wav");
        var light = new GameObject("Lumière roulette").AddComponent<Light>();
        light.transform.SetParent(T, false);
        light.transform.localPosition = new Vector3(0f, TopH + 1.1f, 0f);
        light.type = LightType.Point; light.range = 2.2f; light.intensity = 1.2f; light.color = new Color(1f, 0.92f, 0.75f);
        light.shadows = LightShadows.None;

        foreach (var t in root.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
        EditorUtility.SetDirty(rt);
        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log($"[Roulette] Table posée dans la boutique. {pocketInfo} Piste r={rt.trackRadius:F1} z={rt.trackZ:F1}, case r={rt.pocketRadius:F1} z={rt.pocketZ:F1}.");
        Photo();
    }

    // ---------------- Couleurs du modèle (les matériaux du .blend arrivent tout gris) ----------------
    static void Paint(GameObject inst)
    {
        var red = Lit("Roulette_Rouge", new Color(0.78f, 0.05f, 0.05f), 0.6f);
        var black = Lit("Roulette_Noir", new Color(0.04f, 0.04f, 0.05f), 0.6f);
        var green = Lit("Roulette_Vert", new Color(0.05f, 0.55f, 0.18f), 0.6f);
        var gold = Lit("Roulette_Or", new Color(0.9f, 0.7f, 0.3f), 0.75f, 0.9f);
        var silver = Lit("Roulette_Argent", new Color(0.8f, 0.8f, 0.82f), 0.8f, 0.9f);
        var wood = Lit("Roulette_Bois_Roue", new Color(0.45f, 0.24f, 0.1f), 0.6f);
        var wood2 = Lit("Roulette_Bois_Clair", new Color(0.62f, 0.4f, 0.2f), 0.6f);
        var ivory = Lit("Roulette_Bille", new Color(0.97f, 0.96f, 0.93f), 0.9f);
        var white = Lit("Roulette_Chiffres", new Color(0.95f, 0.95f, 0.9f), 0.5f);
        foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials.ToArray();
            for (int i = 0; i < mats.Length; i++)
            {
                string n = mats[i] != null ? mats[i].name.ToLower() : "";
                Material m;
                if (r.name == "Sphere") m = ivory;
                else if (r.name.StartsWith("Text")) m = white;
                else if (n == "red") m = red;
                else if (n == "black" || n.StartsWith("material.0")) m = black;
                else if (n == "green") m = green;
                else if (n.Contains("metal032")) m = gold;
                else if (n.Contains("metal033")) m = silver;
                else if (n == "wood2") m = wood2;
                else if (n.Contains("wood")) m = wood;
                else if (n.Contains("fabric")) m = wood;
                else if (n == "ball1") m = gold;
                else m = black;
                mats[i] = m;
            }
            r.sharedMaterials = mats;
        }
    }

    // ---------------- Cases ----------------
    static string Pockets(RouletteTable rt, Transform wheel, Transform bowl)
    {
        var mf = wheel.GetComponent<MeshFilter>(); var r = wheel.GetComponent<Renderer>();
        var mesh = mf.sharedMesh; var v = mesh.vertices;
        var all = new List<(float a, RouletteTable.Pocket c, float rad, float z)>();
        for (int s = 0; s < mesh.subMeshCount && s < r.sharedMaterials.Length; s++)
        {
            string n = ColorName(mesh, s, wheel);       // nom du matériau d'origine (red / black / green)
            RouletteTable.Pocket col;
            if (n == "red") col = RouletteTable.Pocket.Red;
            else if (n == "black") col = RouletteTable.Pocket.Black;
            else if (n == "green") col = RouletteTable.Pocket.Green;
            else continue;
            var tris = mesh.GetTriangles(s);
            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector3 c = (v[tris[i]] + v[tris[i + 1]] + v[tris[i + 2]]) / 3f;
                Vector3 p = bowl.InverseTransformPoint(wheel.TransformPoint(c));
                all.Add((Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg, col, new Vector2(p.x, p.y).magnitude, p.z));
            }
        }
        // 37 cases régulières (0 vert + 18 rouges + 18 noires) : on part de la case verte et on avance de 360/37°.
        var angles = new List<float>(); var colors = new List<RouletteTable.Pocket>();
        float radSum = 0f, zSum = 0f; int nTri = all.Count;
        foreach (var x in all) { radSum += x.rad; zSum += x.z; }
        var greens = all.Where(x => x.c == RouletteTable.Pocket.Green).Select(x => x.a).ToList();
        float a0 = greens.Count > 0 ? CircMean(greens) : 0f, step = 360f / 37f;
        var votes = new int[37, 3];
        foreach (var x in all)
        {
            int k = Mathf.RoundToInt(Mathf.Repeat(x.a - a0, 360f) / step) % 37;
            votes[k, (int)x.c]++;
        }
        for (int k = 0; k < 37; k++)
        {
            int best = 0; for (int c = 1; c < 3; c++) if (votes[k, c] > votes[k, best]) best = c;
            if (votes[k, 0] + votes[k, 1] + votes[k, 2] == 0) continue;
            angles.Add(a0 + k * step); colors.Add((RouletteTable.Pocket)best);
        }
        rt.pocketAngles = angles.ToArray();
        rt.pocketColors = colors.ToArray();
        rt.pocketRadius = nTri > 0 ? radSum / nTri : 12f;
        rt.pocketZ = nTri > 0 ? zSum / nTri : 0f;
        return $"Cases : {colors.Count(c => c == RouletteTable.Pocket.Red)} rouges, {colors.Count(c => c == RouletteTable.Pocket.Black)} noires, {colors.Count(c => c == RouletteTable.Pocket.Green)} verte(s).";
    }

    // Nom du matériau d'origine du sous-maillage (dans le modèle, avant de le repeindre).
    static string ColorName(Mesh mesh, int s, Transform wheel)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
        var src = model.GetComponentsInChildren<Renderer>(true).FirstOrDefault(x => x.name == wheel.name);
        if (src == null || s >= src.sharedMaterials.Length || src.sharedMaterials[s] == null) return "";
        return src.sharedMaterials[s].name.ToLower();
    }

    static float CircMean(List<float> a)
    {
        float x = 0f, y = 0f;
        foreach (var d in a) { x += Mathf.Cos(d * Mathf.Deg2Rad); y += Mathf.Sin(d * Mathf.Deg2Rad); }
        return Mathf.Atan2(y, x) * Mathf.Rad2Deg;
    }

    // ---------------- Piste de la bille ----------------
    static void BallTrack(RouletteTable rt, Transform wheel, Transform bowl, Transform ball)
    {
        // Rayon de la bille dans le repère de la cuvette.
        float ballR = WB(ball.gameObject).extents.y / Mathf.Max(1e-5f, bowl.lossyScale.y);
        // On mesure la surface de la cuvette avec des colliders provisoires.
        var temp = new List<MeshCollider>();
        foreach (var t in new[] { bowl, wheel }) { var mc = t.gameObject.AddComponent<MeshCollider>(); temp.Add(mc); }
        Physics.SyncTransforms();
        float Surface(float r, float angle)
        {
            float a = angle * Mathf.Deg2Rad;
            Vector3 top = bowl.TransformPoint(new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 40f));
            Vector3 dir = -bowl.TransformDirection(Vector3.forward).normalized;
            float best = float.NaN, bestD = float.MaxValue;
            foreach (var h in Physics.RaycastAll(top, dir, 5f))
                if (temp.Contains(h.collider as MeshCollider) && h.distance < bestD) { bestD = h.distance; best = bowl.InverseTransformPoint(h.point).z; }
            return best;
        }
        // Rayon de la piste : là où était la bille dans le modèle (sinon un peu au-delà des cases).
        Vector3 b0 = bowl.InverseTransformPoint(ball.position);
        float trackR = new Vector2(b0.x, b0.y).magnitude;
        if (trackR < rt.pocketRadius * 1.1f) trackR = rt.pocketRadius * 1.45f;
        float zt = Surface(trackR, 30f), zp = Surface(rt.pocketRadius, rt.pocketAngles.Length > 0 ? rt.pocketAngles[0] : 0f);
        foreach (var mc in temp) Object.DestroyImmediate(mc);
        rt.trackRadius = trackR;
        rt.trackZ = (float.IsNaN(zt) ? rt.pocketZ + 3f : zt) + ballR;
        rt.pocketZ = (float.IsNaN(zp) ? rt.pocketZ : zp) + ballR;
        ball.localScale = ball.localScale;     // la bille garde sa taille
    }

    // ---------------- Boutons ----------------
    static MenuButton ColorButton(Transform T, string name, Vector3 local, Color col, Color emis, string label, Color textCol)
    {
        var go = new GameObject(name);
        go.transform.SetParent(T, false);
        go.transform.localPosition = local + Vector3.up * (TopH + 0.003f);
        // Avant du bouton vers le bas : quand on le vise, il « monte » (MenuButton le fait avancer vers -Z = le haut).
        go.transform.localRotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
        var m = Lit("Roulette_Bouton_" + name.Split(' ').Last(), col, 0.75f);
        m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emis); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        var gold = Lit("Roulette_Or", new Color(0.9f, 0.7f, 0.3f), 0.75f, 0.9f);
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.DestroyImmediate(ring.GetComponent<Collider>());
        ring.name = "Bague"; ring.transform.SetParent(go.transform, false);
        ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        ring.transform.localScale = new Vector3(0.17f, 0.006f, 0.17f);
        ring.transform.localPosition = new Vector3(0f, 0f, -0.006f);
        ring.GetComponent<Renderer>().sharedMaterial = gold;
        var cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.DestroyImmediate(cap.GetComponent<Collider>());
        cap.name = "Bouton"; cap.transform.SetParent(go.transform, false);
        cap.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        cap.transform.localScale = new Vector3(0.145f, 0.012f, 0.145f);
        cap.transform.localPosition = new Vector3(0f, 0f, -0.016f);
        cap.GetComponent<Renderer>().sharedMaterial = m;
        var col2 = go.AddComponent<BoxCollider>();
        col2.center = new Vector3(0f, 0f, -0.02f); col2.size = new Vector3(0.18f, 0.18f, 0.06f);
        go.AddComponent<XRSimpleInteractable>();
        var mb = go.AddComponent<MenuButton>();
        mb.visual = cap.transform; mb.hoverScale = 1.08f; mb.hoverLift = 0.008f;
        mb.clickClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/SampleJojo/Audio/pop.wav");
        // Texte posé à plat sur le bouton, lisible depuis le joueur.
        var t = Txt(T, "Texte " + name, label, local + Vector3.up * (TopH + 0.032f), new Vector2(0.14f, 0.1f), 0.3f, textCol);
        t.transform.localRotation = Quaternion.Euler(90f, 180f, 0f);
        t.fontStyle = FontStyles.Bold;
        t.outlineWidth = 0.25f; t.outlineColor = new Color32(0, 0, 0, 255);
        t.transform.SetParent(cap.transform, true);
        return mb;
    }

    // ---------------- Boîte à mises ----------------
    static void BetBox(RouletteTable rt, Transform T)
    {
        var wood = Lit("Roulette_Bois_Clair", new Color(0.62f, 0.4f, 0.2f), 0.6f);
        var inside = Lit("Roulette_Tapis_Fonce", new Color(0.03f, 0.25f, 0.12f), 0.1f);
        var box = new GameObject("Boîte à mises");
        box.transform.SetParent(T, false);
        box.transform.localPosition = new Vector3(0.12f, TopH + 0.003f, 0.37f);
        const float W = 0.24f, D = 0.13f, H = 0.05f, e = 0.008f;
        Box(box.transform, "Fond", new Vector3(0f, e * 0.5f, 0f), new Vector3(W, e, D), inside, false);
        Box(box.transform, "Bord avant", new Vector3(0f, H * 0.5f, D * 0.5f - e * 0.5f), new Vector3(W, H, e), wood, false);
        Box(box.transform, "Bord arrière", new Vector3(0f, H * 0.5f, -D * 0.5f + e * 0.5f), new Vector3(W, H, e), wood, false);
        Box(box.transform, "Bord gauche", new Vector3(-W * 0.5f + e * 0.5f, H * 0.5f, 0f), new Vector3(e, H, D), wood, false);
        Box(box.transform, "Bord droit", new Vector3(W * 0.5f - e * 0.5f, H * 0.5f, 0f), new Vector3(e, H, D), wood, false);
        var col = box.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, H * 0.5f + 0.02f, 0f); col.size = new Vector3(W + 0.02f, H + 0.06f, D + 0.02f);
        rt.betBox = box.AddComponent<XRSimpleInteractable>();
        var spot = new GameObject("Pièces misées").transform;
        spot.SetParent(box.transform, false);
        spot.localPosition = new Vector3(0f, e + 0.002f, 0f);
        rt.betSpot = spot;
        rt.coinVisual = CoinVisual(spot);
        var lbl = Txt(box.transform, "Étiquette", "MISES", new Vector3(0f, H * 0.5f, D * 0.5f + 0.0015f), new Vector2(W * 0.9f, H * 0.8f), 0.3f, new Color(1f, 0.85f, 0.35f));
        lbl.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        lbl.fontStyle = FontStyles.Bold;
        // Afficheur de la mise, debout à droite de la boîte.
        var sign = Box(T, "Afficheur mise", new Vector3(-0.2f, TopH + 0.06f, 0.37f), new Vector3(0.2f, 0.1f, 0.012f), Lit("Roulette_Bois_Fonce", new Color(0.22f, 0.11f, 0.05f), 0.4f), false);
        sign.transform.localRotation = Quaternion.Euler(-15f, 0f, 0f);
        rt.betText = Txt(sign.transform, "Mise", "MISE : 0", Vector3.zero, new Vector2(0.18f, 0.085f), 0.3f, new Color(1f, 0.85f, 0.35f));
        rt.betText.transform.localPosition = new Vector3(0f, 0f, 0.6f);     // devant la face avant du cube
        rt.betText.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        // Le texte est enfant d'un cube mis à l'échelle : on compense.
        var ls = sign.transform.localScale;
        rt.betText.transform.localScale = new Vector3(1f / ls.x, 1f / ls.y, 1f / ls.z);
    }

    static GameObject CoinVisual(Transform parent)
    {
        var jar = Object.FindFirstObjectByType<CoinJar>(FindObjectsInactive.Include);
        var def = jar != null ? jar.coinPrefabs.Where(c => c != null && c.prefab != null).OrderBy(c => c.value).FirstOrDefault() : null;
        if (def == null) return null;
        var coin = (GameObject)PrefabUtility.InstantiatePrefab(def.prefab);
        PrefabUtility.UnpackPrefabInstance(coin, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        coin.name = "Pièce misée";
        foreach (var comp in coin.GetComponentsInChildren<Component>(true).Reverse())
            if (!(comp is Transform) && !(comp is MeshFilter) && !(comp is MeshRenderer)) Object.DestroyImmediate(comp);
        coin.transform.SetParent(parent, false);
        // Couchée à plat : on garde la rotation où elle est la plus fine en hauteur.
        Quaternion best = Quaternion.identity; float bestH = float.MaxValue;
        foreach (var q in new[] { Quaternion.identity, Quaternion.Euler(90f, 0f, 0f), Quaternion.Euler(0f, 0f, 90f) })
        {
            coin.transform.localRotation = q;
            var b = WB(coin);
            if (b.size.y < bestH) { bestH = b.size.y; best = q; }
        }
        coin.transform.localRotation = best;
        var bb = WB(coin);
        float d = Mathf.Max(bb.size.x, bb.size.z);
        if (d > 1e-5f) coin.transform.localScale *= 0.03f / d;
        bb = WB(coin);
        coin.transform.position += new Vector3(parent.position.x - bb.center.x, parent.position.y - bb.min.y, parent.position.z - bb.center.z);
        return coin;
    }

    // ---------------- Outils ----------------
    static GameObject Box(Transform parent, string name, Vector3 local, Vector3 size, Material m, bool collider)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = local;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = m;
        return go;
    }

    static TextMeshPro Txt(Transform parent, string name, string text, Vector3 local, Vector2 size, float fontSize, Color c)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = local;
        go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);      // lisible depuis l'avant de la table (+Z)
        var t = go.AddComponent<TextMeshPro>();
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font != null) t.font = font;
        t.text = text; t.color = c;
        t.alignment = TextAlignmentOptions.Center;
        t.enableAutoSizing = true; t.fontSizeMin = fontSize * 0.3f; t.fontSizeMax = fontSize;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.rectTransform.sizeDelta = size;
        return t;
    }

    static Material Lit(string name, Color c, float smooth, float metal = 0f)
    {
        string path = Dir + "Materiaux/" + name + ".mat";
        if (!AssetDatabase.IsValidFolder(Dir + "Materiaux")) AssetDatabase.CreateFolder(Dir.TrimEnd('/'), "Materiaux");
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        m.shader = shader;
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", smooth);
        m.SetFloat("_Metallic", metal);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Bounds WB(GameObject go, GameObject skip = null)
    {
        var rs = go.GetComponentsInChildren<Renderer>().Where(r => !(r is ParticleSystemRenderer) && r.GetComponent<TMP_Text>() == null && (skip == null || !r.transform.IsChildOf(skip.transform))).ToArray();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    // ---------------- Photos ----------------
    [MenuItem("Tools/Jeu/5 - Photos roulette + casier", false, 5)]
    internal static void Photo()
    {
        var corridor = GameObject.Find("Couloir boutique");
        var r = corridor != null ? corridor.transform.Find("Roulette") : null;
        if (r != null)
        {
            Vector3 c = r.TransformPoint(new Vector3(0f, TopH, 0f));
            Shoot(r.TransformPoint(new Vector3(0f, TopH + 0.75f, 1.05f)), c, "roulette_face.png", 60f);
            Shoot(r.TransformPoint(new Vector3(0f, TopH + 0.9f, 0.1f)), r.TransformPoint(new Vector3(0f, TopH, -0.15f)), "roulette_dessus.png", 60f);
            Shoot(corridor.transform.TransformPoint(new Vector3(-0.3f, 1.6f, 4.2f)), c, "roulette_couloir.png", 70f);
        }
        ShopShelfSetup.Photo();
        Debug.Log("[Roulette] photos");
    }

    static void Shoot(Vector3 eye, Vector3 target, string file, float fov)
    {
        var go = new GameObject("PhotoCam");
        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = fov; cam.nearClipPlane = 0.02f;
        go.transform.position = eye; go.transform.LookAt(target, Vector3.up);
        var rtx = new RenderTexture(1280, 800, 24);
        cam.targetTexture = rtx; cam.Render();
        RenderTexture.active = rtx;
        var tex = new Texture2D(1280, 800, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); tex.Apply();
        RenderTexture.active = null;
        System.IO.Directory.CreateDirectory("Assets/SampleJojo/Preview~/");
        System.IO.File.WriteAllBytes("Assets/SampleJojo/Preview~/" + file, tex.EncodeToPNG());
        cam.targetTexture = null;
        Object.DestroyImmediate(rtx); Object.DestroyImmediate(tex); Object.DestroyImmediate(go);
    }
}
