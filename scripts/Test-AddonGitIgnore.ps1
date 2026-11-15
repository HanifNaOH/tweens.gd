# SPDX-License-Identifier: MIT
# SPDX-FileCopyrightText: 2026 Moritz Voss
param([Parameter(Mandatory)][string] $Archive)
$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot
$archivePath = [IO.Path]::GetFullPath($Archive, $repository)
$project = Join-Path $repository "artifacts/addon-gitignore/$([Guid]::NewGuid())"
New-Item -ItemType Directory -Path $project -Force | Out-Null
[IO.Compression.ZipFile]::ExtractToDirectory($archivePath, $project)
$zip = [IO.Compression.ZipFile]::OpenRead($archivePath)
try {
    $files = @($zip.Entries | Where-Object { !$_.FullName.EndsWith('/') } | ForEach-Object FullName)
}
finally {
    $zip.Dispose()
}
if (!$files.Count) {
    throw 'The addon archive has no files.'
}
# Common project and global ignores must not hide any packaged addon files.
[IO.File]::WriteAllText((Join-Path $project '.gitignore'), @"
[Bb]in/
[Gg]enerated/
*.dll
*.so
*.dylib
*.a
*.wasm
*.uid
*.g.cs
"@ + "`n")
git -C $project init --quiet
if ($LASTEXITCODE -ne 0) {
    throw 'Could not initialize the addon Git ignore fixture.'
}
$visible = [Collections.Generic.HashSet[string]]::new([string[]]@(git -C $project ls-files --others --exclude-standard -- addons/tweens_gd/), [StringComparer]::Ordinal)
if ($LASTEXITCODE -ne 0) {
    throw 'Could not list the addon files visible to Git.'
}
$ignored = @($files | Where-Object { !$visible.Contains($_) })
if ($ignored.Count) {
    throw "Project Git ignore rules hide $($ignored.Count) addon files: $(($ignored | Select-Object -First 5) -join ', ')"
}
# The addon's exceptions must leave the project's own build outputs ignored.
$controls = @('bin/build.dll', 'Generated/build.g.cs')
$ignoredControls = @(git -C $project check-ignore -- $controls)
if ($LASTEXITCODE -ne 0 -or ($ignoredControls -join "`n") -cne ($controls -join "`n")) {
    throw 'Addon Git ignore exceptions affect files outside the addon.'
}
Write-Output "Verified $($files.Count) packaged addon files remain visible to Git."
