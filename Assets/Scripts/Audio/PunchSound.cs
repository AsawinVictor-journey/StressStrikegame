using UnityEngine;

/// <summary>
/// Plays a punch sound whenever a hand throws a punch. Subscribes to each
/// PunchDetector's OnPunch event (same event that drives the hand lunge), so it
/// fires for mouse punches and glove punches alike. A random clip is chosen per
/// punch and its volume scales with punch strength, so light taps are quieter
/// than full-charged hits.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class PunchSound : MonoBehaviour
{
    [Tooltip("The punch detectors to listen to — drag in both hands' PunchDetector.")]
    public PunchDetector[] detectors;

    [Tooltip("Punch clips. One is picked at random each punch so it doesn't repeat.")]
    public AudioClip[] clips;

    [Range(0f, 1f)] public float minVolume = 0.5f;
    [Range(0f, 1f)] public float maxVolume = 1f;

    [Tooltip("Random +/- pitch variation per hit, so repeated punches don't sound identical.")]
    [Range(0f, 0.3f)]
    public float pitchJitter = 0.08f;

    AudioSource source;

    void Awake() => source = GetComponent<AudioSource>();

    void OnEnable()
    {
        if (detectors == null) return;
        foreach (var d in detectors)
            if (d != null) d.OnPunch += Play;
    }

    void OnDisable()
    {
        if (detectors == null) return;
        foreach (var d in detectors)
            if (d != null) d.OnPunch -= Play;
    }

    void Play(float strength)
    {
        if (clips == null || clips.Length == 0) return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];
        float volume   = Mathf.Lerp(minVolume, maxVolume, Mathf.Clamp01(strength));

        source.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        source.PlayOneShot(clip, volume);
    }
}
