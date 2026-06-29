using UnityEngine;

public class HandPosition : MonoBehaviour
{
    public Transform origin;
    public float moveSpeed    = 8f;
    public float acceleration = 10f;   // input ramp speed
    public float followSpeed  = 20f;   // how fast actual position chases target (higher = snappier)
    public float maxForward   = 0.8f;
    public float maxBackward  = 0.8f;
    public float maxUp        = 0.8f;
    public float maxDown      = 0.8f;
    public float maxLeft      = 0.8f;
    public float maxRight     = 0.8f;
    public float maxPushDist  = 0.15f;

    [HideInInspector] public Collider col;

    Rigidbody      rb;
    Vector3        targetLocal;
    Vector3        inputVelLocal;
    Vector3        currentPosLocal;  // local to origin so camera rotation doesn't cause world-space drift
    HandPosition[] otherHands;

    void Start()
    {
        rb  = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        rb.isKinematic    = true;
        rb.freezeRotation = true;
        rb.interpolation  = RigidbodyInterpolation.Interpolate;

        targetLocal    = origin.InverseTransformPoint(transform.position);
        currentPosLocal = targetLocal;

        var all = FindObjectsByType<HandPosition>(FindObjectsSortMode.None);
        otherHands = System.Array.FindAll(all, h => h != this);

        if (maxLeft > 3f || maxRight > 3f || maxForward > 3f || maxBackward > 3f || maxUp > 3f || maxDown > 3f)
            Debug.LogWarning($"[HP:{name}] Max distance values are very large (recommended ~0.8). Hand will drift far from origin.");
    }

    void LateUpdate()
    {
        // Rigidbody.Interpolate lags behind when a parent transform rotates in Update.
        // Force the physics position to match so the visual never drifts.
        rb.position = origin.TransformPoint(currentPosLocal);
    }

    void FixedUpdate()
    {
        // Smooth input so movement accelerates/decelerates naturally
        inputVelLocal = Vector3.Lerp(inputVelLocal, GetInputDirection() * moveSpeed, Time.fixedDeltaTime * acceleration);

        targetLocal += inputVelLocal * Time.fixedDeltaTime;
        targetLocal  = ClampLocal(targetLocal);

        Vector3 targetWorld = origin.TransformPoint(targetLocal);

        // Push target away from other hands
        foreach (var other in otherHands)
        {
            if (other == null || other.col == null) continue;
            if (Physics.ComputePenetration(col,       targetWorld,              transform.rotation,
                                           other.col, other.transform.position, other.transform.rotation,
                                           out Vector3 dir, out float dist))
                targetWorld += dir * Mathf.Min(dist, maxPushDist);
        }

        currentPosLocal = Vector3.Lerp(currentPosLocal, origin.InverseTransformPoint(targetWorld), Time.fixedDeltaTime * followSpeed);
        rb.MovePosition(origin.TransformPoint(currentPosLocal));
    }

    Vector3 GetInputDirection()
    {
        Vector3 dir = Vector3.zero;

        if (gameObject.name.Contains("Left"))
        {
            if (Input.GetKey(KeyCode.W)) dir += Vector3.forward;
            if (Input.GetKey(KeyCode.S)) dir -= Vector3.forward;
            if (Input.GetKey(KeyCode.D)) dir += Vector3.right;
            if (Input.GetKey(KeyCode.A)) dir -= Vector3.right;
            if (Input.GetKey(KeyCode.E)) dir += Vector3.up;
            if (Input.GetKey(KeyCode.Q)) dir -= Vector3.up;
        }

        if (gameObject.name.Contains("Right"))
        {
            if (Input.GetKey(KeyCode.UpArrow))     dir += Vector3.forward;
            if (Input.GetKey(KeyCode.DownArrow))   dir -= Vector3.forward;
            if (Input.GetKey(KeyCode.RightArrow))  dir += Vector3.right;
            if (Input.GetKey(KeyCode.LeftArrow))   dir -= Vector3.right;
            if (Input.GetKey(KeyCode.Space))        dir += Vector3.up;
            if (Input.GetKey(KeyCode.LeftControl))  dir -= Vector3.up;
        }

        return dir.normalized;
    }

    Vector3 ClampLocal(Vector3 l)
    {
        l.x = Mathf.Clamp(l.x, -maxLeft,     maxRight);
        l.y = Mathf.Clamp(l.y, -maxDown,      maxUp);
        l.z = Mathf.Clamp(l.z, -maxBackward,  maxForward);
        return l;
    }
}
