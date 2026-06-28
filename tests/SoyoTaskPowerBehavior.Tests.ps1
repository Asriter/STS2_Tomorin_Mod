$ErrorActionPreference = "Stop"

$taskPowerPath = Join-Path $PSScriptRoot "..\Scripts\Powers\EnemyPowers\SoyoPowers\SoyoTaskPower.cs"
$phaseControllerPath = Join-Path $PSScriptRoot "..\Scripts\Powers\EnemyPowers\SoyoPowers\SoyoPhaseControllerPower.cs"
$soyoPath = Join-Path $PSScriptRoot "..\Scripts\Enemy\Soyo.cs"
$estrangementPowerPath = Join-Path $PSScriptRoot "..\Scripts\Powers\EnemyPowers\SoyoPowers\SoyoEstrangementPower.cs"
$maskedDamageReductionPowerPath = Join-Path $PSScriptRoot "..\Scripts\Powers\EnemyPowers\SoyoPowers\SoyoMaskedDamageReductionPower.cs"
$phaseVisualPowerPath = Join-Path $PSScriptRoot "..\Scripts\Powers\EnemyPowers\SoyoPowers\SoyoPhaseVisualPower.cs"

$taskPower = Get-Content -Raw $taskPowerPath
$phaseController = Get-Content -Raw $phaseControllerPath
$soyo = Get-Content -Raw $soyoPath
$estrangementPower = Get-Content -Raw $estrangementPowerPath

if ($soyo -notmatch "TruePhaseThreshold\s*=\s*6") {
    throw "Soyo true phase threshold must be 6."
}

foreach ($methodName in @(
        "EnterTruePhase",
        "EnterMaskPhase",
        "RefreshPhaseAfterCounterChanged",
        "RefreshPhaseForPlayerTurnStart"
    )) {
    if ($soyo -notmatch "\b$methodName\s*\(") {
        throw "Soyo must expose $methodName."
    }
}

if ($soyo -notmatch "RefreshPhaseForPlayerTurnStart[\s\S]*Phase == SoyoPhase\.True[\s\S]*estrangement <= TruePhaseThreshold[\s\S]*EnterMaskPhase") {
    throw "Player turn start must make true phase fall back to mask when counter is at or below threshold."
}

if ($soyo -notmatch "RefreshPhaseForPlayerTurnStart[\s\S]*Phase == SoyoPhase\.True[\s\S]*return Phase;[\s\S]*SoyoEstrangementPower\.Modify\([^;]*,\s*1,\s*this\)") {
    throw "Player turn start must auto-increment counter only while Soyo starts in mask phase."
}

if ($soyo -notmatch "EnterTruePhase[\s\S]*SoyoMaskedDamageReductionPower[\s\S]*PowerCmd\.Remove") {
    throw "Entering true phase must remove SoyoMaskedDamageReductionPower."
}

$maskHealMatch = [regex]::Match($soyo, "private async Task MaskHealMove[\s\S]*?private async Task TrueAttackWoundMove")
if (-not $maskHealMatch.Success) {
    throw "Could not locate Soyo.MaskHealMove."
}

if ($maskHealMatch.Value -match "SoyoEstrangementPower\.Modify") {
    throw "MaskHealMove must not reduce or otherwise modify the counter."
}

if (-not (Test-Path $maskedDamageReductionPowerPath)) {
    throw "SoyoMaskedDamageReductionPower.cs must exist."
}

$maskedDamageReductionPower = Get-Content -Raw $maskedDamageReductionPowerPath

if (-not (Test-Path $phaseVisualPowerPath)) {
    throw "SoyoPhaseVisualPower.cs must exist."
}

$phaseVisualPower = Get-Content -Raw $phaseVisualPowerPath

