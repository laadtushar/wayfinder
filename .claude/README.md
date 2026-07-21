# Wayfinder AI-assistance toolkit

This folder configures Claude Code (and documents the wider AI-dev landscape) for building Wayfinder: a solo-dev Unity 6 game for the Samsung Galaxy XR (Android XR). Primary machine: a Windows PC with an RTX 5070 Ti (Unity, Engine Hub, adb, GDAL, splat/reconstruction authoring); the Mac is docs/research only. It has two parts:

1. **A lean, working config** (the files below) — the high-value subset, ready to use.
2. **A full catalog** (further down) — every relevant tool across coding, architecture, docs, UI/UX, graphics/assets, XR, and backend, so you can pull in more on a real need.

The guiding principle, echoed by every research source: **build this folder incrementally; each file should fix a real friction point, not a hypothetical one.** Don't bulk-install community bundles of 150 agents. The pieces here are the ones that pay for themselves; treat the rest of the catalog as a menu.

## What's in this folder

| Path | What | Priority |
|---|---|---|
| `../CLAUDE.md` | Always-loaded project rules: the pinned stack, the hard XR constraints, C# conventions, the terrain pipeline gotchas. **The single most valuable file after the Unity MCP bridge.** | core |
| `../.mcp.json` | Project MCP servers. Context7 active now; Unity + Blender documented below, added after those apps are installed. | core |
| `settings.json` | Permissions: allow common dev commands, deny reading secrets and destructive shell. | core |
| `agents/unity-reviewer.md` | Reviews C#/URP changes for frame budget, comfort, serialization, allocations. Read-only. | core |
| `agents/xr-architect.md` | Design-only advisor for World Package / pipeline / companion decisions. | nice |
| `agents/shader-dev.md` | URP HLSL / Shader Graph, mobile-XR tuned. | nice |
| `agents/test-writer.md` | EditMode NUnit tests for the pure logic, run headless. | nice |
| `skills/dem-to-terrain/` | Real NASA/USGS DEM → Unity terrain (GDAL recipe, byte-order + NoData gotchas). | core |
| `skills/android-xr-perf/` | The 72/90 fps profiling drill. | core |
| `skills/android-xr-asset-budget/` | Rules every AI-generated mesh/texture/splat must pass before it ships. | core |
| `skills/add-world/` | The repeatable World Package authoring flow. | nice |
| `commands/deploy-headset.md` | Build + adb install to Galaxy XR + tail filtered log. | nice |
| `commands/run-tests.md` | Run EditMode logic tests headless. | nice |

Later, when you hit the need: add safety **hooks** (block accidental `.meta`/scene edits, warn on `GetComponent`-in-Update) — deterministic guardrails a solo dev otherwise forgets. Skipped for now because there's no code yet to guard.

## Setup order (matches the build plan)

1. **Now:** Context7 works on clone (`../.mcp.json`). `CLAUDE.md` and the skills guide any Claude Code session.
2. **Phase 0 (Unity installed):** add the **Unity MCP bridge** — the biggest single upgrade to the workflow. Merge this into `../.mcp.json`:
   ```json
   "unity": { "command": "uvx", "args": ["mcp-for-unity"] }
   ```
   then in Unity install the package (`Window > Package Manager > Add from git URL`: `https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main`) and run `Window > MCP for Unity > Configure All Detected Clients`. Needs Python 3.10+ via `uv`. This lets Claude read the Unity console and edit/verify scenes instead of coding blind. Verify the exact server invocation against the repo README at setup time.
3. **When authoring assets:** add `blender-mcp` (`uvx blender-mcp` + the Blender addon) per-session, not always-on.
4. **When wiring Gemini (v1.1):** Firebase AI Logic SDK for Unity (below).

---

# Full catalog (the landscape)

Priorities are for **this** project (solo dev, Windows RTX 5070 Ti primary + Mac for docs, Unity 6, Galaxy XR). Cited to real repos/vendors; nothing invented.

## AI coding assistants / IDEs (Windows primary)

