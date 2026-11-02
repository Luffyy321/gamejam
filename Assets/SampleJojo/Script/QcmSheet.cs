using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Feuille de QCM (fin du 3e cours) : 3 questions tirées au hasard parmi les 8 du livre,
/// 3 réponses chacune ; on coche avec la pointe du crayon (une seule réponse par question).
/// </summary>
public class QcmSheet : MonoBehaviour
{
    [Tooltip("Centres des 12 cases (sur la feuille).")]
    public Transform[] boxes;
    [Tooltip("Marque (croix au crayon) de chaque case, cachée tant que la case n'est pas cochée.")]
    public GameObject[] marks;
    [Tooltip("Demi-taille d'une case (m).")]
    public float boxHalf = 0.011f;
    [Tooltip("Hauteur max de la pointe au-dessus de la feuille pour cocher (m).")]
    public float touchHeight = 0.012f;

    [Tooltip("Texte de chaque réponse (3 par question).")]
    public TMPro.TMP_Text[] labels;
    [Tooltip("Texte des 3 questions.")]
    public TMPro.TMP_Text[] questionTexts;
    public int answersPerQuestion = 3;

    [Header("Crayon")]
    public Transform pencil;
    public AudioSource audioSource;
    public AudioClip scribbleClip;

    [Header("Réponses (tirées au hasard à chaque examen)")]
    public bool[] correctAnswers = new bool[9];

    public UnityEvent<int, bool> onBoxChanged = new UnityEvent<int, bool>();

    [Header("Fin de l'examen")]
    [Tooltip("Note écrite en rouge sur la copie à la fin.")]
    public TMPro.TMP_Text noteText;
    public bool locked;

    public int QuestionCount => questionTexts != null ? questionTexts.Length : 0;

    public static QcmSheet Active { get; private set; }
    /// <summary>Examen en cours : la feuille est sur la table et on peut encore cocher.</summary>
    public bool InProgress => gameObject.activeInHierarchy && !locked && correctAnswers != null && correctAnswers.Length > 0;
    public bool Revealed { get; private set; }

    /// <summary>Antisèche : les bonnes réponses sont soulignées au crayon bleu sur la copie.</summary>
    public void RevealAnswers()
    {
        if (labels == null || correctAnswers == null) return;
        Revealed = true;
        for (int i = 0; i < labels.Length && i < correctAnswers.Length; i++)
            if (correctAnswers[i] && labels[i] != null && !labels[i].text.StartsWith("<color=#1D4FC4>"))
                labels[i].text = "<color=#1D4FC4><u>" + labels[i].text + "</u></color>";
        Debug.Log("[QCM] Antisèche : les bonnes réponses sont soulignées.");
    }

    /// <summary>Temps écoulé : on ne peut plus cocher, le pourcentage de réussite s'écrit sur la copie.</summary>
    public int Collect()
    {
        locked = true;
        int s = Score();
        bool passed = Passed(s);
        int pct = QuestionCount > 0 ? Mathf.RoundToInt(100f * s / QuestionCount) : 0;
        // La note (pourcentage) s'écrit en rouge sur la copie, avec RÉUSSI ou RATÉ.
        if (noteText != null)
        {
            noteText.text = $"{pct} %\n<size=60%>{(passed ? "RÉUSSI" : "RATÉ")}</size>";
            noteText.color = passed ? new Color(0.1f, 0.55f, 0.2f) : new Color(0.85f, 0.08f, 0.08f);
            noteText.rectTransform.sizeDelta = new Vector2(0.075f, 0.045f);
            noteText.enableAutoSizing = true; noteText.fontSizeMin = 0.05f; noteText.fontSizeMax = 0.12f;
            noteText.gameObject.SetActive(true);
            StartCoroutine(Stamp(noteText.transform));
        }
        if (audioSource != null && scribbleClip != null) audioSource.PlayOneShot(scribbleClip, 0.9f);
        Debug.Log($"[QCM] Copie ramassée : {s} / {QuestionCount} = {pct} % → {(passed ? "RÉUSSI" : "RATÉ")}.");
        return s;
    }

    bool[] state;
    int touching = -1;
    float lastToggle;
    Vector3 tipA, tipB;          // les deux bouts du crayon (repère local du crayon)
    XRGrabInteractable pencilGrab;

    public bool IsChecked(int i) => state != null && i >= 0 && i < state.Length && state[i];

    void OnDestroy() { if (Active == this) Active = null; }

    void Awake()
    {
        Active = this;
        state = new bool[boxes != null ? boxes.Length : 0];
        if (noteText != null) noteText.gameObject.SetActive(false);   // caché jusqu'à la fin de l'examen
        foreach (var m in marks) if (m != null) m.SetActive(false);
    }

