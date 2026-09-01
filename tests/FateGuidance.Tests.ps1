$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot

# 读取仓库内指定 UTF-8 文本文件。
function Get-RepositoryContent([string]$path) {
    return Get-Content -LiteralPath (Join-Path $root $path) -Raw -Encoding utf8
}

# 断言指定仓库文件存在。
function Assert-FileExists([string]$path) {
    if (-not (Test-Path -LiteralPath (Join-Path $root $path))) {
        throw "Missing FateGuidance implementation file: $path"
    }
}

# 断言文本包含指定结构语义。
function Assert-Contains([string]$content, [string]$pattern, [string]$message) {
    if ($content -notmatch $pattern) {
        throw $message
    }
}

# 断言文本不包含禁止结构。
function Assert-NotContains([string]$content, [string]$pattern, [string]$message) {
    if ($content -match $pattern) {
        throw $message
    }
}

# 断言具名结构按照业务顺序出现，不对集合数量作固定断言。
function Assert-PatternsInOrder([string]$content, [string[]]$patterns, [string]$message) {
    $searchStart = 0
    foreach ($pattern in $patterns) {
        $match = [regex]::Match($content, $pattern, [System.Text.RegularExpressions.RegexOptions]::Singleline,
            [TimeSpan]::FromSeconds(2))
        while ($match.Success -and $match.Index -lt $searchStart) {
            $match = $match.NextMatch()
        }

        if (-not $match.Success) {
            throw $message
        }

        $searchStart = $match.Index + $match.Length
    }
}

# 返回指定本地化键空间中的属性名，供双语集合比较。
function Get-LocalizationKeys([pscustomobject]$localization, [string]$prefix) {
    return @($localization.PSObject.Properties.Name | Where-Object { $_.StartsWith($prefix, [StringComparison]::Ordinal) })
}

$paths = @(
    "Scripts/Events/FateGuidance.cs",
    "Scripts/Services/BossMapRouteService.cs",
    "Scripts/Services/BossMapVisualSynchronizer.cs",
    "STS2_Tomorin_Mod/localization/eng/events.json",
    "STS2_Tomorin_Mod/localization/zhs/events.json",
    "STS2_Tomorin_Mod/images/events/Giraffe.png")

foreach ($path in $paths) {
    Assert-FileExists $path
}

$event = Get-RepositoryContent "Scripts/Events/FateGuidance.cs"
$routeService = Get-RepositoryContent "Scripts/Services/BossMapRouteService.cs"
$visualSynchronizer = Get-RepositoryContent "Scripts/Services/BossMapVisualSynchronizer.cs"
$allSources = (Get-ChildItem -LiteralPath (Join-Path $root "Scripts") -Recurse -Filter "*.cs" |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw -Encoding utf8 }) -join [Environment]::NewLine

Assert-Contains $event "sealed\s+class\s+FateGuidance\s*:\s*CustomEventModel" "FateGuidance must be a sealed CustomEventModel."
Assert-Contains $event "IsShared\s*=>\s*true" "FateGuidance must use the native shared-event flow."
Assert-Contains $event "IsAllowed\s*\([^)]*IRunState[^)]*\)[\s\S]{0,300}(?:is\s+STS2_Tomorin_Mod\.Acts\.Stage|Acts\.Stage)" "FateGuidance must only be allowed in Stage."
Assert-Contains $event "res://STS2_Tomorin_Mod/images/events/Giraffe\.png" "FateGuidance must reuse the approved Giraffe portrait."
Assert-PatternsInOrder $event @(
    "CreateOption\s*\(\s*ChooseCrychic\s*,\s*InitialPage\s*,\s*nameof\s*\(\s*ChooseCrychic\s*\)\s*\)",
    "LockedOption\s*\(\s*nameof\s*\(\s*ChooseOblivionis\s*\)\s*,\s*InitialPage\s*\)",
    "LockedOption\s*\(\s*nameof\s*\(\s*ChooseTaki\s*\)\s*,\s*InitialPage\s*\)"
) "FateGuidance named options must keep the approved order, with only Crychic selectable and the other choices natively locked."

