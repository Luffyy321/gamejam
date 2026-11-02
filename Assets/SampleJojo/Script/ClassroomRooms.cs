using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Salles des cours : cours 1 = la classe de départ, cours 2 = la pièce d'à côté, cours 3 = la salle de l'étage.
/// Au chargement du cours, tout l'intérieur de la classe (tables, élèves, notre table et nos objets, la prof et sa ronde,
/// le bureau de la prof, le décor, l'horloge…) est déplacé dans la bonne salle :
/// - les objets au sol suivent la même disposition (ceux qui ne rentrent pas, ou qui touchent un poteau, sont cachés) ;
/// - les objets accrochés aux murs (affiches, horloge, tableau) sont recollés au mur de la nouvelle salle ;
/// - les lampes sont recollées au plafond ;
/// - le bureau de la prof est posé à sa place dans la salle.
/// Rien ne bouge au cours 1 : la classe reste comme elle est.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class ClassroomRooms : MonoBehaviour
{
    [System.Serializable]
    public class Room
    {
        public string name = "Salle";
        [Tooltip("Cours où l'on est dans cette salle (2, 3…).")]
        public int course = 2;
        [Tooltip("Où va notre chaise (le point « Départ partie (chaise) »).")]
        public Vector3 playerSpot;
        [Tooltip("Rotation de la salle par rapport à la classe (degrés, autour de la verticale).")]
        public float yaw;
        [Tooltip("Intérieur de la salle (murs intérieurs, sol → plafond).")]
        public Bounds interior;
        [Tooltip("Poteaux (x, z = position ; y = rayon).")]
        public Vector3[] pillars = new Vector3[0];
        [Tooltip("Centre (au sol) du bureau de la prof dans cette salle.")]
        public Vector3 profDeskSpot;
        [Tooltip("La salle a déjà un tableau : on cache celui de la classe.")]
        public bool hasBoard;
        [Tooltip("Objets qui n'apparaissent QUE dans cette salle (cachés dans les autres cours).")]
        public GameObject[] onlyHere = new GameObject[0];
    }

    [Header("Classe de départ")]
    [Tooltip("Notre chaise dans la classe (repère de la disposition).")]
    public Vector3 sourceSpot;
    public Bounds sourceInterior;

    [Header("Ce qui déménage")]
    public Transform[] movers;
    [Tooltip("Objets du jeu à ne jamais cacher (notre table, la machine, la prof…).")]
    public Transform[] core;
    [Tooltip("Racines dont chaque enfant est traité séparément (tables, décor, élèves).")]
    public Transform[] groups;
    [Tooltip("Bureau de la prof et ce qui est dessus.")]
    public Transform[] profDesk;
    [Tooltip("Tableau de la classe (caché si la salle a déjà le sien).")]
    public Transform classBoard;
    [Tooltip("Points de la ronde de la prof.")]
    public Transform waypoints;

    public Room[] rooms = new Room[0];

    // --- Aperçu en mode édition (menus Tools > Classe > 25/26/27) ---
    [System.Serializable]
    public class EditSnap { public Transform t; public Vector3 p; public Quaternion r; public bool a; }
    [HideInInspector] public List<EditSnap> editSnapshot = new List<EditSnap>();
    [Tooltip("Salle affichée en aperçu dans l'éditeur (0 = la classe). Ne pas toucher à la main.")]
    [HideInInspector] public int editPreview;

    public static Room Current { get; private set; }
    /// <summary>Intérieur de la salle où l'on est (pour garder la ronde de la prof dans la salle).</summary>
    public static Bounds? ActiveInterior { get; private set; }

    void Awake()
    {
        // Si la scène a été enregistrée pendant un aperçu, on remet d'abord la classe telle qu'elle était.
        if (editPreview != 0) RestoreEditSnapshot();
        Current = null;
        ActiveInterior = sourceInterior.size.sqrMagnitude > 0f ? sourceInterior : (Bounds?)null;
        foreach (var r in rooms)
            if (r != null && r.course == GameProgress.Level) { Apply(r); Current = r; ActiveInterior = r.interior; break; }
        // Objets propres à une salle : visibles seulement quand on y est.
        foreach (var r in rooms)
            if (r != null && r.onlyHere != null)
                foreach (var go in r.onlyHere) if (go != null) go.SetActive(r == Current);
    }

    /// <summary>Tout ce que le déménagement peut bouger ou cacher.</summary>
    public List<Transform> Affected()
    {
        var set = new HashSet<Transform>();
        var groupSet = new HashSet<Transform>(groups ?? new Transform[0]);
        foreach (var m in movers ?? new Transform[0])
        {
            if (m == null) continue;
            set.Add(m);
            if (groupSet.Contains(m)) foreach (Transform c in m) set.Add(c);
        }
        foreach (var t in core ?? new Transform[0]) if (t != null) set.Add(t);
        foreach (var t in profDesk ?? new Transform[0]) if (t != null) set.Add(t);
        if (classBoard != null) set.Add(classBoard);
        foreach (var w in RondePoints()) set.Add(w);
        return new List<Transform>(set);
    }

    /// <summary>Tous les points des rondes (cours 1 et les groupes « Ronde cours 2/3 »).</summary>
    List<Transform> RondePoints()
    {
        var list = new List<Transform>();
        if (waypoints == null) return list;
        foreach (var t in waypoints.GetComponentsInChildren<Transform>(true))
            if (t != waypoints && t.childCount == 0) list.Add(t);
        return list;
    }

    public void TakeEditSnapshot()
    {
        editSnapshot.Clear();
        foreach (var t in Affected())
            editSnapshot.Add(new EditSnap { t = t, p = t.localPosition, r = t.localRotation, a = t.gameObject.activeSelf });
    }

    public void RestoreEditSnapshot()
    {
        foreach (var s in editSnapshot)
        {
            if (s == null || s.t == null) continue;
            s.t.localPosition = s.p; s.t.localRotation = s.r;
            if (s.t.gameObject.activeSelf != s.a) s.t.gameObject.SetActive(s.a);
        }
        editSnapshot.Clear();
        editPreview = 0;
        Physics.SyncTransforms();
    }

    public Room RoomFor(int course)
    {
        foreach (var r in rooms) if (r != null && r.course == course) return r;
        return null;
    }

    public void ApplyRoom(Room room) => Apply(room);

    struct WallInfo { public Vector3 dir; public float gap; }

    void Apply(Room room)
    {
        var rot = Quaternion.Euler(0f, room.yaw, 0f);
        float floor0 = sourceInterior.min.y, ceil0 = sourceInterior.max.y;

        // 1) Avant de bouger : repérer les objets muraux (et leur mur) et ceux du plafond.
        var pieces = new List<Transform>();
        var coreSet = new HashSet<Transform>(core ?? new Transform[0]);
        var groupSet = new HashSet<Transform>(groups ?? new Transform[0]);
        var deskSet = new HashSet<Transform>(profDesk ?? new Transform[0]);
        foreach (var m in movers)
        {
            if (m == null) continue;
            if (groupSet.Contains(m)) foreach (Transform c in m) pieces.Add(c);
            else pieces.Add(m);
        }
        var wall = new Dictionary<Transform, WallInfo>();
        var ceiling = new HashSet<Transform>();
        foreach (var p in pieces)
        {
            if (coreSet.Contains(p) || deskSet.Contains(p) || p == null) continue;
            if (!TryBounds(p, out var b)) continue;
            if (b.min.y > ceil0 - 0.8f && b.max.y > ceil0 - 0.3f) { ceiling.Add(p); continue; }
            if (b.min.y < floor0 + 0.45f) continue;
            // Mur le plus proche.
            float dXmin = b.min.x - sourceInterior.min.x, dXmax = sourceInterior.max.x - b.max.x;
            float dZmin = b.min.z - sourceInterior.min.z, dZmax = sourceInterior.max.z - b.max.z;
            float best = Mathf.Min(Mathf.Min(dXmin, dXmax), Mathf.Min(dZmin, dZmax));
            float thin = Mathf.Min(b.size.x, b.size.z);
            if (best > 0.35f || thin > 0.35f) continue;
            var info = new WallInfo { gap = Mathf.Max(0f, best) };
            info.dir = best == dXmin ? Vector3.left : best == dXmax ? Vector3.right : best == dZmin ? Vector3.back : Vector3.forward;
            wall[p] = info;
        }

        // 2) Tout déménage avec la même disposition.
        Vector3 shift = room.playerSpot;
        foreach (var m in movers)
        {
            if (m == null) continue;
            m.SetPositionAndRotation(shift + rot * (m.position - sourceSpot), rot * m.rotation);
        }
        Physics.SyncTransforms();

        Bounds inner = room.interior;
        float floor1 = inner.min.y, ceil1 = inner.max.y;

        // 3) Bureau de la prof : à sa place dans la salle.
        Bounds deskB = default; bool hasDesk = false;
        foreach (var d in profDesk) if (d != null && TryBounds(d, out var db)) { if (!hasDesk) { deskB = db; hasDesk = true; } else deskB.Encapsulate(db); }
        if (hasDesk)
        {
            Vector3 delta = new Vector3(room.profDeskSpot.x - deskB.center.x, 0f, room.profDeskSpot.z - deskB.center.z);
            foreach (var d in profDesk) if (d != null) d.position += delta;
            deskB.center += delta;
        }

        // 4) Murs, plafond, sol.
        foreach (var p in pieces)
        {
            if (p == null || coreSet.Contains(p) || deskSet.Contains(p)) continue;
            if (p == classBoard && room.hasBoard) { p.gameObject.SetActive(false); continue; }
            if (!TryBounds(p, out var b)) continue;
            if (ceiling.Contains(p))
            {
                p.position += Vector3.up * ((ceil1 - floor1) - (ceil0 - floor0));
                continue;
            }
            if (wall.TryGetValue(p, out var w))
            {
                Vector3 d = rot * w.dir;
                Vector3 move = Vector3.zero;
                if (d.x < -0.5f) move.x = (inner.min.x + w.gap) - b.min.x;
                else if (d.x > 0.5f) move.x = (inner.max.x - w.gap) - b.max.x;
                else if (d.z < -0.5f) move.z = (inner.min.z + w.gap) - b.min.z;
                else move.z = (inner.max.z - w.gap) - b.max.z;
                // Le long du mur : on le fait glisser pour qu'il tienne dans la salle (sinon caché).
                if (Mathf.Abs(d.x) > 0.5f)
                {
                    if (b.min.z + move.z < inner.min.z + 0.05f) move.z += inner.min.z + 0.05f - (b.min.z + move.z);
                    else if (b.max.z + move.z > inner.max.z - 0.05f) move.z -= (b.max.z + move.z) - (inner.max.z - 0.05f);
                }
                else
                {
                    if (b.min.x + move.x < inner.min.x + 0.05f) move.x += inner.min.x + 0.05f - (b.min.x + move.x);
                    else if (b.max.x + move.x > inner.max.x - 0.05f) move.x -= (b.max.x + move.x) - (inner.max.x - 0.05f);
                }
                p.position += move;
                b.center += move;
                if (!InsideXZ(inner, b, 0.06f, d)) p.gameObject.SetActive(false);
                continue;
            }
            // Au sol : caché s'il dépasse, touche un poteau ou le bureau de la prof.
            bool hide = !InsideXZ(inner, b, 0.03f, Vector3.zero) || HitsPillar(room, b);
            if (!hide && hasDesk) { var e = deskB; e.Expand(new Vector3(0.15f, 4f, 0.15f)); hide = e.Intersects(b); }
            if (hide) p.gameObject.SetActive(false);
        }

        // Objets du jeu (la prof, l'ambiance…) : ramenés dans la salle s'ils en sortent.
        foreach (var c in core)
        {
            if (c == null || (c.GetComponent<ProfAI>() == null && c.GetComponent<ClassroomAmbience>() == null)) continue;
            Vector3 q = c.position;
            if (q.x > inner.min.x && q.x < inner.max.x && q.z > inner.min.z && q.z < inner.max.z) continue;
            q.x = Mathf.Clamp(q.x, inner.min.x + 0.4f, inner.max.x - 0.4f);
            q.z = Mathf.Clamp(q.z, inner.min.z + 0.4f, inner.max.z - 0.4f);
            c.position = q;
        }

        // 5) Ronde de la prof : points ramenés dans la salle, hors des poteaux.
        foreach (var w in RondePoints())
            {
                Vector3 q = w.position;
                q.x = Mathf.Clamp(q.x, inner.min.x + 0.4f, inner.max.x - 0.4f);
                q.z = Mathf.Clamp(q.z, inner.min.z + 0.4f, inner.max.z - 0.4f);
                foreach (var pl in room.pillars)
                {
                    Vector3 c = new Vector3(pl.x, q.y, pl.z);
                    Vector3 off = q - c; off.y = 0f;
                    float rr = pl.y + 0.45f;
                    if (off.magnitude < rr) q = c + (off.sqrMagnitude > 1e-4f ? off.normalized : Vector3.right) * rr;
                }
                w.position = q;
            }

        Physics.SyncTransforms();
        Debug.Log($"[Salles] Cours {room.course} : on est dans « {room.name} ».");
    }

    static bool InsideXZ(Bounds inner, Bounds b, float margin, Vector3 wallDir)
    {
        // Pour un objet mural, on ne vérifie que la position le long du mur.
        bool checkX = Mathf.Abs(wallDir.x) < 0.5f, checkZ = Mathf.Abs(wallDir.z) < 0.5f;
        if (checkX && (b.min.x < inner.min.x - margin || b.max.x > inner.max.x + margin)) return false;
        if (checkZ && (b.min.z < inner.min.z - margin || b.max.z > inner.max.z + margin)) return false;
        return true;
    }

    static bool HitsPillar(Room room, Bounds b)
    {
        foreach (var pl in room.pillars)
        {
            float cx = Mathf.Clamp(pl.x, b.min.x, b.max.x), cz = Mathf.Clamp(pl.z, b.min.z, b.max.z);
            if (new Vector2(cx - pl.x, cz - pl.z).sqrMagnitude < pl.y * pl.y) return true;
        }
        return false;
    }

    static bool TryBounds(Transform t, out Bounds b)
    {
        b = default;
        bool first = true;
        foreach (var r in t.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer || !r.enabled) continue;
            if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
        }
        return !first;
    }
}
