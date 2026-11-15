param(
    [Parameter(Mandatory=$true)][string]$Compiler,
    [Parameter(Mandatory=$true)][string]$MinHook
)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
$out = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../resources/state-of-decay-yose'))
$minHookRoot = (Resolve-Path -LiteralPath $MinHook).Path
$csc = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
& $csc /nologo /target:exe "/out:$out/PatchTool.exe" "${PSScriptRoot}\PatchTool.cs"
$sources = @(
    "$PSScriptRoot/Runtime.c"
    "$minHookRoot/src/buffer.c"
    "$minHookRoot/src/hook.c"
    "$minHookRoot/src/trampoline.c"
    "$minHookRoot/src/hde/hde32.c"
)
& $Compiler -std=c11 -O2 -Wall -Wextra -Werror -Wno-unused-parameter -shared -static "-I$minHookRoot/include" -o "$out/YoseTrainer.Runtime.dll" @sources -lbcrypt
$legacy = Join-Path $out '../kawaii'
Copy-Item -LiteralPath "$legacy/Mono.Cecil.dll" -Destination $out
$cecilNotice = (Get-Content -LiteralPath "$legacy/Mono.Cecil.LICENSE.txt" -Raw).TrimEnd()
$minHookNotice = (Get-Content -LiteralPath "$minHookRoot/LICENSE.txt" -Raw).TrimEnd()
Set-Content -LiteralPath "$out/Mono.Cecil.LICENSE.txt" -Value $cecilNotice,'','MinHook (statically linked in YoseTrainer.Runtime.dll)',$minHookNotice
