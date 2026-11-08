param([string]$GamePath='C:\Program Files (x86)\Steam\steamapps\common\Out of Space')
$ErrorActionPreference='Stop'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$resources=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\resources\outofspace'))
$managed=Join-Path $GamePath 'Out of Space_Data\Managed'
& $compiler /nologo /noconfig /target:library /nostdlib "/r:$managed\mscorlib.dll" "/r:$managed\System.dll" "/r:$managed\System.Core.dll" "/r:$managed\PhotonUnityNetworking.dll" "/r:$managed\PhotonRealtime.dll" "/r:$managed\Photon3Unity3D.dll" "/r:$managed\Assembly-CSharp.dll" "/r:$managed\UnityEngine.CoreModule.dll" "/out:$resources\OutOfSpaceTrainer.Runtime.dll" "$PSScriptRoot\Runtime.cs"
if ($LASTEXITCODE) { throw 'Runtime compilation failed.' }
& $compiler /nologo /target:exe "/r:$resources\Mono.Cecil.dll" "/out:$resources\PatchTool.exe" "$PSScriptRoot\PatchTool.cs"
if ($LASTEXITCODE) { throw 'Patcher compilation failed.' }

