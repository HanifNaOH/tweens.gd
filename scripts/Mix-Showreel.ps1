# SPDX-License-Identifier: MIT
# SPDX-FileCopyrightText: 2026 Moritz Voss
param(
    [switch] $Record,
    [string] $Video,
    [string] $Music = 'temp/MokkaMusic - Along The Way (Short Version).wav',
    [string] $Output = 'temp/showreel-along-the-way.mp4',
    [double] $Length = 55,
    [double] $Fade = 0.8
)
$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot
Push-Location $repository
try {
    if (!(Get-Command ffmpeg -ErrorAction SilentlyContinue)) {
        throw 'ffmpeg is not on PATH.'
    }
    if ($Record) {
        # Movie Maker writes into temp/ under the working directory, which is the repository root here.
        $log = dotnet run --project testbed/testbed.2dog -c Release -- --gallery-page 11 --gallery-record 0 2>&1
        if ($LASTEXITCODE) {
            $log | Write-Output
            throw "Recording failed with exit code $LASTEXITCODE."
        }
        $summary = $log | Select-String 'frames at 60 FPS' | Select-Object -Last 1
        if ($summary.Line -notmatch '^3301 frames') {
            throw "Recording is incomplete ($($summary.Line)); keep the window visible and record again."
        }
        Write-Output $summary.Line
    }
    if (!$Video) {
        $Video = Get-ChildItem temp -Filter 'showreel-*.avi' -File | Sort-Object LastWriteTime | Select-Object -Last 1 |
            ForEach-Object FullName
        if (!$Video) {
            throw 'No temp/showreel-*.avi recording; pass -Video or -Record.'
        }
    }
    if (!(Test-Path -LiteralPath $Music)) {
        throw "Music not found: $Music"
    }
    $start = ($Length - $Fade).ToString([Globalization.CultureInfo]::InvariantCulture)
    $end = $Length.ToString([Globalization.CultureInfo]::InvariantCulture)
    $duration = $Fade.ToString([Globalization.CultureInfo]::InvariantCulture)
    # The music fades out over the last $Fade seconds and the movie ends at exactly $Length seconds.
    ffmpeg -hide_banner -loglevel error -stats -y -i $Video -i $Music -map 0:v -map 1:a -c:v libx264 -crf 16 -preset slow `
        -pix_fmt yuv420p -c:a aac -b:a 256k -af "afade=t=out:st=$($start):d=$duration" -t $end -movflags +faststart $Output
    if ($LASTEXITCODE) {
        throw "ffmpeg failed with exit code $LASTEXITCODE."
    }
    Write-Output "Mixed $([IO.Path]::GetFileName($Video)) into $Output."
}
finally {
    Pop-Location
}
