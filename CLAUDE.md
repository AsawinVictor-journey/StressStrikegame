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