    void Start()
    {
        if (pencil == null) { var p = GameObject.Find("Crayon"); if (p != null) pencil = p.transform; }
        if (pencil != null)
        {
            pencilGrab = pencil.GetComponentInParent<XRGrabInteractable>();
            // Bouts du crayon = extrémités de son axe le plus long.
            var mf = pencil.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                var b = mf.sharedMesh.bounds;
                Vector3 axis = b.size.x >= b.size.y && b.size.x >= b.size.z ? Vector3.right : b.size.y >= b.size.z ? Vector3.up : Vector3.forward;
                float half = Vector3.Dot(b.extents, axis);
                tipA = pencil.InverseTransformPoint(mf.transform.TransformPoint(b.center + axis * half));
                tipB = pencil.InverseTransformPoint(mf.transform.TransformPoint(b.center - axis * half));
            }
        }
    }

    void Update()
    {
        if (pencil == null || boxes == null || locked) return;
        if (pencilGrab != null && !pencilGrab.isSelected) { touching = -1; return; }   // il faut tenir le crayon
        // Le bout le plus proche de la feuille sert de pointe.
        Vector3 a = transform.InverseTransformPoint(pencil.TransformPoint(tipA));
        Vector3 b = transform.InverseTransformPoint(pencil.TransformPoint(tipB));
        Vector3 tip = Mathf.Abs(a.y) < Mathf.Abs(b.y) ? a : b;   // y local = hauteur au-dessus de la feuille
        int hit = -1;
        if (tip.y > -0.01f && tip.y < touchHeight)
            for (int i = 0; i < boxes.Length; i++)
            {
                Vector3 c = transform.InverseTransformPoint(boxes[i].position);
                if (Mathf.Abs(tip.x - c.x) < boxHalf * 1.3f && Mathf.Abs(tip.z - c.z) < boxHalf * 1.3f) { hit = i; break; }
            }
        if (hit != touching)
        {
            touching = hit;
            if (hit >= 0 && Time.time - lastToggle > 0.25f) Toggle(hit);
        }
    }

    public void Toggle(int i)
    {
        if (i < 0 || i >= state.Length) return;
        lastToggle = Time.time;
        state[i] = !state[i];
        // Une seule réponse par question : cocher une case décoche les autres de la même question.
        if (state[i] && answersPerQuestion > 1)
        {
            int q0 = i / answersPerQuestion * answersPerQuestion;
            for (int k = q0; k < q0 + answersPerQuestion && k < state.Length; k++)
                if (k != i && state[k]) { state[k] = false; if (marks != null && k < marks.Length && marks[k] != null) marks[k].SetActive(false); }
        }
        if (marks != null && i < marks.Length && marks[i] != null) marks[i].SetActive(state[i]);
        if (audioSource != null && scribbleClip != null) audioSource.PlayOneShot(scribbleClip, 0.7f);
        onBoxChanged?.Invoke(i, state[i]);
        Debug.Log($"[QCM] Case {i + 1} {(state[i] ? "cochée" : "décochée")}.");
    }

    /// <summary>Tire 3 questions au hasard parmi celles du niveau et mélange leurs réponses.</summary>
    public void PickQuestions()
    {
        var bank = QcmBank.Level1;
        var order = new System.Collections.Generic.List<int>();
        for (int i = 0; i < bank.Length; i++) order.Add(i);
        for (int i = order.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (order[i], order[j]) = (order[j], order[i]); }
        int nq = questionTexts != null ? questionTexts.Length : 0;
        correctAnswers = new bool[nq * answersPerQuestion];
        var log = new System.Text.StringBuilder("[QCM] Questions : ");
        for (int q = 0; q < nq && q < order.Count; q++)
        {
            var qu = bank[order[q]];
            if (questionTexts[q] != null) questionTexts[q].text = $"<b>{q + 1}. {qu.text}</b>";
            var ans = new System.Collections.Generic.List<int> { 0, 1, 2 };
            for (int i = ans.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (ans[i], ans[j]) = (ans[j], ans[i]); }
            for (int a = 0; a < answersPerQuestion && a < ans.Count; a++)
            {
                int box = q * answersPerQuestion + a;
                if (labels != null && box < labels.Length && labels[box] != null) labels[box].text = qu.answers[ans[a]];
                correctAnswers[box] = ans[a] == qu.correct;
            }
            log.Append($"Q{order[q] + 1} ");
        }
        Debug.Log(log.ToString());
    }

    /// <summary>Réussi si au moins 50 % des questions sont justes.</summary>
    public bool Passed(int score) => QuestionCount > 0 && score * 2 >= QuestionCount;

    /// <summary>Nombre de bonnes réponses cochées (sur le nombre de questions).</summary>
    public int Score()
    {
        int s = 0;
        for (int i = 0; i < state.Length && i < correctAnswers.Length; i++) if (state[i] && correctAnswers[i]) s++;
        return s;
    }

    /// <summary>La feuille apparaît (glisse et se pose sur la table).</summary>
    public void Show()
    {
        gameObject.SetActive(true);
        PickQuestions();
        StartCoroutine(Appear());
    }

    IEnumerator Stamp(Transform t)
    {
        Vector3 s1 = t.localScale;
        for (float k = 0f; k < 1f; k += Time.deltaTime / 0.35f)
        {
            t.localScale = s1 * (Mathf.Lerp(1.8f, 1f, Mathf.SmoothStep(0f, 1f, k)));
            yield return null;
        }
        t.localScale = s1;
    }

    IEnumerator Appear()
    {
        Vector3 p1 = transform.position, s1 = transform.localScale;
        Vector3 p0 = p1 + Vector3.up * 0.25f;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.6f)
        {
            float e = Mathf.SmoothStep(0f, 1f, t);
            transform.position = Vector3.Lerp(p0, p1, e);
            transform.localScale = s1 * Mathf.Lerp(0.3f, 1f, e);
            yield return null;
        }
        transform.position = p1; transform.localScale = s1;
    }

    [ContextMenu("Test : cocher la case 1")] void TestFirst() => Toggle(0);
}
