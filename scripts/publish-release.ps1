param([Parameter(Mandatory=$true)][string]$BuildDirectory)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
Set-Location -LiteralPath (Split-Path -Parent $PSScriptRoot)
$repo = 'ByteTrainer/trainer-hub'
$version = (Get-Content -Raw package.json | ConvertFrom-Json).version
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Versão inválida.' }
$notes = (Get-Content -Raw assets/changelog.json | ConvertFrom-Json) | Where-Object version -eq $version
if (@($notes).Count -ne 1 -or !$notes.changes) { throw 'O changelog precisa conter esta versão.' }
if (git status --porcelain) { throw 'Revise e faça commit dos arquivos antes da publicação.' }
if ((git remote get-url origin) -notmatch '^(https://github\.com/|git@github\.com:)ByteTrainer/trainer-hub(?:\.git)?$') {
    throw 'Repositório de origem inesperado.'
}
$installer = Get-Item -LiteralPath (Join-Path $BuildDirectory "Trainer Hub Setup $version.exe")
$manifest = Get-Item -LiteralPath (Join-Path $BuildDirectory 'latest.yml')
$assetName = "Trainer-Hub-Setup-$version.exe"
$hash = [Convert]::ToBase64String([Convert]::FromHexString((Get-FileHash -Algorithm SHA512 -LiteralPath $installer.FullName).Hash))
$yaml = Get-Content -Raw -LiteralPath $manifest.FullName
if (!$yaml.Contains("version: $version") -or !$yaml.Contains("url: $assetName") -or !$yaml.Contains("sha512: $hash")) {
    throw 'O manifesto e o instalador não correspondem.'
}
$temp = Join-Path $installer.DirectoryName $assetName
if (!(Test-Path -LiteralPath $temp)) { New-Item -ItemType HardLink -Path $temp -Target $installer.FullName | Out-Null }
if ((Get-FileHash -Algorithm SHA512 -LiteralPath $temp).Hash -ne (Get-FileHash -Algorithm SHA512 -LiteralPath $installer.FullName).Hash) {
    throw 'O instalador preparado para envio difere do original.'
}
$target = git rev-parse HEAD
git push origin HEAD:main
$release = (gh api "repos/$repo/releases" | ConvertFrom-Json) | Where-Object tag_name -eq "v$version"
if (!$release) {
    ($notes.changes | ForEach-Object { "- $_" }) | gh release create "v$version" $temp $manifest.FullName --repo $repo --draft --target $target --title "Trainer Hub $version" --notes-file -
}
$draft = (gh api "repos/$repo/releases" | ConvertFrom-Json) | Where-Object tag_name -eq "v$version"
if (!$draft.draft -or $draft.target_commitish -ne $target) { throw 'O rascunho não corresponde ao commit atual.' }
foreach ($file in @((Get-Item -LiteralPath $temp), $manifest)) {
    $entry = $draft.assets | Where-Object name -eq $file.Name
    $checksum = (Get-FileHash -Algorithm SHA256 -LiteralPath $file.FullName).Hash.ToLowerInvariant()
    if (!$entry -or $entry.size -ne $file.Length -or $entry.digest -ne "sha256:$checksum") {
        throw "O arquivo enviado precisa ser verificado no rascunho: $($file.Name)"
    }
}
gh release edit "v$version" --repo $repo --draft=false --latest
