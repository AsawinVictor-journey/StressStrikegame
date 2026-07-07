using System;
using UnityEngine;

/// <summary>
/// Sits on the punch hitbox GameObject (the Collider PunchController toggles
/// on/off for the 0.1-0.2s punch window). Unity delivers collision callbacks
/// to whichever GameObject owns the Collider, so this just forwards that
/// event outward — PunchController doesn't need to live on the same
/// GameObject as the hitbox itself.
/// </summary>
public class PunchHitbox : MonoBehaviour
{
    public event Action<Collision> OnHit;

    void OnCollisionEnter(Collision collision) => OnHit?.Invoke(collision);
}
