param(
    [Parameter(Mandatory=$true)][string]$Compiler,
    [Parameter(Mandatory=$true)][string]$MinHook
)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
$out = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../resources/last-hope-bunker'))
$minHookRoot = (Resolve-Path -LiteralPath $MinHook).Path
$csc = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
& $csc /nologo /target:exe "/out:$out\PatchTool.exe" "$PSScriptRoot\PatchTool.cs"
& $Compiler -std=c11 -O2 -Wall -Wextra -Werror -shared -static "-I$minHookRoot/include" -o "$out/BunkerTrainer.Runtime.dll" `
    "$PSScriptRoot/Runtime.c" "$minHookRoot/src/buffer.c" "$minHookRoot/src/hook.c" `
    "$minHookRoot/src/trampoline.c" "$minHookRoot/src/hde/hde64.c" -lbcrypt -Wno-unused-parameter
$legacy = Join-Path $out '../kawaii'
# Existing signed-package readers require these two filenames.
Copy-Item -LiteralPath "$legacy/Mono.Cecil.dll" -Destination $out
$cecilNotice = (Get-Content -LiteralPath "$legacy/Mono.Cecil.LICENSE.txt" -Raw).TrimEnd()
$minHookNotice = (Get-Content -LiteralPath "$minHookRoot/LICENSE.txt" -Raw).TrimEnd()
Set-Content -LiteralPath "$out/Mono.Cecil.LICENSE.txt" -Value "$cecilNotice`nMinHook (statically linked in BunkerTrainer.Runtime.dll)`n$minHookNotice"
