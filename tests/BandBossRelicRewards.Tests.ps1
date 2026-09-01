$ErrorActionPreference = "Stop"

function Get-RepositoryContent([string]$Path) {
    return Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot "..\$Path")
}

function Assert-Matches([string]$Content, [string]$Pattern, [string]$Message) {
    if ($Content -notmatch $Pattern) {
        throw $Message
    }
}

function Assert-NotMatches([string]$Content, [string]$Pattern, [string]$Message) {
    if ($Content -match $Pattern) {
        throw $Message
    }
}

$relicPaths = @(
    "Scripts/Relics/AnonGuitar.cs",
    "Scripts/Relics/TakiDrum.cs",
    "Scripts/Relics/SoyoBase.cs",
    "Scripts/Relics/RaanaGuitar.cs")
$tomorinPool = Get-RepositoryContent "Scripts/RelicPools/TomorinRelicPool.cs"
$lifecycle = Get-RepositoryContent "Scripts/Enemy/BandMemberRelicRewardLifecycle.cs"
$encounterRewards = Get-RepositoryContent "Scripts/Encounters/BandMemberEncounterRewardPolicy.cs"

foreach ($relicPath in $relicPaths) {
    $relic = Get-RepositoryContent $relicPath
    Assert-Matches $relic "Pool\(typeof\(EventRelicPool\)\)" "$relicPath must register in EventRelicPool."
    Assert-Matches $relic "RelicRarity\.Event" "$relicPath must use event rarity."
    Assert-NotMatches $relic "TomorinRelicPool" "$relicPath must not register in the normal Tomorin relic pool."
}

foreach ($relicName in @("AnonGuitar", "TakiDrum", "SoyoBase", "RaanaGuitar")) {
    Assert-NotMatches $tomorinPool "ModelDb\.Relic<$relicName>" `
        "$relicName must not be available from TomorinRelicPool."
}

$takiDrum = Get-RepositoryContent "Scripts/Relics/TakiDrum.cs"
Assert-Matches $takiDrum "override\s+Task\s+BeforeCombatStart\s*\(" `
    "TakiDrum must reset its counter at combat start."
Assert-NotMatches $takiDrum "override\s+Task\s+AfterSideTurnStart\s*\(" `
    "TakiDrum must not reset its counter every turn."
Assert-Matches $takiDrum "_cardPlayCount\s*==\s*4" `
    "TakiDrum must replay only the fifth manually counted card."

$rewardHelper = Get-RepositoryContent "Scripts/Enemy/BandBossRelicReward.cs"
Assert-Matches $rewardHelper "room\.RoomType\s*!=\s*RoomType\.Boss" `
    "Band boss relic rewards must reject non-boss rooms."
Assert-Matches $rewardHelper "ModelDb\.Relic<TRelic>\(\)\.ToMutable\(\)" `
    "Band boss relic rewards must create the requested relic."
Assert-Matches $rewardHelper "new\s+RelicReward\(relic,\s*player\)" `
    "Band boss relic rewards must use the requested relic instead of a random rarity roll."

$bossRewards = @{
    "Scripts/Enemy/Anon.cs" = "AnonGuitar"
    "Scripts/Enemy/Taki.cs" = "TakiDrum"
    "Scripts/Enemy/Soyo.cs" = "SoyoBase"
    "Scripts/Enemy/Raana.cs" = "RaanaGuitar"
}

foreach ($entry in $bossRewards.GetEnumerator()) {
    $boss = Get-RepositoryContent $entry.Key
    Assert-Matches $boss "ShouldGrantBossReward\s*=>\s*true" `
        "$($entry.Key) must enable its boss-only relic reward by default."
    Assert-Matches $boss "BandMemberRelicRewardLifecycle\.RecordEarnedAndGrantBossReward<$($entry.Value)>\s*\(" `
        "$($entry.Key) must route $($entry.Value) through the original boss reward trigger."
    Assert-NotMatches $boss "new\s+RelicReward\(RelicRarity\." `
        "$($entry.Key) must not generate a random relic reward."
}

