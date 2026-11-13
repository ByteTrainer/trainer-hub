param(
    [Parameter(Mandatory)]
    [string]$GameDirectory
)

$ErrorActionPreference = 'Stop'
$exe = Join-Path $GameDirectory 'Binaries/Win64/HatinTimeGame.exe'
$expectedHash = '0197F98641C2150C5E90E5E911F4464E38527C241E50817736CA7072C0B99892'

if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) {
    throw "A Hat in Time executable not found: $exe"
}

$hash = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash
if ($hash -ne $expectedHash) {
    throw "Unrecognized A Hat in Time executable. SHA-256: $hash"
}

[pscustomobject]@{
    SteamAppId = 253230
    ReferenceSteamBuildId = 25577316
    Executable = (Get-Item -LiteralPath $exe).FullName
    Sha256 = $hash
}
