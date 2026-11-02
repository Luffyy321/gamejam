using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Menu : Tools > Boutique > 1 - Couloir + casier trésor.
/// À la fin des 3 minutes : un couloir de casiers ; au bout, le casier trésor (étincelles) avec les objets à acheter,
/// la calculatrice (nos pièces) et le post-it (objectif). Les objets achetés sont sur notre table au niveau suivant.
/// </summary>
public static class ShopSetup
{
    const string Dir = "Assets/SampleJojo/model/Props/boutique/";
    static readonly Vector3 C = new Vector3(40f, -60f, 0f);       // début du couloir (au sol)
    const float Length = 9.5f, Width = 2.6f, Height = 3f;
    const float LockerHeight = 1.9f;

    class ItemDef
    {
        public string id, name, path; public int price; public float size;
        public ItemDef(string id, string name, string path, int price, float size) { this.id = id; this.name = name; this.path = path; this.price = price; this.size = size; }
    }

    static readonly ItemDef[] Items =
    {
        new ItemDef("pilule", "Pilule", Dir + "Pilule/Pilule.fbx", 10, 0.10f),
        new ItemDef("coca", "Coca", Dir + "Coca/Coca.blend", 20, 0.15f),
        new ItemDef("seringue", "Seringue", Dir + "Seringue/syringe.obj", 40, 0.18f),
    };

