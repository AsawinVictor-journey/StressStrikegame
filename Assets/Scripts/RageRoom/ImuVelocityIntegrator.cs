using UnityEngine;

/// <summary>
/// Converts a raw acceleration signal (simulated or real IMU) into a bounded,
/// damped velocity. This is the whole "IMU processing" layer: it knows
/// nothing about keyboards, sensors, workspace bounds, or punches — only
/// acceleration in, velocity out.
///
/// Deliberately NOT a MonoBehaviour. It has no scene presence and no
/// lifecycle needs, so HandTarget owns an instance and steps it explicitly.
/// That keeps it trivially reusable by anything else that wants an inertial
/// velocity from an acceleration source (a second hand, a head node, etc.),
/// and makes the accel→velocity math testable in isolation.
///
/// Pipeline per step, matching the IMU processing spec:
///   1. Deadzone  — acceleration below this magnitude is treated as zero, so
///      sensor noise at rest can't accumulate into drift.
///   2. Smoothing — optional low-pass filter so a jittery real accelerometer
///      doesn't produce a jittery velocity. 1 = no smoothing (instant).
///   3. Integrate — velocity += filteredAccel * dt.
///   4. Damping   — velocity *= dampingFactor every step, independent of
///      acceleration. This is what prevents undamped integration from
///      letting velocity (and therefore position) drift forever from sensor
///      bias — the classic IMU dead-reckoning failure. Bias bleeds off every
///      step instead of accumulating.
///   5. Clamp     — hard cap at maxSpeed.
/// </summary>
[System.Serializable]
public class ImuVelocityIntegrator
{
    [Tooltip("Acceleration magnitude (m/s²) below which input is treated as " +
             "zero. Filters sensor noise at rest so it can't accumulate into drift.")]
    public float deadzone = 0.5f;

    [Tooltip("Low-pass filter strength applied to acceleration before integrating. " +
             "1 = no smoothing (instant response). Lower = smoother but laggier — " +
             "useful once a real accelerometer's jitter needs damping out.")]
    [Range(0.05f, 1f)]
    public float smoothing = 1f;

    [Tooltip("Velocity multiplier applied every step, independent of acceleration. " +
             "0.85–0.95 is the usual range: lower = snappier stop, higher = looser coast.")]
    [Range(0.8f, 0.98f)]
    public float dampingFactor = 0.9f;

    [Tooltip("Hard velocity cap (m/s). Clamps punch spikes to a sane top speed.")]
    public float maxSpeed = 8f;

    public Vector3 Velocity { get; set; }

    Vector3 filteredAccel;

    /// <summary>Integrates one physics step. Call once per FixedUpdate.</summary>
    public Vector3 Step(Vector3 acceleration, float dt)
    {
        Vector3 raw = acceleration.magnitude < deadzone ? Vector3.zero : acceleration;
        filteredAccel = Vector3.Lerp(filteredAccel, raw, smoothing);

        Vector3 v = Velocity + filteredAccel * dt;
        v *= dampingFactor;

        Velocity = Vector3.ClampMagnitude(v, maxSpeed);
        return Velocity;
    }

    /// <summary>Instantaneous velocity kick (e.g. a punch lunge), independent of acceleration.</summary>
    public void AddImpulse(Vector3 deltaVelocity) => Velocity += deltaVelocity;
}
