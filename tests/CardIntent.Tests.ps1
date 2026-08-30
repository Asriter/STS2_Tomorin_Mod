$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot

# 将仓库相对路径解析成绝对路径。
function Resolve-RepositoryPath([string]$path) {
    return Join-Path $root $path
}

# 读取仓库中的 UTF-8 文本。
function Get-RepositoryContent([string]$path) {
    return Get-Content -LiteralPath (Resolve-RepositoryPath $path) -Raw -Encoding utf8
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

# 断言文本包含所需结构。
function Assert-Matches([string]$content, [string]$pattern, [string]$message) {
    Assert-True ($content -match $pattern) $message
}

# 断言文本不包含禁止结构。
function Assert-NotMatches([string]$content, [string]$pattern, [string]$message) {
    Assert-True ($content -notmatch $pattern) $message
}

$featureRoot = "Scripts/Enemy/CardIntents"
$requiredPaths = @(
    "$featureRoot/EnemyCardDefinition.cs",
    "$featureRoot/PreparedEnemyResolutionPlan.cs",
    "$featureRoot/EnemyPreparedResolutionPlanner.cs",
    "$featureRoot/EnemyCardCombatState.cs",
    "$featureRoot/EnemyActionMetricPlanner.cs",
    "$featureRoot/EnemyCardMaterialResolver.cs",
    "$featureRoot/EnemyMaterialReservation.cs",
    "$featureRoot/EnemyCardExecutionEngine.cs",
    "$featureRoot/EnemyCardSimulationContext.cs",
    "$featureRoot/EnemyActionProjectionService.cs",
    "$featureRoot/LiveActionProjection.cs",
    "$featureRoot/EnemyCollectionEffectResolver.cs",
    "$featureRoot/EnemyCardRuntimeSyncState.cs",
    "$featureRoot/EnemyCollectionInventory.cs",
    "$featureRoot/CardIntentMoveState.cs",
    "$featureRoot/Presentation/EnemyCardIntentPresentation.cs",
    "$featureRoot/Presentation/EnemyCardIntentPresentationBuilder.cs",
    "$featureRoot/View/EnemyCardDescriptionPresenter.cs",
    "$featureRoot/View/NEnemyIntentCardSlot.cs",
    "$featureRoot/View/NEnemyCardHoverPreview.cs",
    "$featureRoot/View/NCardListIntentView.cs",
    "$featureRoot/Intents/CardListIntent.cs",
    "$featureRoot/BaseCardIntentMonsterModel.cs",
    "$featureRoot/Test/CardIntentTestRules.cs",
    "$featureRoot/Test/CardIntentTestCardCatalog.cs",
    "$featureRoot/Test/CardIntentTestCollectionCatalog.cs",
    "$featureRoot/Test/CardIntentTestDeck.cs",
    "$featureRoot/Test/CardIntentTestMonster.cs",
    "$featureRoot/ShadowTomorin/ShadowTomorinBalance.cs",
    "$featureRoot/ShadowTomorin/ShadowTomorinCardCatalog.cs",
    "$featureRoot/ShadowTomorin/ShadowTomorinCollectionCatalog.cs",
    "$featureRoot/ShadowTomorin/ShadowTomorinDeck.cs",
    "$featureRoot/ShadowTomorin/ShadowTomorinRules.cs",
    "Scripts/Enemy/ShadowTomorin.cs",
    "Scripts/Encounters/ShadowTomorinBoss.cs",
    "Scripts/Powers/EnemyPowers/EnemyCollectionInventoryPower.cs",
    "Scripts/Powers/EnemyPowers/CardIntentSorrowfulRainPower.cs",
    "Scripts/Powers/EnemyPowers/CardIntentAdayumePower.cs",
    "Scripts/Encounters/CardIntentTestEncounter.cs",
    "tests/CardIntentHarness/CardIntentHarness.csproj",
    "tests/CardIntentHarness/DomainIdentityTests.testcs",
    "tests/CardIntentHarness/ActionPlannerTests.testcs",
    "tests/CardIntentHarness/MaterialResolverTests.testcs",
    "tests/CardIntentHarness/CollectionInventoryTests.testcs",
    "tests/CardIntentHarness/ExecutionEngineTests.testcs",
    "tests/CardIntentHarness/FrozenResolutionPlanTests.testcs",
    "tests/CardIntentHarness/LiveProjectionTests.testcs",
    "tests/CardIntentHarness/ReconnectStateTests.testcs",
    "tests/CardIntentHarness/DescriptionOverrideTests.testcs",
    "tests/CardIntentHarness/CardRowLayoutTests.testcs",
    "tests/CardIntentHarness/IntentPresentationTests.testcs",
    "tests/CardIntentHarness/ModelDbBootstrap.testcs",
    "tests/CardIntentModelDbHarness/CardIntentModelDbHarness.csproj",
    "tests/CardIntentModelDbHarness/Directory.Build.props",
    "tests/CardIntentModelDbHarness/CardIntentCatalogModelDbTests.testcs",
    "STS2_Tomorin_Mod/localization/eng/powers.json",
    "STS2_Tomorin_Mod/localization/zhs/powers.json")
foreach ($path in $requiredPaths) {
    Assert-FileExists $path
}

# 旧基础牌与旧 Snapshot 已被当前五牌区语义完整替代，不允许保留迁移分支。
foreach ($obsoletePath in @(
        "$featureRoot/Test/BasicEnemyAttackCard.cs",
        "$featureRoot/Test/BasicEnemyDefendCard.cs",
        "$featureRoot/Intents/CardAggregateAttackIntent.cs",
        "$featureRoot/EnemyPreparedRandomResult.cs",
        "$featureRoot/CardIntentRuntimeSnapshot.cs",
        "tests/CardIntentHarness/CardIntentDomainTests.testcs")) {
    Assert-True (-not (Test-Path -LiteralPath (Resolve-RepositoryPath $obsoletePath))) `
        "旧结构文件仍然存在：$obsoletePath"
}

$allFeatureSource = (Get-ChildItem -LiteralPath (Resolve-RepositoryPath $featureRoot) -Recurse -File -Filter "*.cs" |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw -Encoding utf8 }) -join "`n"
Assert-NotMatches $allFeatureSource "System\.Random|new\s+Random\s*\(" `
    "Card Intent 逻辑不得引入非战斗 RNG。"
Assert-NotMatches $allFeatureSource "CardModel\.OnPlay|\.OnPlay\s*\(" `
    "敌人牌目录和执行引擎不得调用玩家 CardModel.OnPlay。"

# 定义目录必须显式覆盖需求中的玩家牌模型和衍生牌，并以独立工厂创建重复副本。
$catalog = Get-RepositoryContent "$featureRoot/Test/CardIntentTestCardCatalog.cs"
foreach ($modelName in @(
        "SorrowfulRain", "Adayume", "NameOfTear", "StrikeTomorin", "WhyPlayHaruhikage",
        "ThisNoNeed", "DefendTomorin", "AtField", "HopeOnTheVoice", "CannotBeingHuman",
        "Woodlouse", "Hitoshizuku", "NamelessPaper", "HitoshizukuToken", "SongOfBeHuman",
        "Haruhikage", "PrideManSaki")) {
    Assert-Matches $catalog "\b$modelName\b" "敌人牌目录缺少玩家模型：$modelName"
}
Assert-Matches $catalog "CreateInitialDeckFactories" "敌人牌目录必须提供独立初始牌工厂。"
Assert-Matches $catalog "SelectMany[\s\S]*Enumerable\.Range" "每种初始牌必须由工厂建立独立重复副本。"

# 五牌区、收藏品与运行阶段只能由一个战斗级权威状态拥有。
$combatState = Get-RepositoryContent "$featureRoot/EnemyCardCombatState.cs"
foreach ($zoneName in @("DrawPile", "CurrentCards", "RetainedCards", "DiscardPile", "ExhaustPile")) {
    Assert-Matches $combatState "IReadOnlyList<BaseEnemyCard>\s+$zoneName" `
        "权威状态缺少只读牌区：$zoneName"
}
Assert-Matches $combatState "EnemyCollectionInventory\s+CollectionInventory" `
    "权威状态必须唯一拥有收藏品库存。"
Assert-Matches $combatState "AssertUniqueOwnership" "权威状态必须验证跨区域实例唯一性。"

# 规划器必须执行指标排除、双软锁、最后候选提交，并冻结素材实例引用。
$planner = Get-RepositoryContent "$featureRoot/EnemyActionMetricPlanner.cs"
Assert-Matches $planner "LastMetric" "规划器必须根据 LastMetric 排除上次指标。"
Assert-Matches $planner "StaticLocks\.Attack[\s\S]*StaticLocks\.Total[\s\S]*FullLocks\.Attack[\s\S]*FullLocks\.Total" `
    "规划器必须同时应用静态与完整投影的攻击和总评分软锁。"
Assert-Matches $planner "EnemyCandidateCommitMode\.ForcedOverLock" "规划器必须记录最后候选强制提交。"
Assert-Matches $planner "EnemyPreparedResolutionPlanner" "规划器必须通过唯一递归规划器冻结 DFS 行动。"
$resolutionPlanner = Get-RepositoryContent "$featureRoot/EnemyPreparedResolutionPlanner.cs"
Assert-Matches $resolutionPlanner "List<PreparedEnemyCardUnitPlan>[\s\S]*replayIndex" `
    "递归规划器必须提交逐次成功重放的独立单元。"
Assert-Matches $planner "CreateTransactionalClone|CreatePlanningSnapshot" `
    "准备阶段必须使用事务副本，不能提前修改权威牌区或收藏品。"

# 执行层必须只读取递归冻结计划，并且即时选择、回收和投影不得推进随机。
$engine = Get-RepositoryContent "$featureRoot/EnemyCardExecutionEngine.cs"
Assert-Matches $engine "ExecuteUnitPlanAsync" "执行引擎必须只消费冻结执行单元。"
Assert-Matches $engine "PreparedEnemyResolutionStep" "执行引擎必须按显式冻结步骤分派。"
Assert-NotMatches $engine "NextIndex" "执行阶段不得为即时抽牌、回收或生成结果推进 RNG。"
Assert-Matches $engine "StepLimit|stepLimit" "执行引擎必须具有有限步骤上限。"
$enemyExecutionContextSource = Get-RepositoryContent "$featureRoot/EnemyCardExecutionContext.cs"
Assert-NotMatches $enemyExecutionContextSource "\.Targeting\s*\(" `
    "敌人卡牌攻击只能面向全部玩家，执行上下文不得设置单体目标。"
Assert-Matches $enemyExecutionContextSource `
    "DamageCmd\.Attack\(amount\)[\s\S]*?\.WithHitCount\(hitCount\)[\s\S]*?\.FromMonster\(Owner\)[\s\S]*?\.Execute\(ChoiceContext\)" `
    "敌人卡牌攻击必须由单个 FromMonster 命令面向全部玩家执行。"

# 实时投影必须无战斗命令、无 RNG，并用输入指纹处理 Power 变化。
$projection = Get-RepositoryContent "$featureRoot/EnemyActionProjectionService.cs"
Assert-Matches $projection "BuildFingerprint" "实时投影必须提供完整输入指纹兜底缓存。"
Assert-Matches $projection "UnknownModifierIds" "未知第三方修改器必须产生不完整诊断。"
Assert-NotMatches $projection "PowerCmd|DamageCmd|CreatureCmd|NextIndex" `
    "实时投影不得执行战斗命令或推进 RNG。"
Assert-Matches $projection "PreparedEnemyCardUnitPlan" "实时投影必须遍历递归冻结执行单元。"
Assert-Matches $projection "RootSourceKey" "实时投影必须保持公开卡牌根来源。"

# 当前版本重连先构造临时状态，完整校验后才允许行动状态原子替换。
$sync = Get-RepositoryContent "$featureRoot/EnemyCardRuntimeSyncState.cs"
Assert-Matches $sync "CurrentSchemaVersion" "重连 DTO 必须拒绝旧结构版本。"
foreach ($syncMember in @(
        "DrawPile", "CurrentCards", "RetainedCards", "DiscardPile", "ExhaustPile",
        "AvailableCollections", "ConsumedCollections", "PreparedAction", "Cursor", "FaultDiagnostic")) {
    Assert-Matches $sync $syncMember "重连 DTO 缺少成员：$syncMember"
}
Assert-Matches $sync "EnemyCardDeckRegistry\.CreateCombatState" `
    "重连恢复必须先创建临时战斗状态。"
Assert-Matches $sync "restoredState\s*=\s*temporary" `
    "重连恢复只能在全量校验后返回临时状态。"
Assert-Matches $sync "PreparedEnemyCardUnitPlanSyncState|PreparedEnemyResolutionStepSyncState" `
    "重连 DTO 必须显式传输递归冻结计划。"
$cursor = Get-RepositoryContent "$featureRoot/EnemyCardExecutionCursor.cs"
Assert-Matches $cursor "StepPath" "重连游标必须使用递归步骤路径。"
Assert-NotMatches $cursor "ChildStepIndex" "重连游标不得保留旧单层子步骤索引。"
$monsterBase = Get-RepositoryContent "$featureRoot/BaseCardIntentMonsterModel.cs"
Assert-Matches $monsterBase "CaptureReconnectState" "怪物基类必须提供主机捕获入口。"
Assert-Matches $monsterBase "TryApplyReconnectState" "怪物基类必须提供客户端原子应用入口。"
Assert-Matches $monsterBase "ApplyValidatedCombatState" "怪物基类只能应用已验证的完整临时状态。"

# 正式影灯必须使用注册目录和唯一自循环状态，阶段迁移不得替换当前 MoveState。
$shadowMonster = Get-RepositoryContent "Scripts/Enemy/ShadowTomorin.cs"
$shadowCatalog = Get-RepositoryContent "$featureRoot/ShadowTomorin/ShadowTomorinCardCatalog.cs"
$shadowDeck = Get-RepositoryContent "$featureRoot/ShadowTomorin/ShadowTomorinDeck.cs"
Assert-Matches $shadowDeck "EnemyCardContentDirectory" "影灯必须注册正式阶段内容目录。"
Assert-Matches $shadowMonster "CardIntentMoveState\.Create" "影灯必须使用 CardIntent 行动状态。"
Assert-Matches $shadowMonster "FollowUpState\s*=\s*_cardState" "影灯唯一状态必须稳定自循环。"
Assert-NotMatches $shadowMonster "SetMoveImmediate" "影灯阶段切换不得立即替换当前行动。"
Assert-NotMatches $shadowCatalog "Utakotoba" "影灯正式卡池不得包含 Utakotoba。"
Assert-Matches $sync "EnemyCardDeckRegistry\.GetContentDirectory\(expectedDeckId\)[\s\S]*directory\.CreateDefinition" `
    "重连必须按同步 DeckId 从注册目录解析正式卡牌。"

# 动态逐牌视图必须按实例键复用、围绕角色头顶居中扩展，并使用唯一共享 Hover 与原版逐牌 Intent。
$listView = Get-RepositoryContent "$featureRoot/View/NCardListIntentView.cs"
$slotView = Get-RepositoryContent "$featureRoot/View/NEnemyIntentCardSlot.cs"
$hoverView = Get-RepositoryContent "$featureRoot/View/NEnemyCardHoverPreview.cs"
$cardListIntent = Get-RepositoryContent "$featureRoot/Intents/CardListIntent.cs"
Assert-NotMatches $listView "CardSlotCount|MaxDesignWidth" "动态卡列不得保留固定槽位或总宽缩放。"
Assert-Matches $listView "Dictionary<EnemyCardInstanceKey,\s*NEnemyIntentCardSlot>" `
    "动态卡列必须按实例键复用槽位。"
foreach ($layoutName in @("CenterAnchor", "CardRow", "ProjectionStatusHost", "HoverLayer")) {
    Assert-Matches $listView $layoutName "动态卡列缺少节点：$layoutName"
}
Assert-Matches $slotView "EffectRow" "每张缩略牌必须拥有独立效果行。"
foreach ($intentName in @("SingleAttackIntent", "MultiAttackIntent", "DefendIntent", "BuffIntent", "DebuffIntent", "UnknownIntent")) {
    Assert-Matches $slotView $intentName "逐牌槽位缺少原版 Intent 映射：$intentName"
    Assert-Matches $cardListIntent $intentName "CardListIntent 未预加载原版 Intent：$intentName"
}
Assert-Matches $listView "GetGlobalMousePosition" "Hover 必须在中央路径读取全局鼠标位置。"
Assert-Matches $listView "TryGetThumbnailGlobalRect[\s\S]*TryGetPreviewGlobalRect" `
    "Hover 命中必须先检查缩略牌，再用预览矩形保持。"
Assert-Matches $hoverView "MouseFilterEnum\.Ignore" "共享 Hover 必须保持鼠标输入穿透。"
Assert-Matches $listView "PowerApplied[\s\S]*PowerIncreased[\s\S]*PowerDecreased[\s\S]*PowerRemoved" `
    "视图必须订阅四类 Power 事件。"
Assert-Matches $listView "_bindingGeneration|bindingGeneration" "延迟刷新必须校验绑定世代。"
Assert-Matches $slotView "%DescriptionLabel" "描述覆写必须只写专用卡面的描述节点。"
Assert-Matches $slotView "UpdateVisuals[\s\S]*BuildOverrideText" `
    "描述覆写必须发生在原版视觉刷新之后。"

# 测试敌人保持显式 Encounter 隔离，不加入正常 Act、Stage 或 Patch。
$testMonster = Get-RepositoryContent "$featureRoot/Test/CardIntentTestMonster.cs"
Assert-Matches $testMonster "CardIntentMoveState\.Create" "测试敌人必须接入新的行动状态。"
Assert-Matches $testMonster "FollowUpState\s*=\s*\w+" "测试敌人的唯一状态必须循环到自身。"
$testEncounter = Get-RepositoryContent "Scripts/Encounters/CardIntentTestEncounter.cs"
Assert-Matches $testEncounter "IsValidForAct\s*\([\s\S]*?return\s+false" `
    "测试 Encounter 必须对所有正常 Act 返回 false。"
foreach ($normalContentPath in @("Scripts/Acts", "Scripts/Stage", "Scripts/Patch")) {
    $absolutePath = Resolve-RepositoryPath $normalContentPath
    if (Test-Path -LiteralPath $absolutePath) {
        $references = Get-ChildItem -LiteralPath $absolutePath -Recurse -File -Filter "*.cs" |
            Select-String -Pattern "CardIntentTestEncounter|CardIntentTestMonster"
        Assert-True ($null -eq $references) "测试内容不得被正常流程引用：$normalContentPath"
    }
}

# 双语 Power JSON 必须合法，且三类新增 Power 都有非空标题与描述。
$powerLocalizationKeys = @(
    "STS2_TOMORIN_MOD-CARD_INTENT_SORROWFUL_RAIN_POWER",
    "STS2_TOMORIN_MOD-CARD_INTENT_ADAYUME_POWER",
    "STS2_TOMORIN_MOD-ENEMY_COLLECTION_INVENTORY_POWER")
foreach ($language in @("eng", "zhs")) {
    $powers = Get-RepositoryContent "STS2_Tomorin_Mod/localization/$language/powers.json" | ConvertFrom-Json
    foreach ($key in $powerLocalizationKeys) {
        Assert-True (-not [string]::IsNullOrWhiteSpace($powers."$key.title")) `
            "$language Power 标题为空：$key"
        Assert-True (-not [string]::IsNullOrWhiteSpace($powers."$key.description")) `
            "$language Power 描述为空：$key"
    }

    $inventorySmartDescription =
        $powers."STS2_TOMORIN_MOD-ENEMY_COLLECTION_INVENTORY_POWER.smartDescription"
    Assert-Matches $inventorySmartDescription "\{Amount\}" `
        "$language 收藏品 Power 的动态描述必须显示 Amount。"
    Assert-NotMatches $inventorySmartDescription "\{Queue\}" `
        "$language 收藏品 Power 的动态描述当前版本不得引用未注入的 Queue。"
}

# 运行真实领域 Harness；路径由主项目 MSBuild 配置取得，不复制机器配置。
$mainProject = Resolve-RepositoryPath "STS2_Tomorin_Mod.csproj"
$sts2DataDirOutput = @(& dotnet msbuild $mainProject -nologo -getProperty:Sts2DataDir)
Assert-True ($LASTEXITCODE -eq 0) "无法从主项目取得 Sts2DataDir。"
$sts2DataDir = ($sts2DataDirOutput | Select-Object -Last 1).Trim()
Assert-True (Test-Path -LiteralPath (Join-Path $sts2DataDir "sts2.dll")) `
    "Sts2DataDir 不包含可供领域 Harness 使用的 sts2.dll。"
$testModsPath = (Join-Path ([System.IO.Path]::GetTempPath()) "STS2_Tomorin_Mod_CardIntentHarness_Mods") + `
    [System.IO.Path]::DirectorySeparatorChar
& dotnet test (Resolve-RepositoryPath "tests/CardIntentModelDbHarness/CardIntentModelDbHarness.csproj") `
    --nologo --verbosity minimal "-p:Sts2DataDir=$sts2DataDir" "-p:ModsPath=$testModsPath"
Assert-True ($LASTEXITCODE -eq 0) "Card Intent ModelDb 集成 Harness 未通过。"
& dotnet test (Resolve-RepositoryPath "tests/CardIntentHarness/CardIntentHarness.csproj") `
    --nologo --verbosity minimal "-p:Sts2DataDir=$sts2DataDir" "-p:ModsPath=$testModsPath"
Assert-True ($LASTEXITCODE -eq 0) "Card Intent 可执行领域 Harness 未通过。"

Write-Host "Card Intent domain, execution, projection, reconnect, localization and isolation contracts passed."