| Tool | Priority | Why | Get |
|---|---|---|---|
| **Claude Code + a Unity MCP bridge** | must-have | The spine. Plain Claude Code edits `.cs` half-blind; with the bridge it sees the scene, console, and components. Run natively on Windows with Git Bash (not WSL — project lives on the Windows filesystem). | already installed + Unity MCP below |
| **Visual Studio 2026 Community** (Unity workload) | must-have | The free debugger of record on Windows: attach breakpoints to the Editor or a device build. Free for a solo dev even commercially. | https://visualstudio.microsoft.com/ |
| **GitHub Copilot** (in VS / VS Code) | nice | Cheap always-on completions for routine C#; agent mode is strongest on Windows VS/VS Code. | https://github.com/features/copilot |
| JetBrains Rider + AI Assistant | situational | Genuinely great Unity integration, but with Claude Code doing the heavy agent work and VS Community free, paying for Rider is a v1.1-era luxury, not a Phase 0 need. | https://www.jetbrains.com/rider/ |
| Cursor / Windsurf | situational | AI-first VS Code forks; alternatives to Claude Code, not complements. | cursor.com / windsurf.com |
| Unity AI (in-editor) | nice-later | Live replacement for the retired Muse; official MCP + asset gen. Beta, quality uneven, $10/mo. Try the trial; don't build around it yet. | https://unity.com/blog/unity-ai-how-to-get-started |
| ~~Unity Muse~~ | excluded | Retired in 2024. Any Muse tutorial is obsolete; it's Unity AI now. | — |

## MCP servers — Unity / 3D / graphics

