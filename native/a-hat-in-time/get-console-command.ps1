param(
    [Parameter(Mandatory)]
    [string]$GameDirectory,

    [Parameter(Mandatory)]
    [ValidateSet('EnableCheats', 'ToggleHealth', 'MovementSpeed', 'GameSpeed')]
    [string]$Action,

    [ValidateRange(0.25, 4.0)]
    [double]$Multiplier = 1.0
)

$ErrorActionPreference = 'Stop'
$null = & (Join-Path $PSScriptRoot 'verify-build.ps1') -GameDirectory $GameDirectory
$value = $Multiplier.ToString('0.###', [Globalization.CultureInfo]::InvariantCulture)

switch ($Action) {
    'EnableCheats' { 'enablecheats' }
    'ToggleHealth' { 'god' }
    'MovementSpeed' { "setspeed $value" }
    'GameSpeed' { "slomo $value" }
}
