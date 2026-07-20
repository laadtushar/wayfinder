# Wayfinder v1 design

Codename: Wayfinder (internal only; public name still to be cleared in trademark classes 9 and 41).
Target device: Samsung Galaxy XR (Android XR).
Date: 2026-07-20.
Companion to IDEATION.md (the frontier-tech survey this design draws on).

## What this is

A guided-discovery space-travel experience. You command a ship, warp between real
worlds of the solar system, and walk bounded landing sites on foot while their real
science is revealed to you. Awe first, light optional goals, no way to fail or die.

## Locked decisions

- Travel hub: your ship's bridge. You pick a destination on a viewscreen star map and
  pull a warp lever to travel. The warp doubles as the load-masking moment.
- Surface loop: guided discovery. Walk the site, look at points of interest to learn
  what is real about them, a field log fills as you go. No combat, no fail state.
- First worlds: real solar-system places (Mars, the Moon), built from real public
  elevation and imagery data, so the terrain is genuinely the real place.
- Players: solo single-player for v1.
- Engine: Unity 6 (generally available for Android XR), OpenXR, XR Interaction Toolkit.

## Core loop

Bridge -> pick a destination on the viewscreen -> pull warp lever (warp effect plays
while the next world loads) -> step through the airlock onto the surface -> walk the
bounded site and discover its points of interest -> return to the airlock -> back to
the bridge -> pick the next world.

## Architecture: the World Package

- The bridge is one persistent scene that never unloads.
- Each world is a self-contained package of data: its terrain, its list of points of
  interest, its real physics values (gravity, sky color, light direction), and its
  ambient audio.
- Worlds load and unload on top of the bridge on demand. The warp effect exists to
  cover that load and unload time.
- Payoff: adding another world is just another package. The AI companion (later) reads
  the same package data with no rewrite.
- Unity terms: additive scene loading plus an async streamed asset pack per world.

## Real-world content pipeline (Mars and Moon)

- Source: free public data from NASA and the US Geological Survey. A "digital elevation
  model" is a height map of real terrain. For Mars, global MOLA elevation plus
  high-detail HiRISE where it exists; for the Moon, the LRO elevation data. Real orbital
  imagery is draped over the terrain as the surface texture.
- Import: the height map becomes Unity terrain; the imagery becomes its texture. The
  ground you walk is the genuine shape of that place.
- Sites are bounded and iconic: for example the Olympus Mons caldera rim and a stretch
  of Valles Marineris on Mars, and the Shackleton crater rim on the Moon. You walk a
  region, not a whole planet.
- Note: this path needs no Gaussian splats. Splats (the SLAM3R pipeline) are for scanned
  Earth places, which is a later pillar.

## Guided discovery now, AI companion later

- v1: points of interest are pre-authored. Each is a structured record: location, title,
  the real fact, and a source. Looking at or pointing at one surfaces a short panel
  (optionally spoken by a synthesized voice). A field log fills as you discover them.
- v1.1: the Gemini AI companion reads those same records and turns them into live
  conversation and narration. Nothing authored in v1 is thrown away; the companion is an
  upgrade on top, not a redo.

## Comfort and locomotion (from Google's Android XR quality rules, non-negotiable)

- Bridge: stand and use your hands on controls within reach.
- Surface: move by teleport and by grabbing and pulling the world through your play
  space. Snap turning and a comfort vignette. Never smooth continuous camera rotation.
- Warp: a brief bright transition, not a long forward-acceleration tunnel (forward
  motion is the most nausea-inducing thing in VR).
- Everything stays playable from a seated or standing 2 meter space.
- Targets: 72 fps minimum, 90 fps target; render at least 1856x2160 per eye.

## v1 definition of done

- One bridge, one warp transition, three real sites (two Mars, one Moon).
- Roughly five to eight points of interest per site, plus a field log.
- Solo, no AI companion.
- Build site one first, test it on the actual headset for framerate and comfort, then
  replicate the proven setup to sites two and three.
- Ship as an Android App Bundle to the Android XR track (or sideload for playtests).

## Deferred on purpose

- Gemini AI companion (v1.1).
- Gaussian-splat scanned Earth places and the SLAM3R pipeline (v2).
- Real-physics exoplanets (v2).
- Runtime procedural planets (later).
- Multiplayer (later).

## Open item

- Clear a public product name in trademark classes 9 and 41 before any branding.
  "Wayfinder" and "Strange New Worlds" are both taken and unsafe as public names.
