using UnityEngine;
// using UnityEngine.InputSystem; // Removed in favor of legacy Input

public class VRGloveProcessor : MonoBehaviour
{
    [Header("Keyboard Controls")]
    public float turnSpeed = 150f;

    [Header("Punch Detection")]
    public float maxPunchDistance = 1.5f; 

    [Header("Punch Animation Timeline")]
    [Tooltip("Seconds it takes to reach full extension (e.g., 0.1 for a fast jab)")]
    public float timeToExtend = 0.1f; 
    [Tooltip("Seconds it takes to snap back to the guard (e.g., 0.25)")]
    public float timeToRetract = 0.25f; 
    [Tooltip("Cooldown before you can throw another punch")]
    public float punchCooldown = 0.1f;

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
        // --- 1. ROTATION (TURNING) ---
        if (Input.GetKey(KeyCode.H))
        {
            transform.Rotate(Vector3.up, turnSpeed * Time.deltaTime);
        }
        if (Input.GetKey(KeyCode.K))
        {
            transform.Rotate(Vector3.up, -turnSpeed * Time.deltaTime);
        }

        // --- 2. PUNCH DETECTION & STATE MACHINE ---
        if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;

        if (currentState == PunchState.Idle && (Input.GetKeyDown(KeyCode.J) || Input.GetKeyDown(KeyCode.M)) && cooldownTimer <= 0f)
        {
            targetPunchDistance = maxPunchDistance; 
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
                cooldownTimer = punchCooldown;
            }
        }

        // --- 4. KNUCKLE POINTER AIMING ---
        Vector3 aimDirection = transform.localRotation * Vector3.forward;
        transform.localPosition = anchorPosition + (aimDirection * currentPunchDistance);
    }
}