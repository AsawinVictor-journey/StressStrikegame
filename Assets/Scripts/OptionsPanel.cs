using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Binds the Options panel's two sliders to GameAudioSettings.
///
/// The sliders are the source of truth for the UI only -- the actual values live
/// in GameAudioSettings (PlayerPrefs + mixer). This reads them on open rather than
/// trusting whatever the sliders were left at in the scene, so the panel always
/// shows the player's real settings.
/// </summary>
public class OptionsPanel : MonoBehaviour
{
    [Header("Sliders")]
    public Slider musicSlider;
    public Slider punchSlider;

    [Header("Step Buttons")]
    public Button musicMinus;
    public Button musicPlus;
    public Button punchMinus;
    public Button punchPlus;

    [Tooltip("How much one - / + press moves the slider.")]
    [Range(0.01f, 0.5f)] public float step = 0.1f;

    // Guards the read-back in OnEnable: assigning slider.value fires onValueChanged,
    // which would write straight back into GameAudioSettings and, on a fresh install,
    // persist a default as though the player had chosen it.
    bool _applying;

    void Awake()
    {
        if (musicSlider != null) musicSlider.onValueChanged.AddListener(OnMusicChanged);
        if (punchSlider != null) punchSlider.onValueChanged.AddListener(OnPunchChanged);

        Wire(musicMinus, () => Step(musicSlider, -step));
        Wire(musicPlus,  () => Step(musicSlider, +step));
        Wire(punchMinus, () => Step(punchSlider, -step));
        Wire(punchPlus,  () => Step(punchSlider, +step));
    }

    void OnEnable()
    {
        _applying = true;
        if (musicSlider != null) musicSlider.value = GameAudioSettings.Music01;
        if (punchSlider != null) punchSlider.value = GameAudioSettings.Punch01;
        _applying = false;
    }

    static void Wire(Button b, UnityEngine.Events.UnityAction call)
    {
        if (b != null) b.onClick.AddListener(call);
    }

    void Step(Slider s, float delta)
    {
        if (s == null) return;
        s.value = Mathf.Clamp01(s.value + delta);   // onValueChanged does the rest
    }

    void OnMusicChanged(float v)
    {
        if (_applying) return;
        GameAudioSettings.SetMusic(v);
    }

    void OnPunchChanged(float v)
    {
        if (_applying) return;
        GameAudioSettings.SetPunch(v);
    }
}