$anonBoss = Get-RepositoryContent "Scripts/Enemy/Anon.cs"
$anonEscapeBlock = [regex]::Match(
    $anonBoss,
    'private\s+async\s+Task\s+RunState[\s\S]*?(?=protected\s+virtual\s+Task\s+AfterEscapeCompleted)').Value
Assert-Matches $anonBoss "if\s*\(_isSecondPhase\)[\s\S]*?RecordEarnedAndGrantBossReward<AnonGuitar>" `
    "Anon must record its relic only from the second-phase death branch."
Assert-Matches $anonEscapeBlock "CreatureCmd\.Escape" `
    "The Anon escape regression check could not locate the escape path."
Assert-NotMatches $anonEscapeBlock "RecordEarnedAndGrantBossReward" `
    "Anon escape must not earn AnonGuitar."

$takiBoss = Get-RepositoryContent "Scripts/Enemy/Taki.cs"
$takiDeathBlock = [regex]::Match(
    $takiBoss,
    'private\s+void\s+PhaseThreeClearCallBack[\s\S]*?(?=private\s+async\s+Task\s+RunCallBack)').Value
$takiEscapeBlock = [regex]::Match(
    $takiBoss,
    'private\s+async\s+Task\s+RunCallBack[\s\S]*?(?=protected\s+virtual\s+Task\s+AfterEscapeCompleted)').Value
Assert-Matches $takiDeathBlock "RecordEarnedAndGrantBossReward<TakiDrum>" `
    "Taki must record its relic from the actual Creature.Died callback."
Assert-Matches $takiEscapeBlock "CreatureCmd\.Escape" `
    "The Taki escape regression check could not locate the escape path."
Assert-NotMatches $takiEscapeBlock "RecordEarnedAndGrantBossReward" `
    "Taki's third-phase HP-lock escape must not earn TakiDrum."

Assert-Matches $lifecycle "BandMemberEncounter" `
    "The shared boss lifecycle must record earned rewards for BandMemberEncounter."
Assert-Matches $lifecycle "BandBossRelicReward\.Add<TRelic>" `
    "The shared lifecycle must preserve direct rewards for original boss rooms."
Assert-Matches $encounterRewards "player\.Relics" `
    "Encounter rewards must filter relics each player already owns."
Assert-Matches $encounterRewards "room\.ExtraRewards" `
    "Encounter rewards must filter duplicate pending relic rewards."
Assert-Matches $encounterRewards "BandMemberKind\.Anon[\s\S]*AnonGuitar" `
    "Anon must map to AnonGuitar."
Assert-Matches $encounterRewards "BandMemberKind\.Taki[\s\S]*TakiDrum" `
    "Taki must map to TakiDrum."
Assert-Matches $encounterRewards "BandMemberKind\.Soyo[\s\S]*SoyoBase" `
    "Soyo must map to SoyoBase."
Assert-Matches $encounterRewards "BandMemberKind\.Raana[\s\S]*RaanaGuitar" `
    "Raana must map to RaanaGuitar."

foreach ($elitePath in @(
        "Scripts/Enemy/Elite/AnonElite.cs",
        "Scripts/Enemy/Elite/TakiElite.cs",
        "Scripts/Enemy/Elite/SoyoElite.cs",
        "Scripts/Enemy/Elite/RaanaElite.cs")) {
    $elite = Get-RepositoryContent $elitePath
    Assert-Matches $elite "ShouldGrantBossReward\s*=>\s*false" `
        "$elitePath must disable the boss-only relic reward."
}

$zhsRelics = Get-RepositoryContent "STS2_Tomorin_Mod/localization/zhs/relics.json"
$engRelics = Get-RepositoryContent "STS2_Tomorin_Mod/localization/eng/relics.json"
Assert-Matches $zhsRelics 'STS2_TOMORIN_MOD-TAKI_DRUM\.description"\s*:\s*"每场战斗' `
    "The Chinese TakiDrum description must say once per combat."
Assert-Matches $engRelics 'STS2_TOMORIN_MOD-TAKI_DRUM\.description"\s*:\s*"The fifth card you play each combat' `
    "The English TakiDrum description must say once per combat."

Write-Host "Band boss relic reward checks passed."
