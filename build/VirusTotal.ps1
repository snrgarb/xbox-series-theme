# Scans one file on VirusTotal before it's released, and refuses it if any engine finds it malicious or suspicious.
# Called by release.yml for each file it built, just before it creates the release, with the repository secret
# VIRUSTOTAL_API_KEY in $env:VIRUSTOTAL_API_KEY.
#
#   pwsh build/VirusTotal.ps1 <file>
#
# Looks the file's sha256 up first and uploads it only if VirusTotal doesn't know it; an uploaded file is shared with
# VirusTotal's partners. Waits up to 30 min for the analysis. Writes the report's link to $GITHUB_OUTPUT as `report`,
# for the release notes, and the verdict to the job summary. Exits 1 on a detection, a missing key or a timeout.
# The free API allows 4 requests a minute: it polls every 45 s and waits a minute on HTTP 429.
param([Parameter(Mandatory)] [string] $Path)
$ErrorActionPreference = 'Stop'

$key = $env:VIRUSTOTAL_API_KEY
if (-not $key) { throw 'No VIRUSTOTAL_API_KEY: add it as a repository secret (Settings > Secrets and variables > Actions)' }
$headers = @{ 'x-apikey' = $key }
$api = 'https://www.virustotal.com/api/v3'
$file = Get-Item $Path
$sha256 = (Get-FileHash $file.FullName -Algorithm SHA256).Hash.ToLower()
$report = "https://www.virustotal.com/gui/file/$sha256"

# One API call, retried after a minute on the rate limit. Returns $null on 404.
function Invoke-VT([string] $Uri, [string] $Method = 'Get', [hashtable] $Form) {
    for ($try = 1; ; $try++) {
        try {
            if ($Form) { return Invoke-RestMethod $Uri -Method $Method -Headers $headers -Form $Form }
            return Invoke-RestMethod $Uri -Method $Method -Headers $headers
        } catch {
            $status = [int]$_.Exception.Response.StatusCode
            if ($status -eq 404) { return $null }
            if ($status -eq 429 -and $try -lt 10) { "Rate limited; waiting a minute"; Start-Sleep 60; continue }
            throw
        }
    }
}

"$($file.Name), sha256 $sha256"
$known = Invoke-VT "$api/files/$sha256"
if ($known -and @($known.data.attributes.last_analysis_results.PSObject.Properties).Count) {
    "VirusTotal already knows it; using its last analysis"
    $stats = $known.data.attributes.last_analysis_stats
    $results = $known.data.attributes.last_analysis_results
} else {
    # Files over 32 MB go to an upload URL of their own.
    $uploadTo = if ($file.Length -gt 32MB) { (Invoke-VT "$api/files/upload_url").data } else { "$api/files" }
    $analysis = (Invoke-VT $uploadTo -Method Post -Form @{ file = $file }).data.id
    "Uploaded; analysis $analysis"
    $deadline = (Get-Date).AddMinutes(30)
    do {
        Start-Sleep 45
        $result = (Invoke-VT "$api/analyses/$analysis").data.attributes
        "  $($result.status)"
        if ((Get-Date) -gt $deadline) { throw "VirusTotal didn't finish analysing $($file.Name) within 30 min: $report" }
    } while ($result.status -ne 'completed')
    $stats = $result.stats
    $results = $result.results
}

$flagged = @($results.PSObject.Properties | Where-Object { $_.Value.category -in 'malicious', 'suspicious' } |
    ForEach-Object { "$($_.Name): $($_.Value.category), $($_.Value.result)" })
$verdict = "$($file.Name): $($stats.malicious) malicious, $($stats.suspicious) suspicious, $($stats.undetected) undetected"
$verdict
$flagged | ForEach-Object { "  $_" }
"report=$report" >> $env:GITHUB_OUTPUT
$summary = @('### VirusTotal', '', "$verdict ([report]($report))", '') + @($flagged | ForEach-Object { "- $_" })
$summary -join "`n" >> $env:GITHUB_STEP_SUMMARY
if ($flagged.Count) { throw "VirusTotal flagged $($file.Name) ($($flagged.Count) engines): not releasing it. $report" }
