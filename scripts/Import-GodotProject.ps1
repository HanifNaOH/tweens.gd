# SPDX-License-Identifier: MIT
# SPDX-FileCopyrightText: 2026 Moritz Voss
param(
    [Parameter(Mandatory)][string] $Project,
    [string[]] $Property = @()
)
$ErrorActionPreference = 'Stop'
$arguments = @($Property | ForEach-Object { "-p:$_" })
$log = [IO.Path]::GetTempFileName()
try {
    for ($attempt = 1; $attempt -le 3; $attempt++) {
        dotnet msbuild $Project -t:TwoDogImportGodotProject -v:minimal @arguments *> $log
        if ($LASTEXITCODE -eq 0) {
            Get-Content -LiteralPath $log -Tail 8
            break
        }
        $failure = Get-Content -LiteralPath $log -Raw
        # Godot 4.7.2 can crash natively while importing (access violation, heap corruption or SIGSEGV). Retry only
        # that crash, allowing wrapped log lines; never build failures or script errors.
        $crash = '(?s)2dog\.import\.dll.*?exited with code (-1073741819|-1073740940|139)'
        if ($attempt -eq 3 -or $failure -notmatch $crash -or $failure -match '(SCRIPT ERROR|Parse Error)') {
            Get-Content -LiteralPath $log -Tail 40
            throw "Godot project import failed: $Project"
        }
        Write-Warning "Godot crashed during import; retrying ($attempt/3)."
    }
}
finally {
    Remove-Item -LiteralPath $log -ErrorAction SilentlyContinue
}
