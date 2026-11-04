# A stand-in for a running game: a full-screen, borderless window titled "Stand-in game", dark blue with its name in the
# middle, that takes the foreground as a game started from Playnite does, and closes after 10 minutes in case a test
# leaves it behind.
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class StandIn {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    // Windows gives the foreground only to a process that has the last input; a tap of Alt, sent by this process,
    // is that input, as a launcher a user just pressed A in has it.
    public static void TakeForeground(IntPtr h) {
        keybd_event(0x12, 0, 0, UIntPtr.Zero);
        keybd_event(0x12, 0, 2, UIntPtr.Zero);
        SetForegroundWindow(h);
    }
}
'@
[StandIn]::SetProcessDPIAware() | Out-Null

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
$form.Add_Shown({ [StandIn]::TakeForeground($form.Handle); $form.Activate() })
$timer = New-Object System.Windows.Forms.Timer
$timer.Interval = 600000
$timer.Add_Tick({ $form.Close() })
$timer.Start()
[System.Windows.Forms.Application]::Run($form)
