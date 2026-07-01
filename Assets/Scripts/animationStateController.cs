using UnityEngine;

public class animationStateController : MonoBehaviour
{
    Animator animator;
    int isJabHash;
    int isLeftJabHash;
    int isLeftHookHash;
    int isHookHash;
    int isBlockHash;
    int isLeftBlockHash;

    private CombatHudController combatHud;
    private ScoreManager scoreManager;

    [Header("Audio Settings")]
    public AudioClip specialAbilitySound;
    public AudioClip secretAudioClip;
    private AudioSource audioSource;

    bool isValidSetup = false;

    private int punchCount = 0;
    private int requiredPunchesForSpecial = 5;

    void Start()
    {
        animator = GetComponent<Animator>();
        
        if (animator == null)
        {
            Debug.LogError($"[animationStateController] No Animator component found on GameObject: '{gameObject.name}'", gameObject);
            return;
        }

        if (animator.runtimeAnimatorController == null)
        {
            Debug.LogError($"[animationStateController] The Animator on '{gameObject.name}' is missing an Animator Controller! Please assign one in the Inspector.", gameObject);
            return;
        }

        isJabHash = Animator.StringToHash("isJab");
        isLeftJabHash = Animator.StringToHash("isLeftJab");
        isLeftHookHash = Animator.StringToHash("isLeftHook");
        isHookHash = Animator.StringToHash("isHook");
        isBlockHash = Animator.StringToHash("isBlock");
        isLeftBlockHash = Animator.StringToHash("isLeftBlock");
        
        // This confirms everything is set up before allowing Update to run
        combatHud = FindObjectOfType<CombatHudController>();
        scoreManager = FindObjectOfType<ScoreManager>();

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        isValidSetup = true; 
    }

    void Update()
    {
        // Don't run animation logic if the animator isn't set up properly
        if (!isValidSetup) return;

        bool isJab = animator.GetBool(isJabHash);
        bool isLeftJab = animator.GetBool(isLeftJabHash);
        bool isLeftHook = animator.GetBool(isLeftHookHash);
        bool isHook = animator.GetBool(isHookHash);
        bool isBlock = animator.GetBool(isBlockHash);
        bool isLeftBlock = animator.GetBool(isLeftBlockHash);
        
        bool canAct = combatHud == null || !combatHud.IsPlayerExhausted;

        bool Jpressed = Input.GetKey(KeyCode.J) && canAct;
        bool Kpressed = Input.GetKey(KeyCode.K) && canAct;
        bool Lpressed = Input.GetKey(KeyCode.M) && canAct;
        bool Hpressed = Input.GetKey(KeyCode.H) && canAct;
        
        // Blocking does not cost stamina and is not prevented by exhaustion
        bool Bpressed = Input.GetKey(KeyCode.B);
        bool Npressed = Input.GetKey(KeyCode.N);
        
        float jabStaminaCost = 10f;
        float hookStaminaCost = 15f;

        if (!isJab && Jpressed)
        {
            animator.SetBool(isJabHash, true); // Changed "isJab" to isJabHash for consistency
        }

        if (Input.GetKeyDown(KeyCode.J) && canAct)
        {
            if (combatHud != null) combatHud.DrainPlayerStamina(jabStaminaCost);
            if (combatHud != null) combatHud.DrainOpponentHealth(15f);
            if (scoreManager != null) scoreManager.RegisterHit(15f);
            RegisterPunch();
        }

        if (isJab && !Jpressed)
        {
            animator.SetBool(isJabHash, false); // Changed "isJab" to isJabHash for consistency
        }
//////////////////////////////////////////////////////////////////
        if (!isLeftHook && Kpressed)
        {
            animator.SetBool(isLeftHookHash, true);
        }
        
        if (Input.GetKeyDown(KeyCode.K) && canAct)
        {
            if (combatHud != null) combatHud.DrainPlayerStamina(hookStaminaCost);
            if (combatHud != null) combatHud.DrainOpponentHealth(15f);
            if (scoreManager != null) scoreManager.RegisterHit(15f);
            RegisterPunch();
        }
        
        if (isLeftHook && !Kpressed)
        {
            animator.SetBool(isLeftHookHash, false);
        }
///////////////////////////////////////////////////////////////////
        if (!isHook && Hpressed)
        {
            animator.SetBool(isHookHash, true);
        }

        if (Input.GetKeyDown(KeyCode.H) && canAct)
        {
            if (combatHud != null) combatHud.DrainPlayerStamina(hookStaminaCost);
            if (combatHud != null) combatHud.DrainOpponentHealth(15f);
            if (scoreManager != null) scoreManager.RegisterHit(15f);
            RegisterPunch();
        }

        if (isHook && !Hpressed)
        {
            animator.SetBool(isHookHash, false);
        }
////////////////////////////////////////////////////////////////////
        if (!isLeftJab && Lpressed)
        {
            animator.SetBool(isLeftJabHash, true);
        }

        if (Input.GetKeyDown(KeyCode.M) && canAct)
        {
            if (combatHud != null) combatHud.DrainPlayerStamina(jabStaminaCost);
            if (combatHud != null) combatHud.DrainOpponentHealth(15f);
            if (scoreManager != null) scoreManager.RegisterHit(15f);
            RegisterPunch();
        }

        if (isLeftJab && !Lpressed)
        {
            animator.SetBool(isLeftJabHash, false);
        }
/////////////////////////////////////////////////////////////////////
        if (!isBlock && Bpressed)
        {
            animator.SetBool(isBlockHash, true);
        }

        if (isBlock && !Bpressed)
        {
            animator.SetBool(isBlockHash, false);
        }

        if (!isLeftBlock && Npressed)
        {
            animator.SetBool(isLeftBlockHash, true);
        }

        if (isLeftBlock && !Npressed)
        {
            animator.SetBool(isLeftBlockHash, false);
        }
////////////////////////////////////////////////////////////////
        
        ////////////////////////////////////////////////////
        if (Input.GetKeyDown(KeyCode.F))
        {
            if (secretAudioClip != null && audioSource != null)
            {
                audioSource.PlayOneShot(secretAudioClip);
            }
        }

        if (Input.GetKeyDown(KeyCode.P) && canAct)
        {
            if (punchCount >= requiredPunchesForSpecial)
            {
                punchCount = 0;
                if (combatHud != null) combatHud.UpdateSpecialAbilityBar(0f);
                
                animator.SetTrigger("isSpecial");
                animator.SetTrigger("isLeftSpecial");
                
                if (specialAbilitySound != null && audioSource != null)
                {
                    audioSource.PlayOneShot(specialAbilitySound);
                }
                
                if (combatHud != null)
                {
                    combatHud.DrainPlayerStamina(30f); // Special costs more
                    combatHud.ActivateTimeFreezeTint(10f);
                }
                
                BotAnimationControll botController = FindObjectOfType<BotAnimationControll>();
                if (botController != null)
                {
                    botController.FreezeBot(10f);
                }
            }
        }
    }

    private void RegisterPunch()
    {
        if (punchCount < requiredPunchesForSpecial)
        {
            punchCount++;
            if (combatHud != null) 
            {
                combatHud.UpdateSpecialAbilityBar((float)punchCount / requiredPunchesForSpecial);
            }
        }
    }
}
