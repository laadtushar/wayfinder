$input_json = [Console]::In.ReadToEnd() | ConvertFrom-Json
$p = $input_json.tool_input.file_path
if ($p -notmatch '\.cs$') { exit 0 }
$c = Get-Content -Raw $p -ErrorAction SilentlyContinue
if (-not $c) { exit 0 }

if ($c -match '(?ms)(void\s+(Update|LateUpdate)\s*\([^)]*\)\s*\{.*?\})') {
    $body = $matches[1]
    if ($body -match 'GetComponent|Camera\.main|\bFind\(') {
        Write-Output "WARNING: $p - possible GetComponent/Find/Camera.main call inside Update/LateUpdate. Cache in Awake instead (CLAUDE.md: no per-frame allocations/lookups in hot paths)."
    }
    if ($body -match '\bnew\s+\w|\.Where\(|\.Select\(|string\s*\+|"\s*\+') {
        Write-Output "WARNING: $p - possible allocation (new/LINQ/string concat) inside Update/LateUpdate. Frame-budget risk."
    }
}

if ($c -match 'UnityEngine\.Input\.|(?<!New)\bInput\.(Get|mousePosition)') {
    Write-Output "WARNING: $p - legacy UnityEngine.Input usage detected. CLAUDE.md mandates the new Input System / XR action maps only."
}
exit 0
