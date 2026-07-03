using UnityEngine;

/// <summary>
/// Abstract input layer. Swap this for a BNO055 implementation without
/// touching the target or physics layers.
///
/// Keyboard implementation: KeyboardHandInput.cs
/// BNO055 implementation:   Bno055HandInput.cs  (create when hardware is ready)
/// </summary>
public abstract class HandInputProvider : MonoBehaviour
{
    /// <summary>
    /// Normalised direction the hand target should move this frame, in world space.
    /// Keyboard maps keys → local axes. BNO055 will map sensor delta → world axes.
    /// </summary>
    public abstract Vector3 GetMoveDirection();

    /// <summary>
    /// Optional target rotation. Unused by keyboard; BNO055 will return its fused quaternion.
    /// </summary>
    public virtual Quaternion GetTargetRotation() => Quaternion.identity;
}
