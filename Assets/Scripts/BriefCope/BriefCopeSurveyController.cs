using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// "Coach Byte" survey flow, laid out to match the designer's mockups in
// Assets/UI/BriefCOPEExample (Frames 31-38):
//   intro -> question (select an answer, then confirm with Next) -> halfway
//   beat at the midpoint -> straight into this session's AI check-in.
// Always skippable. Brief-COPE itself no longer shows a result screen - its
// old ResultPanel/wordmark UI was handed off to CheckInResultPanel, shown once
// the check-in actually decides a mode. Every exit still highlights the
// recommended mode on the main menu via RecommendedModeHighlighter.
public class BriefCopeSurveyController : MonoBehaviour
{
    private const string MenuSceneName = "MainMenuScene";

    [Header("Check-in handoff")]
    [Tooltip("The check-in canvas living in this same scene. Activated once Brief-COPE " +
             "finishes or is skipped (and immediately for a returning player).")]
    [SerializeField] private GameObject checkInCanvas;

    [Header("Panels")]
    [SerializeField] private GameObject introPanel;
    [SerializeField] private GameObject questionPanel;
    [SerializeField] private GameObject halfwayPanel;

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

    [Header("Answer selection tint")]
    [SerializeField] private Color answerIdleColor = Color.white;
    [SerializeField] private Color answerSelectedColor = new Color(1f, 0.35f, 0.37f);

    [Header("AI Coach (Gemini, via backend)")]
    [SerializeField] private bool useAiCoachMessage = true;
    [SerializeField] private string geminiModel = "gemini-3.5-flash-lite";

    private readonly Dictionary<int, int> answers = new Dictionary<int, int>();
    private readonly List<int> currentBatchQuestionIds = new List<int>();
    private int currentQuestionIndex;
    private int pendingNextIndex;
    private int? pendingAnswer;
    private string currentSurveyId;
    private const string PrefsKey = "BriefCope_LastResult";
    private const string LastCompletedDateKey = "BriefCope_LastCompletedDate";
    private const string QuestionOrderKey = "BriefCope_QuestionOrder";
    private const string QuestionCursorKey = "BriefCope_QuestionCursor";
    private const string CurrentBatchKey = "BriefCope_CurrentBatch";
    private const string CurrentBatchDateKey = "BriefCope_CurrentBatchDate";
    private const string AccumulatedAnswersKey = "BriefCope_AccumulatedAnswers";
    private const int QuestionsPerDay = 5;

    [Serializable]
    private class AnswerEntry
    {
        public int id;
        public int value;
    }

    [Serializable]
    private class AnswerStore
    {
        public List<AnswerEntry> entries = new List<AnswerEntry>();
    }

