# CLAUDE.md Audit & Update Plan

> **For Hermes:** This is a documentation-maintenance task. Execution modifies ONLY `CLAUDE.md`. No C#, no scenes, no gameplay, no packages.

**Goal:** Replace the stale `CLAUDE.md` with an accurate, concise project-facts document verified against the actual Unity project, so a fresh Claude Code session knows what the project is, what systems exist, what not to break, and the project-specific gotchas.

**Scope guard:** Only `/mnt/c/Users/User/Documents/StressStrikegame/CLAUDE.md` is written. Everything else is read-only inspection.

---

## Step 1 — Audit of existing CLAUDE.md (classification)

Existing file is 21 lines, 4 sections. Classification per line-block:

| Section | Content | Class | Action |
|---|---|---|---|
| Render Pipeline | URP confirmed via GraphicsSettings; use URP/Lit not Standard | A/B STABLE FACT + RULE | **KEEP** (verified true) |
| Assets/MarpaStudio | Boxing Arena pack, Built-In/ already URP-shaded, don't import URP/HDRP unitypackages, DemoScene lighting ref, Lighting Settings asset | A/B/C STABLE FACT + RULE | **KEEP** (accurate, useful, hard to re-derive) |
| MCP tooling | Unity-MCP connected; blender-mcp "setup in progress" with install/enable/start/register steps | D TEMPORARY STATE (blender half) + A partial fact (Unity-MCP) | **REWRITE**: drop the mid-flight setup steps; keep only the stable fact that Unity-MCP is used |
| Brief-COPE "(not yet ported)" | Described as prototype/reference-only, "not integrated" | **F STALE / INCORRECT** | **REWRITE**: it is now fully implemented in C# |

---

## Step 2 — Verification against the actual project (source of truth)

Verified by reading real files this session:

1. **URP** — CLAUDE.md said URP. Not re-diffed GraphicsSettings this pass, but URP is corroborated everywhere (URP/Lit materials, RealToon URP GUI, CTI URP components, URP-only scene variants). **Claim stands — KEEP.**
2. **MarpaStudio** — `Assets/MarpaStudio/Scene/DemoScene.unity` exists; pack present. Prior `Floor.mat` URP-property finding was documented and is consistent. **KEEP.**
3. **Brief-COPE — NOW IMPLEMENTED.** Real C# exists:
   - `Assets/Scripts/BriefCope/BriefCopeData.cs` — 14-item Brief-COPE (Carver 1997), subscale + bucket scoring.
   - `Assets/Scripts/BriefCope/GameModeRecommendation.cs` — `GameMode {Boxing, RageRoom, Meditate}`, bucket→mode logic, `SceneNames` map, disclaimer guardrail.
   - `Assets/Scripts/BriefCope/BriefCopeSurveyController.cs` — "Coach Byte" survey UI flow (intro→question→halfway→result), PlayerPrefs `BriefCope_LastResult`, optional local Ollama coach text, optional cloud mirror.
   - `Assets/Scripts/BriefCope/BriefCopeResult.cs`, `RecommendedModeHighlighter.cs`, `Assets/Editor/BriefCopeEditorUtils.cs`.
   - Scene `Assets/b-o-o-k/BriefCopeSurvey.unity` exists.
   - The old CLAUDE.md's "not yet ported / reference material only" is **false now.** Prototype still lives at `docs/brief-cope-prototype/` (BRIEF_COPE_CONTEXT.md etc.) as the design source.
4. **Scenes / game modes** — 3 gameplay modes confirmed, but the OLD scene paths were partly WRONG:
   - Boxing: `Assets/b-o-o-k/BoxingMenu.unity` ✔ (build-enabled).
   - Rage Room: `Assets/Scenes/Rage Room/Rage Room.unity` (+ `Rage Room Menu.unity`) ✔ — NOT the old `Assets/Scenes/Rage Room/` folder-only reference.
   - Meditate/"Yoga": old CLAUDE.md said `Assets/evococo/meditation.unity` — **that file does NOT exist** (search returned 0). Actual scenes are `Assets/Scenes/Yoga/Yoga.unity` + `Yoga/Yoga Menu.unity` (both build-enabled). NOTE: `GameModeRecommendation.SceneNames` maps `Meditate → "meditation"`, a scene name that is not in build settings and has no matching `.unity` — a likely latent routing bug. **Document as a caveat/open question; DO NOT fix code.**
