$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot

# 读取仓库内指定 UTF-8 文本文件。
function Get-RepositoryContent([string]$path) {
    return Get-Content -LiteralPath (Join-Path $root $path) -Raw -Encoding utf8
}

# 断言指定仓库文件存在。
function Assert-FileExists([string]$path) {
    if (-not (Test-Path -LiteralPath (Join-Path $root $path))) {
        throw "Missing Stage implementation file: $path"
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

$paths = @(
    "Scripts/Acts/Stage.cs",
    "Scripts/Encounters/ShadowTomorinBoss.cs",
    "Scripts/Stage/StageActMap.cs",
    "Scripts/Stage/StageRouteDefinition.cs",
    "Scripts/Stage/StageRegistrationPolicy.cs",
    "Scripts/Stage/StageEligibility.cs",
    "Scripts/Stage/StageRoomResolver.cs",
    "Scripts/Stage/StageRunCompatibilityPolicy.cs",
    "Scripts/Stage/StageRunProgressModifier.cs",
    "Scripts/Patch/StageActSaveCompatibilityPatch.cs",
    "Scripts/Patch/StageNeowCompatibilityPatch.cs",
    "Scripts/Patch/StageRunRegistrationPatch.cs",
    "Scripts/Patch/StageRoomResolverPatch.cs",
    "Scripts/Patch/StageFpoProgressPatch.cs",
    "Scripts/Patch/StageBossRewardLifecyclePatch.cs",
    "Scripts/Patch/StageActTransitionPatch.cs",
    "STS2_Tomorin_Mod/localization/eng/acts.json",
    "STS2_Tomorin_Mod/localization/zhs/acts.json")

foreach ($path in $paths) {
    Assert-FileExists $path
}

$stage = Get-RepositoryContent "Scripts/Acts/Stage.cs"
$map = Get-RepositoryContent "Scripts/Stage/StageActMap.cs"
$route = Get-RepositoryContent "Scripts/Stage/StageRouteDefinition.cs"
$registration = Get-RepositoryContent "Scripts/Patch/StageRunRegistrationPatch.cs"
$resolver = Get-RepositoryContent "Scripts/Stage/StageRoomResolver.cs"
$compatibility = Get-RepositoryContent "Scripts/Stage/StageRunCompatibilityPolicy.cs"
$progress = Get-RepositoryContent "Scripts/Stage/StageRunProgressModifier.cs"
$saveCompatibilityPatch = Get-RepositoryContent "Scripts/Patch/StageActSaveCompatibilityPatch.cs"
$neowCompatibilityPatch = Get-RepositoryContent "Scripts/Patch/StageNeowCompatibilityPatch.cs"
$deathPatch = Get-RepositoryContent "Scripts/Patch/StageFpoProgressPatch.cs"
$rewardPatch = Get-RepositoryContent "Scripts/Patch/StageBossRewardLifecyclePatch.cs"
$transitionPatch = Get-RepositoryContent "Scripts/Patch/StageActTransitionPatch.cs"
$allStageSources = ($paths | Where-Object { $_ -like "*.cs" } | ForEach-Object { Get-RepositoryContent $_ }) -join [Environment]::NewLine

Assert-Contains $stage "sealed\s+class\s+Stage\s*:\s*CustomActModel" "Stage must be an independent CustomActModel."
Assert-Contains $stage "GloryAssets" "Stage must centralize temporary Glory resource reuse."
Assert-Contains $stage "TODO" "Temporary Stage resources must retain searchable TODO markers."
Assert-Contains $map "StageRouteDefinition\.Nodes" "Stage map must use StageRouteDefinition as its source."
Assert-Contains $map "AddChildPoint" "Stage route must use the engine map connection API."
Assert-Contains $map "startMapPoints\.Add\(StartingMapPoint\)" "Stage starting point must be registered with the engine."

$lastPosition = -1
foreach ($kind in @("Ancient", "FirstEvent", "Elite", "Shop", "SecondEvent", "RestSite", "Boss")) {
    $position = $route.IndexOf("StageRouteNodeKind.$kind", [StringComparison]::Ordinal)
    if ($position -le $lastPosition) {
        throw "StageRouteDefinition semantic order is invalid: $kind"
    }
    $lastPosition = $position
}

Assert-Contains $registration "RunState.*CreateForNewRun" "Stage must only register while creating a new Run."
Assert-Contains $registration "StageRunProgressModifier" "Stage registration must include the saved progress modifier."
Assert-Contains $progress "\[SavedProperty\]" "Stage progress must participate in Run persistence and synchronization."
Assert-Contains $compatibility "FilterNeowModifiers" "Stage compatibility must expose the Neow modifier filter policy."
Assert-Contains $compatibility "NormalizeRoomCollections" "Stage compatibility must normalize omitted room collections."
Assert-Contains $neowCompatibilityPatch "Neow.*GenerateInitialOptions" "Stage progress must not suppress Neow's normal options."
Assert-Contains $saveCompatibilityPatch "ActModel.*FromSave" "Stage room collections must be normalized before ActModel restores them."
Assert-Contains $saveCompatibilityPatch "ModelDb\.Act<.*Stage" "Room normalization must be scoped to the Stage act."
Assert-Contains $deathPatch "wasRemovalPrevented" "FPO progress must reject prevented deaths."
Assert-Contains $deathPatch "ModelDb\.Monster<FullPowerOblivionis>" "FPO progress must use the stable monster model."

Assert-Contains $resolver "ModelDb\.AncientEvent<GiraffeAncient>" "Stage Ancient must resolve to GiraffeAncient."
Assert-Contains $resolver "ModelDb\.Event<FeedTheCat>" "Both Stage event nodes must resolve to FeedTheCat."
Assert-Contains $resolver "ModelDb\.Encounter<MechaKnightElite>" "Stage elite must resolve to MechaKnightElite."
Assert-Contains $resolver "StageRouteNodeKind\.Boss\s*=>\s*runState\.Act\.BossEncounter" "Stage boss rooms must resolve from the current authoritative primary boss."
Assert-NotContains $resolver "StageRouteNodeKind\.Boss\s*=>\s*ModelDb\.Encounter<CrychicPhatomBoss>" "Stage boss rooms must not hard-code the default Crychic encounter."
Assert-NotContains $allStageSources "LoadModBoss" "Stage must not read LoadModBoss."

Assert-Contains $stage "BossDiscoveryOrder" "Stage must define its default primary-boss discovery order."
$bossDiscoveryStart = $stage.IndexOf("BossDiscoveryOrder", [StringComparison]::Ordinal)
$encounterEnumerationStart = $stage.IndexOf("GenerateAllEncounters", [StringComparison]::Ordinal)
if ($bossDiscoveryStart -lt 0 -or $encounterEnumerationStart -le $bossDiscoveryStart) {
    throw "Stage boss discovery and encounter enumeration members must have a stable source boundary."
}

$bossDiscovery = $stage.Substring($bossDiscoveryStart, $encounterEnumerationStart - $bossDiscoveryStart)
Assert-Contains $bossDiscovery "ModelDb\.Encounter<ShadowTomorinBoss>" "Stage must use ShadowTomorinBoss as its default primary boss."
Assert-NotContains $bossDiscovery "ModelDb\.Encounter<CrychicPhatomBoss>" "Stage must replace the temporary Crychic primary-boss route."
Assert-NotContains $bossDiscovery "ModelDb\.Encounter<(?:OblivionisBoss|TakiBoss)>" "Alternative FateGuidance bosses must not enter the default discovery order."

$encounterEnumeration = $stage.Substring($encounterEnumerationStart)
foreach ($encounter in @("BandMemberEncounter", "ShadowTomorinBoss", "OblivionisBoss", "TakiBoss")) {
    Assert-Contains $encounterEnumeration ("ModelDb\.Encounter<{0}>" -f $encounter) "Stage legal encounters must include $encounter."
}
Assert-NotContains $encounterEnumeration "ModelDb\.Encounter<CrychicPhatomBoss>" "Stage legal encounters must not retain its Crychic placeholder route."

Assert-Contains $rewardPatch "RewardsSet\.WithRewardsFromRoom" "FPO reward eligibility must adapt the native reward entry point."
Assert-Contains $rewardPatch "EmptyForRoom" "Ineligible Glory bosses must retain a terminal empty rewards screen."
Assert-NotContains $rewardPatch "new\s+(GoldReward|CardReward|PotionReward|RelicReward)" "Stage must not copy native reward contents."
Assert-Contains $transitionPatch "RunManager.*EnterNextAct" "Stage transition must run after multiplayer readiness synchronization."
Assert-Contains $transitionPatch "runState\?\.Act\s+is\s+STS2_Tomorin_Mod\.Acts\.Stage" "Stage must remain the final act even when other custom acts follow it."
Assert-Contains $transitionPatch "TheArchitect" "Ineligible Stage runs must retain the native Architect ending."
Assert-NotContains $transitionPatch "\.WinRun\(" "Glory eligibility failure must not bypass the Architect ending."

$engActs = Get-RepositoryContent "STS2_Tomorin_Mod/localization/eng/acts.json" | ConvertFrom-Json
$zhsActs = Get-RepositoryContent "STS2_Tomorin_Mod/localization/zhs/acts.json" | ConvertFrom-Json
if ($engActs.'STS2_TOMORIN_MOD-STAGE.title' -ne "Stage" -or
    [string]::IsNullOrWhiteSpace($zhsActs.'STS2_TOMORIN_MOD-STAGE.title')) {
    throw "Stage English or Simplified Chinese act title is invalid."
}

$gitignore = Get-RepositoryContent ".gitignore"
Assert-Contains $gitignore "(?m)^/local-tests/Stage\.Tests/$" "The local xUnit project must be ignored by Git."

Write-Host "Stage route, registration, progress, reward and transition checks passed."
