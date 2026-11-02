using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Prof qui fait sa ronde dans la classe.
/// - Elle marche d'un point de ronde à l'autre et s'arrête de temps en temps pour regarder autour d'elle.
/// - Si elle voit le joueur avec la machine à sous devant lui, un « ! » orange apparaît au-dessus
///   de sa tête et se remplit de rouge tant qu'elle le voit (il se vide si on se cache ou range la machine).
/// - Quand le « ! » est entièrement rouge, elle court vers le joueur et s'arrête devant lui.
/// - Une fois la machine rangée dans le sac, elle reprend sa ronde.
/// </summary>
public class ProfAI : MonoBehaviour
{
    public enum State { Ronde, Pause, Soupcon, Poursuite, Attrape }

    [Header("Ronde")]
    [Tooltip("Points de passage (enfants de « Ronde du prof »), dans l'ordre.")]
    public Transform[] waypoints;
    [Tooltip("Ronde du cours 2 (vide = même ronde qu'au cours 1). Tools > Classe > 29 pour la créer.")]
    public Transform[] rondeCours2 = new Transform[0];
    [Tooltip("Ronde du cours 3 (vide = même ronde qu'au cours 1).")]
    public Transform[] rondeCours3 = new Transform[0];
    public float walkSpeed = 0.9f;
    public float turnSpeed = 220f;
    [Range(0f, 1f)] public float pauseChance = 0.5f;
    public Vector2 pauseDuration = new Vector2(2f, 5f);
    [Tooltip("À chaque point, chance qu'elle fasse demi-tour et reparte dans l'autre sens.")]
    [Range(0f, 1f)] public float turnBackChance = 0.25f;

    [Header("Vision")]
    public float viewDistance = 9f;
    [Tooltip("Angle de vision total, en degrés.")]
    public float viewAngle = 130f;
    public float eyeHeight = 1.6f;
    [Tooltip("La machine compte comme « devant le joueur » si elle est à moins de cette distance de sa tête.")]
    public float machineNearPlayer = 1.6f;
    public LayerMask obstacles = ~0;

    [Header("Point d'exclamation")]
    [Tooltip("Ancien « ! » (shader) : caché, sert seulement de repère.")]
    public Renderer exclamation;
    public float exclamationHeight = 2.1f;
    public float exclamationSize = 1.2f;
    [Tooltip("Matériaux Unlit du « ! » (créés par le menu de configuration ; sinon créés au lancement).")]
    public Material exclamationOrange;
    public Material exclamationRed;
    public Material exclamationDark;
    [Tooltip("Temps pour que le « ! » devienne entièrement rouge (en secondes).")]
    public float fillTime = 3.5f;
    [Tooltip("Temps pour qu'il se vide quand elle ne voit plus rien.")]
    public float drainTime = 2f;

    [Header("Poursuite")]
    public float runSpeed = 3.6f;
    [Tooltip("Distance à laquelle elle s'arrête devant le joueur.")]
    public float stopDistance = 0.8f;
    [Tooltip("Après avoir rangé la machine, temps avant qu'elle reprenne sa ronde.")]
    public float releaseDelay = 2.5f;

    [Header("Animations (noms des états de l'Animator)")]
    public Animator animator;
    public string idleState = "Idle";
    public string walkState = "Walk";
    public string runState = "Run";
    [Tooltip("Animation quand elle est devant nous (elle nous montre du doigt).")]
    public string pointState = "Point";
    [Tooltip("Rotation du corps quand elle pointe, pour que le doigt vise le joueur (degrés).")]
    public float pointYaw = -45f;
    [Tooltip("Recul du corps quand elle pointe (elle se penche vers nous) (m).")]
    public float pointBack = 0.25f;

    [Header("Sons")]
    public AudioSource audioSource;
    [Tooltip("Elle part en courant vers nous (énervée).")]
    public AudioClip alertClip;
    [Tooltip("Elle nous remarque (le « ! » commence à se remplir).")]
    public AudioClip noticeClip;
    [Tooltip("Elle arrive devant nous.")]
    public AudioClip arriveClip;

    [Header("Événements")]
    public UnityEvent onSpotted;     // le « ! » est plein : elle part en courant
    public UnityEvent onCaught;      // elle est arrivée devant le joueur

    [Header("État (lecture seule)")]
    [SerializeField] State state;
    [SerializeField, Range(0f, 1f)] float alert;

