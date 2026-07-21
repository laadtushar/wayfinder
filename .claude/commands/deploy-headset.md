---
description: Build the Unity project to Android XR and deploy it to the connected Galaxy XR over adb, then tail the filtered device log.
---

Build and deploy Wayfinder to the connected Galaxy XR headset, then show the relevant device log. Galaxy XR is Android under the hood, so `adb` is the tool (not any Meta/Quest utility).

Steps:

1. Confirm a device is connected: `adb devices` (expect the Galaxy XR listed as `device`, not `unauthorized`). If unauthorized, tell me to accept the USB-debugging prompt in the headset. If no device: the USB-C port is hidden under a cap on the RIGHT STRAP (it is data-only — the battery pack charges the headset, not this port), and the cable must be a data cable, not charge-only.
2. Build the APK from Unity for the Android XR target (via the Unity MCP bridge if connected, or tell me the exact Build Settings to use: Android platform, Vulkan, IL2CPP, ARM64). Prefer a **development build** so the profiler can attach.
3. Install: `adb install -r <path-to.apk>`.
4. Launch and tail a filtered log so we see our own output and crashes without the Android firehose:
   `adb logcat -c && adb logcat Unity:V DEBUG:E AndroidRuntime:E "*:S"`
5. Report what happened: did it install, launch, and render, or did it crash. If it crashed, surface the stack from logcat.

Do not claim it works on the headset unless the log shows it launched. The emulator is functional testing only; framerate/comfort must be judged on the real device.
