param([string]$GamePath='C:\Program Files (x86)\Steam\steamapps\common\Tinykin')
$ErrorActionPreference='Stop'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$resources=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\resources\tinykin'))
$managed=Join-Path $GamePath 'Tinykin_Data\Managed'
node "$PSScriptRoot\generate-text.cjs"
if($LASTEXITCODE){throw "Tracker localization generation failed."}
& $compiler /nologo /noconfig /target:library /nostdlib "/r:$managed\mscorlib.dll" "/r:$managed\System.dll" "/r:$managed\System.Core.dll" "/r:$managed\netstandard.dll" "/r:$managed\Game.dll" "/r:$managed\DLC-Challenges.dll" "/r:$managed\UnityEngine.CoreModule.dll" "/r:$managed\UnityEngine.PhysicsModule.dll" "/r:$managed\UnityEngine.IMGUIModule.dll" "/r:$managed\UnityEngine.TextRenderingModule.dll" "/r:$managed\I2Localization.dll" "/r:$managed\Wwise.dll" "/r:$resources\0Harmony.dll" "/out:$resources\TinykinTrainer.Runtime.dll" "$PSScriptRoot\Runtime.cs" "$PSScriptRoot\TimerHooks.cs" "$PSScriptRoot\TrackerScanner.cs" "$PSScriptRoot\Tracker.cs" "$PSScriptRoot\..\..\.research\tinykin\TrackerText.cs"
if($LASTEXITCODE){throw 'Runtime compilation failed.'}
& $compiler /nologo /target:exe "/r:$resources\Mono.Cecil.dll" "/out:$resources\PatchTool.exe" "$PSScriptRoot\PatchTool.cs"
if($LASTEXITCODE){throw 'Patcher compilation failed.'}
