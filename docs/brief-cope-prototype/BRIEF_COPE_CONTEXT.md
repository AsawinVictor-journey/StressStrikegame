# Brief-COPE Game Mode Selector — Context for StressStrikegame

## What this is

Before a player starts fighting, they can answer a short, validated
psychology-based survey (**Brief-COPE**, Carver 1997) about how they've been
coping with stress lately. Based on their answers, the game suggests which
mode fits them, with a coach/bot character explaining why. Always optional,
always skippable, explicitly framed as "just a suggestion" — never a
diagnosis.

This is currently a **TypeScript + HTML prototype**, developed and tested in
a separate repo (`zBiblion/Brief-COPE` on GitHub). It has **not** been
ported into this Unity project yet. This folder is a copy of that prototype
for reference while porting.

## Files in this folder

| File | What it is |
|---|---|
| `briefCopeData.ts` | The 28 canonical Brief-COPE questions, 14 subscales, scoring functions |
| `gameModeRecommendation.ts` | Bucket logic, the 3 game modes, coach message copy |
| `demo.ts` | A runnable script proving the scoring logic works end-to-end |
| `index.html` | A full interactive prototype ("Coach Byte") — open directly in a browser to play through it |

## ⚠️ The game only has 3 modes — Boxing, Rage Room, Meditate

An earlier draft of this design assumed 5 modes (Release Mode, Career Mode,
Duo Gym Mode, Recovery Corner, Trash Talk Arcade). That was wrong. Verified
against this Unity project directly:

- `Assets/b-o-o-k/BoxingMenu.unity` + boxing animations/controllers → **Boxing**
- `Assets/Scenes/Rage Room/Rage Room.unity`, `Assets/Scripts/RageRoom/PunchController.cs`,
  `Assets/Scripts/RageRoom/GameManger.cs`, `Assets/Scripts/RageRoom/RageRoomCameraRotation.cs` → **Rage Room**
- `Assets/evococo/meditation.unity`, `Assets/Scripts/MeditationSessionManager.cs`,
  `Assets/Scripts/MeditationHUD.cs` → **Meditate**

Scene loading is handled by `Assets/Scripts/SceneTransitionManager.cs`
(generic `LoadScene(string sceneName)` + coroutine transition) — **not** a
`LevelSelector.cs`, which does not exist in this project.

## The survey → mode correlation (current, 3-mode version)

Brief-COPE is 28 items (rated 1–4 each) that collapse into **14 coping
subscales** (2 items each), which collapse further into **3 buckets**:

| Bucket | Subscales inside it |
|---|---|
| **Approach** | Active Coping, Planning, Positive Reframing, Acceptance, Emotional Support, Instrumental Support |
| **Avoidant** | Denial, Substance Use, Behavioral Disengagement, Self-Distraction, Self-Blame |
| **Context** | Humor, Religion, Venting (valence depends on degree, not fixed good/bad) |

Routing logic (implemented in `recommendGameMode()` in
`gameModeRecommendation.ts`):

1. Score all 28 answers into the 14 subscales, then sum into the 3 buckets.
2. Whichever bucket scores highest decides the mode:
   - **Approach highest** → **Boxing** (active/strategic copers get
     structured, strategy-rewarded fights)
   - **Avoidant highest** → **Meditate** (calm, no-pressure onboarding —
     never framed as punishment for "avoiding")
   - **Context highest** → look at which subscale inside Context scored
     highest: Religion → **Meditate** (reflective/calm fits meditation);
     Humor or Venting → **Rage Room** (catharsis / expressive release)
3. Every recommendation carries a fixed disclaimer: *"This is just a
   suggestion based on how you said you've been coping lately — not a
   diagnosis. Pick whatever mode you're actually in the mood for."*

## The 3 modes and their Unity hooks

| Mode | Trigger | Feel | Relevant existing scripts/scenes |
|---|---|---|---|
| **Boxing** | High Approach coping (Active Coping / Planning / etc.) | Structured fights, strategy rewarded | `Assets/b-o-o-k/BoxingMenu.unity`, `BotAnimationControll.cs`, `StadiumArenaGenerator.cs`, `CombatHudController.cs` |
| **Rage Room** | High Venting / Humor, or high Context overall | Pure catharsis, hit stuff, no fail state | `Assets/Scenes/Rage Room/Rage Room.unity`, `RageRoom/PunchController.cs`, `RageRoom/PhysicsHandController.cs`, `RageRoom/GameManger.cs`, `RageRoom/RageRoomCameraRotation.cs`, `DummyRockingDoll.cs` |
| **Meditate** | High Avoidant coping (any subscale), or high Religion | Guided breathing, zero pressure | `Assets/evococo/meditation.unity`, `MeditationSessionManager.cs`, `MeditationHUD.cs`, `Background3DController.cs` |

## UI flow (as prototyped in `index.html`)

1. **Intro screen** — "Coach Byte" (a bot mascot) explains what's about to
   happen: 28 quick questions, ~3 minutes, stays on-device, not a diagnosis,
   skippable. This is the only place this explanation is given — not
   repeated mid-quiz, to avoid killing pacing.
2. **One question at a time**, tap-to-answer, auto-advances — reads as a
   dialogue/text-game rather than a long form.
3. **One short "halfway there" encouragement beat** after question 14 —
   no re-explanation, just pacing.
