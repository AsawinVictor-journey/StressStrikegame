using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class YogaTracker : MonoBehaviour
{
    // ESP32Glove is an InputDevice, not a UnityEngine.Object, so it can't be
    // wired up in the Inspector — it has to be fetched from the Input System.
    ESP32Glove glove;

    [Header("Current Target")]
    public Vector3 targetRotation;

    [Header("Score")]
    public float accuracy;
    public TMP_Text accuracyText;

    public float smoothSpeed = 5f;
    public bool tracking;

    [Header("Session Result")]
    [Tooltip("How often (seconds) accuracy is sampled for the end-of-session result.")]
    public float sampleInterval = 0.2f;

    [Tooltip("Average accuracy across the whole hold, computed when tracking stops.")]
    public float alignment;

    [Tooltip("How little accuracy wobbled during the hold (100 = rock steady).")]
    public float steadiness;

    readonly List<float> accuracySamples = new List<float>();
    float sampleTimer;

    void Update()
    {
        if (!tracking)
            return;

        if (glove == null)
        {
            glove = InputSystem.GetDevice<ESP32Glove>();
            if (glove == null) return;
        }


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

        sampleTimer += Time.deltaTime;
        if (sampleTimer >= sampleInterval)
        {
            sampleTimer = 0f;
            accuracySamples.Add(accuracy);
        }
    }


    public void SetTargetPose(Vector3 rotation)
    {
        targetRotation = rotation;
    }


    Quaternion GetGloveRotation()
    {
        // Same BNO055 -> Unity axis remap that VRGloveProcessor uses.
        float qX = glove.x.ReadValue();
        float qY = glove.y.ReadValue();
        float qZ = glove.z.ReadValue();
        float qW = glove.w.ReadValue();

        return new Quaternion(-qY, -qZ, qX, qW).normalized;
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
        accuracySamples.Clear();
        sampleTimer = 0f;
    }


    public void StopTracking()
    {
        tracking = false;
        CalculateSessionResult();
    }

    void CalculateSessionResult()
    {
        if (accuracySamples.Count == 0)
        {
            alignment = accuracy;
            steadiness = 100f;
            return;
        }

        float sum = 0f;
        foreach (float sample in accuracySamples)
            sum += sample;
        float mean = sum / accuracySamples.Count;

        float variance = 0f;
        foreach (float sample in accuracySamples)
            variance += (sample - mean) * (sample - mean);
        variance /= accuracySamples.Count;
        float stdDev = Mathf.Sqrt(variance);

        alignment = mean;

        // A stdDev of ~30 or more reads as very shaky; 0 is rock steady.
        steadiness = Mathf.Clamp(100f - (stdDev / 30f) * 100f, 0f, 100f);
    }
}