foreach ($mapping in @(
    @{ Handler = "ChooseCrychic"; Boss = "CrychicPhatomBoss"; Page = "CrychicPage" },
    @{ Handler = "ChooseOblivionis"; Boss = "OblivionisBoss"; Page = "OblivionisPage" },
    @{ Handler = "ChooseTaki"; Boss = "TakiBoss"; Page = "TakiPage" })) {
    Assert-Contains $event ("{0}[\s\S]{{0,900}}ModelDb\.Encounter<{1}>" -f $mapping.Handler, $mapping.Boss) `
        ("{0} must resolve {1}." -f $mapping.Handler, $mapping.Boss)
    Assert-Contains $event ("{0}[\s\S]{{0,1200}}SetEventFinished\s*\(\s*PageDescription\s*\(\s*{1}\s*\)\s*\)" -f $mapping.Handler, $mapping.Page) `
        ("{0} must finish on its dedicated result page." -f $mapping.Handler)
}

Assert-Contains $event "BossMapRouteService\.ChangePrimaryBoss" "FateGuidance must delegate boss changes to BossMapRouteService."
Assert-NotContains $event "SetBossEncounter|SetSecondBossEncounter|BossMapVisualSynchronizer" "FateGuidance must not mutate boss slots or map visuals directly."
Assert-NotContains $event "PlayerChoiceSynchronizer|VoteCoordinator|SharedEventOptionChosenMessage|Modifier" "FateGuidance must not implement a custom vote coordinator, message, or persistence modifier."
Assert-NotContains $event "EventOption\s*\([^\r\n]*null" "Locked choices must use the native LockedOption helper instead of a custom null callback."
Assert-NotContains $allSources "FateGuidance(?:VoteCoordinator|Coordinator|Modifier)|(?:VoteCoordinator|Modifier)\s*<\s*FateGuidance" "FateGuidance must not add a custom coordinator or persistence modifier elsewhere in the mod."

Assert-Contains $routeService "enum\s+PrimaryBossChangeResult" "The route service must expose the stable primary-boss result enum."
Assert-Contains $routeService "AlreadySelected" "The route service must report identity-preserving no-op changes."
Assert-Contains $routeService "PrimaryBossChanged" "The route service must report successful primary-boss changes."
Assert-Contains $routeService "ChangePrimaryBoss\s*\(\s*IRunState\s+runState\s*,\s*EncounterModel\s+targetBoss\s*\)" "The route service must expose the approved ChangePrimaryBoss contract."
Assert-Contains $routeService "targetBoss\.RoomType[\s\S]{0,200}RoomType\.Boss" "The route service must reject non-boss encounters."
Assert-Contains $routeService "\.BossEncounter" "The route service must compare the current primary boss."
Assert-Contains $routeService "\.SecondBossEncounter" "The route service must compare the current secondary boss without modifying it."
Assert-Contains $routeService "\.Id" "Boss identity de-duplication must use stable ModelId values."
Assert-Contains $routeService "CanonicalInstance\.ToMutable\s*\(\s*\)" "The primary slot must receive a mutable canonical boss instance."
Assert-PatternsInOrder $routeService @(
    "SetBossEncounter\s*\(",
    "BossMapVisualSynchronizer\.RefreshCurrentBossVisuals\s*\(\s*runState\s*\)"
) "The route service must refresh map visuals only after changing the primary boss."
Assert-NotContains $routeService "SetSecondBossEncounter" "Changing the primary boss must never write the secondary slot."
Assert-NotContains $routeService "ReferenceEquals\s*\([^,]*(?:BossEncounter|targetBoss)" "Boss de-duplication must not depend on object reference identity."

