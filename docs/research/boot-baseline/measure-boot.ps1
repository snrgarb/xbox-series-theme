# The map's baseline phases for the last N boots, no admin needed. Seconds unless marked.
#   POST       FwPOSTTime (Kernel-Boot 30 StartOSImageStart)
#   k->signin  kernel start (Kernel-General 12, RTC whole second, so +-0.5 s) to autologon (Winlogon/Operational 1)
#   signin->PN autologon to DesktopApp's first playnite.log line (the Startup-folder shortcut)
#   PN->shown  that line to the Bundle's "[Start-up timing] Start-up shown"
#   ->frame    Start-up shown to the Start-up window's first frame
#   total      POST + k->signin + signin->PN + PN->shown + ->frame
# Also the in-kernel markers the checklist research used, and how many Playnite processes started in the boot's first 3 min
# (more than 2 means a restart: the daily backup, or a crash).
param([int]$Boots = 5)

$pn = "$env:APPDATA\Playnite"
function Lines($file) {
  Get-Content $file | ForEach-Object {
    if ($_ -match '^(\d\d)-(\d\d) (\d\d:\d\d:\d\d\.\d{3})\|') { [pscustomobject]@{ Day = [int]$matches[1]; Month = [int]$matches[2]; Clock = $matches[3]; Text = $_ } }
  }
}
$pnLog = Lines "$pn\playnite.log"
$exLog = Lines "$pn\extensions.log" | Where-Object Text -match '\[Start-up timing\] (Start-up shown at 0 ms|Start-up window''s first frame)'
function At($l, $year) { [datetime]::ParseExact(('{0}-{1:00}-{2:00} {3}' -f $year, $l.Month, $l.Day, $l.Clock), 'yyyy-MM-dd HH:mm:ss.fff', $null) }
function FirstAfter($lines, $t0, $pattern, [int]$skip = 0) {
  $lines | Where-Object { $_.Text -match $pattern } | ForEach-Object { $t = At $_ $t0.Year; if ($t -ge $t0 -and $t -lt $t0.AddMinutes(5)) { $t } } | Select-Object -Skip $skip -First 1
}

$starts = Get-WinEvent -FilterHashtable @{ LogName = 'System'; ProviderName = 'Microsoft-Windows-Kernel-General'; Id = 12 } -MaxEvents $Boots | Sort-Object TimeCreated
'boot              POST  k->signin signin->PN PN->shown ->frame  total | volume wininit autologon explorer | PN procs | type'
foreach ($s in $starts) {
  $k0 = [datetime]::Parse((([xml]$s.ToXml()).Event.EventData.Data | Where-Object Name -eq 'StartTime').'#text').ToLocalTime()
  $w = @{ StartTime = $s.TimeCreated.AddSeconds(-1); EndTime = $s.TimeCreated.AddSeconds(120) }
  function First($log, $prov, $id) {
    $f = @{ LogName = $log } + $w; if ($prov) { $f.ProviderName = $prov }; if ($id) { $f.Id = $id }
    Get-WinEvent -FilterHashtable $f -ErrorAction SilentlyContinue | Sort-Object TimeCreated | Select-Object -First 1
  }
  $fw = First 'System' 'Microsoft-Windows-Kernel-Boot' 30
  $post = if ($fw) { [double](([xml]$fw.ToXml()).Event.EventData.Data | Where-Object Name -eq 'StartOSImageStart').'#text' / 1000 }
  $bt = First 'System' 'Microsoft-Windows-Kernel-Boot' 27
  $type = if ($bt) { (([xml]$bt.ToXml()).Event.EventData.Data)[0].'#text' }
  $vol = (First 'System' 'Microsoft-Windows-FilterManager' $null).TimeCreated
  $wini = (First 'System' 'Microsoft-Windows-Wininit' $null).TimeCreated
  $auto = (First 'Microsoft-Windows-Winlogon/Operational' $null 1).TimeCreated
  $expl = (First 'Microsoft-Windows-Shell-Core/Operational' $null 62170).TimeCreated

  $desk = FirstAfter $pnLog $k0 'ProgramEntry:App arguments'
  $shown = FirstAfter $exLog $k0 'Start-up shown at 0 ms'
  $frame = FirstAfter $exLog $k0 'first frame'
  $procs = @($pnLog | Where-Object { $_.Text -match 'ProgramEntry:App arguments' } | Where-Object { $t = At $_ $k0.Year; $t -ge $k0 -and $t -lt $k0.AddMinutes(3) }).Count

  function D($a, $b) { if ($a -and $b) { ($b - $a).TotalSeconds } }
  function F($v, $w = 6) { if ($null -ne $v) { "{0,${w}:N2}" -f $v } else { ' ' * ($w - 1) + '-' } }
  $p = @($post, (D $k0 $auto), (D $auto $desk), (D $desk $shown), (D $shown $frame))
  $total = if (($p | Where-Object { $null -eq $_ }).Count -eq 0) { ($p | Measure-Object -Sum).Sum }
  '{0:MM-dd HH:mm:ss}  {1} {2}     {3}    {4}  {5} {6} | {7} {8}  {9}   {10} | {11,8} | {12}' -f $k0,
    (F $p[0] 5), (F $p[1]), (F $p[2]), (F $p[3]), (F $p[4]), (F $total 6),
    (F (D $k0 $vol) 6), (F (D $k0 $wini) 6), (F (D $k0 $auto) 6), (F (D $k0 $expl) 6), $procs, $type
}
