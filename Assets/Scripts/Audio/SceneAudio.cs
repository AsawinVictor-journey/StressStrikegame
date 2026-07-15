using System.Collections;
using UnityEngine;

/// <summary>
/// Plays one looping track for a scene, with an optional fade-in on load. Use it
/// for a music soundtrack (Rage Room, Boxing, Meditate, Menu) OR an ambient bed
/// (Yoga) — it's the same thing mechanically, just a different clip. Put one on a
/// GameObject in the scene and assign the clip.
///
/// For layered audio (e.g. yoga wants ONLY ambient, another scene wants music +
/// ambient), just add two of these with different clips.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class SceneAudio : MonoBehaviour
{
    [Tooltip("The looping track: a soundtrack, or an ambient loop (e.g. yoga).")]
    public AudioClip clip;

    [Range(0f, 1f)]
    [Tooltip("Target volume once faded in. Keep ambient/music lower than SFX (~0.3-0.5).")]
    public float volume = 0.4f;

    [Tooltip("Seconds to fade up from silence on scene start. 0 = start at full volume.")]
    public float fadeInDuration = 0.1f;

    AudioSource source;

    void Awake()
    {
        source = GetComponent<AudioSource>();
        source.clip        = clip;
        source.loop        = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f; // 2D — plays everywhere, not positional
    }

    void Start()
    {
        if (clip == null)
        {
            Debug.LogWarning($"[SceneAudio] {name}: no clip assigned.", this);
            return;
        }

        source.volume = fadeInDuration > 0f ? 0f : volume;
        source.Play();

        if (fadeInDuration > 0f)
            StartCoroutine(FadeIn());
    }

    IEnumerator FadeIn()
    {
        float t = 0f;
        while (t < fadeInDuration)
        {
            t += Time.deltaTime;
            source.volume = Mathf.Lerp(0f, volume, t / fadeInDuration);
            yield return null;
        }
        source.volume = volume;
    }
}
