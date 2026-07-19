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
    [SerializeField] private TMP_Text introTextTop;
    [SerializeField] private Button startButton;
    [SerializeField] private Button introSkipButton;

    [Header("Question")]
    [SerializeField] private TMP_Text questionText;
    [SerializeField] private TMP_Text progressLabel;
    [SerializeField] private Button[] answerButtons; // 4, in the order BriefCopeData.ResponseScale defines
    [SerializeField] private Button nextQuestionButton;
    [SerializeField] private Button questionSkipButton;

    [Header("Halfway")]
    [SerializeField] private TMP_Text halfwayText;
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

    [Header("AI Coach (Ollama, local)")]
    [SerializeField] private bool useAiCoachMessage = true;
    // Must be a model actually pulled on this machine (`ollama list`). gemma3:4b was
    // never installed here, so every request failed silently into the onError path.
    [SerializeField] private string ollamaModel = "gemma4:e4b";

    private readonly Dictionary<int, int> answers = new Dictionary<int, int>();
    private int currentQuestionIndex;
    private int pendingNextIndex;
    private int? pendingAnswer;
    private string currentSurveyId;
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
        PrepareIntro();
    }

    // Intro line is personalised from the PREVIOUS session (last saved result +
    // last coaching message), because at this point the player hasn't answered
    // anything yet. Deterministic copy shows first so the panel is never blank
    // or half-written while Ollama thinks.
    private void PrepareIntro()
    {
        if (introTextTop == null) return;

        var previous = LoadPreviousResult();
        bool returning = previous != null && !previous.skipped && !string.IsNullOrEmpty(previous.mode);

        introTextTop.text = returning
            ? "Welcome back. Let's see where your head's at today."
            : "Hey, I'm Coach Byte. A few quick questions and I'll point you at a mode.";

        if (!useAiCoachMessage) return;

        string prompt =
            "You are Coach Byte, a friendly, upbeat AI coach in a stress-relief game. " +
            (returning
                ? $"The player is returning; last time their recommended mode was '{previous.mode}'. " +
                  "Greet them back and say you want to check in on how they're coping today. "
                : "The player is about to take a short coping-style check-in for the first time. Introduce yourself warmly. ") +
            "Write 1 short sentence (max 25 words) to show above the survey intro. " +
            "Do not diagnose them or use clinical language. No emojis, no quotation marks.";

        StartCoroutine(OllamaClient.Generate(
            ollamaModel, prompt,
            onSuccess: aiText =>
            {
                if (string.IsNullOrWhiteSpace(aiText) || introTextTop == null) return;
                introTextTop.text = aiText;
                CoachByteHistory.Append("BriefCopeIntro", previous?.mode ?? "", aiText);
            },
            onError: err => Debug.LogWarning("[CoachByte] " + err)
        ));
    }

    private BriefCopeResult LoadPreviousResult()
    {
        string json = PlayerPrefs.GetString(PrefsKey, "");
        if (string.IsNullOrEmpty(json)) return null;
        try { return JsonUtility.FromJson<BriefCopeResult>(json); }
        catch { return null; }
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
            PrepareHalfway();
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

    // Mid-survey encouragement. Grounded in the partial answers so it reflects the
    // leaning so far, but deliberately never names a mode - the recommendation is
    // the result screen's job, and the pattern can still flip on the back half.
    private void PrepareHalfway()
    {
        if (halfwayText == null) return;

        halfwayText.text = "Nice work — you're halfway. Keep going, there's no wrong answer here.";

        if (!useAiCoachMessage) return;

        string leaning = "";
        try
        {
            var partialRec = GameModeRecommendation.Recommend(answers);
            leaning = $"So far their answers lean toward: {partialRec.reason} ";
        }
        catch
        {
            // Partial scoring can be inconclusive this early; the generic prompt is fine.
        }

        string prompt =
            "You are Coach Byte, a friendly, upbeat AI coach in a stress-relief game. " +
            "A player is exactly halfway through a short coping-style check-in. " + leaning +
            "Write 1 short encouraging sentence (max 25 words) to keep them going. " +
            "Do NOT name or suggest any game mode yet. Do not diagnose them or use clinical " +
            "language. No emojis, no quotation marks.";

        StartCoroutine(OllamaClient.Generate(
            ollamaModel, prompt,
            onSuccess: aiText =>
            {
                if (string.IsNullOrWhiteSpace(aiText) || halfwayText == null) return;
                halfwayText.text = aiText;
                CoachByteHistory.Append("BriefCopeHalfway", "", aiText);
            },
            onError: err => Debug.LogWarning("[CoachByte] " + err)
        ));
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

        // Skip rate is worth measuring - if most players bail, the survey is too long.
        StressStrike.Cloud.CloudSyncService.Instance?.RecordSurvey(
            new StressStrike.Cloud.SurveyRecord { skipped = true });

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

        // Local save above is the source of truth; this is the opt-in cloud mirror.
        // A fresh id per completed survey, reused by the AI-text update below.
        currentSurveyId = Guid.NewGuid().ToString("N");
        SyncSurvey(rec, null);

        // Deterministic text above is shown immediately (works with Ollama offline).
        // If the local Ollama server answers in time, its reply replaces reasonText
        // with a warmer, non-canned version - purely cosmetic, never blocks the flow.
        if (useAiCoachMessage && reasonText != null)
        {
            StartCoroutine(OllamaClient.Generate(
                ollamaModel,
                BuildCoachPrompt(rec),
                onSuccess: aiText =>
                {
                    if (string.IsNullOrWhiteSpace(aiText)) return;
                    reasonText.text = aiText;
                    CoachByteHistory.Append("BriefCopeSurvey", rec.mode.ToString(), aiText);
                    // Mirror the AI-written version once it exists. Same surveyId as
                    // the send below, so this updates that record instead of adding one.
                    SyncSurvey(rec, aiText);
                },
                onError: err => Debug.LogWarning("[CoachByte] " + err)
            ));
        }
    }

    // No-op unless the player has opted into cloud sync (CloudSyncService.SetConsent).
    // Only the recommendation and winning subscale go up - never the individual
    // question answers. See CloudModels.SurveyRecord for why.
    private void SyncSurvey(ModeRecommendation rec, string aiMessage)
    {
        var sync = StressStrike.Cloud.CloudSyncService.Instance;
        if (sync == null) return;

        sync.RecordSurvey(new StressStrike.Cloud.SurveyRecord
        {
            surveyId = currentSurveyId,
            skipped = false,
            recommendedMode = rec.mode.ToString(),
            topSubscale = rec.topSubscale.ToString(),
            aiMessage = aiMessage ?? "",
            aiModel = string.IsNullOrEmpty(aiMessage) ? "" : ollamaModel,
        });
    }

    private string BuildCoachPrompt(ModeRecommendation rec)
    {
        return "You are Coach Byte, a friendly, upbeat AI coach in a stress-relief boxing game. " +
               $"A player just took a short coping-style survey and the recommended mode is '{rec.modeName}'. " +
               $"The reason: {rec.reason} " +
               "Write 1-2 short, warm sentences (max 40 words) explaining this recommendation directly to the player. " +
               "Do not diagnose them or use clinical language - this is just a game mode suggestion, not a diagnosis. " +
               "No emojis, no quotation marks.";
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
