using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class HeartRateYogaUI : MonoBehaviour
{
    [Header("Panels")]
    public GameObject preGameConnectionPanel;
    public GameObject preGameCalibrationPanel;
    public GameObject postGameCalibrationPanel;

    [Header("Bluetooth Connection UI")]
    public TMP_Dropdown deviceDropdown;
    public Button scanButton;
    public Button connectButton;
    public TMP_Text connectionStatusText;

    [Header("Calibration UI (Pre-Game)")]
    public TMP_Text calibrationStatusText;
    public Slider calibrationProgressSlider;

    [Header("Calibration UI (Post-Game)")]
    public TMP_Text postCalibrationStatusText;
    public Slider postCalibrationProgressSlider;

    [Header("Pulse Indicator UI")]
    public TMP_Text liveBpmText;
    public RectTransform beatingHeartIcon;

    [Header("Result Summary Overlay UI")]
    public GameObject hrResultsGroup; // A custom panel/container inside YogaManager's result panel
    public TMP_Text baselineHRText;
    public TMP_Text postGameHRText;
    public TMP_Text baselineHRVText;
    public TMP_Text postGameHRVText;
    public TMP_Text caloriesText;

    private List<string> scannedDevices = new List<string>();
    private float heartPulseTimer = 0f;

    private void Start()
    {
        if (scanButton != null) scanButton.onClick.AddListener(OnScanClicked);
        if (connectButton != null) connectButton.onClick.AddListener(OnConnectClicked);
        
        // Initial setup
        if (deviceDropdown != null) deviceDropdown.options.Clear();
        if (connectButton != null) connectButton.interactable = false;

        // Force disable panels initially; let FlowManager orchestrate
        HideAllPanels();
    }

    private void Update()
    {
        UpdateHeartPulse();
    }

    public void OnStateChanged(HeartRateYogaFlowManager.FlowState state)
    {
        HideAllPanels();

        switch (state)
        {
            case HeartRateYogaFlowManager.FlowState.PreGameConnection:
                if (preGameConnectionPanel != null) preGameConnectionPanel.SetActive(true);
                break;

            case HeartRateYogaFlowManager.FlowState.PreGameCalibration:
                if (preGameCalibrationPanel != null) preGameCalibrationPanel.SetActive(true);
                break;

            case HeartRateYogaFlowManager.FlowState.YogaSelection:
                // Everything HR calibration UI is closed, user plays
                break;

            case HeartRateYogaFlowManager.FlowState.YogaGameplay:
                // Shows live HR indicator during gameplay if desired
                if (liveBpmText != null) liveBpmText.gameObject.SetActive(true);
                if (beatingHeartIcon != null) beatingHeartIcon.gameObject.SetActive(true);
                break;

            case HeartRateYogaFlowManager.FlowState.PostGameCalibration:
                if (postGameCalibrationPanel != null) postGameCalibrationPanel.SetActive(true);
                break;

            case HeartRateYogaFlowManager.FlowState.Results:
                if (liveBpmText != null) liveBpmText.gameObject.SetActive(false);
                if (beatingHeartIcon != null) beatingHeartIcon.gameObject.SetActive(false);
                break;
        }
    }

    private void HideAllPanels()
    {
        if (preGameConnectionPanel != null) preGameConnectionPanel.SetActive(false);
        if (preGameCalibrationPanel != null) preGameCalibrationPanel.SetActive(false);
        if (postGameCalibrationPanel != null) postGameCalibrationPanel.SetActive(false);
    }

    public void UpdateCalibrationProgress(float progressPercent)
    {
        var activeState = HeartRateYogaFlowManager.Instance.currentState;
        int remainingSeconds = Mathf.CeilToInt(HeartRateYogaFlowManager.Instance.calibrationDuration * (1f - progressPercent));

        if (activeState == HeartRateYogaFlowManager.FlowState.PreGameCalibration)
        {
            if (calibrationProgressSlider != null) calibrationProgressSlider.value = progressPercent;
            if (calibrationStatusText != null)
            {
                calibrationStatusText.text = $"Calibrating Baseline...\n{remainingSeconds}s remaining";
            }
        }
        else if (activeState == HeartRateYogaFlowManager.FlowState.PostGameCalibration)
        {
            if (postCalibrationProgressSlider != null) postCalibrationProgressSlider.value = progressPercent;
            if (postCalibrationStatusText != null)
            {
                postCalibrationStatusText.text = $"Measuring Cooldown...\n{remainingSeconds}s remaining";
            }
        }
    }

    public void DisplayResults()
    {
        var flow = HeartRateYogaFlowManager.Instance;
        
        if (baselineHRText != null) baselineHRText.text = $"{Mathf.RoundToInt(flow.baselineHR)} BPM";
        if (postGameHRText != null) postGameHRText.text = $"{Mathf.RoundToInt(flow.postGameHR)} BPM";
        
        if (baselineHRVText != null) baselineHRVText.text = $"{Mathf.RoundToInt(flow.baselineHRV)} ms";
        if (postGameHRVText != null) postGameHRVText.text = $"{Mathf.RoundToInt(flow.postGameHRV)} ms";
        
        if (caloriesText != null) caloriesText.text = $"{flow.caloriesBurned:F1} kcal";

        if (hrResultsGroup != null) hrResultsGroup.SetActive(true);

        // Show the standard result panel (Score/Accuracy/Coins/Level/level bar) and start
        // its return-to-menu countdown from here — this is the point HR results actually
        // become visible, not ~calibrationDuration seconds earlier when post-game
        // calibration started. Calling ShowResult() instead of just SetActive(true) on
        // resultGroup also means the heart-rate flow no longer skips Score/Accuracy/Coins/
        // Level, which it previously did.
        if (flow.yogaManager != null)
        {
            flow.yogaManager.ShowResult();
        }
    }

    // The pulse sensor now rides on the ESP32 glove (USB HID), so there is nothing
    // to pair over Bluetooth — "scan" just checks whether the glove is plugged in
    // and reporting a pulse.
    private void OnScanClicked()
    {
        if (connectionStatusText != null) connectionStatusText.text = "Looking for the glove...";

        scannedDevices.Clear();

        bool gloveFound = InputSystem.GetDevice<ESP32Glove>() != null;
        if (gloveFound) scannedDevices.Add("ESP32 VR Glove");

        if (deviceDropdown != null)
        {
            deviceDropdown.options.Clear();
            foreach (var device in scannedDevices)
            {
                deviceDropdown.options.Add(new TMP_Dropdown.OptionData(device));
            }
            deviceDropdown.RefreshShownValue();
        }

        if (connectionStatusText != null)
        {
            connectionStatusText.text = gloveFound
                ? "Glove found. Press Connect to begin."
                : "No glove detected. Plug it in and scan again.";
        }

        if (connectButton != null) connectButton.interactable = gloveFound;
    }

    private void OnConnectClicked()
    {
        var engine = BiometricEngine.Instance;

        if (engine == null || !engine.isConnected)
        {
            if (connectionStatusText != null)
            {
                connectionStatusText.text = "Glove is not reporting a pulse yet. Rest your finger on the sensor.";
            }
            return;
        }

        if (connectionStatusText != null) connectionStatusText.text = "Pulse detected!";

        // Advance to calibration after short delay
        Invoke(nameof(StartCalibration), 1.5f);
    }

    private void StartCalibration()
    {
        HeartRateYogaFlowManager.Instance.SetState(HeartRateYogaFlowManager.FlowState.PreGameCalibration);
    }

    private void UpdateHeartPulse()
    {
        var engine = BiometricEngine.Instance;

        if (engine == null || !engine.isConnected)
        {
            if (liveBpmText != null) liveBpmText.text = "-- BPM";
            return;
        }

        float bpm = engine.currentBPM;
        if (liveBpmText != null)
        {
            liveBpmText.text = $"{Mathf.RoundToInt(bpm)} <size=70%>BPM</size>";
        }

        if (beatingHeartIcon != null)
        {
            // Beat frequency based on current BPM
            float pulseSpeed = (bpm / 60f) * 2f * Mathf.PI;
            heartPulseTimer += Time.deltaTime * pulseSpeed;

            // Compute scale multiplier
            float baseScale = 1.0f;
            float pulseScale = baseScale + Mathf.Max(0, Mathf.Sin(heartPulseTimer)) * 0.15f;
            beatingHeartIcon.localScale = new Vector3(pulseScale, pulseScale, 1.0f);
        }
    }
}