4. **Result screen**:
   - Opens with a plain-language reason drawn from the player's actual
     top-scoring subscale (e.g. "You scored highest on planning — you like
     to map out a strategy before you make your move.")
   - Then the recommended mode name + Coach Byte's flavor line + the
     disclaimer.
   - **All 3 modes are shown as selectable cards below**, with the
     recommended one marked "★ suggested" and pre-selected — the player can
     tap either other mode instead. Nothing is ever locked in.
5. Skipping the survey (available on the intro screen and mid-quiz) drops
   the player straight into the same 3-mode picker with nothing
   pre-selected.

## Guardrails to preserve when porting to C#

- Survey is always skippable, at any point.
- Results stored locally by default — no forced cloud sync.
- Never present this as a clinical/diagnostic tool. Keep the disclaimer
  text attached to every recommendation.
- Meditate copy must never read as punishment for "avoiding" —
  frame it as a warm-up, not a consolation prize.
- Brief-COPE item wording is Carver's, freely usable for research/applied
  use with attribution (not for resale as a standalone product) — cite the
  source (Carver, C.S., 1997, *International Journal of Behavioral
  Medicine*, 4(1), 92–100) in an in-game "about" screen if one exists.

## Porting checklist

- [x] `BriefCopeData.cs` — ported to `Assets/Scripts/BriefCope/BriefCopeData.cs`
      (`CopeSubscale`/`CopeBucket` enums, `CopeQuestion` struct, `Questions`,
      `ScoreSubscales`, `ScoreBuckets`, `TopSubscaleInBucket`).
- [x] `GameModeRecommendation.cs` — ported to
      `Assets/Scripts/BriefCope/GameModeRecommendation.cs` (`GameMode` enum,
      `SceneNames` map, `Recommend(answers)`). Confirmed exact scene names:
      `"BoxingMenu"`, `"Rage Room"`, `"meditation"`.
- [x] `BriefCopeResult.cs` — local persistence record
      (`Assets/Scripts/BriefCope/BriefCopeResult.cs`), saved via
      `PlayerPrefs` as JSON, no cloud sync.
- [x] `BriefCopeSurveyController.cs` — UI controller
      (`Assets/Scripts/BriefCope/BriefCopeSurveyController.cs`) reproducing
      the `index.html` flow: intro → one-question-at-a-time (with Back) →
      halfway beat after Q14 → result screen with all 3 modes selectable
      (recommended one badged, icon/blurb per card), skip button on intro
      and mid-quiz with the prototype's exact skip copy. Wires into
      `SceneTransitionManager.LoadScene(string)` with fallback to
      `SceneManager.LoadScene`. Reason text is drawn from the *winning
      bucket's* top subscale (matches `index.html`'s
      `topSubscaleInBucket(subscaleScores, topBucket)`), not the overall
      top subscale — fixed after an initial port mistake that could show a
      mismatched reason (e.g. a Planning-flavored reason next to a
      Meditate recommendation). Response-scale button labels match the
      shortened prototype wording ("Not at all" / "A little bit" / "A
      medium amount" / "A lot"), and the disclaimer is rendered as its own
      text field, not concatenated into the coach message.
- [x] **Canvas/UI built and wired** at
      `Assets/b-o-o-k/BriefCopeSurvey.unity` (standalone scene, does not
      touch `MainMenuScene`). Built programmatically via one C# builder run
      through the `ai-game-developer` Unity MCP's `script-execute` tool
      (dark "Coach Byte" card theme matching `index.html`): intro/
      question/halfway/result panels, progress `Slider`, 4 answer buttons
      with child `TextMeshProUGUI` labels, Back/Skip/Continue/Play/Restart
      buttons, and 3 mode cards (button + recommended badge + selected
      indicator + title/blurb text) — all assigned to
      `BriefCopeSurveyController`'s serialized fields via `SerializedObject`.
      Mode card icon emoji (🏆🥊🌿) were **left blank** rather than set as
      TMP text, since TextMeshPro won't render them without a configured
      emoji sprite asset — revisit later if icons are wanted.
- [x] **Compiles cleanly** — confirmed via `console-get-logs`, zero errors
      or warnings from any `BriefCope/` file.
- [x] **Functionally verified end-to-end** (Edit Mode, via `script-execute`
      driving real `Button.onClick.Invoke()` calls and reading back the
      wired `TMP_Text` values — not just a compile check):
      - Full 28-question run with Approach-leaning answers → correctly
        recommended **Boxing**, reason text matched the top subscale,
        only the Boxing mode card's recommended badge was active.
      - Halfway panel appeared exactly once, right after question 14.
      - Back button correctly re-shows the previous question and lets you
        re-answer it.
      - Both skip paths (intro skip, mid-quiz skip) show the exact
        prototype copy ("No problem — hop in whenever you're ready...")
        with nothing pre-selected and Play button correctly disabled until
        a mode card is clicked.
      - Clicking a mode card selects it and enables the Play button.
      - **Not yet tested:** actual Play Mode (the MCP's
        `editor-application-set-state` / `screenshot-game-view` tools
        aren't reachable in this project's tool config), and the final
        `OnPlay` → `SceneManager.LoadScene(...)` handoff, which needs
        `BoxingMenu`/`Rage Room`/`meditation` present in Build Settings.
- [ ] Decide where in the flow this survey triggers (e.g. before
      `ModeCarouselController`, or as a one-time first-launch flow) — not
      wired into any existing menu scene yet. Note: the current menu has
      4 carousel pages (Level/Training/Rage Room/Meditate — see
      `menu_flow` memory) which don't map 1:1 onto the survey's 3 modes
      (Boxing/RageRoom/Meditate); this needs a real routing decision, not
      a guess.
- [ ] Old pre-pivot scaffolding (`Assets/Scripts/FeedbackManager.cs`,
      `OpenAIService.cs`, `FeedbackRecord.cs`, `OutlineHighlight.cs`,
      `AIRecommendationHighlighter.cs`) is now dead code — not referenced by
      any scene. Left in place for now; consider deleting once the new
      Brief-COPE flow is confirmed working.
