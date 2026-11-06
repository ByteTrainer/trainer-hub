param(
    [Parameter(Mandatory=$true)][string]$SiteDirectory,
    [Parameter(Mandatory=$true)][string[]]$Package
)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
$site = (Resolve-Path -LiteralPath $SiteDirectory).Path
$files = @($Package | ForEach-Object { (Resolve-Path -LiteralPath $_).Path })
if ((git -C $site remote get-url origin) -notmatch '^(https://github\.com/|git@github\.com:)ByteTrainer/trainer-integrations(?:\.git)?$') {
    throw 'Repositório do site inesperado.'
}
if (git -C $site status --porcelain) { throw 'O repositório do site deve estar limpo antes da publicação.' }
node (Join-Path $PSScriptRoot 'prepare-integration-site.mjs') $site @files
$paths = @('index.html', 'catalogo.html', 'jogos', 'catalog-v2.json') + @($files | ForEach-Object { 'packages/' + (Split-Path -Leaf $_) })
git -C $site add -- @paths
git -C $site diff --cached --check
if (git -C $site diff --cached --name-only) {
    git -C $site commit -m 'Publica integrações assinadas e atualiza o catálogo do site'
}
git -C $site push origin HEAD:main
