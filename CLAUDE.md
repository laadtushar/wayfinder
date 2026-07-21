# Wayfinder — project instructions for Claude Code

Wayfinder is a solo-developer game for the **Samsung Galaxy XR** headset (Android XR). You command a ship, warp between real solar-system worlds, and walk their surfaces on foot. Read [DESIGN.md](DESIGN.md) and [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) before changing anything structural. The build plan is [docs/plans/2026-07-20-wayfinder-v1.md](docs/plans/2026-07-20-wayfinder-v1.md). Tooling notes are in [.claude/README.md](.claude/README.md).

## Machines

The **Windows PC (RTX 5070 Ti) is the canonical dev machine**: Unity (6000.3.5f2+ floor), Engine Hub/Direct Preview, adb, the Unity MCP bridge, GDAL (via OSGeo4W or conda-forge, not brew), and all splat/reconstruction authoring run there. Claude Code runs natively on Windows with Git Bash (not WSL — the project lives on the Windows filesystem). The **Mac is for docs, research, and repo/plan work only — never install Unity on it**; one engine install means one source of truth. Push before switching machines.

## The stack (pin these; do not drift)

- **Engine:** Unity 6 LTS, C#. Universal Render Pipeline (URP), **Vulkan** graphics API. Not Built-in pipeline, not GLES.
- **XR:** OpenXR + `com.unity.xr.androidxr-openxr`, XR Interaction Toolkit, XR Hands. Input via the **new Input System / XR action maps**, never legacy `Input`.
- **Target:** Android XR (Galaxy XR, Snapdragon XR2+ Gen 2). This is **Android under the hood** (adb works). It is **not Meta/Quest** — ignore Meta XR Simulator, OVR Metrics, and any Quest-specific package or tutorial.
- **Shaders:** URP HLSL / ShaderLab / Shader Graph. Any GLSL / ShaderToy reference is math to port by hand, never a drop-in.
- **Gemini companion (later):** Google **Firebase AI Logic SDK for Unity**. Never embed a raw Gemini API key in the client; use Firebase App Check. Gemini Live (real-time voice) for Unity was still landing as of mid-2026 — treat it as not-yet-turnkey.

## Hard rules from the platform (non-negotiable, they gate store placement)

- **Frame budget:** 72 fps minimum, 90 target (~13.8 / 11.1 ms per frame, stereo). This is the master constraint.
- **Comfort:** teleport / world-grab / snap-turn only. **No smooth continuous camera rotation, ever.** The warp is a brief bright transition, not a forward-acceleration tunnel.
- **Space:** everything playable within a 2.0 m radius, seated or standing.
- **Input:** hand tracking is the default; must be playable with **no controllers**.
- **Render:** at least 1856×2160 per eye. Turn on **eye-tracked foveated rendering** early (Project Settings > XR Plug-in Management > OpenXR > Foveated Rendering).

## Architecture in one line

One **persistent Bridge scene**; each world is a **World Package** (terrain + points of interest + real physics values + sky/light + audio) loaded additively behind the warp effect. Adding a world = adding a package, no travel/locomotion/discovery code changes. Full detail: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## C# / Unity conventions

- **No per-frame allocations** in Update/LateUpdate or any hot path. Cache references in Awake; do not `GetComponent` in Update.
- Serialize with `[SerializeField] private`, not public fields. Null-check serialized refs in Awake and fail loudly.
- Respect assembly definition (`.asmdef`) boundaries so generated scripts land where they compile. Keep **EditMode logic tests** and **PlayMode / on-device tests** in separate assemblies.
- Every new asset has a committed `.meta` file. Never edit or delete a `.meta` or `.unity` scene file by hand unless explicitly asked.
- Real surface gravity is data, not magic numbers scattered around: Mars 3.72, Moon 1.62 m/s², set on the World Package.

## Testing

- Pure logic (travel state machine, world registry, field log, coordinate/terrain math) gets EditMode NUnit tests, run headless so you can self-verify. Scene/rig/interaction work climbs three tiers: **XR Interaction Simulator** in-editor (no headset, logic/UI wiring) → **Direct Preview via Engine Hub** (headset streams the editor live over USB, seconds per iteration, real hand tracking) → **device build over adb** (the truth). **Direct Preview and the editor are never framerate evidence; only an on-device build is** — the PC's GPU renders the preview.
- The build plan's rule stands: **Site One must hold 72+ fps on the real headset before any other site is built.**

## Docs, versions, and honesty

- Unity / OpenXR / XRI / URP / Firebase AI Logic move fast. Before writing code against a version-sensitive API, **fetch current docs via Context7** rather than trusting training data, then verify what it returns (its Unity C# coverage is thinner than web frameworks).
- Flag unverified claims as `[unverified]`. Don't invent MCP server names, package versions, or API shapes.

## The terrain data pipeline (the fiddly part)

Real NASA/USGS elevation → Unity terrain via **GDAL**: reproject → clip → `gdal_translate` to a **16-bit unsigned RAW** heightmap at a **2^n+1** resolution (513/1025/2049). Two gotchas that bite every time: Unity wants **big-endian ("Mac byte order")** RAW, and **NoData holes** must be filled or they become spikes/craters. The repeatable recipe is the `dem-to-terrain` skill.

## Don't

- Don't add a second Unity MCP server alongside the primary one (tool-name collisions, token bloat).
- Don't wire paid asset-generator MCPs (Meshy/Tripo/Rodin) into the always-on config — enable per-session when actually authoring assets.
- Don't paste web-UI-generator output (v0/Figma Make) as Unity UI; rebuild spatial UI by hand in UI Toolkit / world-space canvas.

## graphify

This project has a graphify knowledge graph at graphify-out/.

Rules:
- Before answering architecture or codebase questions, read graphify-out/GRAPH_REPORT.md for god nodes and community structure
- If graphify-out/wiki/index.md exists, navigate it instead of reading raw files
- After modifying code files in this session, run `python3 -c "from graphify.watch import _rebuild_code; from pathlib import Path; _rebuild_code(Path('.'))"` to keep the graph current

## Agent skills

### Issue tracker

Issues live in this repo's GitHub Issues (`laadtushar/wayfinder`), via the `gh` CLI. See `docs/agents/issue-tracker.md`.

### Triage labels

Default five-role vocabulary (`needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`). See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: `CONTEXT.md` + `docs/adr/` at the repo root (created lazily; until then `DESIGN.md`/`docs/ARCHITECTURE.md` are the glossary of record). See `docs/agents/domain.md`.
