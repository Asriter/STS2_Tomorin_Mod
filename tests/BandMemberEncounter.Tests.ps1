$ErrorActionPreference = "Stop"

# Windows PowerShell 的内置 C# 编译器不支持项目所用语法，因此自动转交给已安装的 PowerShell 7。
if ($PSVersionTable.PSVersion.Major -lt 7) {
    & pwsh -NoProfile -File $PSCommandPath
    exit $LASTEXITCODE
}

$root = Split-Path -Parent $PSScriptRoot

# 将仓库相对路径解析为绝对路径。
function Resolve-RepositoryPath([string]$path) {
    return (Join-Path $root $path)
}

# 读取仓库内的 UTF-8 文本文件。
function Get-RepositoryContent([string]$path) {
    return (Get-Content -LiteralPath (Resolve-RepositoryPath $path) -Raw -Encoding utf8)
}

# 断言条件成立。
function Assert-True([bool]$condition, [string]$message) {
    if (-not $condition) {
        throw $message
    }
}

# 断言文件存在。
function Assert-FileExists([string]$path) {
    Assert-True (Test-Path -LiteralPath (Resolve-RepositoryPath $path)) "缺少文件：$path"
}

# 断言文本包含指定结构。
function Assert-Matches([string]$content, [string]$pattern, [string]$message) {
    Assert-True ($content -match $pattern) $message
}

# 断言文本不包含指定结构。
function Assert-NotMatches([string]$content, [string]$pattern, [string]$message) {
    Assert-True ($content -notmatch $pattern) $message
}

$selectorPath = "Scripts/Encounters/BandMemberSelector.cs"
$encounterPath = "Scripts/Encounters/BandMemberEncounter.cs"
$rewardPolicyPath = "Scripts/Encounters/BandMemberEncounterRewardPolicy.cs"
$rewardLifecyclePath = "Scripts/Enemy/BandMemberRelicRewardLifecycle.cs"
$combatEndPatchPath = "Scripts/Patch/HookAfterCombatEndPatch.cs"
$coordinatorPath = "Scripts/Encounters/BandSurroundedCoordinator.cs"
$scalerPath = "Scripts/Enemy/Elite/EliteStatScaler.cs"
$elitePaths = @(
    "Scripts/Enemy/Elite/AnonElite.cs",
    "Scripts/Enemy/Elite/TakiElite.cs",
    "Scripts/Enemy/Elite/SoyoElite.cs",
    "Scripts/Enemy/Elite/RaanaElite.cs")
$baseBossPaths = @(
    "Scripts/Enemy/Anon.cs",
    "Scripts/Enemy/Taki.cs",
    "Scripts/Enemy/Soyo.cs",
    "Scripts/Enemy/Raana.cs")
$scenePath = "STS2_Tomorin_Mod/scenes/encounters/band_member_encounter.tscn"
$engEncountersPath = "STS2_Tomorin_Mod/localization/eng/encounters.json"
$zhsEncountersPath = "STS2_Tomorin_Mod/localization/zhs/encounters.json"
$designPath = "docs/2026-08-20-band-member-encounter-design.md"
$lifecycleHarnessPaths = @(
    "tests/BandMemberEncounterHarness/BandMemberEncounterHarness.csproj",
    "tests/BandMemberEncounterHarness/Directory.Build.props",
    "tests/BandMemberEncounterHarness/BandMemberEncounterLifecycleTests.testcs")

foreach ($path in @($selectorPath, $encounterPath, $rewardPolicyPath, $rewardLifecyclePath, $combatEndPatchPath,
        $coordinatorPath, $scalerPath, $scenePath,
        $engEncountersPath, $zhsEncountersPath, $designPath) + $elitePaths + $baseBossPaths +
        $lifecycleHarnessPaths) {
    Assert-FileExists $path
}

