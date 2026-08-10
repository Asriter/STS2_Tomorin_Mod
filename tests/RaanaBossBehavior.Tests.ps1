$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot

function Resolve-RepoPath($Path) {
    Join-Path $root $Path
}

function Assert-FileExists($Path) {
    if (-not (Test-Path -LiteralPath (Resolve-RepoPath $Path))) {
        throw "Missing file: $Path"
    }
}

function Get-RepoContent($Path) {
    Get-Content -LiteralPath (Resolve-RepoPath $Path) -Raw -ErrorAction Stop
}

function Assert-Contains($Path, $Pattern, $Message) {
    $content = Get-RepoContent $Path
    if ($content -notmatch $Pattern) {
        throw $Message
    }
}

function Assert-NotContains($Path, $Pattern, $Message) {
    $content = Get-RepoContent $Path
    if ($content -match $Pattern) {
        throw $Message
    }
}

$interestPowerPath = "Scripts/Powers/EnemyPowers/RaanaPowers/RaanaInterestPower.cs"
$unwellPowerPath = "Scripts/Powers/EnemyPowers/RaanaPowers/RaanaUnwellPower.cs"
$risingMoodPowerPath = "Scripts/Powers/EnemyPowers/RaanaPowers/RaanaRisingMoodPower.cs"
$raanaPath = "Scripts/Enemy/Raana.cs"
$engMonstersPath = "STS2_Tomorin_Mod/localization/eng/monsters.json"
$zhsMonstersPath = "STS2_Tomorin_Mod/localization/zhs/monsters.json"
$engPowersPath = "STS2_Tomorin_Mod/localization/eng/powers.json"
$zhsPowersPath = "STS2_Tomorin_Mod/localization/zhs/powers.json"

Assert-FileExists $interestPowerPath
Assert-FileExists $unwellPowerPath
Assert-FileExists $risingMoodPowerPath
Assert-FileExists $raanaPath
Assert-FileExists $engMonstersPath
Assert-FileExists $zhsMonstersPath
Assert-FileExists $engPowersPath
Assert-FileExists $zhsPowersPath

Assert-Contains $interestPowerPath "class\s+RaanaInterestPower\s*:\s*BasePowerModel" "RaanaInterestPower must extend BasePowerModel."
Assert-Contains $interestPowerPath "PowerType\.Buff" "RaanaInterestPower must be a buff."
Assert-Contains $interestPowerPath "PowerStackType\.Counter" "RaanaInterestPower must be a counter power."
Assert-Contains $interestPowerPath "override\s+int\s+DisplayAmount\s*=>\s*Amount" "RaanaInterestPower must display Amount."
Assert-Contains $interestPowerPath "LowThreshold\s*=>\s*18\s*\*\s*Math\.Max\(1,\s*CombatState\.Players\.Count\)" "RaanaInterestPower low threshold must scale by player count."
Assert-Contains $interestPowerPath "HighThreshold\s*=>\s*30\s*\*\s*Math\.Max\(1,\s*CombatState\.Players\.Count\)" "RaanaInterestPower high threshold must scale by player count."
Assert-Contains $interestPowerPath "ModifyInterest\s*\(\s*PlayerChoiceContext\s+choiceContext\s*,\s*int\s+delta\s*,\s*CardModel\?\s+source\s*\)" "RaanaInterestPower must expose ModifyInterest(PlayerChoiceContext, int, CardModel?)."
Assert-Contains $interestPowerPath "Math\.Max\(0,\s*Amount\s*\+\s*delta\)" "RaanaInterestPower must clamp interest to zero."
Assert-Contains $interestPowerPath "InvokeDisplayAmountChanged\s*\(" "RaanaInterestPower must refresh the visible counter."
Assert-Contains $interestPowerPath "RefreshInterestMoveStateIfNeeded\s*\(" "RaanaInterestPower must notify Raana after interest changes."
Assert-Contains $interestPowerPath "AfterCardPlayed\s*\(\s*PlayerChoiceContext\s+choiceContext\s*,\s*CardPlay\s+cardPlay\s*\)" "RaanaInterestPower must listen after cards are played."
Assert-Contains $interestPowerPath "CardRarity\.Uncommon\s*=>\s*2" "Uncommon cards must add 2 total interest."
Assert-Contains $interestPowerPath "CardRarity\.Rare\s*=>\s*5" "Rare cards must add 5 total interest."
Assert-Contains $interestPowerPath "AfterCardExhausted\s*\(\s*PlayerChoiceContext\s+choiceContext\s*,\s*CardModel\s+card\s*,\s*bool\s+causedByEthereal\s*\)" "RaanaInterestPower must listen after cards are exhausted."
Assert-Contains $interestPowerPath "card\s+is\s+LeftoverBuffet[\s\S]*-2" "LeftoverBuffet exhaust must reduce interest by 2 net."
Assert-Contains $interestPowerPath "card\.Owner\?\.Creature\?\.Side\s*==\s*CombatSide\.Player" "RaanaInterestPower must only count player-side cards."

