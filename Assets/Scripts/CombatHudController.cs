using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using DG.Tweening;
using UnityEngine.SceneManagement;
using System.Collections;

public class CombatHudController : MonoBehaviour
{
    [Header("UI Text & Portraits")]
    [SerializeField] private TextMeshProUGUI _playerNameText;
    [SerializeField] private TextMeshProUGUI _opponentNameText;
    [SerializeField] private TextMeshProUGUI _roundText;
    [SerializeField] private Image _playerPortraitImage;
    [SerializeField] private Image _opponentPortraitImage;

    [Header("Opponent Profiles (Assign in Inspector)")]
    [SerializeField] private string _stage1Name = "Damien";
    [SerializeField] private Sprite _stage1Icon;
    [SerializeField] private string _stage2Name = "Brock";
    [SerializeField] private Sprite _stage2Icon;
    [SerializeField] private string _stage3Name = "Silas";
    [SerializeField] private Sprite _stage3Icon;

    [Header("Player Health")]
    [SerializeField] private Image _playerHealthFillImage;
    [SerializeField] private Image _playerHealthTrailingFillImage;

    [Header("Player Stamina")]
    [SerializeField] private Image _playerStaminaFillImage;
    [SerializeField] private Image _playerStaminaTrailingFillImage;

    [Header("Opponent Health")]
    [SerializeField] private Image _opponentHealthFillImage;
    [SerializeField] private Image _opponentHealthTrailingFillImage;

    [Header("Opponent Stamina")]
    [SerializeField] private Image _opponentStaminaFillImage;
    [SerializeField] private Image _opponentStaminaTrailingFillImage;

    [Header("Special Ability")]
    [SerializeField] private Image _specialAbilityFireFillImage;

    [Header("Screen Effects")]
    [SerializeField] private Image _timeFreezeTintImage;
    [Tooltip("How visible the yellow tint is (0 = invisible, 1 = solid yellow).")]
    [SerializeField] [Range(0f, 1f)] private float _tintMaxAlpha = 0.01f;

    [Header("Tween Settings")]
    [SerializeField] private float _trailDelay = 0.4f;
    [SerializeField] private float _fillDuration = 0.25f;
    [SerializeField] private float _trailDuration = 0.3f;

    [Header("Player Stats")]
    [SerializeField] private float _playerMaxHealth = 100f;
    [SerializeField] private float _playerMaxStamina = 150f;

    [Header("Opponent Stats")]
    [SerializeField] private float _opponentMaxHealth = 200f;
    [SerializeField] private float _opponentMaxStamina = 100f;

    private float _playerCurrentHealth;
    private float _playerCurrentStamina;
    private float _opponentCurrentHealth;
    private float _opponentCurrentStamina;

    private int _currentStage = 1;
    private bool _isCleared = false;
    private bool _isMatchOver = false;

    [Header("KO Screen (Optional)")]
    [Tooltip("Assign whichever TMP_Text/Image objects your KO UI uses for each stat — " +
             "leave any of them empty to skip that stat entirely.")]
    [SerializeField] private GameObject _koScreenPanel;
    [SerializeField] private CanvasGroup _koScreenCanvasGroup;
    [SerializeField] private TextMeshProUGUI _koWinnerText;
    [SerializeField] private TextMeshProUGUI _koScoreText;
    [SerializeField] private TextMeshProUGUI _koComboStreakText;
    [SerializeField] private TextMeshProUGUI _koCoinsEarnedText;
    [SerializeField] private Image _koLevelBarFill;
    [SerializeField] private AudioClip _playerWinSound;
    [SerializeField] private AudioClip _playerLoseSound;

    [Header("Result Screen Flow")]
    [Tooltip("How long the KO screen stays up before fading into the mode menu.")]
    [SerializeField] private float _resultScreenDuration = 7f;
    [SerializeField] private string _menuSceneName = "BoxingMenu";

    public bool IsPlayerExhausted { get; private set; }
    public bool IsOpponentExhausted { get; private set; }

    [Header("Stamina Settings")]
    [SerializeField] private float _staminaRegenRate = 20f;

    // Track active tweens so we can kill them before starting new ones
    private Sequence _playerHealthSequence;
    private Sequence _playerStaminaSequence;
    private Sequence _opponentHealthSequence;
    private Sequence _opponentStaminaSequence;

