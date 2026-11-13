param(
    [Parameter(Mandatory=$true)][string]$Compiler,
    [Parameter(Mandatory=$true)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$output = (Resolve-Path -LiteralPath $OutputDirectory).Path
$csc = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
& $csc /nologo /target:exe "/out:$output/PatchTool.exe" "${PSScriptRoot}\PatchTool.cs"
& $Compiler -std=c11 -Wall -Wextra -Werror -shared -static -o "$output/HatTrainer.Runtime.dll" "${PSScriptRoot}\Runtime.c" -lgdi32 -luser32
