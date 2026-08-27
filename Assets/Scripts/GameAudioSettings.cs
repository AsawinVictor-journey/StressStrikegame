using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Owns music/SFX volume: persists it, and applies it to the AudioMixer in every
/// scene. Spawns itself via RuntimeInitializeOnLoadMethod (same approach as
/// EscapeToModeMenu) so no scene has to carry a copy and entering any scene
/// directly in the Editor still applies the player's settings.
///
/// Volumes are stored 0..1 (what a slider hands you) and converted to decibels on
/// the way out. That conversion is NOT optional: mixer volume is logarithmic, so
/// feeding a linear 0..1 straight in makes the whole usable range sit in the top
/// few percent of slider travel and everything below read as silence.
/// </summary>
public class GameAudioSettings : MonoBehaviour
{
    public const string MusicParam = "MusicVolume";
    public const string PunchParam = "PunchVolume";
    public const string MasterParam = "MasterVolume";

    const string MusicPref = "Audio_Music01";
    const string PunchPref = "Audio_Punch01";

    const float SilenceDb = -80f;

    // The mixer carries a hand-tuned balance (Master -15dB, Punch +11dB at time of
    // writing). A slider at 100% must restore exactly that, never 0dB -- otherwise
    // "full volume" is LOUDER than the mix was ever authored to be.
    //
    // Read from the mixer at Awake rather than hardcoded: Awake runs before this
    // class has written anything, so whatever is in the mixer then IS the authored
    // value (runtime SetFloat never persists back to the asset). That also means
    // this keeps working when the Music group is added, with no constant to update.
    float _musicMaxDb, _punchMaxDb;

    static GameAudioSettings _instance;
    AudioMixer _mixer;

    public static float Music01 { get; private set; } = 1f;
    public static float Punch01 { get; private set; } = 1f;

    /// <summary>Raised whenever a volume changes, so any open Options panel can follow along.</summary>
    public static event System.Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (_instance != null) return;
        var go = new GameObject("GameAudioSettings (auto-created)");
        _instance = go.AddComponent<GameAudioSettings>();
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        // Via a Resources-loadable handle rather than Resources.Load on the mixer
        // itself: the mixer lives in Assets/Audio/, which Resources cannot see, and
        // moving a shared asset just to satisfy that would churn a file other work
        // may be touching. See AudioMixerRef.
        var handle = Resources.Load<AudioMixerRef>("AudioMixerRef");
        _mixer = handle != null ? handle.mixer : null;
        if (_mixer == null)
        {
            Debug.LogWarning("[GameAudioSettings] Assets/Resources/AudioMixerRef.asset is missing or has no " +
                             "mixer assigned -- volume will persist but will not reach the mixer.", this);
        }

        CaptureAuthoredLevels();

        Music01 = PlayerPrefs.GetFloat(MusicPref, 1f);
        Punch01 = PlayerPrefs.GetFloat(PunchPref, 1f);
        Apply();
    }

    public static void SetMusic(float v01)
    {
        Music01 = Mathf.Clamp01(v01);
        PlayerPrefs.SetFloat(MusicPref, Music01);
        PlayerPrefs.Save();
        if (_instance != null) _instance.Apply();
        var c = Changed; if (c != null) c();
    }

    public static void SetPunch(float v01)
    {
        Punch01 = Mathf.Clamp01(v01);
        PlayerPrefs.SetFloat(PunchPref, Punch01);
        PlayerPrefs.Save();
        if (_instance != null) _instance.Apply();
        var c = Changed; if (c != null) c();
    }

    // Must run before the first Apply(), while the mixer still holds design values.
    void CaptureAuthoredLevels()
    {
        _musicMaxDb = 0f;
        _punchMaxDb = 0f;
        if (_mixer == null) return;

        // Music prefers its own group; falls back to Master while that group does
        // not exist yet -- matching what Apply() drives.
        if (!_mixer.GetFloat(MusicParam, out _musicMaxDb))
            _mixer.GetFloat(MasterParam, out _musicMaxDb);

        _mixer.GetFloat(PunchParam, out _punchMaxDb);
    }

    void Apply()
    {
        if (_mixer == null) return;

        // MusicVolume may not exist yet -- the mixer currently has only Master and
        // Punch groups. SetFloat returns false rather than throwing, so this
        // degrades to "music slider does nothing" instead of erroring every frame.
        if (!_mixer.SetFloat(MusicParam, ToDecibels(Music01, _musicMaxDb)))
            _mixer.SetFloat(MasterParam, ToDecibels(Music01, _musicMaxDb));

        _mixer.SetFloat(PunchParam, ToDecibels(Punch01, _punchMaxDb));
    }

    static float ToDecibels(float v01, float maxDb)
    {
        if (v01 <= 0.0001f) return SilenceDb;   // log10(0) is -infinity
        return maxDb + Mathf.Log10(v01) * 20f;
    }
}
