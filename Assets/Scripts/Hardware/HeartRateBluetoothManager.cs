using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class HeartRateBluetoothManager : MonoBehaviour
{
    public static HeartRateBluetoothManager Instance { get; private set; }

    [Header("Connection State")]
    public bool isConnected = false;
    public string connectedDeviceName = "";
    
    [Header("Live Data")]
    public float currentBPM = 0f;
    public float currentHRV = 0f; // RMSSD in ms

    private List<float> rrIntervals = new List<float>(); // in ms
    private float simBaseBPM = 72f;
    private float simRespirationFreq = 0.25f; // 15 breaths per minute
    private float simRespirationTimer = 0f;
    private Coroutine simulationCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public List<string> ScanDevices()
    {
        // Mock Bluetooth Scan Results
        return new List<string>
        {
            "Polar H10 (BLE_HR_8923)",
            "Garmin Dual HR (BLE_HR_0411)",
            "Mock BLE Heart Rate Monitor"
        };
    }

    public void ConnectDevice(string deviceName)
    {
        isConnected = true;
        connectedDeviceName = deviceName;
        rrIntervals.Clear();

        // Adjust base BPM depending on simulation name
        if (deviceName.Contains("Polar")) simBaseBPM = 65f;
        else if (deviceName.Contains("Garmin")) simBaseBPM = 70f;
        else simBaseBPM = 75f;

        if (simulationCoroutine != null) StopCoroutine(simulationCoroutine);
        simulationCoroutine = StartCoroutine(HeartRateSimulationRoutine());
        
        Debug.Log("Connected to Bluetooth HR Device: " + deviceName);
    }

    public void DisconnectDevice()
    {
        isConnected = false;
        connectedDeviceName = "";
        currentBPM = 0f;
        currentHRV = 0f;
        rrIntervals.Clear();
        if (simulationCoroutine != null)
        {
            StopCoroutine(simulationCoroutine);
            simulationCoroutine = null;
        }
        Debug.Log("Disconnected Bluetooth HR Device.");
    }

    public void SetBaseBPM(float targetBPM)
    {
        simBaseBPM = targetBPM;
    }

    public float GetAverageBPM()
    {
        if (rrIntervals.Count == 0) return simBaseBPM;
        
        // Convert average RR interval to BPM
        float sum = 0f;
        foreach (var rr in rrIntervals) sum += rr;
        float avgRR = sum / rrIntervals.Count; // in ms
        return 60000f / avgRR;
    }

    public float CalculateFinalHRV()
    {
        if (rrIntervals.Count < 2) return Random.Range(35f, 55f); // default normal HRV

        float sumSquaredDiffs = 0f;
        for (int i = 0; i < rrIntervals.Count - 1; i++)
        {
            float diff = rrIntervals[i + 1] - rrIntervals[i];
            sumSquaredDiffs += diff * diff;
        }

        // RMSSD calculation
        float rmssd = Mathf.Sqrt(sumSquaredDiffs / (rrIntervals.Count - 1));
        return rmssd;
    }

    private IEnumerator HeartRateSimulationRoutine()
    {
        while (isConnected)
        {
            // Sinus Arrhythmia simulation (respirations cause heart rate variation)
            simRespirationTimer += Time.deltaTime;
            float respMod = Mathf.Sin(2f * Mathf.PI * simRespirationFreq * simRespirationTimer);
            
            // Fluctuations in BPM
            float targetBPM = simBaseBPM + (respMod * 4f) + Random.Range(-1.5f, 1.5f);
            currentBPM = Mathf.Lerp(currentBPM == 0 ? targetBPM : currentBPM, targetBPM, Time.deltaTime * 2f);

            // Compute RR interval from current BPM (60,000 / BPM)
            float currentRR = (60000f / currentBPM) + Random.Range(-10f, 10f); // Add raw HRV variation
            rrIntervals.Add(currentRR);

            // Limit data storage to last 100 beats
            if (rrIntervals.Count > 100) rrIntervals.RemoveAt(0);

            // Dynamically estimate current HRV
            currentHRV = CalculateFinalHRV();

            // Wait until next heartbeat
            float beatDuration = 60f / currentBPM;
            yield return new WaitForSeconds(beatDuration);
        }
    }
}
