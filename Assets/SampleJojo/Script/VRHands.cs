using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Remplace les manettes par des mains avec avant-bras.
/// - Les modèles de manette sont cachés (les rayons de téléportation restent).
/// - Une main (modèle XR Hands) + un avant-bras avec manche sont attachés à chaque manette.
/// - Les doigts se plient avec la gâchette (index) et la poignée (les autres doigts + le pouce).
/// À mettre sur le XR Origin. En mode suivi des mains (sans manettes), ce sont les mains suivies qui s'affichent.
/// </summary>
public class VRHands : MonoBehaviour
{
    [Header("Modèles")]
    public GameObject leftHandModel;
    public GameObject rightHandModel;
    public Material skinMaterial;
    public Material sleeveMaterial;

    [Header("Manettes (trouvées par leur nom si vide)")]
    public Transform leftController;
    public Transform rightController;
    public bool hideControllerModels = true;

    [Header("Position de la main sur la manette (réglages fins)")]
    public Vector3 positionOffset = Vector3.zero;
    public Vector3 rotationOffset = Vector3.zero;
    public float handScale = 1f;
    [Tooltip("Inclinaison de la main vers le bas (degrés). Négatif = vers le haut.")]
    public float handPitch = 10f;

    [Header("Avant-bras")]
    public bool showForearm = false;
    public float forearmLength = 0.27f;
    public float wristRadius = 0.026f;
    public float elbowRadius = 0.038f;
    [Tooltip("Où commence la manche (0 = poignet, 1 = coude).")]
    [Range(0f, 1f)] public float sleeveStart = 0.45f;

    [Header("Doigts")]
    public float restCurl = 0.12f;
    public float curlSpeed = 14f;

    class Finger
    {
        public Transform[] joints;        // proximal (ou métacarpe du pouce) -> distal
        public Quaternion[] rest;
        public Vector3[] axis;            // axe de flexion, dans le repère local de chaque os
        public float[] maxAngle;
    }

    class Hand
    {
        public bool left;
        public Transform root;
        public Finger index, thumb;
        public List<Finger> others = new List<Finger>();
        public InputAction grip, trigger;
        public float g, t;
    }

    readonly List<Hand> hands = new List<Hand>();

    void Start()
    {
        if (leftController == null) leftController = FindDeep(transform, "Left Controller");
        if (rightController == null) rightController = FindDeep(transform, "Right Controller");

        if (leftController != null && leftHandModel != null) hands.Add(BuildHand(true, leftController, leftHandModel));
        if (rightController != null && rightHandModel != null) hands.Add(BuildHand(false, rightController, rightHandModel));

        if (hideControllerModels)
        {
            HideControllerModels();
            Invoke(nameof(HideControllerModels), 1f);     // modèles créés un peu après le démarrage
        }
    }

    void OnDestroy()
    {
        foreach (var h in hands) { h.grip?.Dispose(); h.trigger?.Dispose(); }
    }

