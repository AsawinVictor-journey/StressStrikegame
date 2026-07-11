using System.Collections;
using UnityEngine;

public class MenuDummyPuncher : MonoBehaviour
{
    [Tooltip("Seconds to rest at idle between each 4-punch combo.")]
    [SerializeField] private float idleDuration = 0.5f;

    [Tooltip("Fraction of each punch clip's length to wait before advancing to the next punch in the combo. Keeps punches seamless, ahead of the clip's own return-to-idle exit time.")]
    [SerializeField] [Range(0f, 1f)] private float comboAdvanceFraction = 0.75f;

    // Trigger name + its clip's real length (seconds), hardcoded to avoid racing the Animator's
    // transition when reading GetCurrentAnimatorClipInfo right after SetTrigger.
    private static readonly (string trigger, float clipLength)[] Combo =
    {
        ("RightJabTrigger", 1.2166667f),  // Cross Punch.fbx
        ("LeftJabTrigger", 1.73333347f),  // Boxing.fbx
        ("RightHookTrigger", 1.96666682f),// Hook Punch.fbx
        ("LeftHookTrigger", 1.98333347f), // Hook left.fbx
    };

    private Animator animator;

    private void Start()
    {
        animator = GetComponent<Animator>();
        StartCoroutine(ComboLoop());
    }

    private IEnumerator ComboLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(idleDuration);

            foreach (var (trigger, clipLength) in Combo)
            {
                FireTrigger(trigger);
                yield return new WaitForSeconds(clipLength * comboAdvanceFraction);
            }
        }
    }

    private void FireTrigger(string trigger)
    {
        // Reset every other trigger first so a stale, unconsumed trigger from
        // a previous punch can't leak into a later cycle and fire out of order.
        foreach (var (t, _) in Combo)
        {
            if (t != trigger) animator.ResetTrigger(t);
        }
        animator.SetTrigger(trigger);
    }
}
