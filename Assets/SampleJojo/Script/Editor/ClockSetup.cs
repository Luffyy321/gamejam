using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Deux horloges digitales dans la classe (mur du tableau + mur du fond) avec un compte à rebours 3 min.
/// Les chiffres utilisent les vrais segments du modèle (allumés / éteints), pas une police.
/// Menu : Tools > Jackpot > 24 - Horloges (compte à rebours 3 min).
/// </summary>
public static class ClockSetup
{
    const string ClockBlend = "Assets/SampleJojo/model/Props/clasroom/Digital_Clock.blend";
    const string MeshAsset = "Assets/SampleJojo/model/Props/clasroom/Horloge_segments.asset";
    const string RootName = "Salle - objets du .blend";
    const float ClockWidth = 0.6f;

    // Dans le mesh, +X est à GAUCHE quand on regarde l'écran : le 1er chiffre est en x = +4.8.
    static readonly float[] DigitX = { 4.8f, 3.1f, 0.85f, -0.85f, -3.1f, -4.8f };
    static readonly byte[] Glyphs = { 0x3F, 0x06, 0x5B, 0x4F, 0x66, 0x6D, 0x7D, 0x07, 0x7F, 0x6F };

    [MenuItem("Tools/Jackpot/24 - Horloges (compte à rebours 3 min)", false, 135)]
    static void Setup()
    {
        if (Application.isPlaying) { Debug.LogWarning("[Horloge] Quitte le mode Play avant."); return; }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ClockBlend);
        if (prefab == null) { Debug.LogError("[Horloge] introuvable : " + ClockBlend); return; }
        if (!BuildMeshes(prefab, out var housing, out var segs, out var segStart, out var mats)) return;

        var root = GameObject.Find(RootName);
        if (root == null) { root = new GameObject(RootName); Undo.RegisterCreatedObjectUndo(root, RootName); }
        var old = root.transform.Find("Horloges");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        var holder = new GameObject("Horloges");
        Undo.RegisterCreatedObjectUndo(holder, "Horloges");
        holder.transform.SetParent(root.transform, false);

        // Mur du tableau (x = 1.955, face vers -X), près de la fenêtre ; mur du fond (x = -7.435, face vers +X).
        var a = MakeClock(prefab, holder.transform, "Horloge (tableau)", new Vector3(1.955f, 3.05f, 7.45f), Vector3.left, housing, segs, segStart, mats);
        var b = MakeClock(prefab, holder.transform, "Horloge (fond)", new Vector3(-7.435f, 3.05f, 3.6f), Vector3.right, housing, segs, segStart, mats);

        var cd = holder.AddComponent<ClassroomCountdown>();
        cd.durationSeconds = 180f;
        cd.clocks = new[] { a, b }.Where(t => t != null).ToArray();
        cd.displays = new TMPro.TMP_Text[0];

