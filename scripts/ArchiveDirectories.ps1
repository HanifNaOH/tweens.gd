# SPDX-License-Identifier: MIT
# SPDX-FileCopyrightText: 2026 Moritz Voss
# Godot's zip import creates only directories that have their own entries, so each one precedes its contents.
# See https://github.com/godotengine/godot/issues/124461.

# Returns every directory of the given entry names, with a trailing slash, parents before children.
function Get-ArchiveDirectories([string[]] $Names) {
    $directories = [Collections.Generic.SortedSet[string]]::new([StringComparer]::Ordinal)
    foreach ($name in $Names) {
        for ($end = $name.IndexOf('/'); $end -ge 0; $end = $name.IndexOf('/', $end + 1)) {
            $null = $directories.Add($name.Substring(0, $end + 1))
        }
    }
    , [string[]]@($directories)
}

function Add-ArchiveDirectories([IO.Compression.ZipArchive] $Archive, [string[]] $Directories) {
    foreach ($directory in $Directories) {
        $entry = $Archive.CreateEntry($directory)
        $entry.LastWriteTime = [DateTimeOffset]::new(2000, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
    }
}
