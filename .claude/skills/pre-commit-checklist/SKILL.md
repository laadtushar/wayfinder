---
name: pre-commit-checklist
description: Run before committing any C#/scene change — chains EditMode tests, unity-reviewer, and a scan for legacy Input/GetComponent-in-Update. Use before every commit that touches gameplay or engine code.
---

# Pre-commit checklist

Solo-dev gate: no CI, so this is the CI. Run every step; report actual pass/fail, don't assume.

## Steps

1. **Run EditMode tests** — invoke the `run-tests` command (headless .NET runner, or Unity CLI fallback). Report real pass/fail counts.
2. **Run the `unity-reviewer` agent** against the staged diff (`git diff --staged`). It is read-only; surface its findings, ranked most-severe first.
3. **Grep for drift the reviewer might miss on a quick pass:**
   - Legacy `UnityEngine.Input` (`Input.Get`, `Input.mousePosition`, etc.) — should be XR action maps only.
   - `GetComponent`/`Find`/`Camera.main` inside `Update`/`LateUpdate` bodies.
   - `new` allocations, LINQ (`.Where(`, `.Select(`), or string concatenation inside `Update`/`LateUpdate`.
4. If tests fail or the reviewer/grep finds a severe issue, **do not commit** — report the failure and stop. Fixing is a separate step, not silently folded in here.
5. If everything is clean, say so plainly and proceed to the commit the user asked for.

Never report "ready to commit" without having actually run steps 1–3.
