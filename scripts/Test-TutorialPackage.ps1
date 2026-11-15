# SPDX-License-Identifier: MIT
# SPDX-FileCopyrightText: 2026 Moritz Voss
param(
    [Parameter(Mandatory)][string] $Project,
    [Parameter(Mandatory)][string] $TwoDogVersion
)
$ErrorActionPreference = 'Stop'
# Build before import: 2dog's editor raises the extracted project's target to net10.0.
# The subsequent --no-build run must exercise the original net8.0 assembly instead.
dotnet build $Project -c Release "-p:TwoDogVersion=$TwoDogVersion" -p:TwoDogAutoImport=false
if ($LASTEXITCODE -ne 0) {
    throw 'Tutorial verification build failed.'
}
& (Join-Path $PSScriptRoot 'Import-GodotProject.ps1') -Project $Project -Property Configuration=Release, "TwoDogVersion=$TwoDogVersion"
dotnet test $Project -c Release --no-build --no-restore "-p:TwoDogVersion=$TwoDogVersion"
if ($LASTEXITCODE -ne 0) {
    throw 'Tutorial archive tests failed.'
}
