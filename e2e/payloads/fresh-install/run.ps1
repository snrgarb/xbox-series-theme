# A fresh install of the Bundle: Playnite as a new user has it, the theme and extension installed as Playnite installs
# them, then Fullscreen started. Passes when the extension says it started on the Dashboard, Playnite logs no error, and
# Playnite is still up and in front a while later.
Import-Module (Join-Path $PSScriptRoot 'lib\E2E.psm1') -Force
$result = 'FAIL'
$failures = @()
try {
    Initialize-Playnite $PSScriptRoot
    $playnite = Start-Fullscreen
    $verdict = Wait-PlayniteLog 'Xbox Series extension (started on the Dashboard|stays dormant[^\r\n]*)' 120
    Say "verdict: $(if ($verdict) { $verdict.Trim() } else { 'nothing within 120 s' })"
    Save-Screen 'started'
    if ($verdict -notmatch 'started on the Dashboard') { $failures += 'the extension did not start on the Dashboard' }

    # Still up, and in front, once the Start-up has had time to finish.
    Start-Sleep -Seconds 20
    Save-Screen 'settled'
    $front = Get-Foreground
    Say "after 20 s: running $(-not $playnite.HasExited), foreground $($front.Process) '$($front.Title)'"
    if ($playnite.HasExited) { $failures += 'Playnite exited' }
    elseif ($front.Pid -ne $playnite.Id) { $failures += "Playnite is not in front: $($front.Process)" }

    $errors = @(([regex]'(?m)^.*\|ERROR\|.*$').Matches((Get-PlayniteLog)) | ForEach-Object Value)
    $errors | ForEach-Object { Say "logged: $_" }
    if ($errors.Count) { $failures += "$($errors.Count) errors logged" }

    if ($failures.Count -eq 0) { $result = 'PASS' }
} catch {
    $failures += "error: $_"
} finally {
    $failures | ForEach-Object { Say "failed: $_" }
    Say "result: $result"
    Save-PlayniteFiles
    Stop-Playnite
}
exit $(if ($result -eq 'PASS') { 0 } else { 1 })
