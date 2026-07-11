using System.Collections;
using UnityEngine;

public class YogaInstructor : MonoBehaviour
{
    [Header("References")]
    public Animator animator;

    [Header("Animation Names")]
    public string idle = "rig|Idle";

    public string prayerTransition = "rig|PrayerTransition";
    public string prayer = "rig|Prayer";

    public string RaiseArmsTransition = "rig|RaiseArmsTransition";
    public string raiseArms = "rig|RaiseArms";

    public string LowerArmsTransition = "rig|LowerArmsTransition";

    public string SideBendLeftTransition = "rig|SideBendLeftTransition";
    public string sideBendLeft = "rig|SideBendLeft";
    public string SideBendLeftDown = "rig|SideBendLeftDown";

    public string SideBendRightTransition = "rig|SideBendRightTransition";
    public string sideBendRight = "rig|SideBendRight";
    public string SideBendRightDown = "rig|SideBendRightDown";

    public string OpenArmsTransition = "rig|OpenArmsTransition";
    public string openArms = "rig|OpenArms";

    public string handOnKneesTransition = "rig|HandOnKneesTransition";
    public string handOnKnees = "rig|HandOnKnees";

    [Header("Settings")]
    public float idleStartTime = 2f;

    private void Start()
    {
        StartCoroutine(YogaRoutine());
    }

    IEnumerator YogaRoutine()
    {
        animator.Play(idle);
        yield return new WaitForSeconds(idleStartTime);

        yield return Play(prayerTransition);
        yield return Play(prayer);

        yield return Play(RaiseArmsTransition);
        yield return Play(raiseArms);

        yield return Play(LowerArmsTransition);
        yield return Play(idle);

        yield return Play(SideBendLeftTransition);
        yield return Play(sideBendLeft);
        yield return Play(SideBendLeftDown);

        yield return Play(SideBendRightTransition);
        yield return Play(sideBendRight);
        yield return Play(SideBendRightDown);

        yield return Play(OpenArmsTransition);
        yield return Play(openArms);

        yield return Play(handOnKneesTransition);
        yield return Play(handOnKnees);

        animator.Play(idle);
    }

    IEnumerator Play(string clipName)
    {
        Debug.Log("Trying to play: " + clipName);

        animator.Play(clipName, 0, 0f);

        yield return null;

        Debug.Log("Current State: " + animator.GetCurrentAnimatorStateInfo(0).IsName(clipName));

        yield return new WaitForSeconds(GetClipLength(clipName));
    }

    float GetClipLength(string clipName)
    {
        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip.name == clipName)
                return clip.length;
        }

        Debug.LogWarning($"Animation '{clipName}' not found.");
        return 1f;
    }
}