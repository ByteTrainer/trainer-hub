param([string]$GamePath='C:\Program Files (x86)\Steam\steamapps\common\My Friend Pedro')
$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$managed=Join-Path $GamePath 'My Friend Pedro - Blood Bullets Bananas_Data/Managed'
& $compiler /nologo /target:library /nostdlib "/r:$managed\mscorlib.dll" "/out:$projectRoot\resources\pedro\PedroTrainer.Runtime.dll" "$PSScriptRoot\TrainerRuntime.cs"
if($LASTEXITCODE -ne 0){throw 'Falha ao compilar runtime.'}
& $compiler /nologo /target:exe "/r:$projectRoot\resources\pedro\Mono.Cecil.dll" "/out:$projectRoot\resources\pedro\PatchTool.exe" "$PSScriptRoot\PatchTool.cs"
if($LASTEXITCODE -ne 0){throw 'Falha ao compilar instalador.'}
