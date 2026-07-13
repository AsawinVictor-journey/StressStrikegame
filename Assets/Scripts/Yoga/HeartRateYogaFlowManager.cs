using UnityEngine;
using System.Collections;

public class HeartRateYogaFlowManager : MonoBehaviour
{
    public static HeartRateYogaFlowManager Instance { get; private set; }

    public enum FlowState
    {
        PreGameConnection,
        PreGameCalibration,
        YogaSelection,
        YogaGameplay,
        PostGameCalibration,
        Results
    }

    [Header("State")]
    public FlowState currentState = FlowState.PreGameConnection;

    [Header("References")]
    public YogaManager yogaManager;
    public HeartRateYogaUI heartRateUI;

    [Header("Calibration Settings")]
    public float calibrationDuration = 10f;

    [Header("Session Metrics")]
    public float baselineHR = 0f;
    public float baselineHRV = 0f;
    public float postGameHR = 0f;
    public float postGameHRV = 0f;
    public float caloriesBurned = 0f;

    private float playStartTime = 0f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (yogaManager == null) yogaManager = FindObjectOfType<YogaManager>();
        
        // Hide standard Yoga pose selection on start
        var posesSelection = GameObject.Find("Canvas/PosesSelection");
        if (posesSelection != null) posesSelection.SetActive(false);

        // Hide result canvas
        if (yogaManager.resultGroup != null) yogaManager.resultGroup.gameObject.SetActive(false);

        // Start connection state
        SetState(FlowState.PreGameConnection);
    }

    public void SetState(FlowState newState)
    {
        currentState = newState;
        Debug.Log("HR Yoga State: " + newState);

        // Notify UI
        if (heartRateUI != null) heartRateUI.OnStateChanged(newState);

        switch (newState)
        {
            case FlowState.PreGameConnection:
                // Pre-game UI active, rest disabled
                break;

            case FlowState.PreGameCalibration:
                StartCoroutine(CalibratePreGameRoutine());
                break;

            case FlowState.YogaSelection:
                // Enable standard Pose Selection
                var posesSelection = GameObject.Find("Canvas/PosesSelection") ?? yogaManager.descriptionImage?.transform.parent?.parent?.Find("PosesSelection")?.gameObject;
                if (posesSelection != null) posesSelection.SetActive(true);
                else
                {
                    // Fallback using child name or search
                    var canvases = FindObjectsOfType<Canvas>();
                    foreach (var c in canvases)
                    {
                        var ps = c.transform.Find("PosesSelection");
                        if (ps != null) ps.gameObject.SetActive(true);
                    }
                }
                break;

            case FlowState.YogaGameplay:
                playStartTime = Time.time;
                // Temporarily increase heart rate base to simulate slight effort
                if (HeartRateBluetoothManager.Instance != null)
                {
                    HeartRateBluetoothManager.Instance.SetBaseBPM(78f);
                }
                break;

            case FlowState.PostGameCalibration:
                StartCoroutine(CalibratePostGameRoutine());
                break;

            case FlowState.Results:
                CalculateFinalMetrics();
                if (heartRateUI != null) heartRateUI.DisplayResults();
                break;
        }
    }

    private IEnumerator CalibratePreGameRoutine()
    {
        float timer = 0f;
        while (timer < calibrationDuration)
        {
            timer += Time.deltaTime;
            if (heartRateUI != null) heartRateUI.UpdateCalibrationProgress(timer / calibrationDuration);
            yield return null;
        }

        if (HeartRateBluetoothManager.Instance != null)
        {
            baselineHR = HeartRateBluetoothManager.Instance.GetAverageBPM();
            baselineHRV = HeartRateBluetoothManager.Instance.CalculateFinalHRV();
        }

        SetState(FlowState.YogaSelection);
    }

    private IEnumerator CalibratePostGameRoutine()
    {
        // Lower simulated heart rate as yoga leads to calm/relaxation
        if (HeartRateBluetoothManager.Instance != null)
        {
            HeartRateBluetoothManager.Instance.SetBaseBPM(62f);
        }

        float timer = 0f;
        while (timer < calibrationDuration)
        {
            timer += Time.deltaTime;
            if (heartRateUI != null) heartRateUI.UpdateCalibrationProgress(timer / calibrationDuration);
            yield return null;
        }

        if (HeartRateBluetoothManager.Instance != null)
        {
            postGameHR = HeartRateBluetoothManager.Instance.GetAverageBPM();
            postGameHRV = HeartRateBluetoothManager.Instance.CalculateFinalHRV();
        }

        SetState(FlowState.Results);
    }

    private void CalculateFinalMetrics()
    {
        float playDurationSeconds = Time.time - playStartTime;
        float playDurationMinutes = playDurationSeconds / 60f;

        // Formula for calories burned (Yoga MET ~ 2.5 to 3.0)
        // Average energy expenditure for Yoga: ~3.0 kcal/kg/hr.
        // Assuming a standard weight of 70kg: Calories = 3.0 * 70 * (duration in hours)
        // Or roughly: Calories = duration in minutes * 3.5
        // We modulate it based on average heart rate during gameplay.
        float avgHR = (baselineHR + postGameHR) / 2f;
        float hrIntensityFactor = Mathf.Clamp(avgHR / 70f, 0.8f, 1.5f);
        
        caloriesBurned = playDurationMinutes * 3.8f * hrIntensityFactor;
    }
}
