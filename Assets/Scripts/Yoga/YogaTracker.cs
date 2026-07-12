using UnityEngine;
using TMPro;

public class YogaTracker : MonoBehaviour
{
    public ESP32Glove glove;

    [Header("Current Target")]
    public Vector3 targetRotation;

    [Header("Score")]
    public float accuracy;
    public TMP_Text accuracyText;

    public float smoothSpeed = 5f;
    public bool tracking;


    void Update()
    {
        if (!tracking || glove == null)
            return;


        Quaternion current = GetGloveRotation();

        Quaternion target = Quaternion.Euler(targetRotation);

        float newAccuracy = CalculateAccuracy(
            current,
            target
        );


        accuracy = Mathf.Lerp(
            accuracy,
            newAccuracy,
            Time.deltaTime * smoothSpeed
        );


        if(accuracyText != null)
        {
            accuracyText.text =
                "Accuracy: " +
                Mathf.RoundToInt(accuracy) +
                "%";
        }
    }


    public void SetTargetPose(Vector3 rotation)
    {
        targetRotation = rotation;
    }


    Quaternion GetGloveRotation()
    {
        return new Quaternion(
            glove.x.ReadValue(),
            glove.y.ReadValue(),
            glove.z.ReadValue(),
            glove.w.ReadValue()
        );
    }


    float CalculateAccuracy(
        Quaternion current,
        Quaternion target)
    {
        float difference =
            Quaternion.Angle(
                current,
                target
            );


        return Mathf.Clamp(
            100f - difference,
            0,
            100
        );
    }

    public void StartTracking()
    {
        tracking = true;
    }


    public void StopTracking()
    {
        tracking = false;
    }
}