    [MenuItem("Tools/Boutique/1 - Couloir + casier trésor", false, 1)]
    static void Setup()
    {
        if (Application.isPlaying) { Debug.LogWarning("[Boutique] Quitte le mode Play avant."); return; }
        var lockerFbx = AssetDatabase.LoadAssetAtPath<GameObject>(Dir + "Casier/Casier.fbx");
        if (lockerFbx == null) { Debug.LogError("[Boutique] Casier introuvable."); return; }
        var menu = Object.FindFirstObjectByType<GameMenu>();
        if (menu == null) { Debug.LogError("[Boutique] Menu (GameMenu) introuvable : lance d'abord Jackpot > 31 et 34."); return; }

        var old = GameObject.Find("Couloir boutique");
        if (old != null) Undo.DestroyObjectImmediate(old);
        var root = new GameObject("Couloir boutique");
        Undo.RegisterCreatedObjectUndo(root, "Boutique");
        root.transform.position = C;

        // ---------------- Le couloir ----------------
        var wallMat = SceneMat("Salle - objets du .blend/Murs/Mur fond") ?? Lit("Boutique_Mur", new Color(0.62f, 0.72f, 0.64f), 0.1f);
        var floorMat = SceneMat("Salle - objets du .blend/Sol") ?? Lit("Boutique_Sol", new Color(0.45f, 0.45f, 0.48f), 0.2f);
        var ceilMat = Lit("Boutique_Plafond", new Color(0.92f, 0.92f, 0.9f), 0.05f);
        float zc = Length * 0.5f - 0.5f;
        Slab(root.transform, "Sol", new Vector3(0f, -0.05f, zc), new Vector3(Width, 0.1f, Length), floorMat, true);
        Slab(root.transform, "Plafond", new Vector3(0f, Height + 0.05f, zc), new Vector3(Width, 0.1f, Length), ceilMat, false);
        Slab(root.transform, "Mur gauche", new Vector3(-Width * 0.5f - 0.05f, Height * 0.5f, zc), new Vector3(0.1f, Height, Length), wallMat, true);
        Slab(root.transform, "Mur droit", new Vector3(Width * 0.5f + 0.05f, Height * 0.5f, zc), new Vector3(0.1f, Height, Length), wallMat, true);
        Slab(root.transform, "Mur fond", new Vector3(0f, Height * 0.5f, Length - 0.45f), new Vector3(Width, Height, 0.1f), wallMat, true);
        Slab(root.transform, "Mur départ", new Vector3(0f, Height * 0.5f, -0.55f), new Vector3(Width, Height, 0.1f), wallMat, true);
        for (int i = 0; i < 3; i++)
        {
            var l = new GameObject("Lumière " + (i + 1)).AddComponent<Light>();
            l.transform.SetParent(root.transform, false);
            l.transform.localPosition = new Vector3(0f, Height - 0.3f, 1f + i * 3.2f);
            l.type = LightType.Point; l.range = 5f; l.intensity = 1.6f; l.color = new Color(1f, 0.96f, 0.88f);
            l.shadows = LightShadows.None;
        }

        // Casiers de décoration le long des murs.
        var deco = new GameObject("Casiers décor").transform;
        deco.SetParent(root.transform, false);
        var bodyMat = Lit("Casier_Bleu", new Color(0.24f, 0.43f, 0.74f), 0.45f, 0.4f);
        var doorMat = Lit("Casier_Bleu_Porte", new Color(0.3f, 0.52f, 0.85f), 0.5f, 0.4f);
        var shelfMat = Lit("Casier_Etageres", new Color(0.6f, 0.62f, 0.66f), 0.3f, 0.3f);
        for (int i = 0; i < 10; i++)
        {
            float z = 1.2f + i * 0.56f;
            OpenableDeco(Locker(lockerFbx, deco, "Casier G" + (i + 1), new Vector3(-Width * 0.5f + 0.28f, 0f, z), 0f, bodyMat, doorMat, shelfMat), i);
            OpenableDeco(Locker(lockerFbx, deco, "Casier D" + (i + 1), new Vector3(Width * 0.5f - 0.28f, 0f, z), 180f, bodyMat, doorMat, shelfMat), i + 10);
        }

        // ---------------- Le casier trésor ----------------
        var goldBody = Lit("Casier_Or", new Color(0.95f, 0.72f, 0.2f), 0.45f, 0.5f);
        var goldDoor = Lit("Casier_Or_Porte", new Color(1f, 0.8f, 0.3f), 0.5f, 0.5f);
        var treasure = Locker(lockerFbx, root.transform, "Casier trésor", new Vector3(0f, 0f, Length - 0.8f), 90f, goldBody, goldDoor, shelfMat);
        Object.DestroyImmediate(treasure.GetComponent<BoxCollider>());   // sinon il bloque le rayon vers les objets
        OpenFront(treasure);                                             // on voit l'intérieur quand la porte s'ouvre
        var shop = treasure.AddComponent<LockerShop>();
        Bounds tb = WorldBounds(treasure);

        // Porte sur une charnière : arête arrière de la porte, contre le corps (bord gauche vu du joueur).
        var hinge = MakeHinge(treasure, out var door, out float dir);
        if (hinge != null)
        {
            shop.door = hinge;
            shop.hingeAxis = Vector3.up;
            shop.openAngle = 110f * dir;                     // s'ouvre vers le joueur
            door.gameObject.AddComponent<BoxCollider>();
            shop.doorInteractable = door.gameObject.AddComponent<XRSimpleInteractable>();
        }

        // Les objets, sur trois hauteurs dans le casier.
        var shopItems = new List<ShopItem>();
        var prefabs = new List<LevelFlow.ItemPrefab>();
        float[] heights = { 1.45f, 1.12f, 0.8f };
        var bodyR = treasure.GetComponentsInChildren<MeshRenderer>().FirstOrDefault(r => r.name == "Корпус");
        Vector3 inside = bodyR != null ? bodyR.bounds.center : tb.center;
        Planks(treasure, heights);
        Debug.Log($"[Boutique] casier : bornes {tb.center:F2} / corps {inside:F2}");
        for (int i = 0; i < Items.Length; i++)
        {
            var prefab = ItemPrefab(Items[i]);
            if (prefab == null) continue;
            prefabs.Add(new LevelFlow.ItemPrefab { itemId = Items[i].id, prefab = prefab });
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            inst.name = "À vendre : " + Items[i].name;
            inst.transform.SetParent(treasure.transform, true);
            inst.transform.position = new Vector3(inside.x, C.y + heights[i], inside.z) - Vector3.forward * 0.03f;
            inst.transform.rotation = Quaternion.identity;
            Object.DestroyImmediate(inst.GetComponent<Consumable>());
            Object.DestroyImmediate(inst.GetComponent<XRGrabInteractable>());
            Object.DestroyImmediate(inst.GetComponent<Rigidbody>());
            inst.AddComponent<XRSimpleInteractable>();
            var si = inst.AddComponent<ShopItem>();
            si.itemId = Items[i].id; si.displayName = Items[i].name; si.price = Items[i].price;
            si.priceLabel = Label(inst.transform.parent, "Prix " + Items[i].name, inst.transform.position + new Vector3(0.42f, 0.02f, -0.25f));
            shopItems.Add(si);
        }
        shop.items = shopItems.ToArray();

        // Étincelles dorées « trésor » + lumière dorée.
        shop.treasureFx = Sparkles(root.transform, tb);
        var glow = new GameObject("Lueur trésor").AddComponent<Light>();
        glow.transform.SetParent(root.transform, false);
        glow.transform.position = tb.center + new Vector3(0f, 0.6f, -1.4f);
        glow.type = LightType.Point; glow.range = 3f; glow.intensity = 0.5f; glow.color = new Color(1f, 0.8f, 0.35f);
        glow.shadows = LightShadows.None;
        var sign = Label(root.transform, "Enseigne", tb.center + new Vector3(0f, tb.extents.y + 0.3f, -0.35f), 4f);
        sign.text = "BOUTIQUE";
        sign.gameObject.SetActive(true);

        var src = treasure.AddComponent<AudioSource>();
        src.playOnAwake = false; src.spatialBlend = 1f;
        shop.audioSource = src;
        shop.openClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/SampleJojo/Audio/lever_pull.wav");
        shop.buyClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/SampleJojo/Audio/coin_collect.wav");
        shop.refuseClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/SampleJojo/Audio/reel_stop.wav");

        // ---------------- Calculatrice + post-it sur un socle ----------------
        string calcInfo = CalculatorStand(root.transform, new Vector3(0.85f, 0f, Length - 1.1f));

        // ---------------- Bouton « Niveau suivant » ----------------
        var flow = FindOrAddFlow();
        NextButton(root.transform, new Vector3(-0.85f, 1.25f, Length - 1.1f), flow);

        // ---------------- Départ dans le couloir ----------------
        var spawn = new GameObject("Départ boutique").transform;
        spawn.SetParent(root.transform, false);
        spawn.localPosition = new Vector3(0f, 0f, 0.3f);
        spawn.localRotation = Quaternion.identity;

        // ---------------- Enchaînement ----------------
        Undo.RecordObject(flow, "Niveaux");
        flow.shopSpawn = spawn;
        flow.menu = menu;
        flow.countdown = Object.FindFirstObjectByType<ClassroomCountdown>();
        flow.prof = Object.FindFirstObjectByType<ProfAI>();
        flow.fader = Object.FindFirstObjectByType<GameOverRestart>();
        flow.itemPrefabs = prefabs.ToArray();
        flow.tableSpot = TableSpot();
        EditorUtility.SetDirty(flow);

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log($"[Boutique] Couloir + casier trésor créés ({shopItems.Count} objets). {calcInfo}");
    }

