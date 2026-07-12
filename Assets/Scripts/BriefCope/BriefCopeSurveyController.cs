using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// Ports the "Coach Byte" flow from docs/brief-cope-prototype/index.html:
// intro -> one question at a time (with Back) -> halfway beat after Q14 ->
// result with all 3 modes selectable (recommended one badged). Always skippable.
public class BriefCopeSurveyController : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject introPanel;
    [SerializeField] private GameObject questionPanel;
    [SerializeField] private GameObject halfwayPanel;
    [SerializeField] private GameObject resultPanel;

    [Header("Intro")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button introSkipButton;

    [Header("Question")]
    [SerializeField] private TMP_Text questionText;
    [SerializeField] private TMP_Text progressLabel;
    [SerializeField] private Slider progressBar;
    [SerializeField] private Button[] answerButtons; // expects 4, matching BriefCopeData.ResponseScale
    [SerializeField] private Button backButton;
    [SerializeField] private Button questionSkipButton;

    [Header("Halfway")]
    [SerializeField] private Button halfwayContinueButton;

    [Header("Result")]
    [SerializeField] private TMP_Text reasonText;
    [SerializeField] private TMP_Text modeNameText;
    [SerializeField] private TMP_Text coachMessageText;
    [SerializeField] private TMP_Text disclaimerText;
    [SerializeField] private Button playButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private ModeCard[] modeCards; // exactly 3, one per GameMode

    [Serializable]
    public class ModeCard
    {
        public GameMode mode;
        public Button button;
        public GameObject recommendedBadge;
        public GameObject selectedIndicator;
        public TMP_Text iconText;
        public TMP_Text titleText;
        public TMP_Text blurbText;
    }

    private readonly Dictionary<int, int> answers = new Dictionary<int, int>();
    private int currentQuestionIndex;
    private int pendingNextIndex;
    private GameMode? selectedMode;
    private const string PrefsKey = "BriefCope_LastResult";

    private void Awake()
    {
        if (startButton != null) startButton.onClick.AddListener(BeginSurvey);
        if (introSkipButton != null) introSkipButton.onClick.AddListener(SkipToPicker);
        if (questionSkipButton != null) questionSkipButton.onClick.AddListener(SkipToPicker);
        if (backButton != null) backButton.onClick.AddListener(OnBack);
        if (halfwayContinueButton != null) halfwayContinueButton.onClick.AddListener(OnHalfwayContinue);
        if (restartButton != null) restartButton.onClick.AddListener(BeginSurvey);
        if (playButton != null) playButton.onClick.AddListener(OnPlay);

        for (int i = 0; i < answerButtons.Length; i++)
        {
            int value = i + 1; // buttons map 1..4 in the order BriefCopeData.ResponseScale defines
            answerButtons[i].onClick.AddListener(() => OnAnswer(value));
        }

        if (modeCards != null)
        {
            foreach (var card in modeCards)
            {
                var m = card.mode;
                card.button.onClick.AddListener(() => OnModeCardClicked(m));

                var info = FindModeCardInfo(m);
                if (card.iconText != null) card.iconText.text = info.icon;
                if (card.titleText != null) card.titleText.text = info.title;
                if (card.blurbText != null) card.blurbText.text = info.blurb;
            }
        }
    }

    private void Start()
    {
        ShowOnly(introPanel);
    }

    private void BeginSurvey()
    {
        answers.Clear();
        currentQuestionIndex = 0;
        ShowOnly(questionPanel);
        ShowQuestion(0);
    }

    private void ShowQuestion(int index)
    {
        var q = BriefCopeData.Questions[index];
        if (questionText != null) questionText.text = q.text;

        if (progressBar != null) progressBar.value = (float)index / BriefCopeData.Questions.Length;
        if (progressLabel != null) progressLabel.text = $"Question {index + 1} of {BriefCopeData.Questions.Length}";

        for (int i = 0; i < answerButtons.Length && i < BriefCopeData.ResponseScale.Length; i++)
        {
            var label = answerButtons[i].GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = BriefCopeData.ResponseScale[i].label;
        }

        if (backButton != null) backButton.gameObject.SetActive(index != 0);
    }

    private void OnAnswer(int value)
    {
        var q = BriefCopeData.Questions[currentQuestionIndex];
        answers[q.id] = value;
        int nextIndex = currentQuestionIndex + 1;

        if (q.id == 14)
        {
            pendingNextIndex = nextIndex;
            ShowOnly(halfwayPanel);
            return;
        }

        if (nextIndex >= BriefCopeData.Questions.Length)
        {
            Finish();
        }
        else
        {
            currentQuestionIndex = nextIndex;
            ShowQuestion(currentQuestionIndex);
        }
    }

    private void OnBack()
    {
        if (currentQuestionIndex <= 0) return;
        currentQuestionIndex--;
        ShowQuestion(currentQuestionIndex);
    }

    private void OnHalfwayContinue()
    {
        currentQuestionIndex = pendingNextIndex;
        ShowOnly(questionPanel);
        ShowQuestion(currentQuestionIndex);
    }

    private void SkipToPicker()
    {
        // Drops the player into the same 3-mode picker with nothing pre-selected.
        ShowOnly(resultPanel);

        if (reasonText != null) reasonText.text = "";
        if (modeNameText != null) modeNameText.text = "Pick a mode";
        if (coachMessageText != null) coachMessageText.text = "No problem — hop in whenever you're ready. Pick any mode you like below.";
        if (disclaimerText != null) disclaimerText.text = "";
        if (restartButton != null) restartButton.gameObject.SetActive(false);

        RenderModeGrid(null);
        SaveResult(null, skipped: true);
    }

    private void Finish()
    {
        ShowOnly(resultPanel);

        var rec = GameModeRecommendation.Recommend(answers);

        if (reasonText != null) reasonText.text = rec.reason;
        if (modeNameText != null) modeNameText.text = rec.modeName;
        if (coachMessageText != null) coachMessageText.text = rec.coachMessage;
        if (disclaimerText != null) disclaimerText.text = GameModeRecommendation.Disclaimer;
        if (restartButton != null) restartButton.gameObject.SetActive(true);

        RenderModeGrid(rec.mode);
        SaveResult(rec.mode, skipped: false);
    }

    private void RenderModeGrid(GameMode? recommendedMode)
    {
        selectedMode = recommendedMode;

        if (modeCards != null)
        {
            foreach (var card in modeCards)
            {
                bool isRecommended = recommendedMode.HasValue && card.mode == recommendedMode.Value;
                bool isSelected = selectedMode.HasValue && card.mode == selectedMode.Value;
                if (card.recommendedBadge != null) card.recommendedBadge.SetActive(isRecommended);
                if (card.selectedIndicator != null) card.selectedIndicator.SetActive(isSelected);
            }
        }

        if (playButton != null) playButton.interactable = selectedMode.HasValue;
    }

    private void OnModeCardClicked(GameMode mode)
    {
        selectedMode = mode;
        if (modeCards != null)
        {
            foreach (var card in modeCards)
            {
                bool isSelected = card.mode == mode;
                if (card.selectedIndicator != null) card.selectedIndicator.SetActive(isSelected);
            }
        }
        if (playButton != null) playButton.interactable = true;
    }

    private void OnPlay()
    {
        if (!selectedMode.HasValue) return;
        string sceneName = GameModeRecommendation.SceneNames[selectedMode.Value];
        if (SceneTransitionManager.Instance != null)
            SceneTransitionManager.Instance.LoadScene(sceneName);
        else
            SceneManager.LoadScene(sceneName);
    }

    private static ModeCardInfo FindModeCardInfo(GameMode mode)
    {
        foreach (var info in GameModeRecommendation.ModeCards)
            if (info.mode == mode) return info;
        return default;
    }

    private void ShowOnly(GameObject panel)
    {
        if (introPanel != null) introPanel.SetActive(panel == introPanel);
        if (questionPanel != null) questionPanel.SetActive(panel == questionPanel);
        if (halfwayPanel != null) halfwayPanel.SetActive(panel == halfwayPanel);
        if (resultPanel != null) resultPanel.SetActive(panel == resultPanel);
    }

    private void SaveResult(GameMode? mode, bool skipped)
    {
        var result = new BriefCopeResult
        {
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            mode = mode?.ToString() ?? "",
            skipped = skipped,
        };
        PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(result));
        PlayerPrefs.Save();
    }
}
