param([string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\ToothAndTail')
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$resources = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\resources\tooth-and-tail'))
Push-Location $PSScriptRoot
try {
    & $compiler /nologo /target:library "/r:$GamePath\ToothAndTail.exe" "/r:$GamePath\MonoGame.Framework.dll" "/out:$resources\ToothAndTailTrainer.Runtime.dll" Runtime.cs
    if ($LASTEXITCODE) { throw 'Runtime compilation failed.' }
    & $compiler /nologo /target:exe "/r:$resources\Mono.Cecil.dll" "/out:$resources\PatchTool.exe" PatchTool.cs
    if ($LASTEXITCODE) { throw 'Patcher compilation failed.' }
} finally {
    Pop-Location
}
