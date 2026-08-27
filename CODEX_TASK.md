# Codex Task — StressStrike (Unity, URP)

## STEP 0 — DO THIS FIRST, DO NOT SKIP

Run `/review` on this repo, then read these files **in full** before writing a single line:

```
Assets/Scripts/BriefCope/BriefCopeSurveyController.cs
Assets/Scripts/BriefCope/BriefCopeData.cs
Assets/Scripts/BriefCope/BriefCopeResult.cs
Assets/Scripts/BriefCope/GameModeRecommendation.cs
Assets/Scripts/BriefCope/RecommendedModeHighlighter.cs
Assets/Scripts/CheckIn/CheckInManager.cs
Assets/Scripts/CheckIn/CheckInResultPanel.cs
Assets/Scripts/CheckIn/CheckInSceneRouter.cs
Assets/Scripts/CheckIn/CheckInModeMapping.cs
Assets/Scripts/CoachByte/CoachByteMenuGreeting.cs
Assets/Scripts/CoachByte/CoachByteHistory.cs
Assets/Scripts/MenuRigSwitcher.cs
CLAUDE.md
```

**Report back what you found before proposing fixes.** Some of these tasks are blocked by
existing design decisions (documented below) — I need you to confirm you understand the blocker
before you touch anything.

---

## Project constraints (non-negotiable)

- Unity project, **URP** (not Built-in RP). Any material/shader work targets URP/Lit family.
- 3 game modes only: **Boxing**, **RageRoom**, **Meditate** (a.k.a. Yoga). Scene routing goes
  through `Assets/Scripts/SceneTransitionManager.cs` → `LoadScene(string sceneName)`.
- The canonical menu scene is `Assets/Scenes/MainMenuScene.unity`. There are stale duplicate
  "menu-shaped" scenes (`idlee.unity`, `UI Mainmenu.unity`, `copy of current.unity`) — **never
  target those.**
- Survey persistence is `PlayerPrefs` key `"BriefCope_LastResult"`, serialized `BriefCopeResult`.
- Coach Byte text comes from Gemini via a local backend proxy (`GeminiClient.Generate`), model
  `gemini-3.5-flash-lite`. Every call must degrade silently if the backend is unreachable.

---

# TASK GROUP A — Brief-COPE (MainMenuScene)

## A1. Daily question batching (5 per day, rotating)

**Wanted:** Show 5 questions per day. Next day, show the next 5. Keep cycling through the
14-question bank; once exhausted, reshuffle and repeat so questions reappear in a new random
order rather than the same fixed sequence.

**Blockers you must resolve first — read these carefully:**

1. `BriefCopeSurveyController.Start()` (~lines 88–98) currently gates the survey to **once per
   lifetime**: if a previous non-skipped result exists, it calls `HideSurveyPopup()` and routes
   straight to check-in. Daily batching directly contradicts this. You need a
   *last-completed-date* check instead of a *has-ever-completed* check.

2. The halfway beat at `ConfirmAnswer()` fires on `nextIndex == BriefCopeData.Questions.Length / 2`
   — that's 7, hardcoded off the full 14-item bank. With a 5-question batch this either never
   fires or fires at the wrong place. Make it relative to the *current batch length*, or drop the
   halfway beat for short batches.

3. `BriefCopeData.Questions` is a `static readonly` array of 14 items, one per subscale. **Scoring
   depends on subscale coverage** — `ScoreSubscales()` / `ScoreBuckets()` in `BriefCopeData.cs`
   sum across all 14 subscales, and `GameModeRecommendation.Recommend(answers)` reads those totals.
   With only 5 answers you get a partial, biased score. Decide and tell me which you're doing:
   - **(a)** accumulate answers across days in PlayerPrefs, only recompute the recommendation once
     all 14 subscales have been answered at least once; or
   - **(b)** score each 5-question batch on its own and accept a noisier recommendation.

   I lean toward **(a)**. Argue for the other only if you have a strong reason.

**Implementation notes:**
- Add persistent state (new PlayerPrefs keys) for: last-completed date (use
  `DateTimeOffset.UtcNow` / local date string), the current shuffled question order, and the
  cursor into that order.
- "New day" = local calendar date differs from the stored one. Do not use a 24-hour timer.
- Keep `BriefCopeResult` backward-compatible — old saved values must still deserialize
  (`JsonUtility` leaves missing string fields as `""`, not null).

## A2. Check-in emotion panel breaks in fullscreen

**Symptom:** the mood-chip / free-text check-in panel glitches (misaligned, stretched, or
clipped) when the game runs fullscreen. It looks fine in the smaller Game view.