foreach ($visualPowerClass in @("SoyoMaskVisualPower", "SoyoTruthViauslPower")) {
    if ($phaseVisualPower -notmatch "class\s+$visualPowerClass\s*:\s*BasePowerModel") {
        throw "$visualPowerClass must extend BasePowerModel."
    }

    $visualPowerMatch = [regex]::Match($phaseVisualPower, "class\s+$visualPowerClass\s*:\s*BasePowerModel[\s\S]*?^\s*}", [System.Text.RegularExpressions.RegexOptions]::Multiline)
    if (-not $visualPowerMatch.Success) {
        throw "Could not locate $visualPowerClass body."
    }

    if ($visualPowerMatch.Value -notmatch "PowerType\.Buff" -or $visualPowerMatch.Value -notmatch "PowerStackType\.Single") {
        throw "$visualPowerClass must be a visible single-stack buff."
    }

    if ($visualPowerMatch.Value -match "\b(Before|After|Modify|Should)\w+\s*\(") {
        throw "$visualPowerClass must be display-only and contain no gameplay hook logic."
    }
}

if ($soyo -notmatch "AfterAddedToRoom[\s\S]*PowerCmd\.Apply<SoyoMaskVisualPower>") {
    throw "Soyo must show SoyoMaskVisualPower when entering the room."
}

if ($soyo -notmatch "EnterTruePhase[\s\S]*PowerCmd\.Remove<SoyoMaskVisualPower>[\s\S]*PowerCmd\.Apply<SoyoTruthViauslPower>") {
    throw "Soyo must replace the mask visual power with the truth visual power when entering true phase."
}

if ($soyo -notmatch "EnterMaskPhase[\s\S]*PowerCmd\.Remove<SoyoTruthViauslPower>[\s\S]*PowerCmd\.Apply<SoyoMaskVisualPower>") {
    throw "Soyo must replace the truth visual power with the mask visual power when returning to mask phase."
}

if ($maskedDamageReductionPower -notmatch "class\s+SoyoMaskedDamageReductionPower\s*:\s*BasePowerModel") {
    throw "SoyoMaskedDamageReductionPower must extend BasePowerModel."
}

if ($maskedDamageReductionPower -notmatch "PowerType\.Buff" -or $maskedDamageReductionPower -notmatch "PowerStackType\.Counter") {
    throw "SoyoMaskedDamageReductionPower must be a counter buff."
}

if ($maskedDamageReductionPower -notmatch "CustomPackedIconPath[\s\S]*SoyoEstrangementPower\.png" -or
    $maskedDamageReductionPower -notmatch "CustomBigIconPath[\s\S]*big/SoyoEstrangementPower\.png") {
    throw "SoyoMaskedDamageReductionPower must reuse an existing Soyo icon instead of resolving to a missing default icon."
}

if ($maskedDamageReductionPower -notmatch "ModifyHpLostBeforeOstyLate[\s\S]*target != (base\.)?Owner[\s\S]*amount \* 0\.25m") {
    throw "SoyoMaskedDamageReductionPower must reduce owner HP loss to 25%."
}

if ($maskedDamageReductionPower -notmatch "BeforeSideTurnEnd[\s\S]*CombatSide\.Player[\s\S]*(PowerCmd\.Remove|PowerCmd\.ModifyAmount)") {
    throw "SoyoMaskedDamageReductionPower must decay at player turn end."
}

if ($maskedDamageReductionPower -notmatch "AfterModifyingHpLostBeforeOsty[\s\S]*Flash\(\)") {
    throw "SoyoMaskedDamageReductionPower must flash after modifying HP loss."
}

if ($taskPower -match "BeforeSideTurnEnd[\s\S]*ApplyRandomTask") {
    throw "Soyo task refresh must not happen from SoyoTaskPower.BeforeSideTurnEnd."
}

if ($phaseController -notmatch "BeforeSideTurnStart[\s\S]*SoyoTaskPower\.ApplyRandomTask\(choiceContext, Owner\)") {
    throw "Soyo task refresh must happen from SoyoPhaseControllerPower.BeforeSideTurnStart."
}

if ($taskPower -notmatch "currentTaskType[\s\S]*availableTaskIds[\s\S]*taskId = availableTaskIds\[taskIndex\]") {
    throw "Soyo task refresh must exclude the current task type when choosing the next task."
}