5. **SceneTransitionManager** — `Assets/Scripts/SceneTransitionManager.cs`: singleton (`Instance`, `DontDestroyOnLoad`), `LoadScene(string)` with async fade, resets `Time.timeScale`/`fixedDeltaTime`, guards against scenes missing from build list. `SceneButton.cs` and others call it. **Accurate — KEEP, expand slightly.**
6. **Combat/punch — TWO DISTINCT PIPELINES (must be documented as different):**
   - **Boxing:** `HandInputManager.cs` (mouse-debug now, ESP32 Bluetooth path is a TODO stub) drives glove targets → `PhysicsHandController` → `PunchCollider.cs` uses physics `OnCollisionEnter`, velocity→damage mapping, hits `DummyRockingDoll`/`BotAnimationControll`, reports to `CombatHudController` + `StressStrike.ScoreManager`.
   - **Rage Room:** abstract `HandInputProvider` (IMU accel, `KeyboardHandInput` impl) → `PunchDetector` (spike detection) → `PunchController` (orchestration, hitbox window) → `HandTarget` (bounded damped position sim) + `PunchHitbox` → `ImpactReaction`/`DeformableMesh`/`ObjectDestruction`. Explicitly NOT physics-collision-velocity based; explicitly avoids IMU double-integration/dead-reckoning.
7. **HID / hardware** — `Assets/Scripts/Hardware/`: `ESP32Glove.cs` (`: InputDevice`), `VRGloveProcessor (1).cs`, `BiometricEngine.cs`. Confirmed present. (Note two files have `(1)` duplicate-suffix names.)
8. **Main menu ambiguity** — build index 0 is `Assets/Scenes/UI Mainmenu.unity` (enabled); `Assets/Scenes/MainMenuScene.unity` is also enabled. `BriefCopeSurveyController` hardcodes `MenuSceneName = "MainMenuScene"`. So Brief-COPE's canonical menu is `MainMenuScene`. Two menu scenes coexist — **document as fact + open question, do not resolve.**
9. **MCP** — Cannot verify live server status from here. Keep only the stable fact (Unity-MCP is the AI-editor-control tool in use); drop blender-mcp temporary setup checklist.

---

## Step 3 — What the rewrite REMOVES

- blender-mcp mid-flight setup steps ("install addon", "start server from N-panel", "run claude mcp add ...", "setup in progress"). Temporary status, not a durable fact.
- "Brief-COPE not yet ported / reference material only" — now false.
- Stale scene path `Assets/evococo/meditation.unity`.
- "no `LevelSelector.cs`" trivia (absence-of-file note, low value).

## Step 4 — What the rewrite CORRECTS

- Brief-COPE section → describes the CURRENT C# implementation and files.
- Meditate scene path → Yoga scenes; add the `"meditation"` SceneNames-map mismatch caveat.
- Rage Room scene path → the actual `Rage Room.unity`.

## Step 5 — What the rewrite ADDS

- Architecture Overview with the TWO separate punch pipelines documented as different (Boxing physics-collision vs Rage Room IMU/detector/bounded-sim).
- Game Modes / Scene Flow table (mode → menu scene → gameplay scene → build-enabled?).
- SceneTransitionManager singleton/timeScale-reset gotchas.
- Unity gotchas: singleton `DontDestroyOnLoad`, scene-must-be-in-build-list, `Time.timeScale` reset on transition, event subscribe/unsubscribe in OnEnable/OnDisable (PunchController), FixedUpdate-vs-Update timing note.
- Pointer to `docs/brief-cope-prototype/BRIEF_COPE_CONTEXT.md` as design source, and suggestion to put deep architecture in a future `Learning/ARCHITECTURE.md` (do NOT create it now).

## Step 6 — What is intentionally NOT included

- Per-file walkthroughs, C# tutorial content, session history/diary, TODO lists.
- The `"meditation"` scene-name bug FIX (only noted as a caveat).
- Main-menu-scene consolidation decision (noted as open question).
- Third-party asset packs unrelated to core (Synty, Ithappy, TextMesh Pro, SkySeries, RealToon, ProceduralTerrainPainter) beyond a one-line "vendor assets live under Assets/<Vendor>/" note.

