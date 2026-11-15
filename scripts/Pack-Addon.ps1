# SPDX-License-Identifier: MIT
# SPDX-FileCopyrightText: 2026 Moritz Voss
param(
    [string] $Version,
    [string] $OutputDirectory = 'artifacts/packages',
    [switch] $AllowMissingNative
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ArchiveDirectories.ps1')
$repository = Split-Path -Parent $PSScriptRoot
if (!$Version) {
    $project = [xml](Get-Content -LiteralPath (Join-Path $repository 'csharp/tweens.gd.csproj') -Raw)
    $Version = [string]$project.Project.PropertyGroup.Version
}
if ($Version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') {
    throw 'Use a semantic package version.'
}
& (Join-Path $PSScriptRoot 'Generate-CSharpDefinitions.ps1') -Check
node (Join-Path $PSScriptRoot 'generate-gdscript-catalog.mjs') --check
if ($LASTEXITCODE -ne 0) {
    throw 'GDScript catalog is stale.'
}
& (Join-Path $PSScriptRoot 'Generate-AddonUids.ps1') -Check
$output = [IO.Path]::GetFullPath($OutputDirectory, $repository)
New-Item -ItemType Directory -Path $output -Force | Out-Null
$destination = Join-Path $output "tweens.gd-$Version.zip"
$source = Join-Path $repository 'addons/tweens_gd'
$files = @(Get-ChildItem -LiteralPath $source -File -Recurse | Sort-Object FullName)
if (!$files.Count -or !(Test-Path -LiteralPath (Join-Path $source 'tweens.gd'))) {
    throw 'Addon source is missing.'
}
# The C# sources compile inside projects with any nullable setting, so each file enables its own context.
$unannotated = @($files | Where-Object { $_.Extension -eq '.cs' -and !(Select-String -LiteralPath $_.FullName -Pattern '^#nullable enable' -Quiet) })
if ($unannotated.Count) {
    throw "C# files without #nullable enable: $($unannotated.Name -join ', ')"
}
# Every library the GDExtension manifest names ships in the package; CI assembles them from the native builds.
$manifest = Get-Content -LiteralPath (Join-Path $source 'tweens_gd.gdextension') -Raw
$libraries = @([regex]::Matches($manifest, '(?m)^[\w.]+\s*=\s*"(bin/[^"]+)"') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
if (!$libraries.Count) {
    throw 'The GDExtension manifest lists no libraries.'
}
$missing = @($libraries | Where-Object { !(Test-Path -LiteralPath (Join-Path $source $_)) })
if ($missing.Count -and !$AllowMissingNative) {
    throw "Native libraries are missing: $($missing -join ', ')"
}
$entries = [Collections.Generic.SortedDictionary[string, IO.FileInfo]]::new([StringComparer]::Ordinal)
foreach ($file in $files) {
    $entries.Add([IO.Path]::GetRelativePath($source, $file.FullName).Replace('\', '/'), $file)
}
# Ship the exceptions only in packages, so this checkout's native build outputs stay ignored.
$entries.Add('.gitignore', (Get-Item -LiteralPath (Join-Path $PSScriptRoot 'templates/addon/.gitignore') -Force))
$directories = Get-ArchiveDirectories @($entries.Keys | ForEach-Object {
        "addons/tweens_gd/$_"
    })
$stream = [IO.File]::Open($destination, [IO.FileMode]::Create)
$zip = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create)
try {
    Add-ArchiveDirectories $zip $directories
    foreach ($relative in $entries.Keys) {
        $file = $entries[$relative]
        $native = $relative -in $libraries
        if ($file.Extension -notin '.gd', '.uid', '.md', '.cs', '.gdextension' -and $file.Name -notin 'LICENSE', '.gitignore' -and !$native) {
            throw "Unexpected addon file: $relative"
        }
        $bytes = [IO.File]::ReadAllBytes($file.FullName)
        # Text ships as the repository stores it; .gitattributes keeps checkouts LF on every platform.
        if (!$native -and [Array]::IndexOf($bytes, [byte]13) -ge 0) {
            throw "CR line endings in $relative; check it out again so .gitattributes applies."
        }
        $entry = $zip.CreateEntry("addons/tweens_gd/$relative", [IO.Compression.CompressionLevel]::Optimal)
        $entry.LastWriteTime = [DateTimeOffset]::new(2000, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
        $content = $entry.Open()
        try {
            $content.Write($bytes, 0, $bytes.Length)
        }
        finally {
            $content.Dispose()
        }
    }
}
finally {
    $zip.Dispose()
    $stream.Dispose()
}
$verify = [IO.Compression.ZipFile]::OpenRead($destination)
try {
    $names = @($verify.Entries | ForEach-Object FullName)
    foreach ($required in '.gitignore', 'tweens.gd', 'catalog.gd', 'adapter.gd', 'shader_adapter.gd', 'tweens_gd.gdextension', 'LICENSE', 'THIRD-PARTY-NOTICES.md', 'README.md', 'CATALOG.md', 'csharp/README.md', 'csharp/Godot/TweenRunner.cs', 'csharp/Generated/Position2D.g.cs') {
        if ("addons/tweens_gd/$required" -notin $names) {
            throw "Package lacks $required"
        }
    }
    if ($names.Count -ne $entries.Count + $directories.Count) {
        throw 'Package entry count mismatch.'
    }
    $written = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($name in $names) {
        $parent = $name.Substring(0, $name.TrimEnd('/').LastIndexOf('/') + 1)
        if ($parent -and !$written.Contains($parent)) {
            throw "Package entry precedes its directory: $name"
        }
        $null = $written.Add($name)
    }
}
finally {
    $verify.Dispose()
}
if ($missing.Count) {
    Write-Warning "Packaged without $($missing.Count) native libraries; this archive only runs where they are present."
}
Write-Output "Packaged $($entries.Count) addon files: $destination"
