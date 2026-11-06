param([string]$GamePath='C:\Program Files (x86)\Steam\steamapps\common\Moventure')
$ErrorActionPreference='Stop'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$resources=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\resources\moventure'))
$managed=Join-Path $GamePath 'Moventure_Data\Managed'
& $compiler /nologo /noconfig /target:library /nostdlib "/r:$managed\mscorlib.dll" "/r:$managed\System.dll" "/out:$resources\MoventureTrainer.Runtime.dll" "$PSScriptRoot\Runtime.cs"
if ($LASTEXITCODE) { throw 'Runtime compilation failed.' }
& $compiler /nologo /target:exe "/r:$resources\Mono.Cecil.dll" "/out:$resources\PatchTool.exe" "$PSScriptRoot\PatchTool.cs"
if ($LASTEXITCODE) { throw 'Patcher compilation failed.' }
