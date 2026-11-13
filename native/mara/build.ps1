param([string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\Summer in Mara')
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$resources = Join-Path $PSScriptRoot '..\..\resources\mara'
$managed = Join-Path $GamePath 'Summer in Mara_Data\Managed'
$references = @('mscorlib.dll', 'System.dll', 'System.Core.dll', 'LeanTween.dll', 'Assembly-CSharp.dll', 'UnityEngine.UI.dll', 'UnityEngine.IMGUIModule.dll', 'UnityEngine.TextRenderingModule.dll', 'UnityEngine.dll', 'UnityEngine.CoreModule.dll', 'Sirenix.Serialization.dll', 'netstandard.dll') |
    ForEach-Object { "/r:$(Join-Path $managed $_)" }
& $compiler /nologo /noconfig /nostdlib /target:library @references "/out:$resources\MaraTrainer.Runtime.dll" "$PSScriptRoot\Runtime.cs"
if ($LASTEXITCODE) { throw 'Runtime compilation failed.' }
& $compiler /nologo /target:exe "/r:$resources\Mono.Cecil.dll" "/out:$resources\PatchTool.exe" "$PSScriptRoot\PatchTool.cs"
if ($LASTEXITCODE) { throw 'Patcher compilation failed.' }
