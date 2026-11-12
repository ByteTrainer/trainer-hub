param([string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\Tunche')
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$resources = Join-Path $PSScriptRoot '..\..\resources\tunche'
$managed = Join-Path $GamePath 'Tunche_Data\Managed'
$references = @('mscorlib.dll', 'System.dll', 'Assembly-CSharp.dll', 'UnityEngine.CoreModule.dll') |
    ForEach-Object { "/r:$(Join-Path $managed $_)" }
& $compiler /nologo /noconfig /nostdlib /target:library @references "/out:$resources\TuncheTrainer.Runtime.dll" "$PSScriptRoot\Runtime.cs"
if ($LASTEXITCODE) { throw 'Runtime compilation failed.' }
& $compiler /nologo /target:exe "/r:$resources\Mono.Cecil.dll" "/out:$resources\PatchTool.exe" "$PSScriptRoot\PatchTool.cs"
if ($LASTEXITCODE) { throw 'Patcher compilation failed.' }