Assert-Contains $unwellPowerPath "class\s+RaanaUnwellPower\s*:\s*BasePowerModel" "RaanaUnwellPower must extend BasePowerModel."
Assert-Contains $unwellPowerPath "PowerType\.Debuff" "RaanaUnwellPower must be a debuff."
Assert-Contains $unwellPowerPath "PowerStackType\.Counter" "RaanaUnwellPower must be a counter power."
Assert-Contains $unwellPowerPath "ModifyDamageMultiplicative\s*\(" "RaanaUnwellPower must modify outgoing damage."
Assert-Contains $unwellPowerPath "dealer\s*==\s*(base\.)?Owner[\s\S]*0\.75m" "RaanaUnwellPower must multiply owner damage by 0.75."
Assert-Contains $unwellPowerPath "BeforeSideTurnEnd\s*\(" "RaanaUnwellPower must decay at holder turn end."
Assert-Contains $unwellPowerPath "side\s*!=\s*(base\.)?Owner\.Side" "RaanaUnwellPower decay must be scoped to owner side."
Assert-Contains $unwellPowerPath "PowerCmd\.Remove\(this\)" "RaanaUnwellPower must remove itself at zero."

Assert-Contains $risingMoodPowerPath "class\s+RaanaRisingMoodPower\s*:\s*BasePowerModel" "RaanaRisingMoodPower must extend BasePowerModel."
Assert-Contains $risingMoodPowerPath "PowerType\.Buff" "RaanaRisingMoodPower must be a buff."
Assert-Contains $risingMoodPowerPath "PowerStackType\.Single" "RaanaRisingMoodPower must be single stack."
Assert-Contains $risingMoodPowerPath "BeforeSideTurnEnd\s*\(" "RaanaRisingMoodPower must trigger at enemy turn end."
Assert-Contains $risingMoodPowerPath "side\s*!=\s*CombatSide\.Enemy" "RaanaRisingMoodPower must only trigger at enemy turn end."
Assert-Contains $risingMoodPowerPath "PowerCmd\.Apply<StrengthPower>\([^;]*,\s*1,\s*(base\.)?Owner" "RaanaRisingMoodPower must give Raana 1 Strength."

