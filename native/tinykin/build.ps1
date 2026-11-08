param([string]$GamePath='C:\Program Files (x86)\Steam\steamapps\common\Tinykin')
$ErrorActionPreference='Stop'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$resources=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\resources\tinykin'))
$managed=Join-Path $GamePath 'Tinykin_Data\Managed'
& $compiler /nologo /noconfig /target:library /nostdlib "/r:$managed\mscorlib.dll" "/r:$managed\System.dll" "/r:$managed\System.Core.dll" "/r:$managed\netstandard.dll" "/r:$managed\Game.dll" "/r:$managed\UnityEngine.CoreModule.dll" "/r:$managed\UnityEngine.PhysicsModule.dll" "/r:$resources\0Harmony.dll" "/out:$resources\TinykinTrainer.Runtime.dll" "$PSScriptRoot\Runtime.cs" "$PSScriptRoot\TimerHooks.cs"
if($LASTEXITCODE){throw 'Runtime compilation failed.'}
& $compiler /nologo /target:exe "/r:$resources\Mono.Cecil.dll" "/out:$resources\PatchTool.exe" "$PSScriptRoot\PatchTool.cs"
if($LASTEXITCODE){throw 'Patcher compilation failed.'}
