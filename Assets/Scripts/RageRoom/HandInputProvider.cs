using UnityEngine;

/// <summary>
/// Abstract input layer. Swap this for a BNO055 implementation without
/// touching the motion, movement, or punch layers.
///
/// This reports only what a real IMU can actually measure — linear
/// acceleration and orientation. It never reports position. Nothing
/// downstream is allowed to double-integrate this into a position estimate;
/// that is the classic IMU dead-reckoning drift trap. The only "position" in
/// this system is a bounded, damped simulation living in HandTarget, driven
/// by this signal, not reconstructed from it.
///
/// Keyboard implementation: KeyboardHandInput.cs
/// BNO055 implementation:   Bno055HandInput.cs  (create when hardware is ready)
/// </summary>
public abstract class HandInputProvider : MonoBehaviour
{
    /// <summary>
    /// Simulated/real linear acceleration this frame, in the hand's local
    /// axes (m/s²). Sustained input (holding a key / tilting the sensor)
    /// should hold this near a constant magnitude; a punch is a short spike
    /// far above that magnitude so PunchDetector can tell the two apart.
    /// </summary>
    public abstract Vector3 GetAcceleration();

    /// <summary>
    /// Optional fused orientation. Unused by keyboard (HandRotation.cs
    /// already drives hand orientation from the mouse independently);
    /// BNO055 will return its fused quaternion here.
    /// </summary>
    public virtual Quaternion GetOrientation() => Quaternion.identity;
}
