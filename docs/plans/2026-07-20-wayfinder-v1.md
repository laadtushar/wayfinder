# Wayfinder v1 Implementation Plan

**Goal:** Ship a solo Galaxy XR experience where you command a ship's bridge, warp to three real solar-system landing sites (two Mars, one Moon) built from real NASA/USGS terrain data, and explore each on foot with guided points of interest and a field log. No AI companion yet (v1.1).

**Architecture:** One persistent bridge scene. Each world is a self-contained "World Package" (terrain, points of interest, real physics values, sky/light, audio) loaded additively on demand; the warp transition covers the load. Surface movement is teleport plus world-grab within a 2 m space, never smooth camera rotation.

**Tech Stack:** Unity 6 LTS, Universal Render Pipeline on Vulkan, OpenXR + Unity OpenXR: Android XR provider, XR Interaction Toolkit, XR Hands, AR Foundation. Android App Bundle to the Android XR Play track. Data: NASA/USGS digital elevation models converted with GDAL/QGIS.

**Read before starting:** `~/Projects/wayfinder/DESIGN.md` (the approved design) and `~/Projects/wayfinder/IDEATION.md` (why these choices, the hardware constraints).

**Developer notes:** You are on macOS. Unity builds and deploys to Galaxy XR fine from a Mac, but the live "Engine Hub" in-editor preview is Windows-only, so your loop is build-and-deploy-APK, which is slower. Budget for that. You are new to XR; each phase teaches the toolchain as it goes.

**The one sequencing rule that governs everything:** Build Site One completely and validate framerate and comfort on the actual Galaxy XR headset (Task 2.9) BEFORE building Sites Two and Three. Do not author three worlds and discover on day 20 that none hold framerate.

---

## Confidence flags on external facts