Assert-Contains $visualSynchronizer "RefreshCurrentBossVisuals\s*\(\s*IRunState\s+runState\s*\)" "The visual synchronizer must expose the approved run-state entry point."
Assert-Contains $visualSynchronizer "_bossPointNode" "The visual synchronizer must refresh the primary boss node."
Assert-Contains $visualSynchronizer "_secondBossPointNode" "The visual synchronizer must preserve and refresh the secondary boss node when present."
Assert-Contains $visualSynchronizer "BossEncounter" "The primary visual must be projected from the authoritative Act boss state."
Assert-Contains $visualSynchronizer "SecondBossEncounter" "The secondary visual must be projected from the authoritative Act boss state."
Assert-Contains $visualSynchronizer "BossNodePath" "The PNG branch must read the encounter boss-node path."
Assert-Contains $visualSynchronizer "\.png" "The PNG branch must bind the boss image resource."
Assert-Contains $visualSynchronizer "_outline\.png" "The PNG branch must bind the boss outline resource."
Assert-Contains $visualSynchronizer "BossNodeSpineResource" "The Spine branch must read the encounter Spine resource."
foreach ($fieldName in @("_usesSpine", "_spriteContainer", "_spineSprite", "_animController", "_material", "_placeholderImage", "_placeholderOutline")) {
    Assert-Contains $visualSynchronizer ([regex]::Escape($fieldName)) "The visual synchronizer is missing the approved NBossMapPoint field contract: $fieldName"
}
Assert-Contains $visualSynchronizer "RefreshVisualsInstantly" "Rebound boss nodes must reuse the native immediate visual refresh."
Assert-Contains $visualSynchronizer "catch\s*\(\s*Exception" "Map refresh failures must degrade safely instead of escaping into the shared event."
Assert-Contains $visualSynchronizer "Log\.Error" "Map refresh failures must be written to the game log."
Assert-NotContains $visualSynchronizer "SetBossEncounter|SetSecondBossEncounter" "The visual synchronizer must never mutate authoritative boss state."

$localizationPrefix = "STS2_TOMORIN_MOD-FATE_GUIDANCE."
$requiredLocalizationKeys = @(
    "${localizationPrefix}title",
    "${localizationPrefix}pages.INITIAL.description",
    "${localizationPrefix}pages.INITIAL.options.ChooseCrychic.title",
    "${localizationPrefix}pages.INITIAL.options.ChooseCrychic.description",
    "${localizationPrefix}pages.INITIAL.options.ChooseOblivionis.title",
    "${localizationPrefix}pages.INITIAL.options.ChooseOblivionis.description",
    "${localizationPrefix}pages.INITIAL.options.ChooseTaki.title",
    "${localizationPrefix}pages.INITIAL.options.ChooseTaki.description",
    "${localizationPrefix}pages.CRYCHIC.description",
    "${localizationPrefix}pages.OBLIVIONIS.description",
    "${localizationPrefix}pages.TAKI.description")

$engEvents = Get-RepositoryContent "STS2_Tomorin_Mod/localization/eng/events.json" | ConvertFrom-Json
$zhsEvents = Get-RepositoryContent "STS2_Tomorin_Mod/localization/zhs/events.json" | ConvertFrom-Json
$engKeys = Get-LocalizationKeys $engEvents $localizationPrefix
$zhsKeys = Get-LocalizationKeys $zhsEvents $localizationPrefix

if (Compare-Object ($engKeys | Sort-Object) ($zhsKeys | Sort-Object)) {
    throw "FateGuidance English and Simplified Chinese localization key sets must match."
}

foreach ($key in $requiredLocalizationKeys) {
    if ($key -notin $engKeys -or $key -notin $zhsKeys) {
        throw "Missing FateGuidance localization key: $key"
    }

    if ([string]::IsNullOrWhiteSpace([string]$engEvents.$key) -or
        [string]::IsNullOrWhiteSpace([string]$zhsEvents.$key)) {
        throw "FateGuidance localization value must be non-empty in both languages: $key"
    }
}

Write-Host "FateGuidance event, boss route service, visual projection and localization checks passed."
