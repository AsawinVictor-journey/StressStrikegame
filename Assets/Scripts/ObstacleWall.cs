using UnityEngine;
using UnityEngine.Events;

public class ObstacleWall : MonoBehaviour
{
    public enum WallState { Idle, Active, Hit, Dodged }
    public WallState currentState = WallState.Idle;

    [Header("Settings")]
    public float hitDistance = 0.5f;
    public float activeDuration = 3.0f;
    private float timer = 0f;

    [Header("References")]
    private Transform leftHand;
    private Transform rightHand;
    private Renderer wallRenderer;
    public ParticleSystem breakVFX;
    public AudioSource breakAudio;

    // true = Dodged (Good), false = Hit (Miss)
    public UnityAction<bool> OnWallResolved;

    void Start()
    {
        wallRenderer = GetComponent<Renderer>();
        if(wallRenderer != null) wallRenderer.material.color = Color.gray;
        
        FindHands();
    }

    private void FindHands()
    {
        var sim = Object.FindAnyObjectByType<DualHandSimulator>();
        if (sim != null)
        {
            leftHand = sim.leftHand;
            rightHand = sim.rightHand;
        }
    }

    public void ActivateObstacle()
    {
        currentState = WallState.Active;
        timer = activeDuration;
        if(wallRenderer != null) wallRenderer.material.color = Color.magenta;
        FindHands();
    }

    void Update()
    {
        if (currentState != WallState.Active) return;

        if (timer > 0)
        {
            timer -= Time.deltaTime;

            bool hitLeft = leftHand != null && Vector3.Distance(leftHand.position, transform.position) <= hitDistance;
            bool hitRight = rightHand != null && Vector3.Distance(rightHand.position, transform.position) <= hitDistance;

            if (hitLeft || hitRight)
            {
                // Hit the wall -> BREAK (MISS)
                RegisterResult(false);
            }
        }
        else
        {
            // Time ran out and didn't hit it -> DODGED (PERFECT)
            RegisterResult(true);
        }
    }

    private void RegisterResult(bool isDodged)
    {
        currentState = isDodged ? WallState.Dodged : WallState.Hit;
        
        if (!isDodged) // If hit the wall
        {
            if (wallRenderer != null) wallRenderer.enabled = false; // Hide mesh
            if(breakVFX != null) breakVFX.Play();
            if(breakAudio != null) breakAudio.Play();
        }

        gameObject.SetActive(false);
        OnWallResolved?.Invoke(isDodged);
    }
}