# 直接编译并执行纯选择器，覆盖全部已遇成员组合，不固化任何战斗配置数值。
Add-Type -Path (Resolve-RepositoryPath $selectorPath)
$kindType = [STS2_Tomorin_Mod.Encounters.BandMemberKind]
$selectorType = [STS2_Tomorin_Mod.Encounters.BandMemberSelector]
$fixedOrder = @($selectorType::FixedOrder)
$combinationCount = [int][Math]::Pow(2, $fixedOrder.Count)
for ($mask = 0; $mask -lt $combinationCount; $mask++) {
    $encountered = [System.Collections.Generic.HashSet[STS2_Tomorin_Mod.Encounters.BandMemberKind]]::new()
    for ($index = 0; $index -lt $fixedOrder.Count; $index++) {
        if (($mask -band (1 -shl $index)) -ne 0) {
            [void]$encountered.Add($fixedOrder[$index])
        }
    }

    $selection = $selectorType::Select($encountered)
    $repeat = $selectorType::Select($encountered)
    Assert-True ($selection.Left -ne $selection.Right) "选择器不得产生重复成员。"
    Assert-True ($selection.Left -eq $repeat.Left -and $selection.Right -eq $repeat.Right) `
        "相同输入必须产生确定性的左右顺序。"

    $unencountered = @($fixedOrder | Where-Object { -not $encountered.Contains($_) })
    if ($unencountered.Count -ge 2) {
        Assert-True ($selection.Left -eq $unencountered[0] -and $selection.Right -eq $unencountered[1]) `
            "第一轮必须优先使用固定顺序中的未遇成员。"
    }
    elseif ($unencountered.Count -eq 1) {
        Assert-True ($selection.Left -eq $unencountered[0] -or $selection.Right -eq $unencountered[0]) `
            "仅剩的未遇成员必须进入选择结果。"
    }
}

$parsedKind = [STS2_Tomorin_Mod.Encounters.BandMemberKind]::Anon
Assert-True (-not $selectorType::TryParseStableName("0", [ref]$parsedKind)) `
    "稳定状态不得接受枚举整数文本。"

# 直接编译并以属性关系验证缩放函数，不断言任一 Elite 的固定生命或伤害。
Add-Type -Path (Resolve-RepositoryPath $scalerPath)
$scalerType = [STS2_Tomorin_Mod.Enemy.Elite.EliteStatScaler]
$baseDomain = [System.Linq.Enumerable]::Range(0, 128)
$multiplierDomain = [System.Linq.Enumerable]::Range(1, 16) | ForEach-Object { [decimal]$_ / [decimal]7 }
foreach ($baseValue in $baseDomain) {
    foreach ($multiplier in $multiplierDomain) {
        $exact = [decimal]$baseValue * $multiplier
        $scaled = $scalerType::ScaleDown($baseValue, $multiplier)
        Assert-True ([decimal]$scaled -le $exact) "向下取整结果不得大于精确乘积。"
        Assert-True (($exact - [decimal]$scaled) -lt [decimal]1) "向下取整误差必须小于一个整数单位。"
    }
}

$anonBase = Get-RepositoryContent $baseBossPaths[0]
$takiBase = Get-RepositoryContent $baseBossPaths[1]
$soyoBase = Get-RepositoryContent $baseBossPaths[2]
$raanaBase = Get-RepositoryContent $baseBossPaths[3]

foreach ($property in @("NormalSingleAtk", "NormalMultiAtk")) {
    Assert-Matches $anonBase "protected\s+virtual\s+int\s+$property" "Anon 的 $property 必须是保护级虚属性。"
}
foreach ($property in @("PhaseOneStateAtk", "PhaseOneNormalAtk", "PhaseOneBigAtk", "PhaseTwoCardAtk",
        "PhaseThreeAtk", "PhaseOneHp", "PhaseTwoHp")) {
    Assert-Matches $takiBase "protected\s+virtual\s+int\s+$property" "Taki 的 $property 必须是保护级虚属性。"
}
foreach ($property in @("MaskMultiAttack", "TrueAttack", "TrueMultiAttack")) {
    Assert-Matches $soyoBase "protected\s+virtual\s+int\s+$property" "Soyo 的 $property 必须是保护级虚属性。"
}
foreach ($property in @("S1Attack", "S2Attack", "S4Attack", "S4HighAttack")) {
    Assert-Matches $raanaBase "protected\s+virtual\s+int\s+$property" "Raana 的 $property 必须是保护级虚属性。"
}

foreach ($path in $elitePaths) {
    $content = Get-RepositoryContent $path
    Assert-Matches $content "MinInitialHp\s*=>\s*EliteStatScaler\.ScaleDown\(base\.MinInitialHp,\s*StatMultiplier\)" `
        "$path 的生命必须从原 Boss 属性通过统一缩放器派生。"
    Assert-NotMatches $content "MinInitialHp\s*=>\s*\d" "$path 不得硬编码最终生命配置。"
    Assert-Matches $content "override\s+LocString\s+Title\s*=>\s*ModelDb\.Monster<" `
        "$path 必须继续显示对应原 Boss 标题。"
}

$eliteAttackProperties = @{
    $elitePaths[0] = @("NormalSingleAtk", "NormalMultiAtk")
    $elitePaths[1] = @("PhaseOneStateAtk", "PhaseOneNormalAtk", "PhaseOneBigAtk", "PhaseTwoCardAtk",
        "PhaseThreeAtk", "PhaseOneHp", "PhaseTwoHp")
    $elitePaths[2] = @("MaskMultiAttack", "TrueAttack", "TrueMultiAttack")
    $elitePaths[3] = @("S1Attack", "S2Attack", "S4Attack", "S4HighAttack")
}
foreach ($entry in $eliteAttackProperties.GetEnumerator()) {
    $content = Get-RepositoryContent $entry.Key
    foreach ($property in $entry.Value) {
        Assert-Matches $content "override\s+int\s+$property\s*=>\s*EliteStatScaler\.ScaleDown\(base\.$property,\s*StatMultiplier\)" `
            "$($entry.Key) 的 $property 必须从原 Boss 同名属性派生。"
    }
    Assert-NotMatches $content "override\s+int\s+\w*(Block|Heal|Count|Threshold)" `
        "$($entry.Key) 不得覆写格挡、治疗、次数或阶段门槛。"
}

$anonElite = Get-RepositoryContent $elitePaths[0]
$takiElite = Get-RepositoryContent $elitePaths[1]
$anonMultiplier = [decimal]([regex]::Match($anonElite, 'StatMultiplier\s*=\s*([\d.]+)m').Groups[1].Value)
$takiMultiplier = [decimal]([regex]::Match($takiElite, 'StatMultiplier\s*=\s*([\d.]+)m').Groups[1].Value)
$soyoElite = Get-RepositoryContent $elitePaths[2]
$raanaElite = Get-RepositoryContent $elitePaths[3]
$soyoMultiplier = [decimal]([regex]::Match($soyoElite, 'StatMultiplier\s*=\s*([\d.]+)m').Groups[1].Value)
$raanaMultiplier = [decimal]([regex]::Match($raanaElite, 'StatMultiplier\s*=\s*([\d.]+)m').Groups[1].Value)
Assert-True ($anonMultiplier -eq $takiMultiplier) "Anon 与 Taki 必须使用同一缩放口径。"
Assert-True ($soyoMultiplier -eq $raanaMultiplier) "Soyo 与 Raana 必须使用同一缩放口径。"
Assert-True ($anonMultiplier -gt $soyoMultiplier) "Anon/Taki 的缩放档位必须高于 Soyo/Raana。"

foreach ($binding in @(
        @($anonBase, "NormalSingleAtk"), @($anonBase, "NormalMultiAtk"),
        @($takiBase, "PhaseOneStateAtk"), @($takiBase, "PhaseOneNormalAtk"),
        @($takiBase, "PhaseOneBigAtk"), @($takiBase, "PhaseTwoCardAtk"), @($takiBase, "PhaseThreeAtk"),
        @($soyoBase, "MaskMultiAttack"), @($soyoBase, "TrueAttack"), @($soyoBase, "TrueMultiAttack"),
        @($raanaBase, "S1Attack"), @($raanaBase, "S2Attack"), @($raanaBase, "S4Attack"),
        @($raanaBase, "S4HighAttack"))) {
    $source = $binding[0]
    $property = $binding[1]
    Assert-Matches $source "AttackIntent\($property(?:,|\))" "$property 必须直接用于攻击意图。"
    Assert-Matches $source "DamageCmd\.Attack\($property\)" "$property 必须直接用于实际攻击命令。"
}

Assert-Matches $anonBase "ShouldGrantBossReward\s*=>\s*true" "原始 Anon 必须默认保留首领奖励。"
Assert-Matches $takiBase "ShouldGrantBossReward\s*=>\s*true" "原始 Taki 必须默认保留首领奖励。"
Assert-Matches $takiBase "ShouldEndRoomAfterEscape\s*=>\s*true" "原始 Taki 必须默认保留逃跑结束行为。"
Assert-Matches $anonElite "ShouldGrantBossReward\s*=>\s*false" "AnonElite 必须关闭首领专属奖励。"
Assert-Matches $takiElite "ShouldGrantBossReward\s*=>\s*false" "TakiElite 必须关闭首领专属奖励。"
Assert-Matches $takiElite "ShouldEndRoomAfterEscape\s*=>\s*false" "TakiElite 不得单独结束整个房间。"

$encounter = Get-RepositoryContent $encounterPath
$combatEndPatch = Get-RepositoryContent $combatEndPatchPath
Assert-Matches $encounter "class\s+BandMemberEncounter\s*:\s*CustomEncounterModel" `
    "BandMemberEncounter 必须继承 CustomEncounterModel。"
Assert-Matches $encounter "base\(RoomType\.Elite,\s*true\)" "Encounter 必须启用原生精英奖励。"
Assert-Matches $encounter "FullyCenterPlayers\s*=>\s*true" "Encounter 必须居中玩家队伍。"
Assert-Matches $encounter "return\s+false" "Encounter 必须拒绝自然 Act 合法性检查。"
foreach ($eliteName in @("AnonElite", "TakiElite", "SoyoElite", "RaanaElite")) {
    Assert-Matches $encounter "ModelDb\.Monster<$eliteName>" "AllPossibleMonsters 或生成映射缺少 $eliteName。"
}
Assert-Matches $encounter "SaveCustomState\s*\(" "Encounter 必须保存左右成员。"
Assert-Matches $encounter "LoadCustomState\s*\(" "Encounter 必须恢复左右成员。"
Assert-Matches $encounter "TryParseStableName" "恢复必须严格解析稳定成员名称。"
Assert-Matches $encounter "leftRewardEarned" "Encounter 必须持久化左侧奖励资格。"
Assert-Matches $encounter "rightRewardEarned" "Encounter 必须持久化右侧奖励资格。"
Assert-Matches $encounter "MarkRelicRewardEarned" "Encounter 必须按 Boss 原生死亡点记录奖励资格。"
Assert-Matches $encounter "override\s+(?:async\s+)?Task\s+AfterCombatEnd\s*\(" "Encounter 必须在整场胜利后结算遗物。"
Assert-Matches $encounter "GetSelectedMembersForReward" "奖励阶段必须读取既有成员状态。"
Assert-NotMatches ([regex]::Match($encounter, 'AfterCombatEnd[\s\S]*?(?=private\s+BandMemberSelection\s+GetSelectedMembersForReward)').Value) `
    "EnsureMemberSelection" "奖励阶段不得重新选择成员。"
Assert-Matches $combatEndPatch "DispatchBandMemberEncounterAfterCombatEnd" `
    "Hook.AfterCombatEnd 适配层必须显式派发 BandMemberEncounter 结算。"
Assert-Matches $combatEndPatch "await\s+DispatchBandMemberEncounterAfterCombatEnd" `
    "Hook.AfterCombatEnd 必须等待 BandMemberEncounter 奖励结算完成。"
Assert-Matches $encounter "MapPointHistory" "选择过程必须读取当前局地图历史。"
Assert-Matches $encounter "\.Rooms" "历史读取必须遍历 MapPointHistoryEntry 的房间记录。"
Assert-NotMatches $encounter "\bRng\b|Random" "成员选择不得依赖随机数。"

$coordinator = Get-RepositoryContent $coordinatorPath
Assert-Matches $coordinator "GetPower<SurroundedPower>" "夹击初始化必须检查已有 SurroundedPower。"
Assert-Matches $coordinator "HasPower<BackAttackLeftPower>" "左敌人 Power 必须幂等应用。"
Assert-Matches $coordinator "HasPower<BackAttackRightPower>" "右敌人 Power 必须幂等应用。"
Assert-Matches $coordinator "HittableEnemies\.FirstOrDefault" "逃跑刷新必须从剩余可攻击敌人判断方向。"
Assert-Matches $coordinator "surrounded\.AfterDeath" "逃跑刷新必须复用原生 SurroundedPower 死亡方向逻辑。"

$scene = Get-RepositoryContent $scenePath
$leftMatch = [regex]::Match($scene, 'name="LeftMember"[\s\S]*?position\s*=\s*Vector2\(([-\d.]+),\s*([-\d.]+)\)')
$rightMatch = [regex]::Match($scene, 'name="RightMember"[\s\S]*?position\s*=\s*Vector2\(([-\d.]+),\s*([-\d.]+)\)')
Assert-True ($leftMatch.Success -and $rightMatch.Success) "场景必须包含左右 Marker2D 槽位。"
$culture = [System.Globalization.CultureInfo]::InvariantCulture
$leftX = [decimal]::Parse($leftMatch.Groups[1].Value, $culture)
$rightX = [decimal]::Parse($rightMatch.Groups[1].Value, $culture)
Assert-True ($leftX -lt $rightX) "左槽位必须位于右槽位左侧。"
Assert-NotMatches $scene "Kaiser|kaiser" "乐队场景不得引用 Kaiser Crab 专属资源。"

$eng = Get-RepositoryContent $engEncountersPath | ConvertFrom-Json
$zhs = Get-RepositoryContent $zhsEncountersPath | ConvertFrom-Json
Assert-True ($eng.'STS2_TOMORIN_MOD-BAND_MEMBER_ENCOUNTER.title' -eq "Band") "英文 Encounter 标题无效。"
Assert-True ($zhs.'STS2_TOMORIN_MOD-BAND_MEMBER_ENCOUNTER.title' -eq "乐队") "中文 Encounter 标题无效。"

$design = Get-RepositoryContent $designPath
Assert-NotMatches $design "\bTODO\b" "当前任务设计文档不得保留 TODO 标志。"

$mainProject = Resolve-RepositoryPath "STS2_Tomorin_Mod.csproj"
$sts2DataDirOutput = @(& dotnet msbuild $mainProject -nologo -getProperty:Sts2DataDir)
Assert-True ($LASTEXITCODE -eq 0) "无法从主项目取得 Sts2DataDir。"
$sts2DataDir = ($sts2DataDirOutput | Select-Object -Last 1).Trim()
$testModsPath = (Join-Path ([System.IO.Path]::GetTempPath()) "STS2_Tomorin_Mod_BandMemberHarness_Mods") + `
    [System.IO.Path]::DirectorySeparatorChar
& dotnet test (Resolve-RepositoryPath "tests/BandMemberEncounterHarness/BandMemberEncounterHarness.csproj") `
    --no-build --no-restore --nologo --verbosity minimal `
    "-p:Sts2DataDir=$sts2DataDir" "-p:ModsPath=$testModsPath"
Assert-True ($LASTEXITCODE -eq 0) "BandMemberEncounter 生命周期集成 Harness 未通过。"

Write-Host "BandMemberEncounter selection, scaling, encounter, flanking lifecycle, scene and localization checks passed."
