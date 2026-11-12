param([string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\Cooking Live Restaurant Game')
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$resources = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\resources\cooking-live'))
$managed = Join-Path $GamePath 'cl_Data\Managed'
$references = @('mscorlib.dll', 'System.dll', 'System.Core.dll', 'netstandard.dll', 'Assembly-CSharp.dll', 'UnityEngine.CoreModule.dll', 'MG.dll', 'spine-unity.dll', 'UnityEngine.UI.dll') |
    ForEach-Object { "/r:$(Join-Path $managed $_)" }
& $compiler /nologo /noconfig /nostdlib /target:library @references "/out:$resources\CookingLiveTrainer.Runtime.dll" "$PSScriptRoot\Runtime.cs"
if ($LASTEXITCODE) { throw 'Runtime compilation failed.' }
& $compiler /nologo /target:exe "/r:$resources\Mono.Cecil.dll" "/out:$resources\PatchTool.exe" "$PSScriptRoot\PatchTool.cs"
if ($LASTEXITCODE) { throw 'Patcher compilation failed.' }