        EditorSceneManager.MarkSceneDirty(holder.scene);
        Debug.Log($"[Horloge] {cd.clocks.Length} horloges posées (segments du modèle), compte à rebours de 3 min.");
    }

    /// <summary>Découpe le modèle : boîtier seul + un mesh de 46 segments rangés (6 chiffres × 7 + 4 points).</summary>
    static bool BuildMeshes(GameObject prefab, out Mesh housing, out Mesh segs, out int[] segStart, out Material[] mats)
    {
        housing = segs = null; segStart = null; mats = null;
        var mf = prefab.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f => f.name == "Digital.Clock");
        if (mf == null) { Debug.LogError("[Horloge] objet « Digital.Clock » absent."); return false; }
        var src = mf.sharedMesh;
        var srcMats = mf.GetComponent<MeshRenderer>().sharedMaterials;
        int onIdx = System.Array.FindIndex(srcMats, m => m != null && m.name.StartsWith("Digit.On"));
        int offIdx = System.Array.FindIndex(srcMats, m => m != null && m.name.StartsWith("Digit.Off"));
        int zeroIdx = System.Array.FindIndex(srcMats, m => m != null && m.name.StartsWith("Digit.Zero"));
        if (onIdx < 0 || offIdx < 0 || zeroIdx < 0) { Debug.LogError("[Horloge] matériaux Digit.On/Off/Zero introuvables."); return false; }
        mats = new[] { srcMats[zeroIdx], srcMats[onIdx], srcMats[offIdx] };

        var v = src.vertices;
        // Tous les segments (allumés + éteints dans le modèle) → classement chiffre / segment.
        var slots = new List<int>[SevenSegmentDisplay.SegCount];
        int dotN = 0;
        foreach (int sub in new[] { onIdx, offIdx })
        {
            var idx = src.GetIndices(sub);
            foreach (var comp in ClockSegScan.ComponentTris(idx, v))
            {
                var bb = new Bounds(v[idx[comp[0] * 3]], Vector3.zero);
                var tris = new List<int>();
                foreach (var t in comp) for (int k = 0; k < 3; k++) { int vi = idx[t * 3 + k]; tris.Add(vi); bb.Encapsulate(v[vi]); }
                int slot = Classify(bb, ref dotN);
                if (slot < 0 || slots[slot] != null) { Debug.LogWarning($"[Horloge] segment non classé : {bb.center:F2} {bb.size:F2}"); continue; }
                slots[slot] = tris;
            }
        }
        int missing = slots.Count(s => s == null);
        if (missing > 0) Debug.LogWarning($"[Horloge] {missing} segment(s) manquant(s).");

        // Mesh des segments, sommets regroupés par segment.
        segs = new Mesh { name = "Horloge segments", indexFormat = IndexFormat.UInt32 };
        segStart = new int[SevenSegmentDisplay.SegCount + 1];
        var lists = new List<int>[SevenSegmentDisplay.SegCount];
        var builder = new MeshBuilder(src);
        for (int i = 0; i < SevenSegmentDisplay.SegCount; i++)
        {
            builder.NewGroup();
            segStart[i] = builder.Count;
            lists[i] = slots[i] == null ? new List<int>() : slots[i].Select(builder.Map).ToList();
        }
        segStart[SevenSegmentDisplay.SegCount] = builder.Count;
        builder.Fill(segs);
        // État affiché dans l'éditeur : 00:03:00.
        var lit = LitFor(new[] { 0, 0, 0, 3, 0, 0 }, true);
        segs.subMeshCount = 2;
        segs.SetTriangles(Enumerable.Range(0, lists.Length).Where(i => lit[i]).SelectMany(i => lists[i]).ToList(), 0);
        segs.SetTriangles(Enumerable.Range(0, lists.Length).Where(i => !lit[i]).SelectMany(i => lists[i]).ToList(), 1);
        segs.RecalculateBounds();

        // Boîtier seul.
        housing = new Mesh { name = "Horloge boîtier", indexFormat = IndexFormat.UInt32 };
        var hb = new MeshBuilder(src);
        hb.NewGroup();
        var hTris = src.GetIndices(zeroIdx).Select(hb.Map).ToList();
        hb.Fill(housing);
        housing.SetTriangles(hTris, 0);
        housing.RecalculateBounds();

        // Sauvegarde dans un seul fichier.
        AssetDatabase.DeleteAsset(MeshAsset);
        AssetDatabase.CreateAsset(housing, MeshAsset);
        AssetDatabase.AddObjectToAsset(segs, MeshAsset);
        AssetDatabase.SaveAssets();
        return true;
    }

    /// <summary>Index du segment : chiffre*7 + (a..g), ou 42..45 pour les points.</summary>
    static int Classify(Bounds b, ref int dotN)
    {
        Vector3 c = b.center, s = b.size;
        if (s.x < 0.3f && s.y < 0.3f) return dotN < 4 ? SevenSegmentDisplay.DigitCount * 7 + dotN++ : -1;
        int d = 0;
        for (int i = 1; i < DigitX.Length; i++) if (Mathf.Abs(c.x - DigitX[i]) < Mathf.Abs(c.x - DigitX[d])) d = i;
        int seg;
        if (s.x > s.y) seg = c.y > 0.45f ? 0 : c.y < -0.45f ? 3 : 6;                 // a, d, g
        else if (c.x < DigitX[d]) seg = c.y > 0f ? 1 : 2;                           // b, c (à droite vu de face)
        else seg = c.y > 0f ? 5 : 4;                                                // f, e
        return d * 7 + seg;
    }

    static bool[] LitFor(int[] digits, bool colons)
    {
        var lit = new bool[SevenSegmentDisplay.SegCount];
        for (int d = 0; d < 6; d++) for (int s = 0; s < 7; s++) lit[d * 7 + s] = (Glyphs[digits[d]] & (1 << s)) != 0;
        for (int i = 42; i < lit.Length; i++) lit[i] = colons;
        return lit;
    }

    /// <summary>Copie compacte d'une partie des sommets d'un mesh.</summary>
    class MeshBuilder
    {
        readonly Vector3[] v, n; readonly Vector4[] t; readonly Vector2[] uv;
        readonly List<Vector3> ov = new List<Vector3>(), on = new List<Vector3>();
        readonly List<Vector4> ot = new List<Vector4>(); readonly List<Vector2> ouv = new List<Vector2>();
        Dictionary<int, int> map = new Dictionary<int, int>();
        public int Count => ov.Count;
        public MeshBuilder(Mesh m) { v = m.vertices; n = m.normals; t = m.tangents; uv = m.uv; }
        public void NewGroup() => map = new Dictionary<int, int>();
        public int Map(int i)
        {
            if (map.TryGetValue(i, out int j)) return j;
            j = ov.Count; map[i] = j;
            ov.Add(v[i]);
            if (n.Length == v.Length) on.Add(n[i]);
            if (t.Length == v.Length) ot.Add(t[i]);
            if (uv.Length == v.Length) ouv.Add(uv[i]);
            return j;
        }
        public void Fill(Mesh m)
        {
            m.SetVertices(ov);
            if (on.Count == ov.Count) m.SetNormals(on);
            if (ot.Count == ov.Count) m.SetTangents(ot);
            if (ouv.Count == ov.Count) m.SetUVs(0, ouv);
        }
    }

    /// <summary>Pose une horloge collée au mur en <paramref name="wallPoint"/>, l'écran tourné vers <paramref name="facing"/>.</summary>
    static SevenSegmentDisplay MakeClock(GameObject prefab, Transform parent, string name, Vector3 wallPoint, Vector3 facing,
        Mesh housing, Mesh segs, int[] segStart, Material[] mats)
    {
        var src = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        PrefabUtility.UnpackPrefabInstance(src, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        src.transform.position = Vector3.zero;
        var body = src.GetComponentsInChildren<MeshRenderer>().FirstOrDefault(r => r.name == "Digital.Clock");
        if (body == null) { Object.DestroyImmediate(src); return null; }

        // Côté de l'écran : là où sont les segments par rapport au centre du boîtier.
        Vector3 segCenter = body.transform.TransformPoint(segs.bounds.center);
        Vector3 d = segCenter - body.bounds.center;
        Vector3 sz = body.bounds.size;
        Vector3 thin = sz.x <= sz.y && sz.x <= sz.z ? Vector3.right : sz.y <= sz.z ? Vector3.up : Vector3.forward;
        Vector3 frontWorld = Vector3.Dot(d, thin) >= 0f ? thin : -thin;

        // Boîtier seul + segments pilotés par le compte à rebours.
        body.GetComponent<MeshFilter>().sharedMesh = housing;
        body.sharedMaterials = new[] { mats[0] };
        var segGo = new GameObject("Segments");
        segGo.transform.SetParent(body.transform, false);
        segGo.AddComponent<MeshFilter>().sharedMesh = segs;
        var segR = segGo.AddComponent<MeshRenderer>();
        segR.sharedMaterials = new[] { mats[1], mats[2] };
        var disp = segGo.AddComponent<SevenSegmentDisplay>();
        disp.segVertStart = segStart;

        // Support propre : pivot au centre de l'horloge.
        var go = new GameObject(name);
        go.transform.position = body.bounds.center;
        body.transform.SetParent(go.transform, true);
        Object.DestroyImmediate(src);

        // Écran tourné vers la classe, 60 cm de large, collé au mur.
        Vector3 f = frontWorld; f.y = 0f;
        if (f.sqrMagnitude > 1e-4f) go.transform.rotation = Quaternion.FromToRotation(f.normalized, facing) * go.transform.rotation;
        Bounds b = body.bounds;
        float w = Mathf.Max(b.size.x, b.size.z);
        if (w > 1e-4f) go.transform.localScale *= ClockWidth / w;
        b = body.bounds;
        float depth = Mathf.Abs(Vector3.Dot(b.size, new Vector3(Mathf.Abs(facing.x), 0f, Mathf.Abs(facing.z))));
        go.transform.position += wallPoint + facing * (depth * 0.5f + 0.01f) - b.center;
        go.transform.SetParent(parent, true);
        foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
        return disp;
    }
}