- `android.software.xr.api.openxr` manifest feature, URP + Vulkan requirement, Android App Bundle to a dedicated Android XR track, 72 fps min / 90 target, ≥1856×2160 per eye, 2.0 m playable radius, no smooth camera rotation: **[Certain]** (verified against Google's Android XR docs during the ideation research).
- Exact Unity package **version numbers** move constantly: **[verify at install]** against https://developer.android.com/develop/xr/unity before pinning any version.
- Mars/Moon data products (MOLA, HiRISE/HRSC DTMs, LOLA) exist and are public: **[Certain]**. Exact file URLs: **[verify]** on the USGS Astrogeology and NASA PDS portals when you fetch them.
- Real surface gravity constants: Mars 3.72 m/s², Moon 1.62 m/s²: **[Certain]**.

---

## Phase 0 — Toolchain and "hello headset"

Goal: prove you can build a Unity app and run it on the Galaxy XR with hand tracking, before touching game content. If this phase fails, nothing else matters.

### Task 0.1: Install Unity 6 with Android support

**Steps:**
1. Install Unity Hub (unity.com/download).
2. In Hub, install the latest **Unity 6 LTS** (6000.x). In the install options, tick **Android Build Support** (and its child modules: Android SDK & NDK Tools, OpenJDK). On Apple Silicon, install the Apple-silicon editor.
3. Verify: Unity Hub shows Unity 6 LTS with the Android logo under installed modules.

**Check:** `Unity Hub > Installs` lists Unity 6 LTS with Android support. No test; this is setup.

### Task 0.2: Create the project and put it under version control

**Steps:**
1. New project, template **Universal 3D** (URP) or Unity's **Mixed Reality** template if offered for your version. Name it `wayfinder`. Location: `~/Projects/wayfinder/unity` (keep it beside DESIGN.md, not inside docs).
2. In a terminal:
   ```bash
   cd ~/Projects/wayfinder/unity
   git init
   # Unity's default .gitignore: add via Unity or use github.com/github/gitignore Unity.gitignore
   git add .gitignore Assets ProjectSettings Packages
   git commit -m "chore: initialize Unity 6 URP project for Wayfinder"
   ```
3. Confirm a `.gitignore` excludes `Library/`, `Temp/`, `Logs/`, `Build/`.

**Check:** `git log` shows one commit; `git status` does not list `Library/`.

### Task 0.3: Install and configure the XR packages

**Steps:**
1. `Window > Package Manager`. Install (confirm current versions against the Android XR Unity docs first):
   - **XR Plugin Management**
   - **OpenXR Plugin**
   - **Unity OpenXR: Android XR** (`com.unity.xr.androidxr-openxr`)
   - **XR Interaction Toolkit** (+ its Starter Assets sample)
   - **XR Hands**
   - **AR Foundation** (dependency of the Android XR provider)
2. `Project Settings > XR Plug-in Management > Android tab`: enable **OpenXR**. Resolve any red validation warnings it shows.
3. In the OpenXR settings for Android, enable the **Android XR** feature group and the interaction profiles: **Hand Interaction Profile**, **Eye Gaze Interaction**, and (optional) a controller profile.
4. Commit: `git commit -am "feat: add and configure XR packages (OpenXR + Android XR + XRI)"`

**Check:** XR Plug-in Management shows OpenXR enabled on Android with no unresolved validation errors.

### Task 0.4: Configure Android XR build + player settings

**Steps:**
1. `Build Settings > Switch Platform > Android`.
2. `Player Settings`:
   - Graphics API: **Vulkan only** (remove GLES). [Certain: Android XR advanced features require Vulkan.]
   - Scripting backend: **IL2CPP**, target architecture **ARM64**.
   - Minimum API level: set to the Android XR minimum (verify current; Android 14 / API 34 class).
3. Add the XR manifest feature so the store and device treat it as a fully-immersive XR app. In `Assets/Plugins/Android/AndroidManifest.xml` (create if absent), inside `<manifest>`:
   ```xml
   <uses-feature android:name="android.software.xr.api.openxr" android:required="true" />
   ```
   [Certain: this is the feature line Google's publishing docs require for OpenXR/Unity XR apps.]
4. Commit: `git commit -am "chore: Android XR player settings (Vulkan, IL2CPP, XR manifest feature)"`

**Check:** Player Settings show Vulkan-only + IL2CPP + ARM64; the manifest contains the openxr feature line.

### Task 0.5: Deploy "hello headset" and confirm hand tracking

**Steps:**
1. Put a **XR Origin (XR Rig)** in the default scene (via `GameObject > XR > XR Origin`), add the XR Interaction Toolkit input, and drop a lit cube and a ground plane at eye height.
2. Enable developer/sideload on the Galaxy XR (it sideloads by default; no PC dev-mode toggle needed per the ideation research), connect over USB.
3. `Build and Run` to the device.
4. Put the headset on. Expected: you see the cube and plane in a room-scale space, your **hands are tracked** (visible/interactable), and the view is stereo and stable at head movement.

**Check (CHECKPOINT 0):** The app runs on the Galaxy XR, renders in stereo, and tracks your hands. If yes, the toolchain is proven and every later build reuses this exact pipeline. Commit a tag: `git tag v0-hello-headset`.

---

## Phase 1 — The bridge hub

Goal: a persistent bridge you stand on, a viewscreen to pick a destination, a warp lever, and a travel manager that additively loads/unloads worlds behind the warp effect. Worlds are placeholder here; real terrain comes in Phase 2.

### Task 1.1: The World Package data model (TDD — real logic)

**Files:**
- Create: `Assets/Scripts/Worlds/WorldPackage.cs`
- Create: `Assets/Scripts/Worlds/WorldRegistry.cs`
- Test: `Assets/Tests/EditMode/WorldRegistryTests.cs`

**Step 1: Write the failing test** (Unity Test Framework, EditMode/NUnit)

```csharp
using NUnit.Framework;

public class WorldRegistryTests
{
    [Test]
    public void GetById_ReturnsPackage_WhenPresent()
    {
        var mars = ScriptableObject.CreateInstance<WorldPackage>();
        mars.id = "mars-olympus";
        var registry = new WorldRegistry(new[] { mars });
        Assert.AreEqual(mars, registry.GetById("mars-olympus"));
    }

    [Test]
    public void GetById_ReturnsNull_WhenMissing()
    {
        var registry = new WorldRegistry(System.Array.Empty<WorldPackage>());
        Assert.IsNull(registry.GetById("nope"));
    }
}
```

**Step 2: Run it (Window > General > Test Runner > EditMode > Run).** Expected: FAIL (types not defined).

**Step 3: Minimal implementation.**

```csharp
// WorldPackage.cs
using UnityEngine;

[CreateAssetMenu(menuName = "Wayfinder/World Package")]
public class WorldPackage : ScriptableObject
{
    public string id;
    public string displayName;
    public string sceneName;          // additive scene for this world's terrain
    public float surfaceGravity;      // m/s^2 (Mars 3.72, Moon 1.62)
    // POIs added in Phase 2
}
```
```csharp
// WorldRegistry.cs
using System.Collections.Generic;

public class WorldRegistry
{
    private readonly Dictionary<string, WorldPackage> _byId = new();
    public WorldRegistry(IEnumerable<WorldPackage> packages)
    {
        foreach (var p in packages) _byId[p.id] = p;
    }
    public WorldPackage GetById(string id) => _byId.TryGetValue(id, out var p) ? p : null;
}
```

**Step 4: Run tests.** Expected: PASS.

**Step 5: Commit.** `git commit -am "feat: WorldPackage data model + registry with tests"`

### Task 1.2: The travel state machine (TDD — real logic)

**Files:**
- Create: `Assets/Scripts/Travel/TravelState.cs`
- Test: `Assets/Tests/EditMode/TravelStateMachineTests.cs`

**Step 1: Failing test** — enforce the legal transitions and block a double-warp.

```csharp
using NUnit.Framework;

public class TravelStateMachineTests
{
    [Test]
    public void Warp_FromBridge_EntersWarping_ThenSurface()
    {
        var sm = new TravelStateMachine();               // starts OnBridge
        Assert.IsTrue(sm.TryBeginWarp("mars-olympus"));  // -> Warping
        Assert.AreEqual(TravelState.Warping, sm.State);
        sm.CompleteWarp();                               // -> OnSurface
        Assert.AreEqual(TravelState.OnSurface, sm.State);
    }

    [Test]
    public void Warp_WhileWarping_IsRejected()
    {
        var sm = new TravelStateMachine();
        sm.TryBeginWarp("mars-olympus");
        Assert.IsFalse(sm.TryBeginWarp("moon-shackleton")); // no double-trigger
    }
}
```

**Step 2: Run — FAIL. Step 3: implement** a small enum + guarded transitions. **Step 4: Run — PASS. Step 5: commit** `feat: travel state machine with guarded transitions + tests`.

### Task 1.3: The bridge scene

**Files:** Create scene `Assets/Scenes/Bridge.unity`.

**Steps:**
1. Build a simple bridge interior (a floor, a console, a large flat "viewscreen" quad ahead). Keep polygon count low.
2. Place the XR Origin at the standing spot. Ensure the whole console is reachable within ~1.5 m (comfort rule: playable within 2.0 m).
3. Set `Bridge` as the first scene in Build Settings; it is the persistent scene that never unloads.
4. Commit `feat: bridge scene with reachable console and viewscreen`.

**Check:** Build and Run — you stand on the bridge, the console is within arm/pointer reach.

### Task 1.4: Viewscreen star map + destination selection

**Files:** Create `Assets/Scripts/UI/DestinationMenu.cs`.

**Steps:**
1. On the viewscreen, show a small list of destinations sourced from the `WorldRegistry` (placeholder entries for now: "Mars — Olympus Mons", "Mars — Valles Marineris", "Moon — Shackleton").
2. Selection by hand ray + pinch or gaze + pinch (XR Interaction Toolkit UI). Selecting one sets the "pending destination" on the travel manager; it does not travel yet.
3. Commit `feat: viewscreen destination menu bound to world registry`.

**Check:** On device, you can point and select a destination; the selected one is highlighted.

### Task 1.5: The warp lever + travel manager (additive load/unload)

**Files:** Create `Assets/Scripts/Travel/TravelManager.cs` (MonoBehaviour wrapping the tested `TravelStateMachine`).

**Steps:**
1. A grabbable lever (XR Interaction Toolkit interactable). Pulling it, with a destination pending, calls `TryBeginWarp`.
2. `TravelManager` coroutine: start warp visual, `SceneManager.LoadSceneAsync(worldScene, Additive)`, wait for load, then `CompleteWarp`, hide the bridge visuals / fade the player onto the surface. Returning does the reverse and `UnloadSceneAsync`.
3. The warp visual is a **brief bright transition**, not a long forward-motion tunnel (comfort). Keep it under ~2 seconds if the load allows; if the load is slower, hold the bright state, do not add forward acceleration.
4. Commit `feat: warp lever triggers additive world load behind a warp transition`.

**Check (CHECKPOINT 1):** On device: stand on bridge → select a placeholder destination → pull lever → warp effect → an empty placeholder world scene is loaded additively → return works and unloads it. Tag `git tag v1-bridge-loop`.

---

## Phase 2 — Site One, end to end (the real Mars pipeline)

Goal: one complete, real, walkable Mars site with points of interest and a field log, proven at framerate on the headset. This is the make-or-break phase.

### Task 2.1: Acquire real Mars terrain data for one bounded site

**Steps:**
1. Pick a bounded, iconic site: **Olympus Mons caldera rim** (start small, a few km across, not the whole volcano).
2. From USGS Astrogeology / NASA PDS, download an elevation product for that area: MOLA global DEM for base shape, and an HRSC or HiRISE DTM (digital terrain model) for the area if one exists at higher detail. Also grab the co-registered orbital image (for the surface texture).
3. Record in `docs/data-sources.md`: exact product name, resolution, URL, and license/attribution (NASA/USGS data is generally public domain but record the required credit line).

**Check:** You have an elevation raster (GeoTIFF/IMG) and an image raster for the site, plus a recorded source. [verify exact products on the portal — do not guess file names.]

### Task 2.2: Convert the DEM into a Unity terrain heightmap

**Steps:**
1. Using **GDAL** or **QGIS** (both free), crop the DEM to your bounded site and resample it to a Unity-friendly size (e.g. 1025×1025 or 2049×2049), then export as a **16-bit RAW** heightmap. Note the real-world width and the min/max elevation so you can set the terrain's real scale.
2. In Unity, create a `Terrain`, `Import Raw` the heightmap, and set terrain width/length/height to the real metric extents (so slopes are true-to-life).
3. Apply the orbital image as the terrain base texture.
4. Commit the *processing recipe* to `docs/data-sources.md` (not the raw multi-GB files unless small; keep large source rasters out of git, keep the derived heightmap if reasonable).

**Check:** In the editor, the terrain matches the real shape and real scale (the caldera rim reads correctly). This is visual, not a unit test.

### Task 2.3: Assemble the Mars Site One World Package

**Steps:**
1. Create a `WorldPackage` asset `mars-olympus` (menu: Wayfinder/World Package). Set `surfaceGravity = 3.72`, `sceneName` = a new scene `Assets/Scenes/Worlds/MarsOlympus.unity` containing the terrain, sky (Mars-tinted), and directional light (sun angle).
2. Add it to the `WorldRegistry` so the viewscreen lists it.
3. Commit `feat: Mars Olympus world package + scene from real MOLA/HiRISE terrain`.

**Check:** From the bridge, selecting "Mars — Olympus Mons" and pulling the lever loads this real scene.

### Task 2.4: Surface locomotion (teleport + world-grab + snap turn + vignette)

**Files:** Use XR Interaction Toolkit's Locomotion (Teleportation Provider, Snap Turn Provider, Tunneling Vignette).

**Steps:**
1. Add teleport areas on the terrain, snap-turn (not smooth turn), and the tunneling comfort vignette.
2. Add a "grab the world and pull" locomotion so a seated player can reposition within their 2 m space.
3. Explicitly confirm there is **no smooth continuous camera rotation** anywhere. [Certain: Google's rules ban camera rotation over time.]
4. Commit `feat: comfort-first surface locomotion (teleport, world-grab, snap turn, vignette)`.

**Check:** On device, you can move around the Mars site comfortably from a seated position; no nausea from rotation.

### Task 2.5: Point-of-interest system (TDD for the data + reveal logic)

**Files:**
- Create: `Assets/Scripts/Discovery/PointOfInterest.cs`, `Assets/Scripts/Discovery/FieldLog.cs`
- Test: `Assets/Tests/EditMode/FieldLogTests.cs`

**Step 1: Failing test** — a POI is logged once, duplicates ignored, count is correct.

```csharp
using NUnit.Framework;

public class FieldLogTests
{
    [Test]
    public void Discover_AddsOnce_IgnoresDuplicates()
    {
        var log = new FieldLog();
        log.Discover("mars-olympus/caldera-rim");
        log.Discover("mars-olympus/caldera-rim");   // duplicate
        Assert.AreEqual(1, log.Count);
        Assert.IsTrue(log.HasDiscovered("mars-olympus/caldera-rim"));
    }

    [Test]
    public void Count_ReflectsDistinctDiscoveries()
    {
        var log = new FieldLog();
        log.Discover("a"); log.Discover("b");
        Assert.AreEqual(2, log.Count);
    }
}
```

**Step 2: Run — FAIL. Step 3: implement** `FieldLog` (a `HashSet<string>` with `Discover`, `HasDiscovered`, `Count`) and a `PointOfInterest` record (id, title, the real fact text, a source string). **Step 4: Run — PASS. Step 5: commit** `feat: point-of-interest model + field log with tests`.

### Task 2.6: POI reveal UI + field-log panel

**Steps:**
1. Place a few POIs in the Mars scene (each a small marker on the terrain). Looking at / pointing at one reveals a floating panel with its real fact and source, and calls `FieldLog.Discover`.
2. A wrist- or console-mounted field-log panel lists what you have discovered.
3. Commit `feat: POI reveal panels + field-log UI on Mars site`.

**Check:** On device, gazing at a POI shows its real fact and it appears in the field log.

### Task 2.7: Airlock return to the bridge

**Steps:**
1. Add an "airlock"/return interactable on the surface that triggers `TravelManager` to unload the world and restore the bridge.
2. Commit `feat: airlock return-to-bridge from surface`.

**Check:** On device, the full loop runs: bridge → warp → Mars → discover → return → bridge.

### Task 2.8: Profile and optimize Site One on the headset

**Steps:**
1. Build and Run. Use the on-device performance overlay / Unity Profiler over USB.
2. Target: **72 fps minimum, 90 fps target**; render scale meeting **≥1856×2160 per eye**. [Certain: store quality bars.]
3. If under budget, in this order: reduce terrain heightmap resolution and pixel error, add terrain LOD/basemap distance, cut draw calls, bake lighting, enable foveated rendering, and consider Application SpaceWarp only if still short.
4. Commit each optimization separately (`perf: ...`).

**Check (CHECKPOINT 2 — THE GATE):** Site One holds 72+ fps on the actual Galaxy XR and is comfortable. Tag `git tag v2-site-one-proven`. **Do not start Phase 3 until this passes.**

---

## Phase 3 — Replicate to Sites Two and Three

Only begin after CHECKPOINT 2. Each site reuses the exact proven Phase 2 setup.

### Task 3.1: Mars Site Two (Valles Marineris)
Repeat Tasks 2.1–2.3 for a bounded Valles Marineris site: acquire data, convert DEM, assemble `mars-valles` World Package. Same `surfaceGravity = 3.72`. Commit per step.
**Check:** loads from the bridge and renders the real canyon shape.

### Task 3.2: Moon Site (Shackleton crater rim)
Repeat for the Moon using **LOLA** elevation (and LRO NAC DTM if available): assemble `moon-shackleton` World Package, `surfaceGravity = 1.62`, Moon sky (black, no atmosphere) and harsh sun angle.
**Check:** loads and renders; lighting reads as airless.

### Task 3.3: Author points of interest (real facts) for all three sites
5–8 POIs per site, each with a real, sourced fact (record the source in the POI). Reuse the Task 2.6 system.
**Check:** each site's field log fills; facts are real and attributed.

### Task 3.4: Validate all three on the headset
Build and Run; confirm each site holds 72+ fps and the warp load times are acceptable. Optimize any that regressed.
**Check (CHECKPOINT 3):** three real worlds, one bridge, full loop, at framerate. Tag `git tag v3-three-worlds`.

---

## Phase 4 — Ship v1

### Task 4.1: Polish pass
Bridge ambience, warp sound, a comfort-options menu (vignette strength, snap-turn angle), and a title/first-run flow. Commit per item.

### Task 4.2: Store-quality pass
Verify against the Android XR quality guidelines: 72/90 fps, cold start under ~2 s, crash-free, playable within 2.0 m, no banned camera motion. Fix any failures. [Certain: these are the enforced bars.]

### Task 4.3: Package and distribute
1. Build a signed **Android App Bundle (.aab)**.
2. For a playtest now: sideload the APK (Galaxy XR installs from the browser, no PC needed).
3. For the store: Google Play developer account ($25 one-time), the dedicated **Android XR** release track, complete the Data Safety form. A new personal account must run a **closed test with ≥12 testers for 14 consecutive days** before production, so line testers up early. [Certain from ideation research.]

**Check (CHECKPOINT 4):** v1 is installable on a Galaxy XR (sideload or store), passes the quality bars, and runs the full three-world loop. Tag `git tag v1-release-candidate`.

---

## What is explicitly NOT in this plan (deferred, do not build)

- Gemini AI companion (v1.1 — the POI records are already the data it will read).
- Gaussian-splat scanned Earth places and the SLAM3R pipeline (v2).
- Real-physics exoplanets (v2).
- Runtime procedural planets (later).
- Multiplayer (later).
- A cleared public product name — required before any public branding, not before building.

---

**Related skills:**
- `executing-plans` — execute this plan task by task.
- `single-flow-task-execution` — enforce sequential execution with review between tasks.
