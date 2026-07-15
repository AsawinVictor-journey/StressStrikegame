using UnityEngine;
using UnityEngine.InputSystem;

public class VRGloveProcessor : MonoBehaviour
{
    [Header("Punch Detection")]
    public float punchDeadzone = 15.0f; 
    public float forceToDistanceMultiplier = 0.05f; 
    [Tooltip("The absolute maximum distance the virtual hand can travel in meters.")]
    public float maxPunchDistance = 1.5f; 

    [Header("Punch Animation Timeline")]
    [Tooltip("Seconds it takes to reach full extension (e.g., 0.1 for a fast jab)")]
    public float timeToExtend = 0.1f; 
    [Tooltip("Seconds it takes to snap back to the guard (e.g., 0.25)")]
    public float timeToRetract = 0.25f; 
    [Tooltip("Cooldown before you can throw another punch")]
    public float punchCooldown = 0.1f;

    private ESP32Glove gloveDevice;
    private Quaternion manualZeroOffset = Quaternion.identity;
    private Vector3 anchorPosition;
    
    // State Machine Variables
    private enum PunchState { Idle, Extending, Retracting }
    private PunchState currentState = PunchState.Idle;
    
    private float currentPunchDistance = 0f; 
    private float targetPunchDistance = 0f;
    private float animTimer = 0f;
    private float cooldownTimer = 0f;

    void Start()
    {
        anchorPosition = transform.localPosition;
    }

    void Update()
    {
        if (gloveDevice == null)
        {
            gloveDevice = InputSystem.GetDevice<ESP32Glove>();
            if (gloveDevice == null) return;
        }

        // --- 1. PURE QUATERNION MATH ---
        float qX = gloveDevice.x.ReadValue(); 
        float qY = gloveDevice.y.ReadValue(); 
        float qZ = gloveDevice.z.ReadValue(); 
        float qW = gloveDevice.w.ReadValue(); 

        Quaternion rawSensorRotation = new Quaternion(-qY, -qZ, qX, qW); 
        rawSensorRotation = rawSensorRotation.normalized;

        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            manualZeroOffset = rawSensorRotation;
            Debug.Log("Glove Rotation Zeroed!");
        }

        transform.localRotation = Quaternion.Inverse(manualZeroOffset) * rawSensorRotation;

        // --- 2. PUNCH DETECTION & STATE MACHINE ---
        float fY = gloveDevice.forceY.ReadValue() * 327.67f;
        float fZ = gloveDevice.forceZ.ReadValue() * 327.67f;
        float totalPunchForce = new Vector2(fY, fZ).magnitude;

        // Manage the cooldown timer
        if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;

        // Trigger a new punch if we are idle and swing hard enough
        if (currentState == PunchState.Idle && totalPunchForce > punchDeadzone && cooldownTimer <= 0f)
        {
            // Calculate how far to go, but clamp it to the max distance
            targetPunchDistance = Mathf.Clamp(totalPunchForce * forceToDistanceMultiplier, 0f, maxPunchDistance);
            
            currentState = PunchState.Extending;
            animTimer = 0f;
        }

        // --- 3. THE ANIMATION TIMELINE ---
        if (currentState == PunchState.Extending)
        {
            animTimer += Time.deltaTime;
            currentPunchDistance = Mathf.Lerp(0f, targetPunchDistance, animTimer / timeToExtend);

            if (animTimer >= timeToExtend)
            {
                currentState = PunchState.Retracting;
                animTimer = 0f;
            }
        }
        else if (currentState == PunchState.Retracting)
        {
            animTimer += Time.deltaTime;
            currentPunchDistance = Mathf.Lerp(targetPunchDistance, 0f, animTimer / timeToRetract);

            if (animTimer >= timeToRetract)
            {
                currentState = PunchState.Idle;
                currentPunchDistance = 0f;
                cooldownTimer = punchCooldown; // Start cooldown ONLY after punch finishes
            }
        }

        // --- 4. KNUCKLE POINTER AIMING ---
        Vector3 aimDirection = transform.localRotation * Vector3.forward;
        transform.localPosition = anchorPosition + (aimDirection * currentPunchDistance);
    }
}