    private void Awake()
    {
        Time.timeScale = 1f;

        // Use the inspector values for stats so different scenes can have different difficulty settings

        _playerCurrentHealth = _playerMaxHealth;
        _playerCurrentStamina = _playerMaxStamina;
        _opponentCurrentHealth = _opponentMaxHealth;
        _opponentCurrentStamina = _opponentMaxStamina;

        SetFillImmediate(_playerHealthFillImage, 1f);
        SetFillImmediate(_playerHealthTrailingFillImage, 1f);
        SetFillImmediate(_playerStaminaFillImage, 1f);
        SetFillImmediate(_playerStaminaTrailingFillImage, 1f);
        SetFillImmediate(_opponentHealthFillImage, 1f);
        SetFillImmediate(_opponentHealthTrailingFillImage, 1f);
        SetFillImmediate(_opponentStaminaFillImage, 1f);
        SetFillImmediate(_opponentStaminaTrailingFillImage, 1f);
        
        SetFillImmediate(_specialAbilityFireFillImage, 0f);

        UpdateRoundText();
        UpdateOpponentProfile();
    }

    private void Update()
    {
        // Debug controls — remove these when hooking up real gameplay
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            DrainPlayerHealth(10f);
        }

        if (Keyboard.current.leftShiftKey.wasPressedThisFrame)
        {
            DrainPlayerStamina(15f);
        }

        if (Keyboard.current.backspaceKey.wasPressedThisFrame)
        {
            DrainOpponentHealth(10f);
        }

        if (Keyboard.current.rightShiftKey.wasPressedThisFrame)
        {
            DrainOpponentStamina(15f);
        }

        if (Keyboard.current.upArrowKey.wasPressedThisFrame)
        {
            AdvanceRound();
        }

        // Stamina Exhaustion Tracking
        if (_playerCurrentStamina <= 0 && !IsPlayerExhausted)
        {
            IsPlayerExhausted = true;
        }
        else if (IsPlayerExhausted && _playerCurrentStamina >= _playerMaxStamina)
        {
            IsPlayerExhausted = false;
        }

        if (_opponentCurrentStamina <= 0 && !IsOpponentExhausted)
        {
            IsOpponentExhausted = true;
        }
        else if (IsOpponentExhausted && _opponentCurrentStamina >= _opponentMaxStamina)
        {
            IsOpponentExhausted = false;
        }

        // Stamina Regeneration
        if (_playerCurrentStamina < _playerMaxStamina)
        {
            RegenPlayerStamina(_staminaRegenRate * Time.deltaTime);
        }

