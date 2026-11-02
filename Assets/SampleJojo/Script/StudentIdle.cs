using UnityEngine;

/// <summary>
/// Élève assis : joue son animation idle en boucle (départ au hasard pour que les élèves ne bougent pas tous pareil).
/// S'il a une animation « event », il la joue de temps en temps, au hasard, puis revient à l'idle.
/// </summary>
[RequireComponent(typeof(Animator))]
public class StudentIdle : MonoBehaviour
{
    public string stateName = "Idle";
    [Tooltip("Animation jouée de temps en temps (vide = aucune).")]
    public string eventState = "";
    [Tooltip("Temps entre deux animations event (s), min et max.")]
    public Vector2 eventInterval = new Vector2(10f, 28f);
    [Tooltip("Vitesse de l'animation (un peu au hasard autour de 1).")]
    public Vector2 speedRange = new Vector2(0.9f, 1.1f);

    Animator anim;
    float next, baseSpeed = 1f;
    int eventHash;

    void Start()
    {
        anim = GetComponent<Animator>();
        anim.applyRootMotion = false;
        baseSpeed = Random.Range(speedRange.x, speedRange.y);
        anim.speed = baseSpeed;
        anim.Play(stateName, 0, Random.value);
        if (!string.IsNullOrEmpty(eventState))
        {
            eventHash = Animator.StringToHash(eventState);
            if (!anim.HasState(0, eventHash)) eventState = "";
        }
        next = Time.time + Random.Range(eventInterval.x * 0.5f, eventInterval.y);
    }

    void Update()
    {
        anim.speed = baseSpeed * SlowMo.Factor;      // café gourmand
        if (string.IsNullOrEmpty(eventState) || Time.time < next) return;
        next = Time.time + Random.Range(eventInterval.x, eventInterval.y);
        // Seulement depuis l'idle (pas pendant une transition ni pendant l'event).
        if (anim.IsInTransition(0) || anim.GetCurrentAnimatorStateInfo(0).shortNameHash == eventHash) return;
        anim.CrossFadeInFixedTime(eventState, 0.25f, 0, 0f);
    }

    [ContextMenu("Test : jouer l'animation event")]
    public void TestEvent() { if (anim != null && !string.IsNullOrEmpty(eventState)) anim.CrossFadeInFixedTime(eventState, 0.25f, 0, 0f); }
}
