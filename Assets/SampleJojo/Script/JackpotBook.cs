using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Livre ouvert posé sur la table de la machine à sous.
/// - Pincer la page de droite et la tirer vers la gauche : page suivante (la feuille suit la main).
/// - Pincer la page de gauche et la tirer vers la droite : page précédente.
/// - Un pincement rapide tourne la page d'un coup.
/// - Tant que la machine est posée dessus (emplacement occupé), le livre est bloqué.
/// Le texte des pages se modifie dans l'Inspector (liste « Pages »).
/// </summary>
public class JackpotBook : MonoBehaviour
{
    [Header("Contenu (une case = une page, dans l'ordre)")]
    [TextArea(3, 15)]
    public string[] pages =
    {
        "<b>Le livre du Jackpot</b>\n\n<i>(texte à venir)</i>",
        "Page 2\n\n<i>(texte à venir)</i>",
        "Page 3\n\n<i>(texte à venir)</i>",
        "Page 4\n\n<i>(texte à venir)</i>",
        "Page 5\n\n<i>(texte à venir)</i>",
        "Page 6\n\n<i>(texte à venir)</i>",
        "Page 7\n\n<i>(texte à venir)</i>",
        "Page 8\n\n<i>(texte à venir)</i>",
        "Page 9\n\n<i>(texte à venir)</i>",
        "Page 10\n\n<i>(texte à venir)</i>",
    };

    [Header("Image de page")]
    [Tooltip("Une page dont le texte est « #IMG » affiche cette image à la place du texte.")]
    public Material pageImage;

    [Header("Pièces du livre")]
    public TMP_Text leftText;
    public TMP_Text rightText;
    [Tooltip("Feuille qui tourne : pivot sur la reliure, la page s'étend vers +X.")]
    public Transform leaf;
    public TMP_Text leafFrontText;
    public TMP_Text leafBackText;
    public XRSimpleInteractable rightZone;
    public XRSimpleInteractable leftZone;

    [Header("Blocage par la machine")]
    public JackpotSocket blockingSocket;

    [Header("Réglages")]
    public float flipSpeed = 420f;          // degrés par seconde pour finir le geste
    public float followSpeed = 900f;        // vitesse max à laquelle la feuille suit la main
    public AudioSource audioSource;
    public AudioClip flipClip;

    int spread;                 // double page affichée : gauche = 2*spread, droite = 2*spread+1
    int dir;                    // +1 = vers l'avant, -1 = vers l'arrière, 0 = aucune
    float angle;                // 0 = feuille à droite, 180 = feuille à gauche
    float grabTime;
    bool blocked;
    IXRSelectInteractor hand;
    Coroutine finishing;
    readonly List<Collider> zoneColliders = new List<Collider>();

    public bool IsBlocked => blocked;
    public int Spread => spread;
    /// <summary>Une page vient d'être tournée (paramètre : nouvelle double page).</summary>
    public UnityEngine.Events.UnityEvent<int> onPageTurned = new UnityEngine.Events.UnityEvent<int>();
    int SpreadCount => Mathf.Max(1, (pages != null ? pages.Length + 1 : 1) / 2);
    string Page(int i) => Format(pages != null && i >= 0 && i < pages.Length ? pages[i] : "");

    // ---------------- Mots clés (lunettes 3D) ----------------
    /// <summary>Lunettes 3D portées : les mots clés (les réponses du test) sont en gras et surlignés.</summary>
    public static bool ShowKeywords { get; private set; }
    static readonly List<JackpotBook> all = new List<JackpotBook>();

    public static void SetKeywords(bool on)
    {
        ShowKeywords = on;
        foreach (var b in all) if (b != null && b.dir == 0) b.Refresh();
    }

