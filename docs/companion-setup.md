# The bridge companion — Firebase AI Logic setup (human steps)

Wayfinder's bridge companion is an in-game assistant that describes the world
you're visiting and its points of interest, grounded in the real World Package
data + your field log. The game ships with a fully working **offline stub
companion** (deterministic, no network) so the feature is playable today. This
doc covers wiring the **real Gemini backend** via **Firebase AI Logic**, which
needs console + credential access a headless agent cannot do — so these steps
are **human-required**. Until they're done, the game automatically falls back to
the stub. Nothing here blocks the build.

> **Security rule (non-negotiable):** never embed a raw Gemini API key in the
> client. Firebase AI Logic proxies Gemini and gates it with **App Check**, so
> the app attests itself instead of shipping a key. All wiring below uses App
> Check; there is no code path that takes a raw key. See CLAUDE.md.

## Architecture (what the code already does)

- **`ICompanionProvider`** (engine-free, `com.wayfinder.core`) — the backend
  contract: text in → text out. Two implementations:
  - **`StubCompanionProvider`** (core) — offline, deterministic, templated from
    the world/POI/field-log context. The always-available fallback + test target.
  - **`FirebaseCompanionProvider`** (Unity layer, guarded by the
    `WAYFINDER_FIREBASE_AI` scripting define) — wraps `Firebase.AI`. Compiles to
    nothing until you install the SDK and set the define, so the project builds
    clean without Firebase.
- **`CompanionContextBuilder`** (core) — pure function: World Package + POIs +
  field log → a grounded context + system instruction. This is the testable
  heart and is backend-agnostic.
- **`BridgeCompanion`** (Unity) — resolves the current world/POIs/field log at
  runtime, builds the context, calls whichever provider is active, and falls
  back to the stub on any failure.

Text-first by design. **Gemini Live (real-time voice) for Unity was still
landing as of mid-2026** — treat it as not-yet-turnkey; the provider interface
is voice-ready but we don't wire Live until it's stable.

## Human steps

### 1. Firebase project + AI Logic
1. Create (or reuse) a Firebase project in the [Firebase console](https://console.firebase.google.com/).
2. In **Build → AI Logic**, click **Get started** and enable the
   **Gemini Developer API** backend (has a no-cost tier with reasonable quotas;
   `FirebaseAI.Backend.GoogleAI()` in code). Vertex AI is the alternative if you
   later need it — the code backend is a one-line swap.
3. Register an **Android app** with the game's package name (the Galaxy XR build
   id). Download **`google-services.json`**.

### 2. Import the Firebase Unity SDK
1. Download the **Firebase Unity SDK** (firebase.google.com/download/unity).
2. Import **`FirebaseAI.unitypackage`** and **`FirebaseAppCheck.unitypackage`**
   into `unity/`. (The External Dependency Manager it bundles resolves the
   underlying `com.google.firebase:firebase-ai` / `-appcheck` Android libs at
   build time — Vulkan/Android XR build is unaffected.)
3. Put **`google-services.json`** in `unity/Assets/` (git-ignored — it's
   project config, not a secret key, but keep it out of the public repo).

### 3. Turn on App Check
1. In the console, **Build → App Check**, register the Android app with the
   **Play Integrity** provider.
2. For **on-device / editor testing without Play Integrity**, App Check's
   **debug provider** is already wired in code (`DebugAppCheckProviderFactory`).
   Run once, copy the debug token Unity logs, and register it under
   **App Check → Apps → Manage debug tokens**. Never ship the debug token.

### 4. Flip the define
Add **`WAYFINDER_FIREBASE_AI`** to *Project Settings → Player → Scripting Define
Symbols* (Android tab). This compiles in `FirebaseCompanionProvider`; the game
detects it at boot and uses Gemini, falling back to the stub if init fails.

## Definition of done
- Talking to the bridge companion returns a Gemini-generated, correctly
  *grounded* answer (it only states facts present in the World Package / field
  log — the context builder constrains it).
- No raw API key anywhere in the client; App Check attestation is required for
  every call.
- With the define off (or Firebase init failing), the stub still answers — the
  feature never hard-fails.

## Model note
The default model name is a config constant (`CompanionConfig.ModelName`,
currently a Gemini `*-flash` tier for latency/cost). Bump it in one place as
newer Gemini models land; no other code changes.
