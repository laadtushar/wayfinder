$input_json = [Console]::In.ReadToEnd() | ConvertFrom-Json
$p = $input_json.tool_input.file_path
if ($p -match '\.(meta|unity)$') {
    Write-Output "BLOCKED: $p is a .meta/.unity file. CLAUDE.md rule: never edit/delete by hand unless explicitly asked. Confirm with the user first."
    exit 2
}
exit 0
