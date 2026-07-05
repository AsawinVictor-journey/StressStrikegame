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
    private Transform hand;
    private Renderer ringRenderer;
    public ParticleSystem hitVFX; // Placeholder for VFX
    public AudioSource hitAudio; // Placeholder for Sound
    
    public UnityAction<bool> OnTargetResolved; // true = Hit, false = Miss

    void Start()
    {
        // Try to find the MouseHand first for testing, otherwise it could be the physical hand
        GameObject handObj = GameObject.Find("MouseHand");
        if(handObj != null) hand = handObj.transform;

        ringRenderer = GetComponent<Renderer>();
        if(ringRenderer != null) ringRenderer.material.color = Color.gray; // Default inactive
    }

    public void ActivateTarget()
    {
        currentState = HitState.Active;
        timer = activeDuration;
        if(ringRenderer != null) ringRenderer.material.color = Color.red; // Active but not hit
    }

    void Update()
    {
        if (currentState != HitState.Active) return;

        if (timer > 0)
        {
            timer -= Time.deltaTime;

            if (hand != null)
            {
                float d = Vector3.Distance(hand.position, transform.position);

                if (d <= hitDistance)
                {
                    // Correct hand placement
                    if(ringRenderer != null) ringRenderer.material.color = Color.green;
                    RegisterHit(true);
                }
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