    private void Start()
    {
        if (startButton != null) startButton.onClick.AddListener(BeginSurvey);
        if (introSkipButton != null) introSkipButton.onClick.AddListener(SkipSurvey);
        if (questionSkipButton != null) questionSkipButton.onClick.AddListener(SkipSurvey);
        if (halfwayContinueButton != null) halfwayContinueButton.onClick.AddListener(OnHalfwayContinue);
        if (nextQuestionButton != null) nextQuestionButton.onClick.AddListener(ConfirmAnswer);

        if (answerButtons != null)
        {
            for (int i = 0; i < answerButtons.Length; i++)
            {
                int value = i + 1;
                answerButtons[i].onClick.AddListener(() => SelectAnswer(value));
            }
        }

        // Once this session's check-in has already resolved (survey finished/skipped,
        // then check-in decided/skipped), don't show the survey popup again just because
        // the player came back to the main menu (e.g. after finishing a mode) - both the
        // survey and the check-in it hands off to should only appear once per app run.
        if (CheckInManager.HasCheckedInThisSession)
        {
            HideSurveyPopup();
            return;
        }

        var previous = LoadPreviousResult();
        string today = LocalDateString();
        string completedDate = PlayerPrefs.GetString(LastCompletedDateKey, "");

        // Older builds only stored the result timestamp. Migrate that value to the
        // date gate so an old result suppresses the survey only for its actual day.
        if (string.IsNullOrEmpty(completedDate) && previous != null && previous.timestamp > 0)
        {
            completedDate = LocalDateString(previous.timestamp);
            PlayerPrefs.SetString(LastCompletedDateKey, completedDate);
            PlayerPrefs.Save();
        }

        // A completed batch is a daily event, not a lifetime event. On a new day we
        // show the next persisted batch and keep the old recommendation as fallback.
        if (completedDate == today)
        {
            HideSurveyPopup();
            GoToCheckInIfNeeded();
            return;
        }

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

        StartCoroutine(GeminiClient.Generate(
            geminiModel, prompt,
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
        foreach (var entry in LoadAccumulatedAnswers()) answers[entry.Key] = entry.Value;
        EnsureTodayBatch();
        currentQuestionIndex = 0;
        ShowOnly(questionPanel);
        ShowQuestion(0);
    }

    private void ShowQuestion(int index)
    {
        var q = QuestionById(currentBatchQuestionIds[index]);
        if (questionText != null) questionText.text = q.text;
        if (progressLabel != null) progressLabel.text = $"Question {index + 1} of {currentBatchQuestionIds.Count}";

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

        var q = QuestionById(currentBatchQuestionIds[currentQuestionIndex]);
        answers[q.id] = pendingAnswer.Value;
        int nextIndex = currentQuestionIndex + 1;

        // Halfway beat fires by position (middle of the list), not by a specific
        // question id - keeps working regardless of how many questions are in play.
        int halfwayIndex = currentBatchQuestionIds.Count / 2;
        if (halfwayIndex > 0 && nextIndex == halfwayIndex)
        {
            pendingNextIndex = nextIndex;
            ShowOnly(halfwayPanel);
            PrepareHalfway();
            return;
        }

        if (nextIndex >= currentBatchQuestionIds.Count)
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

        StartCoroutine(GeminiClient.Generate(
            geminiModel, prompt,
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
        var accumulatedAnswers = LoadAccumulatedAnswers();
        foreach (var answer in answers)
        {
            accumulatedAnswers[answer.Key] = answer.Value;
        }
        SaveAccumulatedAnswers(accumulatedAnswers);
        SaveDailyResult(null, skipped: true);

        // Skip rate is worth measuring - if most players bail, the survey is too long.
        StressStrike.Cloud.CloudSyncService.Instance?.RecordSurvey(
            new StressStrike.Cloud.SurveyRecord { skipped = true });

        CloseSurveyPopup();
    }

    private void Finish()
    {
        SaveAccumulatedAnswers(answers);

        ModeRecommendation? rec = null;
        if (HasAnsweredEverySubscale(answers))
            rec = GameModeRecommendation.Recommend(answers);

        SaveDailyResult(rec);

        // Local save above is the source of truth; this is the opt-in cloud mirror.
        currentSurveyId = Guid.NewGuid().ToString("N");
        if (rec.HasValue) SyncSurvey(rec.Value, null);

        // No result screen here anymore - CheckInResultPanel shows the result,
        // once the check-in that follows actually decides a mode.
        CloseSurveyPopup();
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
            aiModel = string.IsNullOrEmpty(aiMessage) ? "" : geminiModel,
        });
    }

    private void CloseSurveyPopup()
    {
        HideSurveyPopup();

        // Trigger highlights immediately in case the check-in scene fails to load
        // (see SceneTransitionManager's fallback) and the player stays on this menu.
#if UNITY_2023_1_OR_NEWER
        var highlighter = FindFirstObjectByType<RecommendedModeHighlighter>();
#else
        var highlighter = FindObjectOfType<RecommendedModeHighlighter>();
#endif
        if (highlighter != null)
        {
            highlighter.TriggerHighlight();
        }

        GoToCheckInIfNeeded();
    }

    private void HideSurveyPopup()
    {
        if (introPanel != null && introPanel.transform.parent != null)
        {
            introPanel.transform.parent.gameObject.SetActive(false);
        }
        gameObject.SetActive(false);
    }

    private void GoToCheckInIfNeeded()
    {
        if (CheckInManager.HasCheckedInThisSession) return;

        if (checkInCanvas == null)
        {
            Debug.LogError("BriefCopeSurveyController: checkInCanvas not assigned.");
            return;
        }

        checkInCanvas.SetActive(true);
    }

    private void ShowOnly(GameObject panel)
    {
        if (introPanel != null) introPanel.SetActive(panel == introPanel);
        if (questionPanel != null) questionPanel.SetActive(panel == questionPanel);
        if (halfwayPanel != null) halfwayPanel.SetActive(panel == halfwayPanel);
    }

    private void EnsureTodayBatch()
    {
        string today = LocalDateString();
        string batchDate = PlayerPrefs.GetString(CurrentBatchDateKey, "");
        string savedBatch = PlayerPrefs.GetString(CurrentBatchKey, "");

        if (batchDate == today && !string.IsNullOrEmpty(savedBatch))
        {
            currentBatchQuestionIds.Clear();
            foreach (string token in savedBatch.Split(','))
            {
                if (int.TryParse(token, out int id) && ContainsQuestionId(id) &&
                    !currentBatchQuestionIds.Contains(id))
                    currentBatchQuestionIds.Add(id);
            }
            if (currentBatchQuestionIds.Count == QuestionsPerDay) return;
        }

        var order = LoadQuestionOrder();
        int cursor = Mathf.Clamp(PlayerPrefs.GetInt(QuestionCursorKey, 0), 0, order.Count);
        currentBatchQuestionIds.Clear();
        while (currentBatchQuestionIds.Count < QuestionsPerDay)
        {
            if (cursor >= order.Count)
            {
                order = ShuffleQuestionIds();
                cursor = 0;
            }

            currentBatchQuestionIds.Add(order[cursor]);
            cursor++;
        }

        SaveQuestionOrder(order);
        PlayerPrefs.SetInt(QuestionCursorKey, cursor);
        PlayerPrefs.SetString(CurrentBatchKey, string.Join(",", currentBatchQuestionIds));
        PlayerPrefs.SetString(CurrentBatchDateKey, today);
        PlayerPrefs.Save();
    }

    private static List<int> LoadQuestionOrder()
    {
        var order = new List<int>();
        string saved = PlayerPrefs.GetString(QuestionOrderKey, "");
        foreach (string token in saved.Split(','))
        {
            if (int.TryParse(token, out int id) && ContainsQuestionId(id) && !order.Contains(id))
                order.Add(id);
        }

        if (order.Count != BriefCopeData.Questions.Length)
            order = ShuffleQuestionIds();
        return order;
    }

    private static List<int> ShuffleQuestionIds()
    {
        var order = new List<int>();
        foreach (var q in BriefCopeData.Questions) order.Add(q.id);
        for (int i = order.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            int temp = order[i];
            order[i] = order[j];
            order[j] = temp;
        }
        return order;
    }

    private static void SaveQuestionOrder(List<int> order)
    {
        PlayerPrefs.SetString(QuestionOrderKey, string.Join(",", order));
    }

    private static bool ContainsQuestionId(int id)
    {
        foreach (var q in BriefCopeData.Questions)
            if (q.id == id) return true;
        return false;
    }

    private static CopeQuestion QuestionById(int id)
    {
        foreach (var q in BriefCopeData.Questions)
            if (q.id == id) return q;
        throw new InvalidOperationException($"Brief-COPE question id {id} is not registered.");
    }

    private static Dictionary<int, int> LoadAccumulatedAnswers()
    {
        var answers = new Dictionary<int, int>();
        string json = PlayerPrefs.GetString(AccumulatedAnswersKey, "");
        if (string.IsNullOrEmpty(json)) return answers;

        try
        {
            var store = JsonUtility.FromJson<AnswerStore>(json);
            if (store?.entries == null) return answers;
            foreach (var entry in store.entries)
                if (entry != null && ContainsQuestionId(entry.id) && entry.value >= 1 && entry.value <= 4)
                    answers[entry.id] = entry.value;
        }
        catch { /* Corrupt optional state should never block the survey. */ }
        return answers;
    }

    private static void SaveAccumulatedAnswers(Dictionary<int, int> answers)
    {
        var store = new AnswerStore();
        foreach (var pair in answers)
            store.entries.Add(new AnswerEntry { id = pair.Key, value = pair.Value });
        PlayerPrefs.SetString(AccumulatedAnswersKey, JsonUtility.ToJson(store));
    }

    private static bool HasAnsweredEverySubscale(Dictionary<int, int> answers)
    {
        var covered = new HashSet<CopeSubscale>();
        foreach (var q in BriefCopeData.Questions)
            if (answers.ContainsKey(q.id)) covered.Add(q.subscale);
        return covered.Count == 14;
    }

    private void SaveDailyResult(ModeRecommendation? recommendation, bool skipped = false)
    {
        var previous = LoadPreviousResult();
        if (!recommendation.HasValue && previous != null && !previous.skipped && !string.IsNullOrEmpty(previous.mode))
        {
            // Preserve the last complete recommendation while the 14-subscale
            // profile is still being accumulated across daily five-question batches.
            PlayerPrefs.SetString(LastCompletedDateKey, LocalDateString());
            PlayerPrefs.Save();
            return;
        }

        var result = new BriefCopeResult
        {
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            mode = recommendation?.mode.ToString() ?? "",
            skipped = skipped,
            dominantCopingStyle = recommendation?.topBucket.ToString() ?? "",
        };
        PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(result));
        PlayerPrefs.SetString(LastCompletedDateKey, LocalDateString());
        PlayerPrefs.Save();
    }

    private static string LocalDateString()
    {
        return DateTimeOffset.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static string LocalDateString(long unixTimestamp)
    {
        return DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).ToLocalTime()
            .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