    // ---------------- Casier ----------------
    static GameObject Locker(GameObject fbx, Transform parent, string name, Vector3 localPos, float yaw, Material body, Material door, Material shelves)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
        {
            var m = r.name == "Дверца" ? door : r.name == "Полки" ? shelves : body;
            r.sharedMaterials = Enumerable.Repeat(m, r.sharedMaterials.Length).ToArray();
        }
        var b = WorldBounds(go);
        float s = LockerHeight / Mathf.Max(0.001f, b.size.y);
        go.transform.localScale = Vector3.one * s;
        b = WorldBounds(go);
        Vector3 target = parent.TransformPoint(localPos);
        go.transform.position += new Vector3(target.x - b.center.x, target.y - b.min.y, target.z - b.center.z);
        // Collider du corps seulement (la porte a le sien, pour qu'on puisse cliquer dessus).
        var col = go.AddComponent<BoxCollider>();
        var bodyR = go.GetComponentsInChildren<MeshRenderer>().FirstOrDefault(r => r.name == "Корпус");
        var doorR = go.GetComponentsInChildren<MeshRenderer>().FirstOrDefault(r => r.name == "Дверца");
        if (bodyR != null)
        {
            var lb = LocalBounds(go.transform, bodyR);
            if (doorR != null)
            {
                var ld = LocalBounds(go.transform, doorR);
                float front = Mathf.Min(lb.max.x, ld.min.x - 0.002f / s);
                lb.SetMinMax(lb.min, new Vector3(front, lb.max.y, lb.max.z));
            }
            col.center = lb.center; col.size = lb.size;
        }
        return go;
    }

    /// <summary>Retire la face avant du corps du casier (côté porte, +X du modèle) pour voir dedans.</summary>
    static void OpenFront(GameObject locker)
    {
        var body = locker.GetComponentsInChildren<MeshFilter>().FirstOrDefault(f => f.name == "Корпус");
        if (body == null) return;
        string path = Dir + "Casier_corps_ouvert_v2.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null)
        {
            var src = body.sharedMesh;
            mesh = Object.Instantiate(src);
            mesh.name = "Casier_corps_ouvert";
            var v = src.vertices;
            float maxX = src.bounds.max.x;
            for (int sm = 0; sm < src.subMeshCount; sm++)
            {
                var t = src.GetTriangles(sm);
                var keep = new List<int>();
                for (int i = 0; i < t.Length; i += 3)
                {
                    Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                    // Faces avant (extérieure ET intérieure) : triangle plat en X, dans la moitié avant.
                    bool flatX = Mathf.Abs(a.x - b.x) < 1e-3f && Mathf.Abs(a.x - c.x) < 1e-3f;
                    bool front = flatX && a.x > maxX * 0.3f;
                    if (!front) keep.AddRange(new[] { t[i], t[i + 1], t[i + 2] });
                }
                mesh.SetTriangles(keep, sm);
            }
            AssetDatabase.CreateAsset(mesh, path);
        }
        body.sharedMesh = mesh;
        // Intérieur visible : matériau double face.
        var r = body.GetComponent<MeshRenderer>();
        var m = Object.Instantiate(r.sharedMaterial);
        m.name = r.sharedMaterial.name + "_DoubleFace";
        m.SetFloat("_Cull", 0f);
        m.doubleSidedGI = true;
        string mp = Dir + "Materiaux/" + m.name + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(mp);
        if (existing != null) { existing.CopyPropertiesFromMaterial(m); m = existing; } else AssetDatabase.CreateAsset(m, mp);
        r.sharedMaterials = Enumerable.Repeat(m, r.sharedMaterials.Length).ToArray();

    }

    // ---------------- Objet en vente (prefab réutilisé sur la table) ----------------
    static GameObject ItemPrefab(ItemDef def)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(def.path);
        if (model == null) { Debug.LogWarning("[Boutique] modèle introuvable : " + def.path); return null; }
        var root = new GameObject("Objet " + def.name);
        var vis = (GameObject)PrefabUtility.InstantiatePrefab(model);
        PrefabUtility.UnpackPrefabInstance(vis, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        vis.name = "Visuel";
        vis.transform.SetParent(root.transform, false);
        foreach (var c in vis.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        foreach (var l in vis.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(l.gameObject);
        foreach (var c in vis.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(c.gameObject);
        if (def.id == "coca")
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "Coca/Material.png");
            var m = Lit("Coca_Canette", Color.white, 0.6f, 0.5f);
            if (tex != null) m.SetTexture("_BaseMap", tex);
            foreach (var r in vis.GetComponentsInChildren<Renderer>()) r.sharedMaterials = Enumerable.Repeat(m, r.sharedMaterials.Length).ToArray();
        }
        if (def.id == "seringue")
        {
            // Matériaux de l'OBJ : on les rend lisibles en URP (corps transparent bleuté, produit vert).
            var body = Lit("Seringue_Corps", new Color(0.8f, 0.88f, 0.95f), 0.8f, 0f);
            var drug = Lit("Seringue_Produit", new Color(0.35f, 0.9f, 0.35f), 0.6f, 0f);
            var needle = Lit("Seringue_Aiguille", new Color(0.6f, 0.62f, 0.66f), 0.8f, 1f);
            foreach (var r in vis.GetComponentsInChildren<Renderer>())
                r.sharedMaterials = r.sharedMaterials.Select(x => x == null ? body : x.name.Contains("drug") ? drug : x.name.Contains("needle") ? needle : body).ToArray();
        }
        Bounds b = WorldBounds(vis);
        float s = def.size / Mathf.Max(1e-4f, Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)));
        vis.transform.localScale *= s;
        b = WorldBounds(vis);
        vis.transform.position -= b.center;
        b = WorldBounds(vis);
        var col = root.AddComponent<BoxCollider>();
        col.center = b.center; col.size = b.size;
        var rb = root.AddComponent<Rigidbody>();
        rb.mass = 0.2f; rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        var grab = root.AddComponent<XRGrabInteractable>();
        grab.useDynamicAttach = true;
        grab.throwOnDetach = true;
        // On le porte à la bouche pour le consommer : adrénaline.
        var cons = root.AddComponent<Consumable>();
        cons.itemId = def.id;
        cons.adrenaline = def.id == "pilule" ? 0.25f : def.id == "coca" ? 0.4f : 0.7f;
        string path = Dir + "Objet_" + def.id + ".prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // ---------------- Étincelles ----------------
    static ParticleSystem Sparkles(Transform parent, Bounds around)
    {
        var go = new GameObject("Étincelles trésor");
        go.transform.SetParent(parent, true);
        go.transform.position = around.center;
        go.transform.rotation = Quaternion.identity;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true; main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.12f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.07f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.3f), new Color(1f, 0.97f, 0.7f));
        main.gravityModifier = -0.02f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 300;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var em = ps.emission; em.rateOverTime = 35f;
        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Box;
        sh.scale = new Vector3(around.size.x + 0.35f, around.size.y + 0.2f, around.size.z + 0.35f);
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var sz = ps.sizeOverLifetime; sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0.2f)));
        var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-2f, 2f);
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = SparkleMaterial();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return ps;
    }

    static Material SparkleMaterial()
    {
        string texPath = Dir + "Etincelle.png";
        var imp = AssetImporter.GetAtPath(texPath) as TextureImporter;
        if (imp != null && !imp.alphaIsTransparency) { imp.alphaIsTransparency = true; imp.SaveAndReimport(); }
        string path = Dir + "Etincelle.mat";
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        m.shader = shader;
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
        m.SetColor("_BaseColor", Color.white);
        // Transparent, additif (ça brille).
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 2f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = 3000;
        EditorUtility.SetDirty(m);
        return m;
    }

    // ---------------- Calculatrice + post-it ----------------
    static string CalculatorStand(Transform root, Vector3 localPos)
    {
        var src = GameObject.Find("Calculatrice");
        var stand = Slab(root, "Socle calculatrice", localPos + new Vector3(0f, 0.475f, 0f), new Vector3(0.42f, 0.95f, 0.42f), Lit("Socle_Bois", new Color(0.62f, 0.44f, 0.27f), 0.25f), true);
        if (src == null) return "calculatrice introuvable (menu Jackpot 15 + 28).";
        var copy = Object.Instantiate(src);
        copy.transform.SetParent(root, true);
        copy.name = "Calculatrice (boutique)";
        // L'écran vers le fond, les touches vers le joueur (le haut du texte de l'écran = côté écran).
        var screen = copy.GetComponentsInChildren<TextMeshPro>(true).FirstOrDefault(t => t.GetComponent<CoinCounterDisplay>() != null);
        if (screen != null)
        {
            Vector3 back = screen.transform.up; back.y = 0f;
            if (back.sqrMagnitude > 1e-4f)
            {
                var turn = Quaternion.FromToRotation(back.normalized, Vector3.forward);
                turn = Quaternion.Euler(0f, turn.eulerAngles.y, 0f);
                copy.transform.rotation = turn * copy.transform.rotation;
            }
            screen.GetComponent<CoinCounterDisplay>().bag = Object.FindFirstObjectByType<JackpotBag>();
        }
        var b = WorldBounds(copy, true);
        var sb = stand.GetComponent<Renderer>().bounds;
        copy.transform.position += new Vector3(sb.center.x - b.center.x, sb.max.y - b.min.y + 0.002f, sb.center.z - b.center.z);
        return "Calculatrice + post-it copiés sur le socle.";
    }

    // ---------------- Bouton « Niveau suivant » ----------------
    static void NextButton(Transform root, Vector3 localPos, LevelFlow flow)
    {
        var go = new GameObject("Bouton NIVEAU SUIVANT");
        go.transform.SetParent(root, false);
        go.transform.localPosition = localPos;
        var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "Visuel";
        Object.DestroyImmediate(visual.GetComponent<Collider>());
        visual.transform.SetParent(go.transform, false);
        visual.transform.localScale = new Vector3(0.42f, 0.12f, 0.03f);
        visual.GetComponent<MeshRenderer>().sharedMaterial = Unlit("Bouton_Bleu", new Color(0.17f, 0.42f, 0.86f));
        var t = Text(go.transform, "Texte", "NIVEAU SUIVANT →", new Vector3(0f, 0f, -0.017f), new Vector2(0.38f, 0.09f), Color.white);
        t.transform.SetParent(visual.transform, true);
        var col = go.AddComponent<BoxCollider>();
        col.size = new Vector3(0.42f, 0.12f, 0.05f);
        go.AddComponent<XRSimpleInteractable>();
        var b = go.AddComponent<MenuButton>();
        b.visual = visual.transform;
        if (b.onClick == null) b.onClick = new UnityEngine.Events.UnityEvent();
        b.clickClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/SampleJojo/Audio/page_flip.wav");
        UnityEventTools.AddPersistentListener(b.onClick, flow.NextLevel);
    }

    static LevelFlow FindOrAddFlow()
    {
        var flow = Object.FindFirstObjectByType<LevelFlow>();
        if (flow != null) return flow;
        var partie = GameObject.Find("Partie") ?? new GameObject("Partie");
        return Undo.AddComponent<LevelFlow>(partie);
    }

    /// <summary>Sur notre table, côté joueur, à droite du livre : où poser les objets achetés.</summary>
    static Transform TableSpot()
    {
        var old = GameObject.Find("Objets achetés (table)");
        if (old != null) Object.DestroyImmediate(old);
        var socket = Object.FindFirstObjectByType<JackpotSocket>();
        var start = GameObject.Find("Départ partie (chaise)");
        if (socket == null || start == null) return null;
        Vector3 f = start.transform.forward; f.y = 0f; f.Normalize();
        Vector3 r = Vector3.Cross(Vector3.up, f);
        var spot = new GameObject("Objets achetés (table)").transform;
        Undo.RegisterCreatedObjectUndo(spot.gameObject, "Objets achetés");
        spot.position = socket.transform.position - f * 0.25f + r * 0.2f;
        spot.rotation = Quaternion.LookRotation(f, Vector3.up);
        return spot;
    }

    // ---------------- Outils ----------------
    static TextMeshPro Label(Transform parent, string name, Vector3 worldPos, float scale = 1f)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, true);
        go.transform.position = worldPos;
        go.transform.rotation = Quaternion.identity;               // lisible depuis le couloir (le joueur regarde +Z)
        go.transform.localScale = Vector3.one;
        var lossy = go.transform.lossyScale.x;
        if (lossy > 1e-5f) go.transform.localScale = Vector3.one / lossy;
        var t = go.AddComponent<TextMeshPro>();
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/SampleJojo/Fonts/Cartoon SDF.asset");
        if (font != null) t.font = font;
        t.alignment = TextAlignmentOptions.Center;
        t.fontStyle = FontStyles.Bold;
        t.fontSize = 0.35f * scale;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.rectTransform.sizeDelta = new Vector2(0.5f * scale, 0.15f * scale);
        t.color = new Color(1f, 0.85f, 0.25f);
        t.outlineWidth = 0f;
        go.SetActive(false);
        return t;
    }

    static TextMeshPro Text(Transform parent, string name, string text, Vector3 localPos, Vector2 size, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var t = go.AddComponent<TextMeshPro>();
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/SampleJojo/Fonts/Cartoon SDF.asset");
        if (font != null) t.font = font;
        t.text = text;
        t.color = color;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.enableAutoSizing = true; t.fontSizeMin = 0.05f; t.fontSizeMax = 0.5f;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.rectTransform.sizeDelta = size;
        return t;
    }

    static GameObject Slab(Transform parent, string name, Vector3 localPos, Vector3 size, Material m, bool collider)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = size;
        go.GetComponent<MeshRenderer>().sharedMaterial = m;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static Material SceneMat(string path)
    {
        var go = GameObject.Find(path);
        var r = go != null ? go.GetComponent<Renderer>() : null;
        return r != null ? r.sharedMaterial : null;
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

    static Material Unlit(string name, Color c)
    {
        string path = Dir + "Materiaux/" + name + ".mat";
        if (!AssetDatabase.IsValidFolder(Dir + "Materiaux")) AssetDatabase.CreateFolder(Dir.TrimEnd('/'), "Materiaux");
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        m.shader = shader;
        m.SetColor("_BaseColor", c);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Bounds WorldBounds(GameObject go, bool skipText = false)
    {
        var rs = go.GetComponentsInChildren<Renderer>().Where(r => !(r is ParticleSystemRenderer) && (!skipText || r.GetComponent<TMP_Text>() == null)).ToArray();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    /// <summary>Bornes d'un renderer dans le repère local du casier.</summary>
    static Bounds LocalBounds(Transform root, Renderer r)
    {
        var mf = r.GetComponent<MeshFilter>();
        var mb = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds : new Bounds(r.transform.InverseTransformPoint(r.bounds.center), Vector3.zero);
        Bounds lb = default; bool first = true;
        for (int i = 0; i < 8; i++)
        {
            var c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            var p = root.InverseTransformPoint(r.transform.TransformPoint(c));
            if (first) { lb = new Bounds(p, Vector3.zero); first = false; } else lb.Encapsulate(p);
        }
        return lb;
    }

    /// <summary>
    /// Charnière sur l'arête arrière de la porte (celle qui touche le corps), côté gauche quand on regarde le casier.
    /// La porte tourne autour de cette arête : elle reste collée au casier pendant l'animation.
    /// sign = sens d'ouverture (vers l'extérieur).
    /// </summary>
    static Transform MakeHinge(GameObject locker, out MeshRenderer door, out float sign)
    {
        sign = 1f;
        door = locker.GetComponentsInChildren<MeshRenderer>().FirstOrDefault(r => r.name == "Дверца");
        var body = locker.GetComponentsInChildren<MeshRenderer>().FirstOrDefault(r => r.name == "Корпус");
        if (door == null || body == null) return null;
        var root = locker.transform;
        var ld = LocalBounds(root, door);
        var lbody = LocalBounds(root, body);
        // Dans le modèle, la porte flotte devant le corps : on la recolle contre la face avant.
        float gap = lbody.max.x + 0.004f - ld.min.x;
        if (Mathf.Abs(gap) > 0.001f)
        {
            door.transform.position += root.TransformVector(new Vector3(gap, 0f, 0f));
            ld = LocalBounds(root, door);
        }
        // Avant du casier = +X local (porte devant le corps).
        // Arête arrière de la porte, contre la face avant du corps.
        float back = ld.min.x;
        // Côté gauche vu de face : en regardant le casier depuis +X local, la gauche est -Z local.
        float side = ld.min.z;
        var hinge = new GameObject("Charnière").transform;
        hinge.SetParent(root, false);
        hinge.localPosition = new Vector3(back, ld.center.y, side);
        hinge.localRotation = Quaternion.identity;
        hinge.localScale = Vector3.one;
        door.transform.SetParent(hinge, true);
        // Sens : le centre de la porte doit partir vers l'avant (+X local).
        Vector3 rel = new Vector3(ld.center.x, 0f, ld.center.z) - new Vector3(back, 0f, side);
        Vector3 turned = Quaternion.AngleAxis(90f, Vector3.up) * rel;
        sign = turned.x > rel.x ? 1f : -1f;
        Debug.Log($"[Boutique] {locker.name} : porte {ld.min:F3}→{ld.max:F3}, corps {lbody.min:F3}→{lbody.max:F3}, charnière {hinge.localPosition:F3}, sens {sign}, recollée de {gap:F3}");
        return hinge;
    }

    /// <summary>Remplace le bloc plein des étagères par des planches fines.</summary>
    static void Planks(GameObject locker, float[] heights)
    {
        var bodyR = locker.GetComponentsInChildren<MeshRenderer>().FirstOrDefault(r => r.name == "Корпус");
        var shelvesR = locker.GetComponentsInChildren<MeshRenderer>().FirstOrDefault(r => r.name == "Полки");
        if (shelvesR != null) shelvesR.enabled = false;
        if (bodyR == null) return;
        var bb = bodyR.bounds;
        float innerW = Mathf.Min(bb.size.x, bb.size.z) - 0.04f;
        float floorY = bb.min.y;
        var mat = Lit("Casier_Planche", new Color(0.55f, 0.36f, 0.2f), 0.3f);
        foreach (float h in heights)
        {
            var plank = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plank.name = "Étagère";
            Object.DestroyImmediate(plank.GetComponent<Collider>());
            plank.GetComponent<Renderer>().sharedMaterial = mat;
            plank.transform.SetParent(locker.transform, false);
            plank.transform.position = new Vector3(bb.center.x, floorY + h - 0.075f, bb.center.z);
            plank.transform.rotation = Quaternion.identity;
            var ls = plank.transform.parent.lossyScale;
            plank.transform.localScale = new Vector3(innerW / ls.x, 0.015f / ls.y, innerW / ls.z);
        }
    }

    /// <summary>Casier du couloir : on peut l'ouvrir et le refermer.</summary>
    static void OpenableDeco(GameObject locker, int index)
    {
        OpenFront(locker);
        Planks(locker, new[] { 1.45f, 1.12f, 0.8f, 0.48f });
        var hinge = MakeHinge(locker, out var door, out float sign);
        if (hinge == null) return;
        door.gameObject.AddComponent<BoxCollider>();
        var ld = locker.AddComponent<LockerDoor>();
        ld.hinge = hinge;
        ld.hingeAxis = Vector3.up;
        ld.openAngle = 100f * sign;
        ld.interactable = door.gameObject.AddComponent<XRSimpleInteractable>();
        var src = locker.AddComponent<AudioSource>();
        src.playOnAwake = false; src.spatialBlend = 1f; src.volume = 0.6f;
        ld.audioSource = src;
        ld.openClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/SampleJojo/Audio/lever_pull.wav");
    }

    // ---------------- Adrénaline (vignettage rose) ----------------
    [MenuItem("Tools/Boutique/7 - Effet d'adrénaline", false, 7)]
    static void SetupAdrenaline()
    {
        if (Application.isPlaying) { Debug.LogWarning("[Adrénaline] Quitte le mode Play avant."); return; }
        var host = GameObject.Find("Partie");
        if (host == null) { host = new GameObject("Partie"); Undo.RegisterCreatedObjectUndo(host, "Adrénaline"); }
        foreach (var old in host.GetComponents<AdrenalineBar>()) Undo.DestroyObjectImmediate(old);   // l'ancienne barre
        var fx = host.GetComponent<AdrenalineEffect>() ?? Undo.AddComponent<AdrenalineEffect>(host);
        fx.vignetteShader = Shader.Find("SampleJojo/AdrenalineVignette");
        fx.heartbeatClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/SampleJojo/Audio/heartbeat.wav");
        fx.consumeClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/SampleJojo/Audio/adrenaline_rush.wav");
        EditorUtility.SetDirty(fx);
        EditorSceneManager.MarkSceneDirty(host.scene);
        Debug.Log("[Adrénaline] Effet ajouté sur « Partie » (shader " + (fx.vignetteShader != null ? "OK" : "manquant") +
                  ", sons " + (fx.heartbeatClip != null && fx.consumeClip != null ? "OK" : "manquants") + ").");
    }

    [MenuItem("Tools/Boutique/8 - Test : prendre une pilule (+25 %, en Play)", false, 8)]
    static void TestAdrenaline()
    {
        if (!Application.isPlaying || AdrenalineEffect.Active == null) return;
        var c = Object.FindObjectsByType<Consumable>(FindObjectsSortMode.None).FirstOrDefault(x => x.GetComponent<XRGrabInteractable>() != null && x.enabled);
        if (c != null) c.Consume(); else AdrenalineEffect.Active.Add(0.25f);
    }

    // ---------------- Équilibrage ----------------
    [MenuItem("Tools/Boutique/10 - Équilibrage (gains + prix)", false, 10)]
    static void Balance()
    {
        if (Application.isPlaying) { Debug.LogWarning("[Équilibrage] Quitte le mode Play avant."); return; }
        var lever = Object.FindFirstObjectByType<JackpotLever>();
        if (lever != null)
        {
            Undo.RecordObject(lever, "Équilibrage");
            var t = new Dictionary<string, (int triple, int pair)>
            {
                { "mmi", (200, 10) }, { "claude", (100, 5) }, { "izly", (60, 5) }, { "crous", (40, 5) }, { "couteau", (25, 5) },
            };
            foreach (var r in lever.payTable) if (r != null && t.TryGetValue(r.symbol, out var v)) { r.triple = v.triple; r.pair = v.pair; }
            lever.singleJackpotSymbol = 5;
            EditorUtility.SetDirty(lever);
        }
        // La prof : plus de temps avant qu'elle nous coure après (le « ! » se remplit en 3,5 s au lieu de 1,6 s).
        foreach (var p in Object.FindObjectsByType<ProfAI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Undo.RecordObject(p, "Équilibrage");
            p.fillTime = 3.5f;
            p.drainTime = 2f;
            EditorUtility.SetDirty(p);
        }
        int items = 0;
        foreach (var it in Object.FindObjectsByType<ShopItem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var def = Items.FirstOrDefault(d => d.id == it.itemId);
            if (def == null) continue;
            Undo.RecordObject(it, "Équilibrage");
            it.price = def.price; EditorUtility.SetDirty(it); items++;
        }
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log($"[Équilibrage] gains : {(lever != null ? string.Join(", ", lever.payTable.Select(r => r.symbol + " " + r.triple + "/" + r.pair)) : "machine introuvable")} ; prix mis à jour sur {items} objet(s) ({string.Join(", ", Items.Select(d => d.name + " " + d.price))}).");
    }

    // ---------------- Tests en Play ----------------
    [MenuItem("Tools/Boutique/2 - Test : fin du chrono → boutique (en Play)", false, 2)]
    static void TestShop() { var f = Object.FindFirstObjectByType<LevelFlow>(); if (Application.isPlaying && f != null) f.GoToShop(); }

    [MenuItem("Tools/Boutique/3 - Test : ouvrir le casier (en Play)", false, 3)]
    static void TestOpen() { var s = Object.FindFirstObjectByType<LockerShop>(); if (Application.isPlaying && s != null) s.Open(); }

    [MenuItem("Tools/Boutique/4 - Test : acheter le 1er objet (en Play)", false, 4)]
    static void TestBuy() { var s = Object.FindFirstObjectByType<LockerShop>(); if (Application.isPlaying && s != null && s.items.Length > 0) s.TryBuy(s.items[0]); }

    [MenuItem("Tools/Boutique/5 - Test : niveau suivant (en Play)", false, 5)]
    static void TestNext() { var f = Object.FindFirstObjectByType<LevelFlow>(); if (Application.isPlaying && f != null) f.NextLevel(); }

    [MenuItem("Tools/Boutique/6 - Test : +200 pièces (en Play)", false, 6)]
    static void TestCoins() { var b = JackpotBag.Active; if (Application.isPlaying && b != null) b.SetCoins(b.coinValue + 200); }
}