    // ---------------- Construction ----------------
    Hand BuildHand(bool left, Transform controller, GameObject model)
    {
        var h = new Hand { left = left };
        var holder = new GameObject(left ? "Main gauche" : "Main droite").transform;
        holder.SetParent(controller, false);
        var inst = Instantiate(model, holder);
        inst.name = "Modele main";
        inst.transform.localPosition = Vector3.zero;
        inst.transform.localRotation = Quaternion.identity;
        inst.transform.localScale = Vector3.one * handScale;
        h.root = holder;

        foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
        {
            if (skinMaterial != null) r.sharedMaterial = skinMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
        }
        // Pas de collisions avec ce qu'on attrape.
        foreach (var c in inst.GetComponentsInChildren<Collider>(true)) Destroy(c);
        foreach (var an in inst.GetComponentsInChildren<Animator>(true)) an.enabled = false;

        string p = left ? "L_" : "R_";
        Transform J(string n) => FindDeep(inst.transform, p + n);
        Transform wrist = J("Wrist");
        Transform palm = J("Palm") ?? wrist;
        Transform idx = J("IndexProximal"), mid = J("MiddleProximal"), lit = J("LittleProximal");
        if (wrist == null || idx == null || mid == null || lit == null)
        {
            Debug.LogWarning("[Mains] Os de la main introuvables : la main ne sera pas orientée.");
            return h;
        }

        // Repère de la main, mesuré sur le modèle (dans le repère du support).
        Vector3 f = holder.InverseTransformDirection(mid.position - wrist.position).normalized;   // poignet -> doigts
        Vector3 a = holder.InverseTransformDirection(idx.position - lit.position).normalized;     // auriculaire -> index

        // Main dans l'axe de la manette (comme une poignée de main) : doigts vers l'avant (+Z),
        // pouce vers le haut (+Y), paume vers l'intérieur (+X main gauche / -X main droite).
        Vector3 up = Vector3.ProjectOnPlane(a, f).normalized;
        Quaternion modelFrame = Quaternion.LookRotation(f, up);
        inst.transform.localRotation = Quaternion.Euler(handPitch, 0f, 0f) * Quaternion.Inverse(modelFrame);

        // La paume se place contre la poignée de la manette.
        Vector3 palmLocal = holder.InverseTransformPoint(palm.position);
        float side = left ? -1f : 1f;
        inst.transform.localPosition += new Vector3(side * 0.025f, -0.035f, -0.07f) - palmLocal;     // la manette suit la pointe : la paume est un peu derrière et en dessous

        holder.localPosition = positionOffset;
        holder.localRotation = Quaternion.Euler(rotationOffset);

        // Les doigts (après avoir orienté la main).
        Vector3 palmDirWorld = holder.TransformDirection(left ? Vector3.right : Vector3.left);
        h.index = MakeFinger(inst.transform, p, "Index", palmDirWorld, 75f, 95f, 65f);
        h.others.Add(MakeFinger(inst.transform, p, "Middle", palmDirWorld, 80f, 100f, 65f));
        h.others.Add(MakeFinger(inst.transform, p, "Ring", palmDirWorld, 85f, 100f, 65f));
        h.others.Add(MakeFinger(inst.transform, p, "Little", palmDirWorld, 90f, 100f, 65f));
        h.thumb = MakeThumb(inst.transform, p, palmDirWorld);

        // Avant-bras + manche (désactivé par défaut).
        if (showForearm) BuildForearm(holder, wrist.position, (mid.position - wrist.position).normalized);

        // Entrées gâchette / poignée.
        string hand = left ? "{LeftHand}" : "{RightHand}";
        h.grip = new InputAction(left ? "Grip L" : "Grip R", InputActionType.Value);
        h.grip.AddBinding("<XRController>" + hand + "/{Grip}");
        h.grip.AddBinding("<XRController>" + hand + "/grip");
        h.trigger = new InputAction(left ? "Trigger L" : "Trigger R", InputActionType.Value);
        h.trigger.AddBinding("<XRController>" + hand + "/{Trigger}");
        h.trigger.AddBinding("<XRController>" + hand + "/trigger");
        h.grip.Enable();
        h.trigger.Enable();
        return h;
    }

    Finger MakeFinger(Transform root, string p, string name, Vector3 palmDir, float a0, float a1, float a2)
    {
        var bones = new[] { FindDeep(root, p + name + "Proximal"), FindDeep(root, p + name + "Intermediate"), FindDeep(root, p + name + "Distal"), FindDeep(root, p + name + "Tip") };
        return BuildFinger(bones, palmDir, new[] { a0, a1, a2 });
    }

    Finger MakeThumb(Transform root, string p, Vector3 palmDir)
    {
        var bones = new[] { FindDeep(root, p + "ThumbMetacarpal"), FindDeep(root, p + "ThumbProximal"), FindDeep(root, p + "ThumbDistal"), FindDeep(root, p + "ThumbTip") };
        return BuildFinger(bones, palmDir, new[] { 25f, 35f, 45f });
    }