Assert-Contains $raanaPath "class\s+Raana\s*:\s*CustomMonsterModel" "Raana must extend CustomMonsterModel."
Assert-Contains $raanaPath "AfterAddedToRoom\s*\(" "Raana must initialize route powers after entering the room."
Assert-Contains $raanaPath "PowerCmd\.Apply<RaanaInterestPower>" "Raana must apply RaanaInterestPower at combat start."
Assert-Contains $raanaPath "HasRelic<EmptyParfait>[\s\S]*HasRelic<MatchaParfait>" "Raana route priority must check EmptyParfait before MatchaParfait."
Assert-Contains $raanaPath "MatchaParfait[\s\S]*CreatureCmd\.GainBlock\([^;]*,\s*WeakenedEntryBlock" "MatchaParfait route must gain weakened-entry block."
Assert-Contains $raanaPath "MatchaParfait[\s\S]*SetMoveImmediate\(_sleepState" "MatchaParfait route must start with Sleep."
Assert-Contains $raanaPath "EmptyParfait[\s\S]*PowerCmd\.Apply<RaanaRisingMoodPower>" "EmptyParfait route must apply Rising Mood."
Assert-Contains $raanaPath "ApplyDefaultEmpoweredRoute[\s\S]*PowerCmd\.Apply<RaanaRisingMoodPower>" "No-relic route must apply Rising Mood."
Assert-Contains $raanaPath "new\s+MoveState\(""RAANA_SLEEP""" "Raana must define a Sleep MoveState."
Assert-Contains $raanaPath "new\s+MoveState\(""RAANA_S1_ATTACK""" "Raana must define S1 as a MoveState."
Assert-Contains $raanaPath "new\s+MoveState\(""RAANA_S2_MULTI_BLOCK""" "Raana must define S2 as a MoveState."
Assert-Contains $raanaPath "new\s+MoveState\(""RAANA_S3_CLEANSE_PARFAIT""" "Raana must define S3 as a MoveState."
Assert-Contains $raanaPath "new\s+MoveState\(""RAANA_S4_LOW_INTEREST""" "Raana must define S4 low as a separate MoveState."
Assert-Contains $raanaPath "new\s+MoveState\(""RAANA_S4_MID_INTEREST""" "Raana must define S4 mid as a separate MoveState."
Assert-Contains $raanaPath "new\s+MoveState\(""RAANA_S4_HIGH_INTEREST""" "Raana must define S4 high as a separate MoveState."
Assert-Contains $raanaPath "_sleepState\.FollowUpState\s*=\s*_s1State" "Sleep must lead into S1."
Assert-Contains $raanaPath "_s1State\.FollowUpState\s*=\s*_s2State" "S1 must lead into S2."
Assert-Contains $raanaPath "_s2State\.FollowUpState\s*=\s*_s3State" "S2 must lead into S3."
Assert-Contains $raanaPath "_s4LowState\.FollowUpState\s*=\s*_s1State" "S4 low must return to S1."
Assert-Contains $raanaPath "_s4MidState\.FollowUpState\s*=\s*_s1State" "S4 mid must return to S1."
Assert-Contains $raanaPath "_s4HighState\.FollowUpState\s*=\s*_s1State" "S4 high must return to S1."
Assert-Contains $raanaPath "ResolveInterestMoveState\s*\(" "Raana must expose ResolveInterestMoveState."
Assert-Contains $raanaPath "Amount\s*<\s*interestPower\.HighThreshold" "S4 high branch must start at HighThreshold, so mid interest must be below HighThreshold."
Assert-NotContains $raanaPath "Amount\s*<=\s*interestPower\.HighThreshold" "S4 mid branch must not include the HighThreshold boundary."
Assert-Contains $raanaPath "RefreshInterestMoveStateIfNeeded\s*\(" "Raana must expose RefreshInterestMoveStateIfNeeded."
Assert-Contains $raanaPath "IsCurrentInterestPreviewState\s*\(" "Raana must expose IsCurrentInterestPreviewState."
Assert-Contains $raanaPath "SetMoveImmediate\(ResolveInterestMoveState\(\)" "Raana must switch S4 preview immediately when interest changes."
Assert-Contains $raanaPath "_s3State\.FollowUpState\s*=\s*ResolveInterestMoveState\(\)" "S3 must set S4 by current interest before preview."
Assert-Contains $raanaPath "PowerType\.Debuff" "S3 must filter debuffs for cleanse."
Assert-Contains $raanaPath "power\s+is\s+not\s+RaanaUnwellPower" "S3 must not cleanse RaanaUnwellPower."
Assert-Contains $raanaPath "DistinctBy\(\s*power\s*=>\s*power\.GetType\(\)\s*\)" "S3 heal count must be based on debuff type count."
Assert-Contains $raanaPath "CreateCard<LeftoverBuffet>" "S3 must create LeftoverBuffet cards."
Assert-Contains $raanaPath "BuffetsPerPlayer" "S3 must add the configured number of LeftoverBuffet cards per living player."
Assert-Contains $raanaPath "S4LowMove[\s\S]*PowerCmd\.Apply<WeakPower>" "S4 low must apply Weak."
Assert-Contains $raanaPath "S4MidMove[\s\S]*PowerCmd\.Apply<VulnerablePower>" "S4 mid must apply Vulnerable."
Assert-Contains $raanaPath "S4HighMove[\s\S]*PowerCmd\.Apply<StrengthPower>" "S4 high must give Raana Strength."
Assert-Contains $raanaPath "ClearInterest\s*\(" "S4 moves must clear interest after resolving."
Assert-NotContains $raanaPath "RaanaBoss" "Raana body implementation must not add encounter registration."

foreach ($monsterPath in @($engMonstersPath, $zhsMonstersPath)) {
    Assert-Contains $monsterPath "STS2_TOMORIN_MOD-RAANA\.name" "$monsterPath must localize Raana name."
}

foreach ($powerPath in @($engPowersPath, $zhsPowersPath)) {
    Assert-Contains $powerPath "STS2_TOMORIN_MOD-RAANA_INTEREST_POWER\.title" "$powerPath must localize RaanaInterestPower title."
    Assert-Contains $powerPath "STS2_TOMORIN_MOD-RAANA_INTEREST_POWER\.description" "$powerPath must localize RaanaInterestPower description."
    Assert-Contains $powerPath "STS2_TOMORIN_MOD-RAANA_INTEREST_POWER\.smartDescription" "$powerPath must localize RaanaInterestPower smart description."
    Assert-Contains $powerPath "STS2_TOMORIN_MOD-RAANA_UNWELL_POWER\.title" "$powerPath must localize RaanaUnwellPower title."
    Assert-Contains $powerPath "STS2_TOMORIN_MOD-RAANA_UNWELL_POWER\.description" "$powerPath must localize RaanaUnwellPower description."
    Assert-Contains $powerPath "STS2_TOMORIN_MOD-RAANA_UNWELL_POWER\.smartDescription" "$powerPath must localize RaanaUnwellPower smart description."
    Assert-Contains $powerPath "STS2_TOMORIN_MOD-RAANA_RISING_MOOD_POWER\.title" "$powerPath must localize RaanaRisingMoodPower title."
    Assert-Contains $powerPath "STS2_TOMORIN_MOD-RAANA_RISING_MOOD_POWER\.description" "$powerPath must localize RaanaRisingMoodPower description."
    Assert-Contains $powerPath "STS2_TOMORIN_MOD-RAANA_RISING_MOOD_POWER\.smartDescription" "$powerPath must localize RaanaRisingMoodPower smart description."
}

Write-Host "Raana boss behavior checks passed."
