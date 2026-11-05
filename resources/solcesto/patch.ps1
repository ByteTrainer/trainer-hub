param([ValidateSet('install','restore')][string]$Command,[Parameter(Mandatory=$true)][string]$GamePath)
$ErrorActionPreference='Stop'
function Get-ArchiveHash([string]$File){$stream=[IO.File]::OpenRead($File);$sha=[Security.Cryptography.SHA256]::Create();try{return [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','')}finally{$stream.Dispose();$sha.Dispose()}}
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.IO.Compression
$GamePath=[IO.Path]::GetFullPath($GamePath)
$package=Join-Path $GamePath 'package.nw'
$backup=Join-Path $GamePath 'package.nw.trainerhub-original'
$manifest=Join-Path $GamePath 'SolCestoTrainer.integration.json'
$expected='97C74CD906CBAAE8CE9FEB731F943ACF533CCCA0264CFE33EAF166242E8728A2'
if(Get-Process -Name SolCesto -ErrorAction SilentlyContinue | Where-Object {-not $_.HasExited}){throw 'Feche o Sol Cesto antes de alterar a integração.'}
if(!(Test-Path -LiteralPath (Join-Path $GamePath 'SolCesto.exe'))){throw 'Pasta de Sol Cesto inválida.'}
$current=(Get-ArchiveHash $package)
if(Test-Path -LiteralPath $manifest){$state=Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json;if($current -ne $state.patchedHash){throw 'O jogo foi atualizado ou alterado. Verifique os arquivos pela Steam antes de reinstalar a integração.'}}
elseif($current -ne $expected){throw 'Versão de Sol Cesto incompatível. Seus arquivos foram preservados.'}
if(Test-Path -LiteralPath $backup){if((Get-ArchiveHash $backup) -ne $expected){throw 'Backup original incompatível.'}}
elseif($Command -eq 'install'){Copy-Item -LiteralPath $package -Destination $backup}
else{throw 'Backup original não encontrado.'}
if($Command -eq 'restore'){Copy-Item -LiteralPath $backup -Destination $package -Force;if(Test-Path -LiteralPath $manifest){Remove-Item -LiteralPath $manifest};exit 0}
$temp=Join-Path $GamePath 'package.nw.trainerhub-tmp'
try{
 Copy-Item -LiteralPath $backup -Destination $temp -Force
 $zip=[IO.Compression.ZipFile]::Open($temp,[IO.Compression.ZipArchiveMode]::Update)
 try{
  $entry=$zip.GetEntry('index.html');$reader=[IO.StreamReader]::new($entry.Open());$html=$reader.ReadToEnd();$reader.Dispose();$entry.Delete()
  $html=$html.Replace('<script src="scripts/main.js" type="module"></script>','<script src="scripts/main.js" type="module"></script><script src="trainerhub-engine.js"></script><script src="trainerhub-bridge.js"></script>')
  if(!$html.Contains('trainerhub-bridge.js')){throw 'Inicialização do jogo incompatível.'}
  $entry=$zip.CreateEntry('index.html');$writer=[IO.StreamWriter]::new($entry.Open(),[Text.UTF8Encoding]::new($false));$writer.Write($html);$writer.Dispose()
  foreach($pair in @(@('engine.js','trainerhub-engine.js'),@('bridge.js','trainerhub-bridge.js'))){[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,(Join-Path $PSScriptRoot $pair[0]),$pair[1]) | Out-Null}
 }finally{$zip.Dispose()}
 $patched=(Get-ArchiveHash $temp)
 Move-Item -LiteralPath $temp -Destination $package -Force
 @{protocol=2;version='1.01.4';originalHash=$expected;patchedHash=$patched}|ConvertTo-Json|Set-Content -LiteralPath $manifest -Encoding UTF8
}finally{if(Test-Path -LiteralPath $temp){Remove-Item -LiteralPath $temp}}