        if (_opponentCurrentStamina < _opponentMaxStamina)
        {
            RegenOpponentStamina(_staminaRegenRate * Time.deltaTime);
        }
    }

    // ─────────────────────────────────────────────
    // Public API — Call these from gameplay scripts
    // ─────────────────────────────────────────────

    /// <summary>
    /// Advance the progression: Stage 1 -> Clear -> Stage 2 -> Clear -> Final Stage -> All Clear
    /// </summary>
    public void AdvanceRound()
    {
        if (!_isCleared)
        {
            _isCleared = true; // Beat the current stage
        }
        else
        {
            if (_currentStage < 3)
            {
                _isCleared = false; // Move to next stage
                _currentStage++;
            }
        }
        
        UpdateRoundText();
        UpdateOpponentProfile();
        
        // Optional: Animate the text when it changes
        if (_roundText != null)
        {
            _roundText.transform.DOPunchScale(Vector3.one * 0.2f, 0.3f, 10, 1f);
        }
    }

    private void UpdateRoundText()
    {
        if (_roundText == null) return;

        if (_isCleared)
        {
            if (_currentStage >= 3)
            {
                _roundText.text = "ALL CLEAR!";
                _roundText.fontSize = 60; // Make it fit better
            }
            else
            {
                _roundText.text = "CLEAR!";
                _roundText.fontSize = 72; 
            }
        }
        else
        {
            switch (_currentStage)
            {
                case 1:
                    _roundText.text = "--VS--";
                    _roundText.fontSize = 72;
                    break;
                case 2:
                    _roundText.text = "--VS--";
                    _roundText.fontSize = 72;
                    break;
                case 3:
                    _roundText.text = "--VS--";
                    _roundText.fontSize = 72;
                    break;
            }
        }
    }

    private void UpdateOpponentProfile()
    {
        if (_opponentNameText == null) return;
        
        switch (_currentStage)
        {
            case 1:
                _opponentNameText.text = _stage1Name;
                if (_opponentPortraitImage != null) _opponentPortraitImage.sprite = _stage1Icon;
                break;
            case 2:
                _opponentNameText.text = _stage2Name;
                if (_opponentPortraitImage != null) _opponentPortraitImage.sprite = _stage2Icon;
                break;
            case 3:
                _opponentNameText.text = _stage3Name;
                if (_opponentPortraitImage != null) _opponentPortraitImage.sprite = _stage3Icon;
                break;
        }
    }

    /// <summary>
    /// Drain the player's health bar by the given amount.
    /// </summary>
    public void DrainPlayerHealth(float amount)
    {
        if (_isMatchOver) return;

        if (StressStrike.ScoreManager.Instance != null) StressStrike.ScoreManager.Instance.ResetCombo();

        _playerCurrentHealth = Mathf.Max(_playerCurrentHealth - amount, 0f);
        float ratio = _playerCurrentHealth / _playerMaxHealth;
        AnimateBar(ref _playerHealthSequence, _playerHealthFillImage, _playerHealthTrailingFillImage, ratio);

        if (_playerCurrentHealth <= 0)
        {
            TriggerKO("BOT WINS", false);
        }
    }

    /// <summary>
    /// Drain the player's stamina bar by the given amount.
    /// </summary>
    public void DrainPlayerStamina(float amount)
    {
        _playerCurrentStamina = Mathf.Max(_playerCurrentStamina - amount, 0f);
        float ratio = _playerCurrentStamina / _playerMaxStamina;
        AnimateBar(ref _playerStaminaSequence, _playerStaminaFillImage, _playerStaminaTrailingFillImage, ratio);
    }

    /// <summary>
    /// Drain the opponent's health bar by the given amount.
    /// </summary>
    public void DrainOpponentHealth(float amount)
    {
        if (_isMatchOver) return;

        _opponentCurrentHealth = Mathf.Max(_opponentCurrentHealth - amount, 0f);
        float ratio = _opponentCurrentHealth / _opponentMaxHealth;
        AnimateBar(ref _opponentHealthSequence, _opponentHealthFillImage, _opponentHealthTrailingFillImage, ratio);

        if (_opponentCurrentHealth <= 0)
        {
            TriggerKO("PLAYER WINS", true);
        }
    }

    /// <summary>
    /// Drain the opponent's stamina bar by the given amount.
    /// </summary>
    public void DrainOpponentStamina(float amount)
    {
        _opponentCurrentStamina = Mathf.Max(_opponentCurrentStamina - amount, 0f);
        float ratio = _opponentCurrentStamina / _opponentMaxStamina;
        AnimateBar(ref _opponentStaminaSequence, _opponentStaminaFillImage, _opponentStaminaTrailingFillImage, ratio);
    }

    /// <summary>
    /// Restore the player's health bar by the given amount.
    /// </summary>
    public void RestorePlayerHealth(float amount)
    {
        _playerCurrentHealth = Mathf.Min(_playerCurrentHealth + amount, _playerMaxHealth);
        float ratio = _playerCurrentHealth / _playerMaxHealth;
        AnimateBar(ref _playerHealthSequence, _playerHealthFillImage, _playerHealthTrailingFillImage, ratio);
    }

    /// <summary>
    /// Restore the player's stamina bar by the given amount.
    /// </summary>
    public void RestorePlayerStamina(float amount)
    {
        _playerCurrentStamina = Mathf.Min(_playerCurrentStamina + amount, _playerMaxStamina);
        float ratio = _playerCurrentStamina / _playerMaxStamina;
        AnimateBar(ref _playerStaminaSequence, _playerStaminaFillImage, _playerStaminaTrailingFillImage, ratio);
    }

    /// <summary>
    /// Restore the opponent's health bar by the given amount.
    /// </summary>
    public void RestoreOpponentHealth(float amount)
    {
        _opponentCurrentHealth = Mathf.Min(_opponentCurrentHealth + amount, _opponentMaxHealth);
        float ratio = _opponentCurrentHealth / _opponentMaxHealth;
        AnimateBar(ref _opponentHealthSequence, _opponentHealthFillImage, _opponentHealthTrailingFillImage, ratio);
    }

    /// <summary>
    /// Restore the opponent's stamina bar by the given amount.
    /// </summary>
    public void RestoreOpponentStamina(float amount)
    {
        _opponentCurrentStamina = Mathf.Min(_opponentCurrentStamina + amount, _opponentMaxStamina);
        float ratio = _opponentCurrentStamina / _opponentMaxStamina;
        AnimateBar(ref _opponentStaminaSequence, _opponentStaminaFillImage, _opponentStaminaTrailingFillImage, ratio);
    }

    private void RegenPlayerStamina(float amount)
    {
        _playerCurrentStamina = Mathf.Min(_playerCurrentStamina + amount, _playerMaxStamina);
        float ratio = _playerCurrentStamina / _playerMaxStamina;
        SetFillImmediate(_playerStaminaFillImage, ratio);
        SetFillImmediate(_playerStaminaTrailingFillImage, ratio);
    }

    private void RegenOpponentStamina(float amount)
    {
        _opponentCurrentStamina = Mathf.Min(_opponentCurrentStamina + amount, _opponentMaxStamina);
        float ratio = _opponentCurrentStamina / _opponentMaxStamina;
        SetFillImmediate(_opponentStaminaFillImage, ratio);
        SetFillImmediate(_opponentStaminaTrailingFillImage, ratio);
    }

    // ─────────────────────────────────────────────
    // Internal tweening
    // ─────────────────────────────────────────────

    private void AnimateBar(ref Sequence activeSequence, Image fillImage, Image trailingFillImage, float targetRatio)
    {
        // Kill any running tween on this bar so rapid hits don't stack weirdly
        activeSequence?.Kill();

        Sequence sequence = DOTween.Sequence();

        // Main fill snaps down quickly
        if (fillImage != null)
        {
            sequence.Append(fillImage.DOFillAmount(targetRatio, _fillDuration)
                .SetEase(Ease.InOutSine));
        }

        // Trailing fill follows after a delay
        if (trailingFillImage != null)
        {
            sequence.AppendInterval(_trailDelay);
            sequence.Append(trailingFillImage.DOFillAmount(targetRatio, _trailDuration)
                .SetEase(Ease.InOutSine));
        }

        sequence.Play();
        activeSequence = sequence;
    }

    private static void SetFillImmediate(Image image, float amount)
    {
        if (image != null)
        {
            image.fillAmount = Mathf.Clamp01(amount);
        }
    }

    public void ActivateTimeFreezeTint(float duration)
    {
        if (_timeFreezeTintImage == null) return;
        
        _timeFreezeTintImage.DOKill();
        
        // Ensure it's yellow with 0 alpha to start
        _timeFreezeTintImage.color = new Color(1f, 1f, 0f, 0f);
        
        // Fade in to the max alpha, wait, then fade out
        Sequence sequence = DOTween.Sequence();
        sequence.Append(_timeFreezeTintImage.DOFade(_tintMaxAlpha, 0.5f));
        sequence.AppendInterval(duration - 1f); // 0.5s fade in + 0.5s fade out
        sequence.Append(_timeFreezeTintImage.DOFade(0f, 0.5f));
        sequence.Play();
    }

    public void UpdateSpecialAbilityBar(float fillRatio)
    {
        if (_specialAbilityFireFillImage != null)
        {
            // Smoothly animate the fill amount
            _specialAbilityFireFillImage.DOFillAmount(fillRatio, 0.2f).SetEase(Ease.OutSine);
        }
    }

    private void TriggerKO(string winnerText, bool isPlayerWinner)
    {
        if (_isMatchOver) return;
        _isMatchOver = true;

        int finalScore = 0;
        int highestCombo = 0;
        bool isNewHighScore = false;
        PlayerProgression.SessionRewardResult reward = default;
        if (StressStrike.ScoreManager.Instance != null)
        {
            StressStrike.ScoreManager.Instance.FinalizeMatch(out finalScore, out highestCombo, out isNewHighScore, out reward);
        }
        int coinsAwarded = reward.CoinsAwarded;

        if (_koScreenPanel != null)
        {
            _koScreenPanel.SetActive(true);

            // Fade the panel in if it has a CanvasGroup wired up, using the same UIFade
            // component Rage Room's result panel uses — falls back to the instant
            // SetActive above if no CanvasGroup is assigned.
            if (_koScreenCanvasGroup != null)
            {
                FindFirstObjectByType<UIFade>()?.ShowUI(_koScreenCanvasGroup);
            }

            // Result screen fields: Win/Lose, Score, Combo Streak, Coins Earned. Each is
            // independently null-checked and formatted plainly (no "Score:"/"Coins Earned:"
            // labels baked in) so you can freely swap which TMP_Text objects these point at
            // in the Inspector, or add your own label text around them in the UI itself.
            if (_koWinnerText != null) _koWinnerText.text = winnerText;
            if (_koScoreText != null) _koScoreText.text = finalScore.ToString();
            if (_koComboStreakText != null) _koComboStreakText.text = highestCombo.ToString();
            if (_koCoinsEarnedText != null) _koCoinsEarnedText.text = "+" + coinsAwarded;

            if (_koLevelBarFill != null)
            {
                _koLevelBarFill.fillAmount = reward.PreviousLevelProgress01;
                StartCoroutine(AnimateLevelBar(_koLevelBarFill,
                    reward.PreviousLevelProgress01, reward.LevelProgress01, reward.LeveledUp));
            }
        }
        else
        {
            // Fallback: create a simple canvas so it works without inspector setup
            GameObject canvasObj = new GameObject("KOScreenCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            
            // Add a dark semi-transparent background
            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(canvasObj.transform, false);
            Image bgImage = bgObj.AddComponent<Image>();
            bgImage.color = new Color(0, 0, 0, 0.7f);
            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;

            // Add Text
            GameObject textObj = new GameObject("KOText");
            textObj.transform.SetParent(canvasObj.transform, false);
            TextMeshProUGUI tmpText = textObj.AddComponent<TextMeshProUGUI>();
            string scoreSummary = $"Score: {finalScore}\nCombo Streak: {highestCombo}\nCoins Earned: +{coinsAwarded}";
            tmpText.text = "K.O.\n<size=50%>" + winnerText + "</size>\n<size=40%>" + scoreSummary + "</size>";
            tmpText.fontSize = 120;
            tmpText.alignment = TextAlignmentOptions.Center;
            tmpText.color = Color.red;
            tmpText.fontStyle = FontStyles.Bold;
            
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.sizeDelta = new Vector2(800, 400);
            
            // Animation
            tmpText.transform.localScale = Vector3.zero;
            tmpText.transform.DOScale(1f, 0.5f).SetEase(Ease.OutBack).SetUpdate(true);
        }

        // Play KO Sound based on who won
        AudioClip soundToPlay = isPlayerWinner ? _playerWinSound : _playerLoseSound;
        if (soundToPlay != null)
        {
            AudioSource audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }
            audioSource.PlayOneShot(soundToPlay);
        }

        // Slow down time for dramatic effect
        Time.timeScale = 0.2f;
        
        // Disable player and bot scripts to stop further actions
        var playerAnim = FindObjectOfType<animationStateController>();
        if (playerAnim != null) playerAnim.enabled = false;

        var botAnim = FindObjectOfType<BotAnimationControll>();
        if (botAnim != null) botAnim.enabled = false;

        StartCoroutine(ReturnToMainMenuRoutine());
    }

    private IEnumerator ReturnToMainMenuRoutine()
    {
        // Wait using unscaled time because timeScale is 0.2f during the KO freeze-frame.
        yield return new WaitForSecondsRealtime(_resultScreenDuration);

        // Restore time scale
        Time.timeScale = 1f;

        // Fade into Boxing's own menu scene (SceneTransitionManager fades to black,
        // loads, then fades back in), matching Rage Room/Yoga's result-screen flow.
        if (SceneTransitionManager.Instance != null)
            SceneTransitionManager.Instance.LoadScene(_menuSceneName);
        else
            SceneManager.LoadScene(_menuSceneName);
    }

    // Animates the level bar filling up when the KO screen appears, rather than snapping
    // straight to the post-match value. On a level-up it fills to full, snaps back to empty,
    // then continues filling into the new level, so a level-up reads as a level-up rather than
    // the bar just landing on a lower-looking number. Uses unscaled time since Time.timeScale
    // is 0.2f during the KO freeze-frame.
    private IEnumerator AnimateLevelBar(Image bar, float fromProgress, float toProgress, bool leveledUp)
    {
        const float fillDuration = 0.5f;

        if (leveledUp)
        {
            yield return LerpFill(bar, fromProgress, 1f, fillDuration);
            bar.fillAmount = 0f;
            yield return LerpFill(bar, 0f, toProgress, fillDuration);
        }
        else
        {
            yield return LerpFill(bar, fromProgress, toProgress, fillDuration);
        }
    }

    private IEnumerator LerpFill(Image bar, float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            bar.fillAmount = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        bar.fillAmount = to;
    }

    private void OnDestroy()
    {
        // Clean up all tweens when this object is destroyed
        _playerHealthSequence?.Kill();
        _playerStaminaSequence?.Kill();
        _opponentHealthSequence?.Kill();
        _opponentStaminaSequence?.Kill();
    }
}
