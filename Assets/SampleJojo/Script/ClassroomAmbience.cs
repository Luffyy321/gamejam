using UnityEngine;

/// <summary>
/// Ambiance de la classe : musique et ventilation en boucle, et de temps en temps
/// un bruit d'élève (papier, toux, éternuement) quelque part dans la salle.
/// </summary>
public class ClassroomAmbience : MonoBehaviour
{
    [Header("Boucles")]
    public AudioClip music;
    [Range(0f, 1f)] public float musicVolume = 0.18f;
    public AudioClip ventilation;
    [Range(0f, 1f)] public float ventilationVolume = 0.25f;

    [Header("Bruits d'élèves (au hasard)")]
    public AudioClip[] randomSounds;
    [Range(0f, 1f)] public float randomVolume = 0.55f;
    [Tooltip("Temps entre deux bruits (s), min et max.")]
    public Vector2 interval = new Vector2(8f, 20f);
    [Tooltip("Les bruits viennent d'un point au hasard dans ce rayon autour de la salle (m).")]
    public float radius = 4f;

    float next;
    AudioSource oneShot;

    void Start()
    {
        Loop(music, musicVolume, 0f);
        Loop(ventilation, ventilationVolume, 0.6f);
        var go = new GameObject("Bruits d'élèves");
        go.transform.SetParent(transform, false);
        oneShot = go.AddComponent<AudioSource>();
        oneShot.playOnAwake = false;
        oneShot.spatialBlend = 1f;
        oneShot.minDistance = 1.5f;
        oneShot.maxDistance = 15f;
        oneShot.rolloffMode = AudioRolloffMode.Linear;
        next = Time.time + Random.Range(interval.x, interval.y) * 0.5f;
    }

    void Loop(AudioClip clip, float volume, float spatial)
    {
        if (clip == null) return;
        var s = gameObject.AddComponent<AudioSource>();
        s.clip = clip;
        s.loop = true;
        s.volume = volume;
        s.spatialBlend = spatial;
        s.playOnAwake = false;
        s.Play();
    }

    void Update()
    {
        if (randomSounds == null || randomSounds.Length == 0 || Time.time < next) return;
        next = Time.time + Random.Range(interval.x, interval.y);
        var clip = randomSounds[Random.Range(0, randomSounds.Length)];
        if (clip == null) return;
        Vector2 r = Random.insideUnitCircle * radius;
        oneShot.transform.position = transform.position + new Vector3(r.x, 0.9f, r.y);
        oneShot.pitch = Random.Range(0.92f, 1.08f);
        oneShot.PlayOneShot(clip, randomVolume);
    }
}
