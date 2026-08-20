$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot

function Read-RepositoryFile([string]$path) {
    $fullPath = Join-Path $root $path
    if (-not (Test-Path -LiteralPath $fullPath)) {
        throw "Missing required file: $path"
    }

    return Get-Content -LiteralPath $fullPath -Raw -Encoding utf8
}

function Assert-Matches([string]$content, [string]$pattern, [string]$message) {
    if ($content -notmatch $pattern) {
        throw $message
    }
}

function Assert-NotMatches([string]$content, [string]$pattern, [string]$message) {
    if ($content -match $pattern) {
        throw $message
    }
}

function Assert-LocalizationKeys([string]$path, [string[]]$keys) {
    $json = Read-RepositoryFile $path | ConvertFrom-Json
    $availableKeys = $json.PSObject.Properties.Name
    foreach ($key in $keys) {
        if ($availableKeys -notcontains $key) {
            throw "$path is missing localization key: $key"
        }
    }
}

$enchantmentSource = Read-RepositoryFile "Scripts/Enchantments/StageDeviceEnchantments.cs"
$firstTierSource = Read-RepositoryFile "Scripts/Relics/Giraffe/FirstTierStageDevices.cs"
$laterTierSource = Read-RepositoryFile "Scripts/Relics/Giraffe/SecondAndThirdTierStageDevices.cs"

Assert-Matches $enchantmentSource "class\s+StageDeviceEnchantment\s*:\s*CustomEnchantmentModel" `
    "Stage-device enchantments must use the game's serializable enchantment model."
Assert-Matches $enchantmentSource "CanEnchant\s*\(CardModel\s+card\)\s*=>\s*true" `
    "Stage-device enchantments must explicitly accept every card type."
Assert-Matches $enchantmentSource "ClearEnchantment\(card\)[\s\S]*?Enchant<T>\(card" `
    "Applying a stage-device enchantment must replace an existing enchantment."

Assert-Matches $enchantmentSource "class\s+MassacreStageDeviceEnchantment[\s\S]*?AddKeyword\(CustomKeyWord\.Inspiration\)[\s\S]*?AddKeyword\(CustomKeyWord\.Epiphany\)" `
    "Massacre's enchantment must grant Inspiration and Epiphany."
Assert-Matches $enchantmentSource "class\s+ReproductionStageDeviceEnchantment[\s\S]*?AddKeyword\(CustomKeyWord\.Epiphany\)" `
    "Reproduction's enchantment must grant Epiphany."
Assert-Matches $enchantmentSource "class\s+CompetitionStageDeviceEnchantment[\s\S]*?AddKeyword\(CustomKeyWord\.Epiphany\)[\s\S]*?EnergyCost\.UpgradeBy\(1\)[\s\S]*?EnchantPlayCount\(int\s+playCount\)\s*=>\s*playCount\s*\+\s*1" `
    "Competition's enchantment must grant Epiphany, +1 cost, and +1 replay."

Assert-Matches $firstTierSource "class\s+MassacreStageDevice[\s\S]*?ApplyReplacingExisting<MassacreStageDeviceEnchantment>\(card\)" `
    "Massacre must apply its enchantment to cards present when the relic is obtained."
Assert-Matches $laterTierSource "class\s+ReproductionStageDevice[\s\S]*?ApplyReplacingExisting<ReproductionStageDeviceEnchantment>\(newCard\)" `
    "Reproduction must apply its enchantment to cards obtained after the relic."
Assert-Matches $laterTierSource "class\s+CompetitionStageDevice[\s\S]*?ApplyReplacingExisting<CompetitionStageDeviceEnchantment>\(card\)" `
    "Competition must apply its enchantment to each selected card."

$competitionBody = [regex]::Match(
    $laterTierSource,
    "class\s+CompetitionStageDevice[\s\S]*?(?=///\s*<summary>|$)").Value
Assert-NotMatches $competitionBody "BaseReplayCount\+\+" `
    "Competition replay must be supplied by its enchantment, not a transient card mutation."

$localizationKeys = @(
    "STS2_TOMORIN_MOD-MASSACRE_STAGE_DEVICE_ENCHANTMENT.title",
    "STS2_TOMORIN_MOD-MASSACRE_STAGE_DEVICE_ENCHANTMENT.description",
    "STS2_TOMORIN_MOD-REPRODUCTION_STAGE_DEVICE_ENCHANTMENT.title",
    "STS2_TOMORIN_MOD-REPRODUCTION_STAGE_DEVICE_ENCHANTMENT.description",
    "STS2_TOMORIN_MOD-COMPETITION_STAGE_DEVICE_ENCHANTMENT.title",
    "STS2_TOMORIN_MOD-COMPETITION_STAGE_DEVICE_ENCHANTMENT.description")

Assert-LocalizationKeys "STS2_Tomorin_Mod/localization/eng/enchantments.json" $localizationKeys
Assert-LocalizationKeys "STS2_Tomorin_Mod/localization/zhs/enchantments.json" $localizationKeys

Write-Host "Stage-device enchantment persistence checks passed."
