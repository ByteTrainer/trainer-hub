param([string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\Cat Quest')
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$resources = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\resources\cat-quest'))
$managed = Join-Path $GamePath 'Cat Quest_Data\Managed'
$references = @('mscorlib.dll', 'System.dll', 'System.Core.dll', 'Assembly-CSharp.dll', 'Assembly-CSharp-firstpass.dll', 'UnityEngine.CoreModule.dll') |
    ForEach-Object { "/r:$(Join-Path $managed $_)" }
& $compiler /nologo /noconfig /nostdlib /target:library @references "/out:$resources\CatQuestTrainer.Runtime.dll" "$PSScriptRoot\Runtime.cs"
if ($LASTEXITCODE) { throw 'Runtime compilation failed.' }
& $compiler /nologo /target:exe "/r:$resources\Mono.Cecil.dll" "/out:$resources\PatchTool.exe" "$PSScriptRoot\PatchTool.cs"
if ($LASTEXITCODE) { throw 'Patcher compilation failed.' }