    /// <summary>bones = 3 os qui plient + le bout du doigt (pour la direction du dernier os).</summary>
    static Finger BuildFinger(Transform[] bones, Vector3 palmDir, float[] max)
    {
        if (bones[0] == null || bones[1] == null || bones[2] == null) return null;
        var f = new Finger { joints = new Transform[3], rest = new Quaternion[3], axis = new Vector3[3], maxAngle = max };
        for (int i = 0; i < 3; i++)
        {
            Transform j = bones[i];
            Transform next = bones[i + 1] != null ? bones[i + 1] : null;
            Vector3 dir = next != null ? (next.position - j.position) : (j.position - bones[i - 1 >= 0 ? i - 1 : 0].position);
            if (dir.sqrMagnitude < 1e-10f) dir = j.forward;
            dir.Normalize();
            Vector3 axisW = Vector3.Cross(dir, palmDir);
            if (axisW.sqrMagnitude < 1e-8f) axisW = j.right;
            axisW.Normalize();
            // Sens : le bout du doigt doit aller vers la paume.
            Vector3 moved = Quaternion.AngleAxis(30f, axisW) * dir;
            if (Vector3.Dot(moved - dir, palmDir) < 0f) axisW = -axisW;
            f.joints[i] = j;
            f.rest[i] = j.localRotation;
            f.axis[i] = Quaternion.Inverse(j.rotation) * axisW;
        }
        return f;
    }

