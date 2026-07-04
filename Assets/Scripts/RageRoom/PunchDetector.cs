using System;
using UnityEngine;

/// <summary>
/// Independent module: watches raw acceleration magnitude from a
/// HandInputProvider and fires a discrete event when it spikes past a
/// threshold. Knows nothing about movement, velocity, or hitboxes — it only
/// classifies the incoming acceleration signal, so it works unchanged
/// whether the source is KeyboardHandInput or a real BNO055.
///
/// punchThreshold must sit below the provider's weakest punch spike
/// (KeyboardHandInput.minPunchAccel) so even an uncharged tap reliably
/// crosses it, with enough margin that sensor noise at rest can't
/// false-trigger one. fullStrengthAccel should match the provider's
/// strongest spike (KeyboardHandInput.maxPunchAccel) so a fully-charged
/// punch reads as exactly strength 1.0.
/// </summary>
public class PunchDetector : MonoBehaviour
{
    public HandInputProvider input;

    [Tooltip("Acceleration magnitude (m/s²) that counts as a punch.")]
    public float punchThreshold = 60f;

    [Tooltip("Acceleration magnitude that counts as a full-strength (1.0) punch, " +
             "for normalising the event's strength value. Match this to the " +
             "input provider's maximum charged spike (e.g. KeyboardHandInput.maxPunchAccel).")]
    public float fullStrengthAccel = 150f;

    [Tooltip("Minimum time between punches (s). Prevents one sustained spike " +
             "from firing more than once while it stays above threshold.")]
    public float cooldown = 0.25f;

    /// <summary>Fired once per punch with strength normalised to [0,1].</summary>
    public event Action<float> OnPunch;

    float cooldownTimer;
    bool  armed = true;

    void FixedUpdate()
    {
        if (cooldownTimer > 0f)
            cooldownTimer -= Time.fixedDeltaTime;

        float mag = input.GetAcceleration().magnitude;

        // Re-arm only once the signal drops back under threshold, so a
        // spike that stays high across several frames still fires exactly
        // one punch instead of one per frame.
        if (mag < punchThreshold)
        {
            armed = true;
            return;
        }

        if (!armed || cooldownTimer > 0f) return;

        armed         = false;
        cooldownTimer = cooldown;
        Debug.Log($"[Punch] {name}: PunchDetector fired, mag={mag:F1}", this);
        OnPunch?.Invoke(Mathf.Clamp01(mag / fullStrengthAccel));
    }
}