**Where to look:** the check-in canvas is assigned to `BriefCopeSurveyController.checkInCanvas`
and `CheckInManager.checkInCanvas`; the input area is `CheckInManager.checkInBody`.

This is almost certainly a layout bug, not a script bug. Check, in this order:
- `CanvasScaler` UI Scale Mode — should be **Scale With Screen Size** with a fixed reference
  resolution and a sensible Match value, not **Constant Pixel Size**.
- Anchors on the panel `RectTransform` — hardcoded pixel offsets on a stretched anchor will break
  at other aspect ratios.
- Whether the check-in canvas and the main menu canvas disagree on render mode / sort order.

Report the actual cause before changing values.

## A3. Add an "I don't want that mode" escape on the result screen

**Wanted:** after the survey recommends a mode, the player needs a way to decline it — not just
the existing Continue button.

**Current state:** `CheckInResultPanel` has exactly one button, `continueButton`, which fires the
`onContinue` event. `CheckInManager.Skip()` exists but only applies to the *input* stage, and it
hides the whole canvas without picking a mode.

Add a second serialized `Button` (e.g. `pickAnotherButton`) plus a matching event. Wire it so the
player lands back on the main menu free to choose any mode, with the
`RecommendedModeHighlighter` glow cleared (currently `TriggerHighlight()` always re-reads
PlayerPrefs and re-applies the glow — make sure declining doesn't leave a stale highlight).

Note `CheckInResultPanel.Awake()` has a deliberate comment about *not* calling `Hide()` — read it
and don't break that invariant.

---

# TASK GROUP B — AI Coach Byte

## B1. The mascot overlay was never actually added

**Verified fact:** these two sprites exist on disk but are referenced **zero times** in
`MainMenuScene.unity`:

```
Assets/UI/MainMenu 1/Group 113.png       guid a375f9b01695ac84dbbef2d105cfb1f6
Assets/UI/MainMenu 1/Group 137 (1).png   guid c04f12b8732a8f049a43566cc6d64e54
```

The `AICoachByte` canvas in `MainMenuScene.unity` currently has only a `GreetingText`
(TextMeshProUGUI) and a `Triangle` (speech-bubble tail). There is no mascot `Image` at all — this
was never built, so there is nothing to "fix", only something to add.

**Wanted:** the mascot art rendered as an overlay next to the speech bubble, so the greeting text
from `CoachByteMenuGreeting` reads as coming *from* the character.

- First inspect both PNGs and tell me which is the mascot body and which is the
  bubble/frame/wordmark — I'm not certain myself.
- Add the `Image` object(s) under the existing `AICoachByte` canvas, anchored so the bubble tail
  points at the mascot.
- Must not intercept clicks on the menu behind it — set `raycastTarget = false` on any purely
  decorative Image.
- Must survive resolution changes (same CanvasScaler concern as A2).

## B2. Boxing / Yoga mode overlays glitch in MainMenuScene

**Symptom:** the Boxing and Yoga mode overlays in `MainMenuScene` glitch (z-fighting, wrong
visibility, or flicker on camera pan). RageRoom reportedly does not.

**Relevant context you'll need:**
- The menu is a 3-station 3D carousel — `MenuController` pans the camera between Boxing /
  RageRoom / Yoga stations. All three station GameObjects must stay active; a previous bug had
  Boxing's `m_IsActive` set to `0`, which made the camera pan onto invisible geometry.
- `MenuRigSwitcher.ShowOnly()` toggles both `root` and `UI` per rig — if two rigs share a UI
  object, or a rig's `UI` field is null, you get exactly this class of glitch. Check the
  serialized `rigs[]` array in the scene for duplicate or missing references.
- `RecommendedModeHighlighter` spawns `RecommendedGlow` / `RecommendedStar*` children **at
  runtime** into `boxingTarget` / `rageRoomTarget` / `yogaTarget`, calls `SetAsFirstSibling()`,
  and expands the rect by `glowPadding` (default 40) on all sides. `TriggerHighlight()` is called
  from both `Start()` **and** `BriefCopeSurveyController.CloseSurveyPopup()`, and it
  `DestroyImmediate`s prior highlights each time. Overlapping/duplicated glows here are a strong
  suspect — verify before blaming the carousel.

Diagnose which of these it actually is. Do not guess.

---

## Output format I want

For each task: **(1)** what you found in the code, **(2)** the root cause, **(3)** the fix as a
complete file or a precise diff, **(4)** anything you couldn't verify without opening the Unity
Editor.

Do not rewrite files wholesale when a targeted edit will do. Match the existing comment style —
this codebase explains *why*, not *what*.
