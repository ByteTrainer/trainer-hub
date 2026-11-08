param([string]$GamePath='C:\Program Files (x86)\Steam\steamapps\common\My Little Universe')
$ErrorActionPreference='Stop'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$resources=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\resources\universe'))
$managed=Join-Path $GamePath 'My Little Universe_Data\Managed'
& $compiler /nologo /noconfig /target:library /nostdlib "/r:$managed\mscorlib.dll" "/r:$managed\System.dll" "/r:$managed\System.Core.dll" "/r:$managed\netstandard.dll" "/r:$managed\Assembly-CSharp.dll" "/r:$managed\UnityEngine.CoreModule.dll" "/r:$managed\UnityEngine.IMGUIModule.dll" "/r:$managed\UnityEngine.TextRenderingModule.dll" "/out:$resources\UniverseTrainer.Runtime.dll" "$PSScriptRoot\Runtime.cs" "$PSScriptRoot\Tracker.cs"
if ($LASTEXITCODE) { throw 'Runtime compilation failed.' }
& $compiler /nologo /target:exe "/r:$resources\Mono.Cecil.dll" "/out:$resources\PatchTool.exe" "$PSScriptRoot\PatchTool.cs"
if ($LASTEXITCODE) { throw 'Patcher compilation failed.' }
