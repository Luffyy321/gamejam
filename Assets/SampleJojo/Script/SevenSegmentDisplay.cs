using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Afficheur 7 segments de l'horloge digitale (les vrais segments du modèle).
/// Le mesh contient 6 chiffres (HH:MM:SS) + 4 points ; sous-mesh 0 = segments allumés, 1 = éteints.
/// </summary>
[RequireComponent(typeof(MeshFilter))]
public class SevenSegmentDisplay : MonoBehaviour
{
    public const int DigitCount = 6;
    public const int SegCount = DigitCount * 7 + 4;     // 42 segments + 4 points (deux « : »)

    [Tooltip("Premier sommet de chaque segment dans le mesh (SegCount + 1 valeurs).")]
    public int[] segVertStart;

    // bit 0 = a (haut), b (haut droite), c (bas droite), d (bas), e (bas gauche), f (haut gauche), bit 6 = g (milieu)
    static readonly byte[] Glyphs = { 0x3F, 0x06, 0x5B, 0x4F, 0x66, 0x6D, 0x7D, 0x07, 0x7F, 0x6F };

    Mesh mesh;
    List<int>[] segTris;
    readonly List<int> on = new List<int>(), off = new List<int>();
    readonly bool[] lit = new bool[SegCount];
    bool ready;

    void Awake() => Init();

    void Init()
    {
        if (ready) return;
        var mf = GetComponent<MeshFilter>();
        if (mf.sharedMesh == null || segVertStart == null || segVertStart.Length != SegCount + 1) return;
        mesh = Instantiate(mf.sharedMesh);
        mesh.name = mf.sharedMesh.name + " (jeu)";
        mf.sharedMesh = mesh;
        segTris = new List<int>[SegCount];
        for (int i = 0; i < SegCount; i++) segTris[i] = new List<int>();
        for (int s = 0; s < mesh.subMeshCount; s++)
        {
            var t = mesh.GetTriangles(s);
            for (int i = 0; i < t.Length; i += 3)
            {
                int seg = SegmentOf(t[i]);
                if (seg >= 0) { segTris[seg].Add(t[i]); segTris[seg].Add(t[i + 1]); segTris[seg].Add(t[i + 2]); }
            }
        }
        if (mesh.subMeshCount < 2) mesh.subMeshCount = 2;
        ready = true;
    }

    int SegmentOf(int vertex)
    {
        int lo = 0, hi = SegCount - 1;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (segVertStart[mid] <= vertex) lo = mid; else hi = mid - 1;
        }
        return vertex < segVertStart[SegCount] ? lo : -1;
    }

    /// <summary>Affiche 6 chiffres (−1 = éteint) et les deux « : ».</summary>
    public void Show(int[] digits, bool colons)
    {
        Init();
        if (!ready) return;
        for (int d = 0; d < DigitCount; d++)
        {
            int v = d < digits.Length ? digits[d] : -1;
            byte g = v >= 0 && v <= 9 ? Glyphs[v] : (byte)0;
            for (int s = 0; s < 7; s++) lit[d * 7 + s] = (g & (1 << s)) != 0;
        }
        for (int i = DigitCount * 7; i < SegCount; i++) lit[i] = colons;
        on.Clear(); off.Clear();
        for (int i = 0; i < SegCount; i++) (lit[i] ? on : off).AddRange(segTris[i]);
        mesh.SetTriangles(on, 0, false);
        mesh.SetTriangles(off, 1, false);
    }

    /// <summary>Affiche une durée en HH:MM:SS.</summary>
    public void ShowTime(int totalSeconds, bool colons)
    {
        totalSeconds = Mathf.Max(0, totalSeconds);
        int h = totalSeconds / 3600, m = totalSeconds / 60 % 60, s = totalSeconds % 60;
        Show(new[] { h / 10 % 10, h % 10, m / 10, m % 10, s / 10, s % 10 }, colons);
    }

    /// <summary>Tout éteint.</summary>
    public void Blank() => Show(new[] { -1, -1, -1, -1, -1, -1 }, false);
}
