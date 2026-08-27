using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Brief-COPE's original result screen (ResultPanel/reasonText/disclaimerText/
// modeWordmark/finishButton), reused as-is but now driven by CheckInSceneRouter:
// it's shown once CheckInManager decides a mode, not right after the Brief-COPE
// survey - Brief-COPE itself no longer shows a result screen of its own.
public class CheckInResultPanel : MonoBehaviour
{
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private TMP_Text reasonText;
    [SerializeField] private TMP_Text disclaimerText;
    [SerializeField] private Image modeWordmark;
    [SerializeField] private Sprite boxingWordmark;
    [SerializeField] private Sprite rageRoomWordmark;
    [SerializeField] private Sprite yogaWordmark;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button skipButton;

    public event Action onContinue;
    public event Action onSkip;

    private void Awake()
    {
        if (continueButton != null) continueButton.onClick.AddListener(() => onContinue?.Invoke());
        if (skipButton != null) skipButton.onClick.AddListener(() => onSkip?.Invoke());
        // Not calling Hide() here: resultPanel is this component's own GameObject
        // (self-referencing field), and it starts inactive already (set by the
        // scene builder). Awake() only runs the first time it's activated - which
        // is exactly when Show() calls SetActive(true) - so Hide()'s SetActive(false)
        // would re-enter mid-Show() and immediately undo that activation before
        // Show() finishes setting the text, leaving the panel invisible every time.
    }

    public void Show(string mode, string reason)
    {
        CheckInModeMapping.TryToGameMode(mode, out GameMode gameMode);

        if (resultPanel != null)
        {
            resultPanel.SetActive(true);
            // Saved scene state has this at localScale (0,0,0) - every other
            // Brief-COPE panel is (1,1,1) - so activating it alone collapses it
            // to nothing: active in the hierarchy, invisible on screen.
            resultPanel.transform.localScale = Vector3.one;
        }
        if (reasonText != null) reasonText.text = reason;
        if (disclaimerText != null) disclaimerText.text = GameModeRecommendation.Disclaimer;

        if (modeWordmark != null)
        {
            modeWordmark.sprite = WordmarkFor(gameMode);
            modeWordmark.enabled = modeWordmark.sprite != null;
            if (modeWordmark.enabled) modeWordmark.SetNativeSize();
        }
    }

    public void Hide()
    {
        if (resultPanel != null) resultPanel.SetActive(false);
    }

    private Sprite WordmarkFor(GameMode mode)
    {
        switch (mode)
        {
            case GameMode.Boxing: return boxingWordmark;
            case GameMode.RageRoom: return rageRoomWordmark;
            case GameMode.Meditate: return yogaWordmark;
            default: return null;
        }
    }
}
