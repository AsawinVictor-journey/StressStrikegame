using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// "Coach Byte" survey flow, laid out to match the designer's mockups in
// Assets/UI/BriefCOPEExample (Frames 31-38):
//   intro -> question (select an answer, then confirm with Next) -> halfway
//   beat at the midpoint -> result showing the recommended mode's wordmark.
// Always skippable. Every exit routes back to the main menu, which highlights
// the recommended mode via RecommendedModeHighlighter reading the saved result.
public class BriefCopeSurveyController : MonoBehaviour
{
    private const string MenuSceneName = "MainMenuScene";

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
    [SerializeField] private Button[] answerButtons; // 4, in the order BriefCopeData.ResponseScale defines
    [SerializeField] private Button nextQuestionButton;
    [SerializeField] private Button questionSkipButton;

    [Header("Halfway")]
    [SerializeField] private Button halfwayContinueButton;

    [Header("Result")]
    [SerializeField] private TMP_Text reasonText;
    [SerializeField] private TMP_Text disclaimerText;
    [SerializeField] private Image modeWordmark;
    [SerializeField] private Sprite boxingWordmark;
    [SerializeField] private Sprite rageRoomWordmark;
    [SerializeField] private Sprite yogaWordmark;
    [SerializeField] private Button finishButton;

    [Header("Answer selection tint")]
    [SerializeField] private Color answerIdleColor = Color.white;
    [SerializeField] private Color answerSelectedColor = new Color(1f, 0.35f, 0.37f);

    private readonly Dictionary<int, int> answers = new Dictionary<int, int>();
    private int currentQuestionIndex;
    private int pendingNextIndex;
    private int? pendingAnswer;
    private const string PrefsKey = "BriefCope_LastResult";

    private void Start()
    {
        if (startButton != null) startButton.onClick.AddListener(BeginSurvey);
        if (introSkipButton != null) introSkipButton.onClick.AddListener(SkipSurvey);
        if (questionSkipButton != null) questionSkipButton.onClick.AddListener(SkipSurvey);
        if (halfwayContinueButton != null) halfwayContinueButton.onClick.AddListener(OnHalfwayContinue);
        if (nextQuestionButton != null) nextQuestionButton.onClick.AddListener(ConfirmAnswer);
        if (finishButton != null) finishButton.onClick.AddListener(CloseSurveyPopup);

        if (answerButtons != null)
        {
            for (int i = 0; i < answerButtons.Length; i++)
            {
                int value = i + 1;
                answerButtons[i].onClick.AddListener(() => SelectAnswer(value));
            }
        }

        // Ask/show survey popup every time the scene loads (active by default for now)
        if (introPanel != null && introPanel.transform.parent != null)
        {
            introPanel.transform.parent.gameObject.SetActive(true);
        }
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
        if (progressLabel != null) progressLabel.text = $"Question {index + 1} of {BriefCopeData.Questions.Length}";

        // Answer bar art has its label baked in, so only the selection tint is driven here.
        pendingAnswer = null;
        ClearAnswerTints();
        if (nextQuestionButton != null) nextQuestionButton.interactable = false;
    }

    // Tapping an answer only stages it - "Next Question" is what commits, so the
    // player can change their mind first (matches Frame 32).
    private void SelectAnswer(int value)
    {
        pendingAnswer = value;

        if (answerButtons != null)
        {
            for (int i = 0; i < answerButtons.Length; i++)
            {
                var img = answerButtons[i].targetGraphic as Image;
                if (img != null) img.color = (i + 1 == value) ? answerSelectedColor : answerIdleColor;
            }
        }

        if (nextQuestionButton != null) nextQuestionButton.interactable = true;
    }

    private void ClearAnswerTints()
    {
        if (answerButtons == null) return;
        foreach (var btn in answerButtons)
        {
            var img = btn.targetGraphic as Image;
            if (img != null) img.color = answerIdleColor;
        }
    }

    private void ConfirmAnswer()
    {
        if (!pendingAnswer.HasValue) return;

        var q = BriefCopeData.Questions[currentQuestionIndex];
        answers[q.id] = pendingAnswer.Value;
        int nextIndex = currentQuestionIndex + 1;

        // Halfway beat fires by position (middle of the list), not by a specific
        // question id - keeps working regardless of how many questions are in play.
        if (nextIndex == BriefCopeData.Questions.Length / 2)
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

    private void OnHalfwayContinue()
    {
        currentQuestionIndex = pendingNextIndex;
        ShowOnly(questionPanel);
        ShowQuestion(currentQuestionIndex);
    }

    private void SkipSurvey()
    {
        SaveResult(null, skipped: true);
        CloseSurveyPopup();
    }

    private void Finish()
    {
        ShowOnly(resultPanel);

        var rec = GameModeRecommendation.Recommend(answers);

        if (reasonText != null) reasonText.text = rec.reason;
        // Guardrail (see BRIEF_COPE_CONTEXT.md): the not-a-diagnosis disclaimer
        // stays attached to every recommendation.
        if (disclaimerText != null) disclaimerText.text = GameModeRecommendation.Disclaimer;

        if (modeWordmark != null)
        {
            modeWordmark.sprite = WordmarkFor(rec.mode);
            modeWordmark.preserveAspect = true;
            modeWordmark.enabled = modeWordmark.sprite != null;
        }

        SaveResult(rec.mode, skipped: false);
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

    private void CloseSurveyPopup()
    {
        if (introPanel != null && introPanel.transform.parent != null)
        {
            introPanel.transform.parent.gameObject.SetActive(false);
        }
        gameObject.SetActive(false);

        // Trigger highlights immediately without scene reload
#if UNITY_2023_1_OR_NEWER
        var highlighter = FindFirstObjectByType<RecommendedModeHighlighter>();
#else
        var highlighter = FindObjectOfType<RecommendedModeHighlighter>();
#endif
        if (highlighter != null)
        {
            highlighter.TriggerHighlight();
        }
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