    Transform cam, rig;
    JackpotItem machine;
    int wp;
    int stepDir = 1;          // +1 = sens normal de la ronde, -1 = sens inverse
    float timer, floorY, pauseYaw, checkTimer, releaseTimer;
    bool seesPlayer;
    string currentAnim;
    float shownFill = -1f;
    Transform exRoot, redBar, redDot;
    Renderer[] exRenderers;
    const float BarBottom = 0.15f, BarTop = 0.46f, DotY = 0.06f;

    public State CurrentState => state;
    public float Alert => alert;

    void Start()
    {
        floorY = transform.position.y;
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator != null) animator.applyRootMotion = false;
        FindTargets();
        var ronde = GameProgress.Level == 2 ? rondeCours2 : GameProgress.Level == 3 ? rondeCours3 : null;
        if (ronde != null && ronde.Length >= 2)
        {
            waypoints = ronde;
            Debug.Log($"[Prof] Cours {GameProgress.Level} : ronde n°{GameProgress.Level} ({ronde.Length} points).");
        }
        if (GameProgress.Chapter >= 2) Harder();
        BuildExclamation();
        wp = NearestWaypoint();
        SetState(State.Ronde);
        UpdateExclamation(true);
    }

    [Header("Niveau 2 et plus : prof plus dure")]
    [Tooltip("Le « ! » se remplit plus vite (multiplie le temps de remplissage).")]
    public float hardFillFactor = 0.5f;
    public float hardSpeedFactor = 1.25f;
    public float hardViewAngleBonus = 30f;
    [Tooltip("Distance derrière notre chaise où passe sa ronde (m).")]
    public float behindDistance = 0.85f;

    /// <summary>Niveau 2 : elle nous repère plus vite, marche plus vite, voit plus large, s'arrête moins,
    /// et sa ronde passe derrière nous.</summary>
    void Harder()
    {
        fillTime *= hardFillFactor;
        drainTime *= 1.5f;
        walkSpeed *= hardSpeedFactor;
        runSpeed *= 1.1f;
        viewAngle = Mathf.Min(200f, viewAngle + hardViewAngleBonus);
        pauseChance *= 0.6f;
        turnBackChance = Mathf.Max(turnBackChance, 0.4f);
        AddBehindPlayerRoute();
        Debug.Log($"[Prof] Niveau {GameProgress.Chapter} : prof plus dure (repère en {fillTime:F1} s, ronde derrière le joueur).");
    }

    void AddBehindPlayerRoute()
    {
        var seat = GameObject.Find("Départ partie (chaise)");
        if (seat == null || waypoints == null || waypoints.Length == 0) return;
        Vector3 fwd = seat.transform.forward; fwd.y = 0f; fwd.Normalize();
        Vector3 side = Vector3.Cross(Vector3.up, fwd);
        Vector3 back = seat.transform.position - fwd * behindDistance;
        var parent = waypoints[0] != null ? waypoints[0].parent : null;
        Transform Make(string n, Vector3 p)
        {
            var t = new GameObject(n).transform;
            t.SetParent(parent, true);
            if (ClassroomRooms.ActiveInterior.HasValue)
            {
                var r = ClassroomRooms.ActiveInterior.Value;
                p.x = Mathf.Clamp(p.x, r.min.x + 0.35f, r.max.x - 0.35f);
                p.z = Mathf.Clamp(p.z, r.min.z + 0.35f, r.max.z - 0.35f);
            }
            t.position = new Vector3(p.x, waypoints[0].position.y, p.z);
            return t;
        }
        var a = Make("Derrière (gauche)", back - side * 1.4f);
        var b = Make("Derrière le joueur", back);
        var c = Make("Derrière (droite)", back + side * 1.4f);
        // Insérées après le point de la ronde le plus proche, dans l'ordre du trajet.
        int best = 0; float bd = float.MaxValue;
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null) continue;
            float d = (waypoints[i].position - back).sqrMagnitude;
            if (d < bd) { bd = d; best = i; }
        }
        var list = new System.Collections.Generic.List<Transform>(waypoints);
        list.InsertRange(best + 1, new[] { a, b, c });
        waypoints = list.ToArray();
    }

    void FindTargets()
    {
        var origin = FindFirstObjectByType<XROrigin>();
        if (origin != null)
        {
            rig = origin.transform;
            if (origin.Camera != null) cam = origin.Camera.transform;
        }
        if (cam == null && Camera.main != null) cam = Camera.main.transform;
        if (machine == null) machine = FindFirstObjectByType<JackpotItem>();
    }

    // ---------------- Boucle principale ----------------
    void Update()
    {
        if (cam == null) FindTargets();
        // Café gourmand : le temps ralentit (la prof aussi).
        float dt = Time.deltaTime * SlowMo.Factor;
        if (animator != null) animator.speed = SlowMo.Factor;

        // La vision n'est vérifiée que 10 fois par seconde (économise les FPS).
        checkTimer -= dt;
        if (checkTimer <= 0f)
        {
            checkTimer = 0.1f;
            seesPlayer = CanSeePlayerWithMachine();
        }

        switch (state)
        {
            case State.Ronde: Patrol(dt); break;
            case State.Pause: PauseUpdate(dt); break;
            case State.Soupcon: SuspicionUpdate(dt); break;
            case State.Poursuite: Chase(dt); break;
            case State.Attrape: CaughtUpdate(dt); break;
        }

        // Le « ! » se remplit tant qu'elle voit le joueur avec la machine.
        if (state != State.Poursuite && state != State.Attrape)
        {
            if (seesPlayer)
            {
                alert = Mathf.Min(1f, alert + dt / Mathf.Max(0.05f, fillTime));
                if (state != State.Soupcon)
                {
                    if (audioSource != null && noticeClip != null && Time.time > nextNotice) { audioSource.PlayOneShot(noticeClip); nextNotice = Time.time + 6f; }
                    SetState(State.Soupcon);
                }
                if (alert >= 1f) StartChase();
            }
            else
            {
                alert = Mathf.Max(0f, alert - dt / Mathf.Max(0.05f, drainTime));
            }
        }
    }

    void LateUpdate()
    {
        UpdatePointPose();
        UpdateExclamation(false);
    }

    float nextNotice;
    Transform model;
    Vector3 modelPos;
    Quaternion modelRot;
    float pointBlend;

    /// <summary>Quand elle pointe : le modèle tourne un peu (le doigt vise le joueur) et recule (elle se penche).</summary>
    void UpdatePointPose()
    {
        if (animator == null) return;
        if (model == null) { model = animator.transform; modelPos = model.localPosition; modelRot = model.localRotation; }
        bool pointing = state == State.Attrape && currentAnim == pointState;
        pointBlend = Mathf.MoveTowards(pointBlend, pointing ? 1f : 0f, Time.deltaTime * 4f);
        float k = Mathf.SmoothStep(0f, 1f, pointBlend);
        model.localRotation = modelRot * Quaternion.Euler(0f, pointYaw * k, 0f);
        model.localPosition = modelPos + Vector3.back * pointBack * k;
    }

    // ---------------- États ----------------
    void SetState(State s)
    {
        state = s;
        switch (s)
        {
            case State.Ronde: Play(walkState); break;
            case State.Poursuite: Play(runState); break;
            case State.Attrape: Play(HasState(pointState) ? pointState : idleState); break;
            default: Play(idleState); break;
        }
    }

    bool HasState(string n) => animator != null && !string.IsNullOrEmpty(n) && animator.HasState(0, Animator.StringToHash(n));

    void Play(string anim)
    {
        if (animator == null || string.IsNullOrEmpty(anim) || anim == currentAnim) return;
        currentAnim = anim;
        animator.CrossFadeInFixedTime(anim, 0.25f);
    }

    void Patrol(float dt)
    {
        if (waypoints == null || waypoints.Length == 0) { SetState(State.Pause); timer = 999f; return; }
        Transform t = waypoints[wp];
        if (t == null) { wp = ((wp + stepDir) % waypoints.Length + waypoints.Length) % waypoints.Length; return; }
        if (MoveTo(t.position, walkSpeed, 0.15f, dt))
        {
            int n = waypoints.Length;
            if (n > 1 && Random.value < turnBackChance)
            {
                // Demi-tour : elle se retourne sur place puis repart dans l'autre sens.
                stepDir = -stepDir;
                wp = ((wp + stepDir) % n + n) % n;
                Vector3 to = waypoints[wp] != null ? Flat(waypoints[wp].position - transform.position) : -transform.forward;
                pauseYaw = to.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(to).eulerAngles.y : transform.eulerAngles.y + 180f;
                timer = Random.Range(1.6f, 2.6f);
                SetState(State.Pause);
                return;
            }
            wp = ((wp + stepDir) % n + n) % n;
            if (Random.value < pauseChance)
            {
                timer = Random.Range(pauseDuration.x, pauseDuration.y);
                pauseYaw = transform.eulerAngles.y + Random.Range(-100f, 100f);
                SetState(State.Pause);
            }
        }
    }

    void PauseUpdate(float dt)
    {
        // Elle regarde autour d'elle pendant l'arrêt.
        Quaternion look = Quaternion.Euler(0f, pauseYaw, 0f);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * 0.35f * dt);
        timer -= dt;
        if (timer <= 0f) SetState(State.Ronde);
    }

    void SuspicionUpdate(float dt)
    {
        // Elle s'arrête et fixe le joueur pendant que le « ! » se remplit.
        if (cam != null) TurnTowards(cam.position, dt);
        if (!seesPlayer && alert <= 0f) SetState(State.Ronde);
    }

    void StartChase()
    {
        alert = 1f;
        SetState(State.Poursuite);
        if (audioSource != null && alertClip != null) audioSource.PlayOneShot(alertClip);
        onSpotted?.Invoke();
    }

    /// <summary>Point juste devant le joueur (dans la direction où il regarde), au sol.</summary>
    Vector3 FrontOfPlayer()
    {
        Vector3 fwd = Flat(cam.forward);
        if (fwd.sqrMagnitude < 1e-4f) fwd = Flat(transform.position - cam.position);
        fwd.Normalize();
        Vector3 p = cam.position + fwd * stopDistance;
        p.y = floorY;
        return p;
    }

    void Chase(float dt)
    {
        if (cam == null) return;
        Vector3 target = FrontOfPlayer();
        if (MoveTo(target, runSpeed, 0.2f, dt))
        {
            SetState(State.Attrape);
            if (audioSource != null && arriveClip != null) audioSource.PlayOneShot(arriveClip);
            releaseTimer = releaseDelay;
            onCaught?.Invoke();
        }
    }

    void CaughtUpdate(float dt)
    {
        if (cam == null) return;
        float d = Flat(FrontOfPlayer() - transform.position).magnitude;
        if (d > 1.0f) { SetState(State.Poursuite); return; }   // le joueur bouge ou se retourne : elle se replace devant lui
        TurnTowards(cam.position, dt);

        // Machine rangée : elle laisse tomber et reprend sa ronde.
        bool machineOut = machine != null && !machine.IsStored;
        if (machineOut) releaseTimer = releaseDelay;
        else
        {
            releaseTimer -= dt;
            if (releaseTimer <= 0f)
            {
                alert = 0f;
                wp = NearestWaypoint();
                SetState(State.Ronde);
            }
        }
    }

    // ---------------- Déplacement ----------------
    /// <summary>Avance vers <paramref name="target"/> ; renvoie true une fois arrivée.</summary>
    bool MoveTo(Vector3 target, float speed, float arriveDist, float dt)
    {
        Vector3 to = Flat(target - transform.position);
        float dist = to.magnitude;
        if (dist <= arriveDist) return true;
        TurnTowards(target, dt);
        // Elle ralentit si elle doit beaucoup tourner (pas de glissade de côté).
        float facing = Vector3.Dot(transform.forward, to / dist);
        float k = Mathf.Clamp01((facing + 0.2f) / 1.2f);
        Vector3 step = transform.forward * Mathf.Min(speed * k * dt, dist - arriveDist + 0.001f);
        Vector3 p = transform.position + Flat(step);
        p.y = floorY;
        transform.position = p;
        return false;
    }

    void TurnTowards(Vector3 target, float dt)
    {
        Vector3 to = Flat(target - transform.position);
        if (to.sqrMagnitude < 1e-4f) return;
        Quaternion look = Quaternion.LookRotation(to, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * dt);
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    int NearestWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0) return 0;
        int best = 0; float bd = float.MaxValue;
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null) continue;
            float d = Flat(waypoints[i].position - transform.position).sqrMagnitude;
            if (d < bd) { bd = d; best = i; }
        }
        return best;
    }

    // ---------------- Vision ----------------
    bool CanSeePlayerWithMachine()
    {
        if (cam == null || machine == null || machine.IsStored) return false;

        // La machine doit être « devant nous » (dans les mains ou posée juste devant).
        if (Vector3.Distance(machine.Center, cam.position) > machineNearPlayer) return false;

        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        Vector3 to = cam.position - eye;
        float dist = to.magnitude;
        if (dist > viewDistance) return false;
        if (Vector3.Angle(transform.forward, Flat(to)) > viewAngle * 0.5f) return false;

        // Un mur ou un obstacle entre elle et le joueur ?
        var hits = Physics.RaycastAll(eye, to / dist, dist - 0.1f, obstacles, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            Transform ht = h.transform;
            if (ht.IsChildOf(transform)) continue;
            if (rig != null && ht.IsChildOf(rig)) continue;
            if (ht.IsChildOf(machine.transform)) continue;
            return false;
        }
        return true;
    }

    // ---------------- « ! » au-dessus de la tête ----------------
    // Fait de simples formes avec des matériaux Unlit d'URP : il s'affiche correctement dans les deux yeux du casque.
    void BuildExclamation()
    {
        if (exclamation != null) exclamation.enabled = false;      // l'ancienne version (shader) n'est plus utilisée

        exRoot = new GameObject("Point exclamation VR").transform;
        exRoot.SetParent(transform, false);
        // Dessinés par-dessus tout (le plafond est juste au-dessus de sa tête), dans l'ordre : contour, orange, rouge.
        var dark = exclamationDark != null ? exclamationDark : UnlitMat(new Color(0.12f, 0.03f, 0f));
        var orange = exclamationOrange != null ? exclamationOrange : UnlitMat(new Color(1f, 0.55f, 0.05f));
        var red = exclamationRed != null ? exclamationRed : UnlitMat(new Color(0.95f, 0.06f, 0.04f));
        dark.renderQueue = 4000; orange.renderQueue = 4001; red.renderQueue = 4002;

        var list = new System.Collections.Generic.List<Renderer>();
        // Contour sombre (derrière), forme orange, puis le rouge qui monte (devant).
        list.Add(Piece("Contour barre", RectMesh(), dark, new Vector3(0f, BarBottom - 0.02f, 0.004f), new Vector3(0.13f, BarTop - BarBottom + 0.04f, 1f)));
        list.Add(Piece("Contour point", DiscMesh(), dark, new Vector3(0f, DotY, 0.004f), Vector3.one * 0.13f));
        list.Add(Piece("Barre", RectMesh(), orange, new Vector3(0f, BarBottom, 0.002f), new Vector3(0.09f, BarTop - BarBottom, 1f)));
        list.Add(Piece("Point", DiscMesh(), orange, new Vector3(0f, DotY, 0.002f), Vector3.one * 0.09f));
        var rb = Piece("Barre rouge", RectMesh(), red, new Vector3(0f, BarBottom, 0f), new Vector3(0.09f, 0f, 1f));
        var rd = Piece("Point rouge", DiscMesh(), red, new Vector3(0f, DotY, 0f), Vector3.zero);
        list.Add(rb); list.Add(rd);
        redBar = rb.transform; redDot = rd.transform;
        exRenderers = list.ToArray();
        exRoot.localScale = Vector3.one * exclamationSize;
        SetExVisible(false);
    }

    Renderer Piece(string name, Mesh mesh, Material mat, Vector3 pos, Vector3 scale)
    {
        var go = new GameObject(name);
        go.transform.SetParent(exRoot, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        return r;
    }

    static Mesh rectMesh, discMesh;

    // Rectangle de 1 x 1, le bas au pivot, visible des deux côtés.
    static Mesh RectMesh()
    {
        if (rectMesh != null) return rectMesh;
        rectMesh = new Mesh { name = "Exclamation rect" };
        rectMesh.vertices = new[] { new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f), new Vector3(-0.5f, 1f, 0f), new Vector3(0.5f, 1f, 0f) };
        rectMesh.triangles = new[] { 0, 2, 1, 1, 2, 3, 0, 1, 2, 1, 3, 2 };
        rectMesh.RecalculateBounds();
        return rectMesh;
    }

    // Disque de diamètre 1 centré sur le pivot, visible des deux côtés.
    static Mesh DiscMesh()
    {
        if (discMesh != null) return discMesh;
        const int seg = 24;
        var v = new Vector3[seg + 1];
        var t = new int[seg * 6];
        v[0] = Vector3.zero;
        for (int i = 0; i < seg; i++)
        {
            float a = i / (float)seg * Mathf.PI * 2f;
            v[i + 1] = new Vector3(Mathf.Cos(a) * 0.5f, Mathf.Sin(a) * 0.5f, 0f);
            int n = (i + 1) % seg + 1;
            t[i * 6] = 0; t[i * 6 + 1] = n; t[i * 6 + 2] = i + 1;
            t[i * 6 + 3] = 0; t[i * 6 + 4] = i + 1; t[i * 6 + 5] = n;
        }
        discMesh = new Mesh { name = "Exclamation disque", vertices = v, triangles = t };
        discMesh.RecalculateBounds();
        return discMesh;
    }

    static Material UnlitMat(Color c)
    {
        var sh = Shader.Find("SampleJojo/OverlayUnlit");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
        var m = new Material(sh);
        m.SetColor("_BaseColor", c);
        return m;
    }

    bool exVisible = true;
    void SetExVisible(bool v)
    {
        if (exVisible == v || exRenderers == null) return;
        exVisible = v;
        foreach (var r in exRenderers) if (r != null) r.enabled = v;
    }

    void UpdateExclamation(bool force)
    {
        if (exRoot == null) return;
        bool chasing = state == State.Poursuite || state == State.Attrape;
        bool show = alert > 0.001f || chasing;
        SetExVisible(show);
        if (!show) return;

        // Centré au-dessus de la tête, toujours tourné vers le joueur.
        exRoot.position = transform.position + Vector3.up * (exclamationHeight - 0.1f);
        if (cam != null)
        {
            Vector3 d = exRoot.position - cam.position;
            if (d.sqrMagnitude > 1e-4f) exRoot.rotation = Quaternion.LookRotation(d, Vector3.up);
        }

        float fill = chasing ? 1f : alert;
        // Plein : il pulse.
        float pulse = fill >= 1f ? 1f + 0.12f * Mathf.Sin(Time.time * 14f) : 1f;
        exRoot.localScale = Vector3.one * exclamationSize * pulse;

        if (force || !Mathf.Approximately(fill, shownFill))
        {
            shownFill = fill;
            // Le rouge remplit d'abord le point (en bas), puis monte dans la barre.
            float dot = Mathf.Clamp01(fill / 0.2f);
            float bar = Mathf.Clamp01((fill - 0.2f) / 0.8f);
            redDot.localScale = Vector3.one * 0.09f * dot;
            redBar.localScale = new Vector3(0.09f, (BarTop - BarBottom) * bar, 1f);
        }
    }

    // ---------------- Tests sans casque ----------------
    /// <summary>Le prof repère le joueur tout de suite et lui court dessus.</summary>
    public void TestSpot()
    {
        if (!Application.isPlaying) return;
        FindTargets();
        StartChase();
    }

    /// <summary>Le prof se place devant le joueur et le montre du doigt (sans le faire perdre).</summary>
    public void TestPoint()
    {
        if (!Application.isPlaying) return;
        StartCoroutine(TestPointRoutine());
    }

    System.Collections.IEnumerator TestPointRoutine()
    {
        yield return null;                 // Start() est passé (hauteur du sol, ronde)
        FindTargets();
        if (cam == null) yield break;
        transform.position = FrontOfPlayer();
        Vector3 d = Flat(cam.position - transform.position);
        if (d.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(d);
        releaseTimer = 999f;
        SetState(State.Attrape);
    }

    /// <summary>Le prof oublie tout et reprend sa ronde.</summary>
    public void TestReset()
    {
        if (!Application.isPlaying) return;
        alert = 0f;
        wp = NearestWaypoint();
        SetState(State.Ronde);
    }

    void OnDrawGizmos()
    {
        // Orange = ronde du cours 1, bleu = cours 2, vert = cours 3 (en jeu : la ronde actuelle en orange).
        DrawRonde(waypoints, new Color(1f, 0.5f, 0f), 0.05f);
        if (Application.isPlaying) return;
        DrawRonde(rondeCours2, new Color(0.2f, 0.5f, 1f), 0.1f);
        DrawRonde(rondeCours3, new Color(0.2f, 0.9f, 0.3f), 0.15f);
    }

    static void DrawRonde(Transform[] pts, Color col, float h)
    {
        if (pts == null || pts.Length < 2) return;
        Gizmos.color = col;
        for (int i = 0; i < pts.Length; i++)
        {
            var a = pts[i]; var b = pts[(i + 1) % pts.Length];
            if (a == null || b == null) continue;
            Gizmos.DrawSphere(a.position + Vector3.up * h, 0.12f);
            Gizmos.DrawLine(a.position + Vector3.up * h, b.position + Vector3.up * h);
        }
    }
}