    /// <summary>
    /// Le texte du livre n'est plus en gras. Dans les pages, le titre est le premier « &lt;b&gt;…&lt;/b&gt; » en début de page
    /// (il reste normal, un peu plus grand) ; les autres « &lt;b&gt;…&lt;/b&gt; » sont les mots clés :
    /// normaux sans les lunettes 3D, en gras surlignés avec.
    /// </summary>
    static string Format(string t)
    {
        if (string.IsNullOrEmpty(t) || t.StartsWith("#IMG")) return t;
        var sb = new System.Text.StringBuilder();
        int i = 0;
        bool title = true;
        while (i < t.Length)
        {
            int a = t.IndexOf("<b>", i, System.StringComparison.Ordinal);
            if (a < 0) { sb.Append(t, i, t.Length - i); break; }
            int b = t.IndexOf("</b>", a, System.StringComparison.Ordinal);
            if (b < 0) { sb.Append(t, i, t.Length - i); break; }
            string before = t.Substring(i, a - i);
            string word = t.Substring(a + 3, b - a - 3);
            sb.Append(before);
            bool isTitle = title && before.Trim().Length == 0 || before.EndsWith("size=170%>") || before.EndsWith("size=140%>");
            if (isTitle) sb.Append("<size=115%>").Append(word).Append("</size>");
            else if (ShowKeywords) sb.Append("<b><mark=#FFD23ACC>").Append(word).Append("</mark></b>");
            else sb.Append(word);
            title = false;
            i = b + 4;
        }
        return sb.ToString();
    }

    void OnDestroy() => all.Remove(this);

    void Awake()
    {
        all.Add(this);
        if (rightZone != null) zoneColliders.AddRange(rightZone.GetComponentsInChildren<Collider>(true));
        if (leftZone != null) zoneColliders.AddRange(leftZone.GetComponentsInChildren<Collider>(true));
        if (leaf != null) leaf.gameObject.SetActive(false);
        Refresh();
    }

    void OnEnable()
    {
        if (rightZone != null) { rightZone.selectEntered.AddListener(OnRightGrab); rightZone.selectExited.AddListener(OnRelease); }
        if (leftZone != null) { leftZone.selectEntered.AddListener(OnLeftGrab); leftZone.selectExited.AddListener(OnRelease); }
    }

    void OnDisable()
    {
        if (rightZone != null) { rightZone.selectEntered.RemoveListener(OnRightGrab); rightZone.selectExited.RemoveListener(OnRelease); }
        if (leftZone != null) { leftZone.selectEntered.RemoveListener(OnLeftGrab); leftZone.selectExited.RemoveListener(OnRelease); }
    }

    // Pages-images : un quad posé sur la page, affiché quand son texte est « #IMG ».
    readonly Dictionary<TMP_Text, GameObject> imageQuads = new Dictionary<TMP_Text, GameObject>();

    void LateUpdate()
    {
        ShowImage(leftText); ShowImage(rightText); ShowImage(leafFrontText); ShowImage(leafBackText);
    }

    void ShowImage(TMP_Text t)
    {
        if (t == null || pageImage == null) return;
        bool img = t.text != null && t.text.StartsWith("#IMG");
        imageQuads.TryGetValue(t, out var q);
        if (img && q == null)
        {
            q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "Image page";
            Destroy(q.GetComponent<Collider>());
            q.transform.SetParent(t.transform, false);
            var r = t.rectTransform.rect;
            float size = Mathf.Min(r.width, r.height) * 0.9f;
            q.transform.localPosition = new Vector3(r.center.x, r.center.y, -0.001f);
            q.transform.localRotation = Quaternion.identity;
            q.transform.localScale = new Vector3(size, size, 1f);
            var mr = q.GetComponent<MeshRenderer>();
            mr.sharedMaterial = pageImage;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            imageQuads[t] = q;
        }
        if (q != null && q.activeSelf != img) q.SetActive(img);
        if (t.enabled == img) t.enabled = !img;      // pas de « #IMG » écrit sur la page
    }

    void Refresh()
    {
        if (leftText != null) leftText.text = Page(2 * spread);
        if (rightText != null) rightText.text = Page(2 * spread + 1);
    }

    // ---------------- Début du geste ----------------
    void OnRightGrab(SelectEnterEventArgs args) => StartForward(args.interactorObject);
    void OnLeftGrab(SelectEnterEventArgs args) => StartBackward(args.interactorObject);

    bool StartForward(IXRSelectInteractor interactor)
    {
        if (blocked || dir != 0 || spread >= SpreadCount - 1) return false;
        dir = 1;
        BeginFlip(interactor, 0f);
        leafFrontText.text = Page(2 * spread + 1);       // la page de droite actuelle part vers la gauche
        leafBackText.text = Page(2 * spread + 2);        // son dos sera la nouvelle page de gauche
        rightText.text = Page(2 * spread + 3);           // dessous : la nouvelle page de droite
        return true;
    }

