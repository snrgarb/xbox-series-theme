# What the test VM's desktop looks like to a test: run on the signed-in user's desktop by `vm.sh desktop`, writing
# probe.txt and screen.png to $env:E2E_OUT. Checks the session is a real, unlocked desktop at the size the tests expect.
$ErrorActionPreference = 'Stop'
$out = $env:E2E_OUT

Add-Type -AssemblyName System.Windows.Forms, System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class Desk {
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern IntPtr OpenInputDesktop(uint flags, bool inherit, uint access);
    [DllImport("user32.dll")] public static extern bool CloseDesktop(IntPtr h);
    [DllImport("user32.dll")] public static extern uint GetDpiForSystem();
    public static string Foreground() {
        var s = new StringBuilder(256); GetWindowText(GetForegroundWindow(), s, 256); return s.ToString();
    }
    // The input desktop opens only from an unlocked, active session.
    public static bool InputDesktopOpen() {
        var h = OpenInputDesktop(0, false, 0x0100); if (h == IntPtr.Zero) return false; CloseDesktop(h); return true;
    }
}
'@
[Desk]::SetProcessDPIAware() | Out-Null

$bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$lines = @(
    "user:          $env:USERDOMAIN\$env:USERNAME"
    "session:       $((Get-Process -Id $PID).SessionId)"
    "elevated:      $(([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole('Administrators'))"
    "input desktop: $([Desk]::InputDesktopOpen())"
    "screen:        $($bounds.Width)x$($bounds.Height)"
    "dpi:           $([Desk]::GetDpiForSystem()) (96 = 100%)"
    "screens:       $([System.Windows.Forms.Screen]::AllScreens.Count)"
    "foreground:    $([Desk]::Foreground())"
    "playnite data: $(Test-Path "$env:APPDATA\Playnite")"
    "vigem:         $((Get-PnpDevice -FriendlyName '*Virtual Gamepad Emulation*' -ErrorAction SilentlyContinue).Status)"
)
$lines | Set-Content "$out\probe.txt"
$lines | Write-Output

$bmp = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)
$bmp.Save("$out\screen.png", [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
exit 0
