using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Compte à rebours affiché sur les horloges digitales de la classe (00:03:00 → 00:00:00 au lancement).
/// À zéro, l'affichage clignote et l'événement onTimeUp est appelé (pour brancher la suite du jeu).
/// </summary>
public class ClassroomCountdown : MonoBehaviour
{
    [Tooltip("Durée du compte à rebours, en secondes (180 = 3 minutes).")]
    public float durationSeconds = 180f;
    public bool startOnPlay = true;
    [Tooltip("Les « : » clignotent chaque seconde, comme une vraie horloge.")]
    public bool blinkColons = true;
    public SevenSegmentDisplay[] clocks;
    [Tooltip("Optionnel : textes en plus des horloges.")]
    public TMP_Text[] displays;
    public UnityEvent onTimeUp;

    float remaining;
    bool running, finished;
    int shownSeconds = -1;
    bool shownColons, shownVisible = true;

    public float Remaining => remaining;
    public bool IsFinished => finished;

    void Start()
    {
        remaining = durationSeconds;
        running = startOnPlay;
        Show(Mathf.CeilToInt(remaining), true, true, true);
    }

    void Update()
    {
        if (finished)
        {
            // 00:00:00 qui clignote.
            bool visible = Mathf.Repeat(Time.time, 1f) < 0.6f;
            if (visible != shownVisible) Show(0, true, visible, true);
            return;
        }
        if (!running) return;
        remaining -= Time.deltaTime * SlowMo.Factor;     // café gourmand : le chrono ralentit
        if (remaining <= 0f)
        {
            remaining = 0f;
            finished = true;
            running = false;
            Show(0, true, true, true);
            onTimeUp?.Invoke();
            return;
        }
        int s = Mathf.CeilToInt(remaining);
        bool colons = !blinkColons || Mathf.Repeat(remaining, 1f) > 0.5f;
        Show(s, colons, true, false);
    }

    void Show(int totalSeconds, bool colons, bool visible, bool force)
    {
        if (!force && totalSeconds == shownSeconds && colons == shownColons && visible == shownVisible) return;
        shownSeconds = totalSeconds; shownColons = colons; shownVisible = visible;
        if (clocks != null)
            foreach (var c in clocks)
            {
                if (c == null) continue;
                if (visible) c.ShowTime(totalSeconds, colons); else c.Blank();
            }
        if (displays != null)
        {
            string txt = (totalSeconds / 60) + ":" + (totalSeconds % 60).ToString("00");
            foreach (var d in displays) if (d != null) { d.text = txt; d.enabled = visible; }
        }
    }

    /// <summary>Relance le compte à rebours depuis le début.</summary>
    public void Restart()
    {
        remaining = durationSeconds;
        finished = false;
        running = true;
        Show(Mathf.CeilToInt(remaining), true, true, true);
    }

    /// <summary>Lance un compte à rebours d'une durée donnée (ex. 60 s pour l'examen).</summary>
    public void StartCountdown(float seconds)
    {
        remaining = seconds;
        finished = false;
        running = true;
        Show(Mathf.CeilToInt(remaining), true, true, true);
    }

    public void Pause() => running = false;
    public void Resume() { if (!finished) running = true; }
}
