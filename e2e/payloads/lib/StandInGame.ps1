# A stand-in for a running game: a full-screen, borderless window titled "Stand-in game", dark blue with its name in the
# middle, that closes after 10 minutes in case a test leaves it behind. Start-StandInGame (E2E.psm1) puts it in front.
# No C# here: compiling it on a fresh VM took the window past the test's wait.
# It has no console, so anything that goes wrong is written beside the test's output.
trap { Add-Content (Join-Path $env:E2E_OUT 'stand-in-error.txt') "$_`n$($_.ScriptStackTrace)" -Encoding UTF8; break }
Add-Type -AssemblyName System.Windows.Forms, System.Drawing

$form = New-Object System.Windows.Forms.Form
$form.Text = 'Stand-in game'
$form.FormBorderStyle = 'None'
$form.WindowState = 'Maximized'
$form.BackColor = [System.Drawing.Color]::FromArgb(16, 32, 80)
$form.ShowInTaskbar = $true
$label = New-Object System.Windows.Forms.Label
$label.Text = 'Stand-in game'
$label.ForeColor = [System.Drawing.Color]::White
$label.Font = New-Object System.Drawing.Font('Segoe UI', 48)
$label.AutoSize = $false
$label.Dock = 'Fill'
$label.TextAlign = 'MiddleCenter'
$form.Controls.Add($label)
$timer = New-Object System.Windows.Forms.Timer
$timer.Interval = 600000
$timer.Add_Tick({ $form.Close() })
$timer.Start()
[System.Windows.Forms.Application]::Run($form)
