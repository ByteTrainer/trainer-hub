param([string]$GamePath='C:\Program Files (x86)\Steam\steamapps\common\Shake it Co!')
$ErrorActionPreference='Stop'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$resources=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\resources\shake'))
$managed=Join-Path $GamePath 'Shake it Co!_Data\Managed'
& $compiler /nologo /noconfig /target:library /nostdlib "/r:$managed\mscorlib.dll" "/r:$managed\System.dll" "/r:$managed\System.Core.dll" "/r:$managed\netstandard.dll" "/r:$managed\ShakeItCo.Core.dll" "/r:$managed\System.Numerics.dll" "/r:$managed\Assembly-CSharp.dll" "/r:$managed\UnityEngine.CoreModule.dll" "/out:$resources\ShakeTrainer.Runtime.dll" "$PSScriptRoot\Runtime.cs"
if ($LASTEXITCODE) { throw 'Runtime compilation failed.' }
& $compiler /nologo /target:exe "/r:$resources\Mono.Cecil.dll" "/out:$resources\PatchTool.exe" "$PSScriptRoot\PatchTool.cs"
if ($LASTEXITCODE) { throw 'Patcher compilation failed.' }

