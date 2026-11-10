param([string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\Pigeons Attack')
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$resources = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\resources\pigeons'))
$managed = Join-Path $GamePath 'PigeonsAttack_Data\Managed'
& $compiler /nologo /noconfig /target:library /nostdlib "/r:$managed\mscorlib.dll" "/r:$managed\System.dll" "/r:$managed\Assembly-CSharp.dll" "/r:$managed\UnityEngine.CoreModule.dll" "/r:$managed\PhotonUnityNetworking.dll" "/r:$managed\PhotonRealtime.dll" "/r:$managed\Photon3Unity3D.dll" "/out:$resources\PigeonsTrainer.Runtime.dll" "$PSScriptRoot\Runtime.cs"
if ($LASTEXITCODE) { throw 'Runtime compilation failed.' }
& $compiler /nologo /target:exe "/r:$resources\Mono.Cecil.dll" "/out:$resources\PatchTool.exe" "$PSScriptRoot\PatchTool.cs"
if ($LASTEXITCODE) { throw 'Patcher compilation failed.' }