    void BuildForearm(Transform parent, Vector3 wristWorld, Vector3 fingersDirWorld)
    {
        var go = new GameObject("Avant-bras");
        go.transform.SetParent(parent, false);
        go.transform.position = wristWorld;
        // L'avant-bras part du poignet, à l'opposé des doigts.
        go.transform.rotation = Quaternion.LookRotation(-fingersDirWorld, parent.up);

        var mesh = new Mesh { name = "Avant-bras" };
        const int sides = 14, rings = 9;
        var v = new List<Vector3>(); var n = new List<Vector3>();
        var skinTris = new List<int>(); var sleeveTris = new List<int>();
        for (int r = 0; r < rings; r++)
        {
            float t = r / (float)(rings - 1);
            float z = t * forearmLength;
            float rad = Mathf.Lerp(wristRadius, elbowRadius, Mathf.SmoothStep(0f, 1f, t));
            bool sleeve = t > sleeveStart;
            if (sleeve) rad += 0.006f;           // la manche est un peu plus large
            for (int s = 0; s <= sides; s++)
            {
                float ang = s / (float)sides * Mathf.PI * 2f;
                // Un peu aplati (un avant-bras n'est pas rond).
                var dir = new Vector3(Mathf.Cos(ang) * 1.15f, Mathf.Sin(ang) * 0.85f, 0f);
                v.Add(new Vector3(dir.x * rad, dir.y * rad, z));
                n.Add(new Vector3(dir.x, dir.y, 0f).normalized);
            }
        }
        int row = sides + 1;
        for (int r = 0; r < rings - 1; r++)
        {
            float tMid = (r + 0.5f) / (rings - 1);
            var tris = tMid > sleeveStart ? sleeveTris : skinTris;
            for (int s = 0; s < sides; s++)
            {
                int i0 = r * row + s, i1 = i0 + 1, i2 = i0 + row, i3 = i2 + 1;
                tris.Add(i0); tris.Add(i1); tris.Add(i2);
                tris.Add(i1); tris.Add(i3); tris.Add(i2);
            }
        }
        // Bout du coude fermé.
        int center = v.Count;
        v.Add(new Vector3(0f, 0f, forearmLength)); n.Add(Vector3.forward);
        int last = (rings - 1) * row;
        for (int s = 0; s < sides; s++) { sleeveTris.Add(last + s); sleeveTris.Add(last + s + 1); sleeveTris.Add(center); }

        mesh.SetVertices(v);
        mesh.SetNormals(n);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(skinTris, 0);
        mesh.SetTriangles(sleeveTris, 1);
        mesh.RecalculateBounds();

        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterials = new[] { skinMaterial, sleeveMaterial != null ? sleeveMaterial : skinMaterial };
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // ---------------- Cacher les manettes ----------------
    void HideControllerModels()
    {
        foreach (var c in new[] { leftController, rightController })
        {
            if (c == null) continue;
            foreach (var r in c.GetComponentsInChildren<Renderer>(true))
            {
                if (r is LineRenderer || r is TrailRenderer || r is ParticleSystemRenderer) continue;   // rayons, effets
                if (IsOurs(r.transform)) continue;
                if (LooksLikeController(r.transform, c)) r.enabled = false;
            }
        }
    }

    float testCurl = -1f;

    /// <summary>Test sans casque : montre les deux mains devant la caméra, doigts pliés à <paramref name="curl"/>.</summary>
    public void TestShowInFront(float curl)
    {
        var cam = Camera.main != null ? Camera.main.transform : null;
        if (cam == null) return;
        testCurl = curl;
        foreach (var h in hands)
        {
            if (h.root == null) continue;
            h.root.SetParent(cam, false);
            h.root.localPosition = new Vector3(h.left ? -0.14f : 0.14f, -0.16f, 0.38f);
            h.root.localRotation = Quaternion.Euler(-25f, 0f, 0f);
            h.root.gameObject.SetActive(true);
        }
    }

    bool IsOurs(Transform t)
    {
        foreach (var h in hands) if (h.root != null && t.IsChildOf(h.root)) return true;
        return false;
    }

    static bool LooksLikeController(Transform t, Transform stop)
    {
        for (var x = t; x != null && x != stop.parent; x = x.parent)
        {
            string n = x.name.ToLowerInvariant();
            if (n.Contains("universalcontroller") || n.Contains("controller visual") || n.Contains("controllervisual") ||
                n.Contains("xr controller") || n.Contains("controller_base") || n.Contains("controller model") ||
                n.Contains("questcontroller") || n.Contains("touch"))
                return true;
        }
        // Un mesh directement sur la manette (sans nom parlant) : c'est aussi le modèle.
        var mf = t.GetComponent<MeshFilter>();
        return mf != null && mf.sharedMesh != null && mf.sharedMesh.name.ToLowerInvariant().Contains("controller");
    }

    // ---------------- Animation des doigts ----------------
    void LateUpdate()
    {
        float dt = Time.deltaTime;
        foreach (var h in hands)
        {
            float g = testCurl >= 0f ? testCurl : h.grip != null ? h.grip.ReadValue<float>() : 0f;
            float t = testCurl >= 0f ? testCurl : h.trigger != null ? h.trigger.ReadValue<float>() : 0f;
            float k = 1f - Mathf.Exp(-curlSpeed * dt);
            h.g = Mathf.Lerp(h.g, g, k);
            h.t = Mathf.Lerp(h.t, t, k);

            Pose(h.index, Mathf.Max(restCurl, h.t));
            foreach (var f in h.others) Pose(f, Mathf.Max(restCurl, h.g));
            Pose(h.thumb, Mathf.Max(restCurl, Mathf.Max(h.g, h.t * 0.5f)));
        }
    }

    static void Pose(Finger f, float curl)
    {
        if (f == null) return;
        for (int i = 0; i < f.joints.Length; i++)
            f.joints[i].localRotation = f.rest[i] * Quaternion.AngleAxis(f.maxAngle[i] * curl, f.axis[i]);
    }

    static Transform FindDeep(Transform r, string n)
    {
        if (r.name == n) return r;
        foreach (Transform c in r)
        {
            var x = FindDeep(c, n);
            if (x != null) return x;
        }
        return null;
    }
}