| Server | Priority | Why | Get |
|---|---|---|---|
| **CoplayDev/unity-mcp** | must-have | The Unity Editor bridge: 47 tools — scenes, GameObjects, C# edit with compile-check, console, tests, build. The default. Pick **one** Unity bridge. | https://github.com/CoplayDev/unity-mcp |
| ahujasid/blender-mcp | nice | Model/fetch bespoke props in Blender (landers, markers, flora), export glTF/FBX to Unity. Fronts PolyHaven + Rodin/Tripo. Per-session. | https://github.com/ahujasid/blender-mcp |
| IvanMurzak/Unity-MCP | situational | Free alternative; can expose your own C# methods as tools (e.g. a terrain-loader). Needs a cloud login. Only if Coplay misbehaves. | https://github.com/IvanMurzak/Unity-MCP |
| CoderGamester/mcp-unity | situational | Third free Unity bridge, strong material tools. Backup only. | https://github.com/CoderGamester/mcp-unity |
| Unity official MCP (`com.unity.ai.assistant`) | situational | First-party accuracy but needs a paid Unity AI subscription + Unity Cloud link. | https://unity.com/blog/unity-ai-mcp-how-to-get-started |
| Meshy / Tripo / Rodin(skill) MCP | situational | Paid text/image-to-3D. Keep OUT of always-on config; enable per-session when generating meshes. Rodin ships as a Claude **skill** + Blender hook, not a standalone server. | meshy-dev/meshy-mcp-server ; VAST-AI-Research/tripo-mcp ; DeemosTech/rodin3d-skills |
| TRELLIS MCP / ShaderToy-MCP / Godot MCP | excluded | TRELLIS needs a CUDA GPU (none on Mac). ShaderToy is GLSL (we're URP HLSL). Godot is the wrong engine. | — |

## MCP servers — dev / docs / UI-UX

| Server | Priority | Why | Get |
|---|---|---|---|
| **Context7** (Upstash) | must-have | Live Unity/OpenXR/XRI/URP/Firebase docs into the prompt so generated C# matches the installed version. (Active in `../.mcp.json`.) Verify what it returns — its Unity coverage is thinner than web frameworks. | https://github.com/upstash/context7 |
| GitHub MCP (official, remote) | nice | Repo/issue/PR/CI by natural language, no local process. Overlaps with the `gh` CLI you already use. | https://github.com/github/github-mcp-server |
| Ref / docs-mcp-server | situational | Cheaper or self-hostable doc lookup; index only the packages this project pins. | ref-tools/ref-tools-mcp ; arabold/docs-mcp-server |
| Figma Dev Mode MCP | situational | Only for flat 2D HUD/menu mockups; needs a paid Figma seat and outputs web React/CSS you re-map by hand. No spatial-XR target. | https://www.figma.com/blog/introducing-figma-mcp-server/ |
| Firecrawl / Playwright | situational | Only if scraping a NASA/USGS portal with no clean API, or driving a companion website. Note this env already has an agent-browser MCP. | firecrawl/firecrawl-mcp-server ; microsoft/playwright-mcp |
| filesystem / fetch / memory (reference servers) | excluded | Redundant — Claude Code already reads/writes files, fetches/searches the web, and carries memory via CLAUDE.md. | — |

## 3D asset generation

| Tool | Priority | Why | Get |
|---|---|---|---|
| **Meshy (v6)** | nice | Browser-based, exports Unity-ready FBX/GLB/USDZ + packed PBR. Props, waymarkers, probes. With local TRELLIS now viable on the 5070 Ti, Meshy is convenience rather than necessity. Retopo + ASTC before shipping. | https://www.meshy.ai/ (Pro $20/mo) |
| Tripo | nice | Cheaper Meshy equal; quality varies per prompt. | https://www.tripo3d.ai/ (Pro $19.9/mo) |
| Hyper3D Rodin | situational | Highest detail — hero assets / the companion only (credit-priced, more cleanup). | https://hyper3d.ai/ |
| Microsoft TRELLIS / TRELLIS.2 | situational | Free/open, best topology — runs locally on the RTX 5070 Ti (half-precision/512³ or the quantized ComfyUI build in ~6-9GB VRAM; full 1024³ wants ~30GB). Casual prop generation only, not pipeline. | https://github.com/microsoft/TRELLIS |

Hard rule for **all** of these: they output dense high-poly meshes that must be retopologized/decimated + ASTC-compressed before they're safe on the Galaxy XR GPU. See `skills/android-xr-asset-budget/`.

## Textures, skyboxes, audio

| Tool | Priority | Why | Get |
|---|---|---|---|
| **Blockade Labs Skybox AI** | must-have | Alien skies + true 32-bit HDRI for image-based lighting; official Unity plugin, equirectangular/cubemap for URP. Exactly the tool for planet atmospheres and exoplanet skies. | https://www.blockadelabs.com/ |
| **ElevenLabs Sound Effects** | must-have | Text-to-SFX with looping + commercial license: wind, regolith crunch, probe pings, UI blips, ambient beds. WAV → Unity AudioClip. REST API. | https://elevenlabs.io/sound-effects |
| Adobe Substance 3D Sampler | nice | Industry standard image-to-PBR-material for regolith/rock/ice surfaces on the real terrain. | https://www.adobe.com/products/substance3d/apps/sampler.html |
| Stable Audio | nice | Royalty-clear ambient beds/drones for a contemplative explorer. | https://stableaudio.com/ |
| Scenario | situational | Train one style, keep hundreds of generated assets visually consistent. Worth it once art direction is locked. | https://www.scenario.com/ |
| Suno (v5) | situational | Full tracks (menu theme, discovery cue) only — not in-scene ambience. Licensing is contested; read terms. | https://suno.com/ |
| Midjourney | situational | Pure concept art / mood, upstream of production (no Unity export). Feed images into Meshy/Tripo. | https://www.midjourney.com/ |

## Photoreal capture / Gaussian splats (a v2 pillar)

| Tool | Priority | Why | Get |
|---|---|---|---|
| Scaniverse | nice-later | Free on-device phone splat capture, PLY/SPZ export. | https://scaniverse.com/ |
| Polycam | nice-later | Also gives a real mesh (GLB) you can decimate — cheaper on-headset than a splat. | https://poly.cam/ |
| Postshot | nice-later | Highest-quality local splat trainer — runs on the Windows RTX 5070 Ti. Free tier to learn now; the paid tier (needed for PLY export + commercial use) has contradictory pricing reports [verify at v2 checkout]. | https://www.jawset.com/ |
| aras-p UnityGaussianSplatting | nice-later | The importer that lands splats in Unity 6 URP/Vulkan. **Distant set-dressing only, cap the count, profile on-device** — the author calls it toy-grade. | https://github.com/aras-p/UnityGaussianSplatting |

## UI / UX

Honest finding: **no AI tool outputs Unity UI Toolkit or spatial-XR layouts.** Figma Make / v0 / Uizard / Google Stitch make flat 2D/web mockups only — useful for concepting menus/HUD panels you then rebuild by hand in Unity. Unity's own in-editor AI at least drops sprites/icons into Unity. Treat all of these as concept-only; the real spatial UI is hand-built in UI Toolkit / world-space canvas. Don't paste web UI code into the game.

## XR dev, testing, performance

| Tool | Priority | Why | Get |
|---|---|---|---|
| **Unity OpenXR: Android XR** | must-have | The base runtime that makes a Unity 6 URP/Vulkan project run on Galaxy XR. Cross-device (not vendor-locked). | https://developer.android.com/develop/xr/unity |
| Android XR Emulator (Android Studio) | situational | Demoted: with a real headset + Direct Preview + the XR Interaction Simulator, the emulator is a fourth tier with no unique job. | https://developer.android.com/develop/xr/develop-with-emulator |
| **Engine Hub + Direct Preview** (Windows) | must-have | The inner loop: press Play in the Unity editor and the scene streams live to the headset with real hand tracking/eye gaze. Requires Unity 6000.3.5f2+, USB data cable. Never a framerate signal — the PC renders it. | https://developer.android.com/develop/xr/unity/direct-preview |
| **XR Interaction Simulator** (XRI sample) | must-have | Headset-off tier: fake headset + tracked hands from keyboard/mouse in-editor, for logic/UI/POI wiring at 11pm. | bundled with XR Interaction Toolkit 3.x |
| **Unity Profiler + Frame Debugger + Memory Profiler** | must-have | The real perf instruments, attached over adb. There is no credible "AI profiler". See `skills/android-xr-perf/`. | https://developer.android.com/blog/posts/optimizing-performance-for-android-xr-with-unity |
| **Unity 6 SRP foveated rendering** | must-have | Biggest GPU lever for a fragment-bound terrain scene; eye-tracked on Galaxy XR. | https://docs.unity3d.com/6000.0/Documentation/Manual/xr-foveated-rendering.html |
| Qualcomm Snapdragon Profiler | situational | Adreno-level detail when Unity says "GPU-bound" but not why. | https://www.qualcomm.com/developer/software/snapdragon-profiler |
| Unity Test Framework headless (.NET) | nice | Run EditMode logic tests in seconds so Claude self-verifies. See `commands/run-tests.md`. | https://gamedev.center/run-unity-tests-faster-dotnet/ |
| ~~Meta XR Simulator / OVR Metrics~~ | excluded | **Wrong platform** — Galaxy XR is Qualcomm/Android XR, not Meta/Quest. Ignore all Quest-specific tooling and tutorials. | — |

## Backend / data (Gemini + terrain)

| Tool | Priority | Why | Get |
|---|---|---|---|
| **Firebase AI Logic SDK for Unity** | must-have (v1.1) | The sanctioned Gemini path: Unity 6 + Android XR compatible, App Check keeps your key off the client. **Never embed a raw Gemini key.** Supersedes community wrappers (UGemini archived Feb 2026). | https://firebase.blog/posts/2025/05/ai-logic-unity-androidxr/ |
| Gemini Live API (Unity) | situational | Real-time voice for the companion — but Unity support was still "coming soon" mid-2026. Plan for it; don't assume turnkey. | https://firebase.google.com/docs/ai-logic/live-api |
| Unity Sentis / Inference Engine | situational | Only if a *small* model must run offline on the headset. Gemini itself is a cloud API, so this isn't for Gemini. | https://docs.unity3d.com/Packages/com.unity.ai.inference@latest/ |
| **GDAL** | must-have | Core of the terrain pipeline: NASA/USGS DEM → 16-bit RAW heightmap. CLI, so Claude can drive it. Runs on the Windows box so outputs land beside the Unity project. See `skills/dem-to-terrain/`. | Windows: OSGeo4W (https://trac.osgeo.org/osgeo4w/) or conda-forge; Mac fallback: `brew install gdal` |
| QGIS | nice | Eyes-on inspection/mosaic/clip of DEM tiles before GDAL batch conversion. | https://qgis.org |
| USGS Astrogeology / NASA PDS / EarthExplorer | must-have | The actual elevation data. Record source, projection, units, attribution. | https://astrogeology.usgs.gov/search |
| Cesium for Unity | situational | Only if you ever need globe-scale streaming terrain instead of a baked walkable patch — heavier runtime dependency. | https://github.com/CesiumGS/cesium-unity |

## Second-pass findings (deep-research, adversarially verified July 2026)

A dedicated deep-research pass (103 agents, 3-vote verification per claim) hunted for tools NOT in the catalog above. Twelve survived; verdicts re-slanted for the Windows-primary setup.

| Tool | Category | Priority | Why / honest fit | Get |
|---|---|---|---|---|
| **gis-mcp** (mahdin75) | geospatial MCP | nice | Wraps the Python GIS stack (Rasterio, PyProj, GeoPandas, Shapely) as ~92 MCP tools: DEM reprojection, resampling, hillshade, coordinate transforms driven from Claude Code. A higher-level complement to raw GDAL for the terrain pipeline. Beta (MIT, v0.14.0 Dec 2025); the dem-to-terrain skill's GDAL recipe stays the source of truth. | https://github.com/mahdin75/gis-mcp |
| **Task Master** (eyaltoledano) | project-mgmt MCP | nice | Mature (27.9k stars) AI task management as MCP; first-class Claude Code support and can drive the Claude Code CLI directly, so no separate model API key. Gotcha from its issue tracker: silently falls back to the paid Anthropic API if ANTHROPIC_API_KEY is set in env. | https://github.com/eyaltoledano/claude-task-master |
| **awesome-gamedev-agent-skills** | skill collection | nice | 66 version-pinned skills + a router that fingerprints the engine and loads only matching ones; includes 8 Unity-6-pinned skills (csharp-scripting, input-system, physics, animation, scriptableobjects, navmesh, build-pipeline). Best-matched collection for our stack; healthy (317 stars, active). Cherry-pick per our no-bulk-install rule. | https://github.com/gamedev-skills/awesome-gamedev-agent-skills |
| nowsprinting/unity-coding-skills | skill collection | situational | Test-first Unity C# workflow (9 skills, 3 subagents) — but its run-tests/edit-scene skills hard-require JetBrains Rider's built-in MCP server, no fallback. Only relevant if Rider ever enters the toolchain (currently skipped in favor of VS Community). | https://github.com/nowsprinting/unity-coding-skills |
| cc-plugin-unity-gamedev | skill collection | situational | 21 Unity skills via /plugin install; decent pattern coverage but nothing XR-specific and several skills target paid assets (Wwise, Behavior Designer). Very early (4 stars, single push). | https://github.com/tjboudreaux/cc-plugin-unity-gamedev |
| **Cascadeur** (Nekki) | AI animation | nice-later | AI-assisted keyframe animation (neural AutoPosing), cleans mocap, exports FBX with a documented Unity workflow. Native Windows and macOS builds. The animation-content answer when creatures/characters arrive. | https://cascadeur.com/ |
| DeepMotion Animate 3D | AI animation | situational | Phone video → 3D character animation (no suit), FBX for Unity. Output needs cleanup (foot sliding); free-tier 3D-download unconfirmed. For companion/creature motion later. | https://www.deepmotion.com/animate-3d |
| Convai (Unity plugin) | NPC dialogue | situational | Mature full-stack NPC platform (dialogue, voice, lip-sync; Unity-verified, free plugin, usage-priced cloud). Honest caveat: it REPLACES a Gemini pipeline rather than integrating with it — adopting it means abandoning the Firebase AI Logic companion path. Know it exists; default remains Gemini. | https://docs.convai.com/api-docs/plugins-and-integrations/unity-plugin |
| Inworld Runtime (Unity SDK) | NPC dialogue | situational | The main Convai alternative: composable graph engine + early-access Unity SDK (Unity 6000.0.41+). Same caveat: a competing companion backend, and Android XR support is not claimed. | https://docs.inworld.ai/docs/Unity/runtime/get-started |
| AltTester Unity SDK | playtest/QA | situational | Open-source (GPL) UI-driven test automation; Claude Code can author its C#/Python scripts. Friction: requires the proprietary AltTester Desktop app running during tests, account + license key even on the free tier. A claimed "AltTester MCP server" was investigated and does not exist. Phase-2-at-earliest. | https://github.com/alttester/AltTester-Unity-SDK |
| GameDriver Test Assistant | playtest/QA | situational | Attaches to a running Unity build for live test creation and embeds a local MCP server (7 tools; vendor documents Claude Desktop config — Claude Code registration [unverified]). **Windows-only today — which now fits our primary machine.** Watch-list for phase-2 QA. | https://kb.gamedriver.io/gamedriver-test-assistant |
| ~~yimengfan/claude-code-for-unity3d~~ | — | **excluded** | Search-result trap: despite the name it contains zero Unity content — it's a stale fork of a generic Claude Code bundle with Windows .bat installers added. Do not install. | — |

Verified gaps (nothing real survived the vote): **localization, marketing/store-listing tooling, and spatial-UI/XR-interaction design AI**. For those, the answer today is doing it by hand with the design guidance in [docs/ANDROID-XR-PLATFORM.md](../docs/ANDROID-XR-PLATFORM.md).

## How the `.claude` pieces work (reference)

- **`CLAUDE.md`** (repo root): always loaded into context; project rules and conventions.
- **`.mcp.json`** (repo root): project-scoped MCP servers, offered when you open the repo in Claude Code.
- **`settings.json`**: permissions (and later, hooks).
- **`agents/*.md`**: subagents with YAML frontmatter (`name`, `description`, `tools`) + a system prompt; each runs in its own context so a review or shader task doesn't pollute the main thread.
- **`skills/<name>/SKILL.md`**: repeatable workflows Claude invokes by judgment when relevant.
- **`commands/*.md`**: manual slash-command shortcuts (`/deploy-headset`, `/run-tests`).
- Anthropic's reference: https://code.claude.com/docs/en/sub-agents and https://code.claude.com/docs/en/claude-directory

Template sources worth mining (copy 2–3 files, never bulk-install): everything-claude-unity (URP/shader/serialization skills, safety hooks), VoltAgent/awesome-claude-code-subagents (csharp-developer, game-developer), hesreallyhim/awesome-claude-code. For a free maintained reviewer baseline: `/plugin install pr-review-toolkit@claude-plugins-official`.
