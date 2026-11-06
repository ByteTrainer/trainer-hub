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
$assetName = "ByteTrainer-Setup-$version.exe"
$installer = Get-Item -LiteralPath (Join-Path $BuildDirectory $assetName)
$manifest = Get-Item -LiteralPath (Join-Path $BuildDirectory 'latest.yml')
$hash = [Convert]::ToBase64String([Convert]::FromHexString((Get-FileHash -Algorithm SHA512 -LiteralPath $installer.FullName).Hash))
$yaml = Get-Content -Raw -LiteralPath $manifest.FullName
if (!$yaml.Contains("version: $version") -or !$yaml.Contains("url: $assetName") -or !$yaml.Contains("sha512: $hash")) {
    throw 'O manifesto e o instalador não correspondem.'
}
$target = git rev-parse HEAD
git push origin HEAD:main
$release = (gh api "repos/$repo/releases" | ConvertFrom-Json) | Where-Object tag_name -eq "v$version"
if (!$release) {
    ($notes.changes | ForEach-Object { "- $_" }) | gh release create "v$version" $installer.FullName $manifest.FullName --repo $repo --draft --target $target --title "ByteTrainer $version" --notes-file -
}
$draft = (gh api "repos/$repo/releases" | ConvertFrom-Json) | Where-Object tag_name -eq "v$version"
if (!$draft.draft -or $draft.target_commitish -ne $target) { throw 'O rascunho não corresponde ao commit atual.' }
foreach ($file in @($installer, $manifest)) {
    $entry = $draft.assets | Where-Object name -eq $file.Name
    $checksum = (Get-FileHash -Algorithm SHA256 -LiteralPath $file.FullName).Hash.ToLowerInvariant()
    if (!$entry -or $entry.size -ne $file.Length -or $entry.digest -ne "sha256:$checksum") {
        throw "O arquivo enviado precisa ser verificado no rascunho: $($file.Name)"
    }
}
gh release edit "v$version" --repo $repo --draft=false --latest
