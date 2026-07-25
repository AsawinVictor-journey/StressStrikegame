using UnityEngine;

public class HandInputManager : MonoBehaviour
{
    [Header("Input Mode")]
    public bool useMouseDebug = true;

    [Header("Hand Targets (Assigned at Runtime or Editor)")]
    public Transform leftHandTarget;
    public Transform rightHandTarget;

    [Header("Mouse Debug Settings")]
    public float mouseSensitivity = 0.05f;
    public float punchDepth = 0.8f;
    public float punchSpeed = 15f;
    
    private bool isControllingLeft = false; // Start with right hand active

    // Guard positions relative to their parent
    private Vector3 leftGuardPos;
    private Vector3 rightGuardPos;

    private Vector3 currentActiveHandPos;
    private float currentPunchDepth = 0f;

    void Awake()
    {
        // Initialize guard positions based on where the gloves are placed in the scene
        GameObject lg = GameObject.Find("left glove");
        GameObject rg = GameObject.Find("right glove");

        // Check if targets are pre-assigned, otherwise create them
        if (leftHandTarget == null) 
        {
            GameObject leftT = new GameObject("LeftHandTarget");
            leftHandTarget = leftT.transform;
        }
        if (rightHandTarget == null)
        {
            GameObject rightT = new GameObject("RightHandTarget");
            rightHandTarget = rightT.transform;
        }

        // Parent to camera so they belong to the player's screen and follow your view perfectly
        Camera cam = Camera.main;
        if (cam != null)
        {
            leftHandTarget.SetParent(cam.transform);
            rightHandTarget.SetParent(cam.transform);
            // CRITICAL: Align the target's rotation with the camera perfectly BEFORE the PhysicsHandController calculates its offset!
            leftHandTarget.localRotation = Quaternion.identity;
            rightHandTarget.localRotation = Quaternion.identity;
        }
        else
        {
            leftHandTarget.SetParent(transform);
            rightHandTarget.SetParent(transform);
            leftHandTarget.localRotation = Quaternion.identity;
            rightHandTarget.localRotation = Quaternion.identity;
        }

        if (lg != null) leftHandTarget.position = lg.transform.position;
        if (rg != null) rightHandTarget.position = rg.transform.position;

        leftGuardPos = leftHandTarget.localPosition;
        rightGuardPos = rightHandTarget.localPosition;
        
        currentActiveHandPos = rightGuardPos; // Start controlling right hand
        
        // Lock cursor for testing
        if (useMouseDebug)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (useMouseDebug)
        {
            HandleMouseDebugInput();
        }
        else
        {
            HandleBluetoothInput();
        }
    }

    private void HandleMouseDebugInput()
    {
        // Switch hands
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Tab))
        {
            isControllingLeft = !isControllingLeft;
            
            // Reset the old active hand to its guard position
            if (isControllingLeft) 
            {
                rightHandTarget.localPosition = rightGuardPos;
                currentActiveHandPos = leftHandTarget.localPosition;
            }
            else 
            {
                leftHandTarget.localPosition = leftGuardPos;
                currentActiveHandPos = rightHandTarget.localPosition;
            }
            currentPunchDepth = 0f;
        }

        // X/Y movement from mouse Delta
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Z movement (Punching) via Left Click
        if (Input.GetMouseButton(0))
        {
            currentPunchDepth = Mathf.Lerp(currentPunchDepth, punchDepth, Time.deltaTime * punchSpeed);
        }
        else
        {
            currentPunchDepth = Mathf.Lerp(currentPunchDepth, 0f, Time.deltaTime * punchSpeed);
        }

        Camera cam = Camera.main;
        Transform anchor = leftHandTarget.parent; // Dummy1 (1)

        if (cam != null && anchor != null)
        {
            // Move strictly relative to the screen (Camera right and up)
            Vector3 worldMove = cam.transform.right * mouseX + cam.transform.up * mouseY;
            // Convert to local space of the anchor
            Vector3 localMove = anchor.InverseTransformDirection(worldMove);
            currentActiveHandPos += localMove;
        }
        else
        {
            currentActiveHandPos += new Vector3(mouseX, mouseY, 0);
        }
        
        // Clamp relative to the active guard position using a clean spherical radius (prevents flying off screen)
        Vector3 guardPos = isControllingLeft ? leftGuardPos : rightGuardPos;
        Vector3 offset = currentActiveHandPos - guardPos;
        if (offset.magnitude > 1.5f)
        {
            currentActiveHandPos = guardPos + offset.normalized * 1.5f;
        }

        // Apply punch depth along the Camera's forward vector so it always punches INTO the screen
        Vector3 worldPunch = cam.transform.forward * currentPunchDepth;
        Vector3 localPunch = anchor.InverseTransformDirection(worldPunch);
        Vector3 finalPos = currentActiveHandPos + localPunch;
        
        // --- Free Rotation ---
        // (Tilt will be applied later)

        // --- Eye Tracking / Target Rotation ---
        bool foundEnemy = false;
        Vector3 bestEnemyLocalPos = Vector3.zero;
        
        // Base rotation: just look straight forward relative to the camera
        Quaternion baseLookRot = Quaternion.identity;

        // Shoot a thick laser (SphereCastAll) straight from the camera to see what we are looking at!
        // We use All so that if the laser hits our own gloves first, it punches through and still finds the enemy!
        RaycastHit[] hits = Physics.SphereCastAll(cam.transform.position, 2.0f, cam.transform.forward, 20f);
        foreach (RaycastHit hit in hits)
        {
            // Ignore collisions with our own player character and our own floating gloves!
            if (hit.transform.root != cam.transform.root && !hit.collider.name.ToLower().Contains("glove"))
            {
                // Is this a valid dummy?
                if (hit.collider.name.Contains("Dummy") || hit.transform.root.name.Contains("Dummy") || hit.collider.GetComponentInParent<Animator>() != null)
                {
                    foundEnemy = true;
                    // Calculate where the center of the enemy is in our local space
                    bestEnemyLocalPos = anchor.InverseTransformPoint(hit.collider.bounds.center);
                    break; // Found our target!
                }
            }
        }

        if (foundEnemy)
        {
            // Calculate direction to enemy in local space
            Vector3 lookDir = (bestEnemyLocalPos - finalPos).normalized;
            if (lookDir != Vector3.zero)
            {
                // Make the hand "look at" the enemy target
                baseLookRot = Quaternion.LookRotation(lookDir);
            }
        }

        // Apply mouse tilt on top of the base look rotation!
        Quaternion tilt = Quaternion.Euler(mouseY * -50f, mouseX * 50f, isControllingLeft ? -15f : 15f);
        // Blend smoothly depending on punch depth so it snaps its gaze tightly when throwing a punch
        float trackStrength = Mathf.Lerp(0.3f, 1.0f, currentPunchDepth / punchDepth);
        
        Quaternion dynamicRotation = Quaternion.Slerp(tilt, baseLookRot * tilt, trackStrength);

        if (isControllingLeft)
        {
            leftHandTarget.localPosition = finalPos;
            leftHandTarget.localRotation = dynamicRotation;
        }
        else
        {
            rightHandTarget.localPosition = finalPos;
            rightHandTarget.localRotation = dynamicRotation;
        }
    }

    private void HandleBluetoothInput()
    {
        // TODO: Read serial data from ESP32 MPU6050 here
        // Example: leftHandTarget.localRotation = Quaternion.Euler(espRotationX, espRotationY, espRotationZ);
    }
}