## Files likely to change
- Modify: `/mnt/c/Users/User/Documents/StressStrikegame/CLAUDE.md` (full rewrite). **Nothing else.**

## Validation
- Re-read new CLAUDE.md; confirm every path named exists (already verified above); confirm no temporary status remains; confirm no contradictions/duplication; confirm length stays ~1 screen of sections.

---

## Proposed replacement CLAUDE.md (full content to write on execution)

```markdown
# StressStrikegame

Unity boxing / stress-relief game (URP). Much of the C# was AI-assisted ("vibe
coded") and is being progressively understood and refactored. This file holds
stable project facts and gotchas — not history or TODOs.

## Project Rules
- Modify only what a task needs. Do not redesign game modes or refactor across
  the two punch pipelines (they are intentionally different — see Architecture).
- Any scene you route to via `SceneTransitionManager.LoadScene(string)` MUST be
  in `ProjectSettings/EditorBuildSettings.asset`, or the load throws and the
  transition self-recovers by fading back in (no scene change).
- New imported-asset materials must target URP/Lit, never the legacy Standard shader.

## Render Pipeline
- Project uses **URP (Universal Render Pipeline)**, not Built-in RP (confirmed via
  `ProjectSettings/GraphicsSettings.asset` → `UniversalRenderPipeline`).

## Important Assets
- `Assets/MarpaStudio/` — third-party Boxing Arena pack (ring, stands, seating,
  screens, lamps). Despite the `Built-In/` subfolder name, its materials already
  use **URP/Lit** (verified in `Floor.mat`: `_BaseMap`/`_Surface`/`_WorkflowMode`).
  It is already correctly shaded for this pipeline.
  - Do NOT import the bundled `URP/BoxingArenaURP.unitypackage` or
    `HDRP/BoxingArenaHDRP.unitypackage` — redundant alternate-pipeline duplicates.
  - `Assets/MarpaStudio/Scene/DemoScene.unity` has baked lighting + 2 reflection
    probes — use as a lighting reference before rebaking.
  - `Assets/MarpaStudio/New Lighting Settings.lighting` matches that demo setup.
- Other vendor packs (Synty, Ithappy, TextMesh Pro, SkySeries, RealToon,
  ProceduralTerrainPainter, etc.) live under `Assets/<Vendor>/` and are largely
  demo/support content, not core gameplay.

## Game Modes / Scene Flow
Three gameplay modes. Scene routing goes through
`Assets/Scripts/SceneTransitionManager.cs` (`Instance.LoadScene(string sceneName)`).

| Mode | Gameplay scene | Menu scene |
|---|---|---|
| Boxing | `Assets/b-o-o-k/BoxingMenu.unity` (`"BoxingMenu"`) | via BoxingMenu |
| Rage Room | `Assets/Scenes/Rage Room/Rage Room.unity` (`"Rage Room"`) | `Rage Room Menu.unity` |
| Meditate / "Yoga" | `Assets/Scenes/Yoga/Yoga.unity` (`"Yoga"`) | `Yoga/Yoga Menu.unity` |

- Display name for the Meditate mode is **"Yoga"** (team decision) even though the
  enum is `GameMode.Meditate`.
- CAVEAT: `GameModeRecommendation.SceneNames` maps `Meditate → "meditation"`, but
  no `meditation.unity` exists and it is not in build settings. The live Meditate
  scene is `Yoga`. Treat this map entry as a known discrepancy — verify before
  relying on it for routing.
- Two main-menu scenes coexist: `Assets/Scenes/UI Mainmenu.unity` (build index 0)
  and `Assets/Scenes/MainMenuScene.unity`. Brief-COPE's flow targets
  `"MainMenuScene"` specifically. Which is canonical is unresolved — check before
  wiring new menu logic.

## Brief-COPE Pre-Game Survey (implemented)
"Coach Byte" pre-game questionnaire that recommends a mode from how the player
says they've been coping. Implemented in C# (no longer a prototype):
- `Assets/Scripts/BriefCope/BriefCopeData.cs` — 14-item Brief-COPE (Carver, 1997),
  subscale + Approach/Avoidant/Context bucket scoring.
- `Assets/Scripts/BriefCope/GameModeRecommendation.cs` — bucket→mode logic,
  `GameMode {Boxing, RageRoom, Meditate}`, per-subscale reason text, and the
  not-a-diagnosis `Disclaimer` guardrail (keep it attached to every result).
- `Assets/Scripts/BriefCope/BriefCopeSurveyController.cs` — survey UI flow
  (intro → question → halfway beat → result). Saves last result to PlayerPrefs
  key `BriefCope_LastResult`. Optionally personalises copy via a LOCAL Ollama
  server (best-effort, never blocks the flow) and optionally mirrors a minimal
  record to cloud if the player opted in (never sends individual answers).
- Also: `BriefCopeResult.cs`, `RecommendedModeHighlighter.cs` (highlights the
  recommended mode on the menu), `Assets/Editor/BriefCopeEditorUtils.cs`, and
  scene `Assets/b-o-o-k/BriefCopeSurvey.unity`.
- Design source of truth: `docs/brief-cope-prototype/BRIEF_COPE_CONTEXT.md`
  (survey → mode routing, wording, guardrails).

## Input / Combat — two separate punch pipelines (do not merge)
- **Boxing** — physics/collision based. `HandInputManager.cs` drives glove targets
  (mouse-debug now; ESP32 Bluetooth reader is a TODO stub) →
  `PhysicsHandController` → `PunchCollider.cs` reads `OnCollisionEnter` relative
  velocity, maps velocity→damage, and reports to `CombatHudController` +
  `StressStrike.ScoreManager`.
- **Rage Room** — IMU/detector based, NOT collision-velocity. `HandInputProvider`
  (abstract; `KeyboardHandInput` impl) reports acceleration/orientation →
  `PunchDetector` (spike detection) → `PunchController` (orchestrates the hitbox
  window) → `HandTarget` (a bounded, damped position simulation) + `PunchHitbox`
  → `ImpactReaction` / `DeformableMesh` / `ObjectDestruction`. Deliberately never
  double-integrates IMU data into position (dead-reckoning drift trap).
- **HID / hardware** lives in `Assets/Scripts/Hardware/`: `ESP32Glove` (extends
  Unity `InputDevice`), `VRGloveProcessor (1)`, `BiometricEngine`. (Two files carry
  a `(1)` duplicate-name suffix.)

## Important Unity Gotchas
- `SceneTransitionManager` is a `DontDestroyOnLoad` singleton (`Instance`). It
  resets `Time.timeScale = 1` / `Time.fixedDeltaTime = 0.02` on every transition
  so slow-mo/KO-freeze effects can't leak a paused state into the next scene.
- Loading a scene not in the build list would otherwise hard-stop the transition
  coroutine (stuck black screen, `isTransitioning` stuck true); the manager guards
  and recovers — keep that guard if you touch it.
- `PunchController` subscribes/unsubscribes its punch + hit events in
  `OnEnable`/`OnDisable`. Its hitbox timing runs in `FixedUpdate` on purpose
  (HandTarget's state machine is on the fixed clock); moving it to `Update`
  reintroduces late-retract-on-lag bugs.
- Many scripts use `FindObjectOfType` / `Find("left glove")` at `Start`/`Awake` —
  renaming those GameObjects or changing scene wiring can silently break combat.

## Tooling
- **Unity-MCP** (IvanMurzak/Unity-MCP, "AI Game Developer") is used for direct
  Unity Editor control (scenes, GameObjects, materials, animations) during
  AI-assisted development.

## Note on deeper docs
Keep this file to stable facts. If detailed architecture write-ups are needed,
put them in a separate `Learning/ARCHITECTURE.md` rather than growing this file.
```

---

## Execution handoff
On approval, execute by writing exactly the fenced content above to
`/mnt/c/Users/User/Documents/StressStrikegame/CLAUDE.md` (single `write_file`),
then re-read it to confirm, and report removed / corrected / added / omitted /
unverified as required by the task's Final Verification.

### Unverified / flagged for the user
- Live MCP server connection status (can't be checked from this environment).
- Whether `UI Mainmenu` or `MainMenuScene` is the intended canonical menu.
- Whether the `Meditate → "meditation"` scene-name map is a real bug or dead code.
```
