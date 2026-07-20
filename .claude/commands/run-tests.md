---
description: Run Wayfinder's EditMode NUnit logic tests headless via the .NET runner and report actual pass/fail.
---

Run the fast EditMode logic tests (travel state machine, world registry, field log, terrain/coordinate math) and report the real result.

Steps:

1. Run the EditMode test assembly headless via the .NET path so it completes in seconds without opening the Editor (see https://gamedev.center/run-unity-tests-faster-dotnet/ for the runner setup if not yet wired).
   - Fallback if the headless runner isn't set up: invoke Unity's CLI test runner:
     `<UnityPath>/Unity -batchmode -runTests -projectPath . -testPlatform EditMode -testResults ./TestResults.xml -quit`
2. Read the results (stdout or `TestResults.xml`) and report the actual pass/fail counts and any failing test's message and `file:line`.
3. If something failed, show the failure and the likely cause; do not "fix" a test by weakening its assertion.

Never report green without having run the tests. These cover only pure logic — scene/rig/rendering correctness is verified by building to the emulator/headset, not here.
