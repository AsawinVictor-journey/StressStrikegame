using UnityEngine;

public class PoseChecker : MonoBehaviour
{
    public PoseTarget left;
    public PoseTarget right;
    public PoseTarget head;

    bool passed = false;

    void Update()
    {
        if (passed) return;

        if (left.IsTouched() &&
            right.IsTouched() &&
            head.IsTouched())
        {
            passed = true;
            Debug.Log("POSE PERFECT!");
        }
    }
}