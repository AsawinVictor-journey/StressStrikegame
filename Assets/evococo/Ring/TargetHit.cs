using UnityEngine;
using UnityEngine.Events;

public class TargetHit : MonoBehaviour
{
    public enum HitState { Idle, Active, Hit, Missed }
    public HitState currentState = HitState.Idle;

    [Header("Settings")]
    public float hitDistance = 0.3f;
    public float activeDuration = 2.0f; // Time player has to hit it
    private float timer = 0f;

    [Header("References")]
    private Transform leftHand;
    private Transform rightHand;
    private Renderer ringRenderer;
    public ParticleSystem hitVFX; 
    public AudioSource hitAudio; 
    
    public UnityAction<bool> OnTargetResolved; // true = Hit, false = Miss

    void Start()
    {
        FindHands();

        ringRenderer = GetComponent<Renderer>();
        if(ringRenderer != null) ringRenderer.material.color = Color.gray; // Default inactive
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

    public void ActivateTarget()
    {
        currentState = HitState.Active;
        timer = activeDuration;
        if(ringRenderer != null) ringRenderer.material.color = Color.red; // Active but not hit
        FindHands();
    }

    void Update()
    {
        if (currentState != HitState.Active) return;

        if (timer > 0)
        {
            timer -= Time.deltaTime;

            bool hitLeft = leftHand != null && Vector3.Distance(leftHand.position, transform.position) <= hitDistance;
            bool hitRight = rightHand != null && Vector3.Distance(rightHand.position, transform.position) <= hitDistance;

            if (hitLeft || hitRight)
            {
                // Correct hand placement
                if(ringRenderer != null) ringRenderer.material.color = Color.green;
                RegisterHit(true);
            }
        }
        else
        {
            // Time ran out
            RegisterHit(false);
        }
    }

    private void RegisterHit(bool isHit)
    {
        currentState = isHit ? HitState.Hit : HitState.Missed;
        
        if (isHit)
        {
            if(hitVFX != null) hitVFX.Play();
            if(hitAudio != null) hitAudio.Play();
        }

        // Hide or destroy the target after hit
        gameObject.SetActive(false);
        
        OnTargetResolved?.Invoke(isHit);
    }
}