if ($estrangementPower -notmatch "SetAmount\s*\(" -or $estrangementPower -notmatch "Clear\s*\(") {
    throw "SoyoEstrangementPower must expose SetAmount and Clear APIs."
}

if ($estrangementPower -notmatch "RefreshPhaseAfterCounterChanged\s*\(") {
    throw "SoyoEstrangementPower must refresh phase after counter changes."
}

if ($taskPower -notmatch "CompleteCurrentTask\s*\(") {
    throw "SoyoTaskPower must expose CompleteCurrentTask."
}

if ($taskPower -notmatch "protected\s+async\s+Task\s+AddProgress\s*\(\s*PlayerChoiceContext\s+choiceContext\s*,\s*int\s+amount\s*\)") {
    throw "SoyoTaskPower.AddProgress must be async and receive the choice context so task completion can settle immediately."
}

if ($taskPower -notmatch "AddProgress[\s\S]*Missing\s*==\s*0[\s\S]*SettleAsSuccess\(choiceContext\)") {
    throw "SoyoTaskPower.AddProgress must immediately settle successful tasks when progress reaches the requirement."
}

foreach ($taskClass in @("SoyoAttackTaskPower", "SoyoSkillTaskPower", "SoyoPlayCardsTaskPower")) {
    if ($taskPower -notmatch "class\s+$taskClass[\s\S]*?public override async Task AfterCardPlayed[\s\S]*?await\s+AddProgress\(choiceContext,\s*1\)") {
        throw "$taskClass must await AddProgress from AfterCardPlayed so completion, counter change, and phase refresh happen immediately."
    }
}

if ($taskPower -notmatch "SettleAsSuccess[\s\S]*ApplyReward[\s\S]*SoyoEstrangementPower\.Modify\([^;]*,\s*2,\s*this\)") {
    throw "Task success must apply reward and add 2 counter."
}

$failureSettleMatch = [regex]::Match($taskPower, "SettleAsFailure[\s\S]*?^\s*}", [System.Text.RegularExpressions.RegexOptions]::Multiline)
if (-not $failureSettleMatch.Success) {
    throw "SoyoTaskPower must have SettleAsFailure."
}

if ($failureSettleMatch.Value -match "SoyoEstrangementPower\.Modify") {
    throw "Task failure must not modify counter."
}

if ($phaseController -match "HashSet<Player>") {
    throw "Soyo easter eggs must be once per combat, not once per player."
}

foreach ($flagName in @("_prideTriggered", "_doEverythingTriggered", "_utakotobaTriggered")) {
    if ($phaseController -notmatch "bool\s+$flagName") {
        throw "SoyoPhaseControllerPower must use bool flag $flagName."
    }
}

if ($phaseController -notmatch "side == CombatSide\.Enemy[\s\S]*soyo\.Phase == Soyo\.SoyoPhase\.Mask[\s\S]*PowerCmd\.Apply<SoyoMaskedDamageReductionPower>") {
    throw "Enemy turn start must apply False Mask while Soyo is masked."
}

if ($phaseController -notmatch "PrideManSaki[\s\S]*SoyoEstrangementPower\.SetAmount\([^;]*,\s*7,\s*this\)") {
    throw "PrideManSaki must set counter to 7."
}

if ($phaseController -notmatch "PrideManSaki[\s\S]*SoyoEstrangementPower\.SetAmount\([^;]*,\s*7,\s*this\);[\s\S]*soyo\.StunOneTurn\(\)") {
    throw "PrideManSaki must enter true phase before stunning so the stun follow-up targets the true phase."
}

if ($phaseController -notmatch "DoEverything[\s\S]*SoyoTaskPower\.CompleteCurrentTask") {
    throw "DoEverything must complete the current Soyo task."
}

if ($phaseController -notmatch "UtakotobaToken[\s\S]*SoyoEstrangementPower\.Clear[\s\S]*SoyoMaskedDamageReductionPower") {
    throw "UtakotobaToken must clear counter and remove False Mask."
}

Write-Host "Soyo task power behavior checks passed."
