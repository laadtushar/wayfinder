# Setup roadmap: Windows-primary dev environment

The prioritized, cost-honest plan for the development setup around Wayfinder, produced by a five-dimension ideation pass (iteration loop, local GPU authoring, CI/versioning, learning path, playtest/ship process) plus an adversarial cost-cutting synthesis. Machine facts: Windows PC with RTX 5070 Ti (16 GB VRAM) is primary; Mac is docs/research only; Galaxy XR owned; no motion controllers. Total cash to start: **about $40.**

The iteration-loop model everything hangs on (write once, internalize):

> **Tier 1: XR Interaction Simulator** in the editor, no headset, for logic/UI/POI wiring.
> **Tier 2: Direct Preview via Engine Hub**, headset streams the editor live over USB with real hand tracking, seconds per change.
> **Tier 3: device build over adb**, minutes, the only tier that proves framerate and comfort.
> Direct Preview is rendered by the PC's GPU: it is never performance evidence.

---

## Do now (before/during Phase 0) — ~$40 total

1. **Doc updates macOS → Windows-primary.** Done (this commit).
2. **Open the Google Play personal dev account today, not at ship time.** $25, ~2 evenings of forms. The pipeline in front of production is: identity verification (days) → first closed-test build review (up to ~7 days for new accounts) → 12 testers opted in for 14 consecutive days → production review. That is 4-6 weeks of calendar the code cannot compress; start the clock now.
3. **Buy one 10Gbps-rated USB-C data cable** (~$15). Direct Preview and fast APK installs both ride on it; charge-only cables silently fail. **Where the port is (verified):** the Galaxy XR's USB-C port is hidden under a cap on the RIGHT STRAP, separate from the proprietary battery connector. It is data-capable (adb, file transfer, accessories) but does NOT charge the headset — charging is battery-pack-only, and both can be used at the same time. ([UploadVR](https://www.uploadvr.com/samsung-galaxy-xr-hidden-usb-c-port/), [Samsung support](https://www.samsung.com/us/support/troubleshoot/TSG10007584/))
4. **Unity Hub + editor 6000.3.5f2 or newer** (Windows x64, Android Build Support + SDK/NDK/OpenJDK). The floor covers both Direct Preview (needs 6000.3.5f2+) and built-in crash Diagnostics (needs 6.2+). Vulkan on both the Android and Windows/Standalone tabs.
5. **Engine Hub + Direct Preview as the inner loop.** Free installer from Google; also flip Enter Play Mode Options to skip domain reload (~10 s → ~1-2 s per Play press).
6. **Import the XR Interaction Simulator** from XRI (30 min). Most evening coding needs no headset at all.
7. **Git day-one hygiene** (~1.5 hr): Unity .gitignore; Force Text serialization check; **git LFS for binary asset types only** (10 GiB storage + 10 GiB bandwidth/month free, then metered [verify current billing page]); raw NASA/USGS rasters stay OUT of the repo; UnityYAMLMerge merge driver on both machines. Retrofitting LFS after binaries enter history means a history rewrite: do it before the first Unity commit.
8. **One build/deploy script** (half a day): batchmode `BuildScript.BuildAndroid` + keystore from env vars + `versionCode = git rev-list --count HEAD` + `adb install -r` + launch + filtered logcat. Flags for patch-vs-full and AAB-vs-APK. Add Unity's **Patch and Run** after the first successful device build (script-only changes push in seconds). This one script delivers ~80% of what CI builds would, for free, on a machine faster than any free runner.
9. **Claude Code native on Windows with Git Bash, not WSL.** Unity, Engine Hub, adb, and the Unity MCP bridge are all Windows-side; a WSL split adds filesystem and process friction for nothing.
10. **CoplayDev Unity MCP bridge** (~1 hr): already planned in `.claude/README.md`; run the wizard, Auto-Setup for Claude Code.
11. **Galaxy XR developer mode + wireless adb** as the second channel (USB for installs/Direct Preview; Wi-Fi logcat for untethered comfort tests). [verify the exact settings path on the device; write it into `commands/deploy-headset.md`]
12. **Learning, in order** (free, 2-3 weeks of evenings, parallel to the above): build [android/xr-unity-samples](https://github.com/android/xr-unity-samples) to your own headset in week one (the Drone perf sample first); create a `learn/` folder of read-only sample clones with a tutor CLAUDE.md ("answers must cite file paths in these clones; XRI 3.x only; resolve APIs against Library/PackageCache, never training data"); the [Unity Learn VR pathway](https://learn.unity.com/pathway/vr-development) timeboxed to the first two-thirds (skip the portfolio project); the XRI Examples repo, 3-4 stations only.
13. **Join the three official support channels** (1 hr): Unity Discussions XR category, the android-xr-unity-package GitHub issues (Google's stated support channel), Google Issue Tracker. Reddit/Discords: check once, stay only if alive.
14. **The daily habit from the first playable build:** 10 minutes wearing the headset every build night with an fps HUD; weekly recorded full playthrough watched back at 2x; the [Android XR quality checklist](https://developer.android.com/docs/quality-guidelines/android-xr) at each milestone. XR problems (comfort, scale, judder) are invisible in the editor and obvious in 30 seconds on-device.
15. **Mac demoted deliberately:** docs, research, Play-listing assets, second Claude Code seat. Never install Unity on it; push before switching machines.

## Phase 2 (pre-closed-test window)

- **Built-in Unity Diagnostics before the closed test** (~1 hr; needs 6.2+, already satisfied; the legacy Cloud Diagnostics is deprecated). Smoke-test crash delivery from the headset in week one: the 14-day test with strangers' headsets is exactly when local repro is impossible.
- **Two-tier tester pool** for the 12-tester/14-day gate: ~15-16 friend/family opt-ins via a Google Group (opt-in is a web link tied to a Google account and [likely] does not require owning a headset — the numeric gate) plus 3-5 real Galaxy XR owners recruited from r/AndroidXR, XR Discords, and Samsung forums (the actual playtesting value + honest answers for the production questionnaire). Ship 2+ updates during the 14 days. Do NOT buy testers from marketplaces: their phone-only testers cannot even install an XR-only app.
- **Sideload stays the real feedback loop**; the Play closed track is paperwork, not process.
- **In-game feedback button** (half a day): screenshot + up-to-60s voice memo + state JSON (scene, position, fps, build) to `Application.persistentDataPath`, pulled over adb. No upload backend. [verify Unity Microphone on Android XR with a 10-min spike first]
- **Session viewing:** the headset's built-in screen recorder (Quick settings) + scrcpy mirroring to the PC. Build nothing.
- **Hot Reload by SingularityGroup** free trial only if the free loop (Direct Preview + no-domain-reload + Patch and Run) still hurts; $79.99 one-time only after the trial proves itself on 6000.3 + Android XR.
- **EditMode CI (GameCI, Linux runner)** only if local headless testing has demonstrably failed you. Default: don't.

## v1.1 / v2

- **v1.1:** Firebase Crashlytics arrives with Firebase AI Logic anyway; symbol upload in the build script; turn Unity Diagnostics off (never two crash reporters). Firebase Analytics with a handful of events + the Data Safety mapping. ~1 day.
- **v2 kickoff, in order:** read the Scaniverse license [verify free-tier commercial rights before any content lock]; prove the splat pipe end-to-end ONCE (Scaniverse PLY → Windows → aras-p renderer → editor → device); Postshot license at whatever the real price is [verify: reports contradict, 0-204 EUR/yr]; ~$220 one-time disks (2 TB NVMe + 4 TB backup); Backblaze ($99/yr) the day the first un-revisitable capture exists; `captures/<place>/<date>/` convention, raw captures never in git (Google Drive for phone→PC transfer; Syncthing only if Drive hurts).
- **v2 fallbacks only on concrete failure:** the vggt-low-vram fork (community-measured ~11 GB at 311 frames on 16 GB cards [verify locally]) if Postshot alignment fails; RunPod on-demand (~$0.69/hr RTX 4090 class, expect $10-30 total) for captures too big for 16 GB; SLAM3R last (its 20+ fps number is a 4090 figure; independent reports say ~3 fps untuned, and it buys nothing over Postshot for our use).

## Explicit skips (the do-not-bother list)

No CI Android builds (the single biggest ceremony trap in solo Unity: 30-60 min per run on free runners vs your local script). No Unity Build Automation or Unity Version Control. No Rider purchase (VS 2026 Community is the free debugger of record). No paid courses/bootcamps/books. No analytics before Firebase arrives anyway. No organization Play account ($100-800 + weeks to skip one gate you can pass free). No tester marketplaces. No dual-machine Unity. No disk/backup/cloud-GPU spend before v2. No SLAM3R or TRELLIS on any critical path. No Android XR emulator (a real headset + Direct Preview + simulator covers every tier).

## Verify ledger (claims that must not be treated as fact)

1. Postshot paid-tier pricing (0 vs 204 EUR/yr): at v2 checkout.
2. Scaniverse free-tier commercial rights for shipped content: before v2 content lock.
3. Hot Reload on 6000.3 + Android XR + the $79.99 price: the free trial is the verification.
4. Galaxy XR wireless-debugging settings path: on device at Phase 0.
5. Unity Microphone on Android XR: 10-min spike before the feedback button.
6. Crash-SDK delivery from Galaxy XR: smoke-test in closed-test week one.
7. "12 opt-ins without owning the device satisfies the numeric gate": community-sourced; plan the two-tier pool, expect residual rejection risk.
8. GitHub LFS billing figures: at setup.
9. vggt-low-vram VRAM numbers: re-measure locally at v2.
10. RunPod prices: spot-check when first needed.
11. The 6000.3.5f2 Direct Preview floor: re-check the setup page at install; it is the single load-bearing fact of Phase 0.
12. Play pre-launch report value for XR-only apps: assume zero, confirm in console.
