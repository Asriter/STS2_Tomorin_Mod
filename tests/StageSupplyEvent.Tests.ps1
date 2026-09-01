$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot

function Get-RepositoryContent([string]$path) {
    return Get-Content -LiteralPath (Join-Path $root $path) -Raw -Encoding utf8
}

function Assert-FileExists([string]$path) {
    if (-not (Test-Path -LiteralPath (Join-Path $root $path))) {
        throw "Missing Stage supply event file: $path"
    }
}

function Assert-Contains([string]$content, [string]$pattern, [string]$message) {
    if ($content -notmatch $pattern) {
        throw $message
    }
}

function Assert-NotContains([string]$content, [string]$pattern, [string]$message) {
    if ($content -match $pattern) {
        throw $message
    }
}

function Assert-LocalizationKeys([string]$path, [string[]]$keys) {
    $json = Get-RepositoryContent $path | ConvertFrom-Json
    foreach ($key in $keys) {
        if ($json.PSObject.Properties.Name -notcontains $key) {
            throw "$path is missing localization key: $key"
        }

        if ([string]::IsNullOrWhiteSpace([string]$json.$key)) {
            throw "$path has an empty localization value: $key"
        }
    }
}

$eventPath = "Scripts/Events/StageSupplyEvent.cs"
$resolverPath = "Scripts/Stage/StageRoomResolver.cs"
$localizationPaths = @(
    "STS2_Tomorin_Mod/localization/eng/events.json",
    "STS2_Tomorin_Mod/localization/zhs/events.json")

Assert-FileExists $eventPath
$event = Get-RepositoryContent $eventPath
$resolver = Get-RepositoryContent $resolverPath

Assert-Contains $event "sealed\s+class\s+StageSupplyEvent\s*:\s*CustomEventModel" `
    "StageSupplyEvent must be a dedicated CustomEventModel."
Assert-Contains $event "override\s+bool\s+IsShared\s*=>\s*false" `
    "Each player must receive an independent StageSupplyEvent instance."
Assert-Contains $event "PlayerCmd\.GainGold\(100m,\s*Owner,\s*false\)" `
    "The first layer must grant exactly 100 ordinary reward gold, not stolen gold."
Assert-Contains $event "new\s+CardCreationOptions\([\s\S]*?Owner\.Character\.CardPool[\s\S]*?card\.Rarity\s*==\s*CardRarity\.Rare" `
    "The card pack must contain only rare cards from the owner's character pool."
Assert-Contains $event "CardFactory\.CreateForReward\(Owner,\s*3,\s*options\)\.ToList\(\)" `
    "The second layer must populate three visible card candidates before opening selection."
Assert-Contains $event "SelectCardsToAddToDeckFromGrid\(" `
    "The second layer must use the event card-grid workflow that adds the selected card to the deck."
Assert-Contains $event "new\s+CardSelectorPrefs\([\s\S]*?0,\s*1\)" `
    "The rare pack must allow selecting at most one of the three candidates."
Assert-NotContains $event "ClaimableCardReward|OnSelect\(\)" `
    "The event must not bypass CardReward.Populate by invoking its protected selection method directly."
Assert-Contains $event "RelicFactory\.PullNextRelicFromFront\(Owner\)\.ToMutable\(\)" `
    "The third layer must convert the canonical relic-bag entry to a mutable obtainable instance."
Assert-Contains $event "RelicCmd\.Obtain" `
    "The third layer must obtain the pulled relic through the native command."
Assert-Contains $event "SetEventState\(\s*PageDescription\(RareCardPage\)" `
    "Claiming gold must advance to the rare-card layer."
Assert-Contains $event "SetEventState\(\s*PageDescription\(RelicPage\)" `
    "Resolving the rare-card pack must advance to the relic layer."
Assert-Contains $event "SetEventFinished\(PageDescription\(RelicPage\)\)" `
    "Claiming the relic must finish on the third layer without adding a fourth page."

Assert-Contains $resolver "StageRouteNodeKind\.FirstEvent\s*=>\s*ModelDb\.Event<StageSupplyEvent>\(\)" `
    "The first Stage question room must resolve to StageSupplyEvent."
Assert-Contains $resolver "StageRouteNodeKind\.FateGuidance\s*=>\s*ModelDb\.Event<FateGuidance>\(\)" `
    "The second Stage question room must resolve to the shared FateGuidance event."

$localizationKeys = @(
    "STS2_TOMORIN_MOD-STAGE_SUPPLY_EVENT.title",
    "STS2_TOMORIN_MOD-STAGE_SUPPLY_EVENT.pages.GOLD.description",
    "STS2_TOMORIN_MOD-STAGE_SUPPLY_EVENT.pages.GOLD.options.ClaimGold.title",
    "STS2_TOMORIN_MOD-STAGE_SUPPLY_EVENT.pages.GOLD.options.ClaimGold.description",
    "STS2_TOMORIN_MOD-STAGE_SUPPLY_EVENT.pages.RARE_CARD.description",
    "STS2_TOMORIN_MOD-STAGE_SUPPLY_EVENT.pages.RARE_CARD.cardSelectionPrompt",
    "STS2_TOMORIN_MOD-STAGE_SUPPLY_EVENT.pages.RARE_CARD.options.ClaimRareCard.title",
    "STS2_TOMORIN_MOD-STAGE_SUPPLY_EVENT.pages.RARE_CARD.options.ClaimRareCard.description",
    "STS2_TOMORIN_MOD-STAGE_SUPPLY_EVENT.pages.RELIC.description",
    "STS2_TOMORIN_MOD-STAGE_SUPPLY_EVENT.pages.RELIC.options.ClaimRelic.title",
    "STS2_TOMORIN_MOD-STAGE_SUPPLY_EVENT.pages.RELIC.options.ClaimRelic.description")

foreach ($path in $localizationPaths) {
    Assert-LocalizationKeys $path $localizationKeys
}

Write-Host "Stage supply event route, reward sequence and localization checks passed."