    bool StartBackward(IXRSelectInteractor interactor)
    {
        if (blocked || dir != 0 || spread <= 0) return false;
        dir = -1;
        BeginFlip(interactor, 180f);
        leafFrontText.text = Page(2 * spread - 1);       // revient à droite : l'ancienne page de droite
        leafBackText.text = Page(2 * spread);            // dos = page de gauche actuelle
        leftText.text = Page(2 * spread - 2);            // dessous : l'ancienne page de gauche
        return true;
    }

    /// <summary>Test sans casque : tourne une page (+1 = suivante, -1 = précédente).</summary>
    public void TestTurn(int direction)
    {
        if (direction > 0 ? StartForward(null) : StartBackward(null))
            Finish(direction > 0 ? 180f : 0f);
        else
            Debug.Log("[Jackpot] Livre : impossible de tourner la page (" + (blocked ? "bloqué par la machine" : "début/fin du livre") + ").");
    }

    void BeginFlip(IXRSelectInteractor interactor, float startAngle)
    {
        hand = interactor;
        grabTime = Time.time;
        angle = startAngle;
        if (finishing != null) StopCoroutine(finishing);
        leaf.gameObject.SetActive(true);
        SetLeaf(angle);
        if (audioSource != null && flipClip != null) audioSource.PlayOneShot(flipClip);
    }

    // ---------------- La feuille suit la main ----------------
    void Update()
    {
        bool b = blockingSocket != null && blockingSocket.hasSelection;
        if (b != blocked)
        {
            blocked = b;
            foreach (var c in zoneColliders) if (c != null) c.enabled = !b;
            if (b && dir != 0)
            {
                hand = null;
                Finish(dir == 1 ? 0f : 180f);            // la machine arrive : on remet la page en place
            }
        }

        if (hand == null || dir == 0 || finishing != null) return;
        var zone = dir == 1 ? rightZone : leftZone;
        Vector3 p = transform.InverseTransformPoint(hand.GetAttachTransform(zone).position) - leaf.localPosition;
        float target = Mathf.Atan2(Mathf.Max(0f, p.y), p.x) * Mathf.Rad2Deg;       // 0 = à droite, 180 = à gauche
        angle = Mathf.MoveTowards(angle, Mathf.Clamp(target, 0f, 180f), followSpeed * Time.deltaTime);
        SetLeaf(angle);
    }

    void OnRelease(SelectExitEventArgs args)
    {
        if (args.interactorObject != hand || dir == 0) return;
        hand = null;
        bool quick = Time.time - grabTime < 0.35f;        // simple pincement : on tourne la page d'un coup
        float target = dir == 1
            ? (quick || angle > 70f ? 180f : 0f)
            : (quick || angle < 110f ? 0f : 180f);
        Finish(target);
    }

    void Finish(float target)
    {
        if (finishing != null) StopCoroutine(finishing);
        finishing = StartCoroutine(FinishRoutine(target));
    }

    IEnumerator FinishRoutine(float target)
    {
        while (!Mathf.Approximately(angle, target))
        {
            angle = Mathf.MoveTowards(angle, target, flipSpeed * Time.deltaTime);
            SetLeaf(angle);
            yield return null;
        }
        bool completed = (dir == 1 && target >= 180f) || (dir == -1 && target <= 0f);
        if (completed) { spread = Mathf.Clamp(spread + dir, 0, SpreadCount - 1); onPageTurned?.Invoke(spread); }
        dir = 0;
        leaf.gameObject.SetActive(false);
        Refresh();
        finishing = null;
    }

    void SetLeaf(float a) => leaf.localRotation = Quaternion.AngleAxis(a, Vector3.forward);

    /// <summary>Change le texte d'une page depuis un autre script (index à partir de 0).</summary>
    public void SetPage(int index, string text)
    {
        if (pages == null || index < 0) return;
        if (index >= pages.Length) System.Array.Resize(ref pages, index + 1);
        pages[index] = text;
        if (dir == 0) Refresh();
    }
}
