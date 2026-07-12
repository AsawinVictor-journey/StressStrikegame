# StressStrikegame

## Render Pipeline
- This project uses **URP (Universal Render Pipeline)**, not Built-in RP. Confirmed via `ProjectSettings/GraphicsSettings.asset` (`m_RenderPipelineGlobalSettingsMap` → `UniversalRenderPipeline`).
- Any imported asset material should target the URP/Lit shader family, not the legacy Standard shader.

## Assets/MarpaStudio (Boxing Arena pack)
- Third-party environment pack by Mario Paradiso (boxing arena: ring, stands, seating, screens, lamps, etc.).
- Despite the folder being named `Built-In/`, its materials already use the **URP/Lit shader** (verified in `Floor.mat` — has `_BaseMap`/`_Surface`/`_WorkflowMode`, which are URP-only properties). It is already correctly shaded for this project's pipeline.
- Do **not** import the bundled `URP/BoxingArenaURP.unitypackage` or `HDRP/BoxingArenaHDRP.unitypackage` — both are redundant/alternate-pipeline versions and would just duplicate what's already in `Built-In/`.
- `Assets/MarpaStudio/Scene/DemoScene.unity` has the asset creator's baked lighting + 2 reflection probes — use as a lighting reference before rebaking your own.
- `Assets/MarpaStudio/New Lighting Settings.lighting` is a reusable Lighting Settings asset matching that demo setup.

## MCP tooling in this project
- **AI Game Developer** (`unity-mcp-cli` / IvanMurzak/Unity-MCP) is connected for direct Unity Editor control (scenes, GameObjects, assets, materials, animations, etc.).
- **blender-mcp** (ahujasid/blender-mcp) setup is in progress, for AI-assisted custom asset creation directly in Blender (v5.1, installed locally). Status as of last session: `uv`/`uvx` installed and verified; still need to install/enable the Blender addon, start its MCP server from Blender's N-panel sidebar, and register it with `claude mcp add blender -- uvx blender-mcp`.

## Brief-COPE pre-game survey (not yet ported)
- `docs/brief-cope-prototype/` holds a working TypeScript + HTML prototype (from the separate `zBiblion/Brief-COPE` repo) of a pre-fight questionnaire that recommends a game mode based on how the player says they've been coping with stress. See `docs/brief-cope-prototype/BRIEF_COPE_CONTEXT.md` for the full design, the survey→mode routing logic, and a porting checklist for `BriefCopeData.cs` / `GameModeRecommendation.cs`.
- The game only has **3 modes**: Boxing (`Assets/b-o-o-k/BoxingMenu.unity`), Rage Room (`Assets/Scenes/Rage Room/`), Meditate (`Assets/evococo/meditation.unity`). Scene routing goes through `Assets/Scripts/SceneTransitionManager.cs` (`LoadScene(string sceneName)`) — there is no `LevelSelector.cs`.
- Not integrated into this Unity project yet — the prototype is reference material only.
