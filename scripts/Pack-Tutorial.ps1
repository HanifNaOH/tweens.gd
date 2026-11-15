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
$output = [IO.Path]::GetFullPath($OutputDirectory, $repository)
# Reuse the verified addon archive, including CI's complete set of native builds.
$addonPath = Join-Path $output "tweens.gd-$Version.zip"
if (!(Test-Path -LiteralPath $addonPath -PathType Leaf)) {
    throw "Pack the addon first: $addonPath"
}
$addon = [IO.Compression.ZipFile]::OpenRead($addonPath)
try {
    $entries = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
    foreach ($entry in $addon.Entries) {
        # Directory entries; the tutorial archive writes its own.
        if ($entry.FullName -match '^addons/(tweens_gd/([^/]+/)*)?$') {
            continue
        }
        if ($entry.FullName -notmatch '^addons/tweens_gd/[^\\]+$' -or $entry.FullName.Split('/') -contains '..') {
            throw "Unexpected addon archive entry: $($entry.FullName)"
        }
        $entries.Add($entry.FullName, $entry)
    }
    foreach ($required in '.gitignore', 'tweens.gd', 'catalog.gd', 'tweens_gd.gdextension', 'LICENSE', 'csharp/Godot/TweenRunner.cs', 'csharp/Generated/Position2D.g.cs') {
        if (!$entries.ContainsKey("addons/tweens_gd/$required")) {
            throw "Addon archive lacks $required"
        }
    }
    $reader = [IO.StreamReader]::new($entries['addons/tweens_gd/tweens_gd.gdextension'].Open())
    try {
        $manifest = $reader.ReadToEnd()
    }
    finally {
        $reader.Dispose()
    }
    $libraries = @([regex]::Matches($manifest, '(?m)^[\w.]+\s*=\s*"(bin/[^"]+)"') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
    if (!$libraries.Count) {
        throw 'The GDExtension manifest lists no libraries.'
    }
    $missing = @($libraries | Where-Object { !$entries.ContainsKey("addons/tweens_gd/$_") })
    if ($missing.Count -and !$AllowMissingNative) {
        throw "Native libraries are missing: $($missing -join ', ')"
    }

    $tutorial = Join-Path $repository 'tutorial'
    foreach ($directory in 'Art', 'Fonts', 'Lessons', 'Pages', 'Shell', 'Themes', 'addons/tutorial_editor') {
        foreach ($file in Get-ChildItem -LiteralPath (Join-Path $tutorial $directory) -File -Recurse) {
            # Ignore OS metadata and editor backups while keeping unexpected project content an error.
            if ($file.Name -in 'Thumbs.db', 'desktop.ini', '.DS_Store' -or $file.Name.StartsWith('._') -or $file.Name.EndsWith('~')) {
                continue
            }
            if ($file.Extension -notin '.cs', '.gd', '.uid', '.tscn', '.tres', '.png', '.svg', '.ttf', '.woff2', '.txt', '.import', '.cfg') {
                throw "Unexpected tutorial file: $($file.FullName)"
            }
            $relative = [IO.Path]::GetRelativePath($tutorial, $file.FullName).Replace('\', '/')
            $entries.Add($relative, $file)
        }
    }
    foreach ($name in 'project.godot', 'export_presets.cfg', 'main.tscn', 'icon.svg', 'icon.svg.import') {
        $entries.Add($name, (Get-Item -LiteralPath (Join-Path $tutorial $name)))
    }
    $entries.Add('LICENSE', (Get-Item -LiteralPath (Join-Path $repository 'LICENSE')))
    $entries.Add('README.md', (Get-Item -LiteralPath (Join-Path $PSScriptRoot 'templates/tutorial/README.md')))
    $sourceProject = [xml](Get-Content -LiteralPath (Join-Path $tutorial 'tutorial.csproj') -Raw)
    # Stock Godot builds bundled addon sources directly; no repository reference or 2dog package is needed.
    $entries.Add('tutorial.csproj', @"
<Project Sdk="$($sourceProject.Project.Sdk)">
    <PropertyGroup>
        <TargetFramework>net8.0</TargetFramework>
        <Nullable>enable</Nullable>
        <EnableDynamicLoading>true</EnableDynamicLoading>
        <IsTrimmable>true</IsTrimmable>
        <VerifyReferenceTrimCompatibility>true</VerifyReferenceTrimCompatibility>
        <Version>$Version</Version>
    </PropertyGroup>
    <ItemGroup>
        <EmbeddedResource Include="Lessons/**/*.cs;Lessons/**/*.gd" LogicalName="Lessons/%(RecursiveDir)%(Filename)%(Extension)" />
        <Content Include="Fonts/*.txt" CopyToOutputDirectory="PreserveNewest" CopyToPublishDirectory="PreserveNewest" />
    </ItemGroup>
</Project>
"@)
    $destination = Join-Path $output 'tweens.gd-tutorial.zip'
    $stream = [IO.File]::Open($destination, [IO.FileMode]::Create)
    $zip = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create)
    try {
        Add-ArchiveDirectories $zip (Get-ArchiveDirectories @($entries.Keys))
        foreach ($name in $entries.Keys | Sort-Object) {
            $entry = $zip.CreateEntry($name, [IO.Compression.CompressionLevel]::Optimal)
            $entry.LastWriteTime = [DateTimeOffset]::new(2000, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
            $content = $entry.Open()
            try {
                $source = $entries[$name]
                if ($source -is [string]) {
                    $bytes = [Text.Encoding]::UTF8.GetBytes($source.Replace("`r`n", "`n") + "`n")
                    $content.Write($bytes, 0, $bytes.Length)
                }
                else {
                    $inputStream = if ($source -is [IO.Compression.ZipArchiveEntry]) {
                        $source.Open()
                    }
                    else {
                        $source.OpenRead()
                    }
                    try {
                        $inputStream.CopyTo($content)
                    }
                    finally {
                        $inputStream.Dispose()
                    }
                }
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
}
finally {
    $addon.Dispose()
}
if ($missing.Count) {
    Write-Warning "Packaged without $($missing.Count) native libraries; use this archive only for local testing."
}
Write-Output "Packaged $($entries.Count) tutorial project files: $destination"
