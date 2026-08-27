using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// A Resources-loadable handle to the project's AudioMixer.
///
/// GameAudioSettings spawns itself from code, so it has no Inspector to wire a
/// mixer reference into, and Resources.Load cannot see Assets/Audio/. Rather than
/// MOVING the shared mixer into a Resources folder -- which would churn a file
/// other work may be touching -- this tiny asset lives in Resources and points at
/// the mixer where it already is.
/// </summary>
[CreateAssetMenu(fileName = "AudioMixerRef", menuName = "Audio/Mixer Reference")]
public class AudioMixerRef : ScriptableObject
{
    public AudioMixer mixer;
}
