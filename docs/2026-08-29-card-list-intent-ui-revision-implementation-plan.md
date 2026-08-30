# 敌人卡牌逐牌 Intent 与 Hover 预览实施计划

> 对应规格：`docs/2026-08-29-card-list-intent-ui-revision-design.md`
>
> 状态：Task 1-11 已完成并通过自动化验证；Task 12 实机验收待人工
>
> 执行记录：按确认后的任务拆分并行实施，保留用户工作区既有变更；本任务明确未使用 Basic Memory 或 `superpowers` 插件。

## 实施原则

- 逻辑层现有牌区、评分、卡牌效果和重连实现已经存在，本计划只做规格要求的冻结 DFS 计划补全、投影扩展与 UI 修订。
- 每个任务先新增会失败的聚焦测试，再写最小实现，再运行聚焦测试。
- 不用固定字面数值断言游戏配置；从 `CardModel.DynamicVars`、定义、规则对象或构造输入读取期望。
- 不新增 `.tscn`、图片或其他 Godot 资源；全部视图继续程序化创建，因此最终使用 `dotnet build`，不需要 `dotnet publish`。
- 不保留旧聚合攻击或固定五槽兼容分支。
- 每完成一项任务立即检查 `git diff -- <本任务文件>`，避免覆盖工作区中的无关改动。

## Task 1：为递归冻结计划建立失败测试和不可变类型

**文件：**

- 新增 `Scripts/Enemy/CardIntents/PreparedEnemyResolutionPlan.cs`
- 新增 `tests/CardIntentHarness/FrozenResolutionPlanTests.testcs`
- 修改 `tests/CardIntent.Tests.ps1`

### 1.1 先写失败测试

在 `FrozenResolutionPlanTests.testcs` 覆盖：

- 一个来源的每个成功重放都具有独立 `PreparedEnemyCardUnitPlan`。
- `RootSourceKey` 与公开来源一致，`ExecutingCardKey` 可以是子牌。
- `OrderedSteps` 保持构造顺序且对外不可修改。
- `ControlledDirectOnly` 子单元不能携带递归素材支付步骤或作词结果步骤。
- 构造器拒绝空/非法实例键、负重放索引、空步骤元素、字段组合非法或与来源不一致的根键。
测试期望来自传入的实例键、定义与集合顺序，不比较配置字面数值。

同时在 `tests/CardIntent.Tests.ps1` 的必需文件列表加入新领域文件与测试文件。

### 1.2 运行测试并确认红灯

```powershell
dotnet test tests/CardIntentHarness/CardIntentHarness.csproj --nologo --verbosity minimal --filter "FullyQualifiedName~FrozenResolutionPlanTests"
```

预期：因递归计划类型尚不存在而编译失败。

### 1.3 实现类型

按规格建立：

- `EnemyPreparedExecutionMode`
- `EnemyPreparedRecoveryKind`
- `PreparedEnemyCardUnitPlan`
- 抽象 `PreparedEnemyResolutionStep`
- `PreparedDirectEffectsStep`
- `PreparedConsumedCardStep`
- `PreparedConsumedCollectionStep`
- `PreparedGeneratedCollectionStep`
- `PreparedComposeResultStep`
- `PreparedImmediateCardStep`
- `PreparedRecoveryStep`

所有输入在构造时复制到只读数组；构造器完成字段互斥、身份、重放和子树基本校验。本任务只新增独立计划类型，不迁移 `PreparedEnemyCardSource` 调用方。

### 1.4 运行聚焦测试

```powershell
dotnet test tests/CardIntentHarness/CardIntentHarness.csproj --nologo --verbosity minimal --filter "FullyQualifiedName~FrozenResolutionPlanTests"
```

预期：本任务测试与主项目编译均通过；现有执行路径尚未引用新计划类型。

## Task 2：准备阶段冻结完整 DFS 选择

**文件：**

- 新增 `Scripts/Enemy/CardIntents/EnemyPreparedResolutionPlanner.cs`
- 修改 `Scripts/Enemy/CardIntents/PreparedEnemyCardAction.cs`
- 修改 `Scripts/Enemy/CardIntents/EnemyActionMetricPlanner.cs`
- 删除 `Scripts/Enemy/CardIntents/EnemyPreparedRandomResult.cs`
- 修改 `Scripts/Enemy/CardIntents/EnemyCardCombatState.cs`（仅新增事务快照所需只读/克隆入口）
- 修改 `tests/CardIntentHarness/ActionPlannerTests.testcs`
- 修改 `tests/CardIntentHarness/FrozenResolutionPlanTests.testcs`

### 2.1 先写失败测试

新增场景：

- 最终提交候选冻结素材卡和收藏品的稳定实例引用。
- 灵感素材产生 `ControlledDirectOnly` 子单元，并保持 DFS 顺序。
- 收藏品生成结果在准备时确定，计划中记录定义 ID 与预期实例序号。
- 即时抽牌在准备时只推进一次注入 RNG，计划记录被选卡键。
- 回收卡牌与回收收藏品在准备时确定；卡牌回收产生即时子单元，收藏品回收不产生卡牌子单元。
- Immediate 作词结果记录现有实例增层或预计生成实例，并在需要时递归生成子单元。
- Retained 作词结果记录生成/增层但不执行子单元。
- 素材不足只冻结成功前缀并记录截断，后续重放不生成单元。
- 被软锁拒绝的候选不提交其事务牌区或计划；最终候选的 RNG 推进与实际调用序列一致。
- 循环、未知程序或步骤上限导致准备失败，不提交半成品行动。
- `PreparedEnemyCardSource.Units` 的重放索引连续且不越过 `MaximumAttempts` / `TruncationAttemptIndex`。

### 2.2 确认红灯

```powershell
dotnet test tests/CardIntentHarness/CardIntentHarness.csproj --nologo --verbosity minimal --filter "FullyQualifiedName~ActionPlannerTests|FullyQualifiedName~FrozenResolutionPlanTests"
```

### 2.3 实现事务规划器

`EnemyPreparedResolutionPlanner` 接受：

```csharp
PreparedEnemyCardSource PlanSource(
    BaseEnemyCard source,
    int maximumAttempts,
    EnemyPreparedPlanningState transaction,
    IEnemyCardRandomSource random,
    int stepLimit);
```

`EnemyPreparedPlanningState` 是当前候选牌区、保留区、消耗区、收藏品库存与下一生成序号的事务副本。规划器严格复刻当前 `EnemyCardExecutionEngine` 的 DFS 顺序，但只移动事务副本、选择稳定实例并建树，不执行游戏命令。

`PreparedEnemyCardSource` 在本任务一次性迁移为 `Units`；删除扁平 `MaterialBindings` / `RandomResults`、`EnemyPreparedRandomResult` 及其 `Collection/TryGetCollection` API，确保执行结构只有一个来源。

`EnemyActionMetricPlanner.CreatePreparedSources` 改为调用递归规划器，并只在最终候选提交时把事务结果与行动一起提交。

### 2.4 运行聚焦测试

```powershell
dotnet test tests/CardIntentHarness/CardIntentHarness.csproj --nologo --verbosity minimal --filter "FullyQualifiedName~ActionPlannerTests|FullyQualifiedName~FrozenResolutionPlanTests"
```

## Task 3：执行引擎只消费冻结计划

**文件：**

- 修改 `Scripts/Enemy/CardIntents/EnemyCardExecutionEngine.cs`
- 修改 `Scripts/Enemy/CardIntents/EnemyCardExecutionCursor.cs`
- 修改 `tests/CardIntentHarness/ExecutionEngineTests.testcs`

### 3.1 先写失败测试

- 执行准备行动时，即时抽取、回收、收藏品生成和 Immediate 作词不调用注入 RNG。
- 实际选择的卡牌/收藏品与冻结计划相同。
- 直接效果执行顺序、素材消费、子牌 DFS 和最终来源生命周期与计划一致。
- 同一来源的重放只执行 `Units` 中的成功单元。
- 计划实例不在预期区域、CardId 不匹配、预计生成序号不匹配时标记结构故障并停止后续步骤。
- 正常死亡/离场中止不误报结构故障。
- 安全游标能在递归步骤边界恢复到下一未执行步骤。

### 3.2 确认红灯

```powershell
dotnet test tests/CardIntentHarness/CardIntentHarness.csproj --nologo --verbosity minimal --filter "FullyQualifiedName~ExecutionEngineTests"
```

### 3.3 修改执行器

- 把 `ExecuteSourceAsync` 的选择/重放分支替换为 `ExecuteUnitPlanAsync` 与 `ExecuteStepAsync`。
- 将游标的单层 `ChildStepIndex` 替换为复制后只读的 `IReadOnlyList<int> StepPath`；每个整数表示当前递归层的下一步骤索引，捕获、克隆和恢复时验证所有分量非负且路径指向计划内边界。
- `IEnemyCardEffectNode.ExecuteAsync` 仍是直接效果唯一执行入口。
- 删除 `DrawAndExecuteImmediateAsync` / `RecoverAndResolveAsync` 内的 `random.NextIndex`；改为验证并消费计划选中的实例。
- 生成卡牌和收藏品时验证预期序号，再使用现有权威状态入口提交。
- 所有现有分辨事件继续在等价原子步骤发布。

### 3.4 运行聚焦测试

```powershell
dotnet test tests/CardIntentHarness/CardIntentHarness.csproj --nologo --verbosity minimal --filter "FullyQualifiedName~ExecutionEngineTests"
```

## Task 4：同步 DTO 往返递归计划并提升版本

**文件：**

- 修改 `Scripts/Enemy/CardIntents/EnemyCardRuntimeSyncState.cs`
- 修改 `tests/CardIntentHarness/ReconnectStateTests.testcs`

### 4.1 先写失败测试

- 捕获后 DTO 包含完整递归计划，但不包含对象引用或 `LiveActionProjection`。
- 捕获、恢复、再次捕获保持来源、单元、`StepPath`、实例键与顺序一致。
- 恢复后的执行结果与原状态执行结果结构一致。
- 旧 schema、未知步骤 kind、空根键、越界 replay、负数或越界 `StepPath` 分量、非法生成序号、悬空卡牌/收藏品引用、递归深度/总步骤越界全部整体拒绝。
- 拒绝时原运行状态不发生部分替换。

### 4.2 确认红灯

```powershell
dotnet test tests/CardIntentHarness/CardIntentHarness.csproj --nologo --verbosity minimal --filter "FullyQualifiedName~ReconnectStateTests"
```

### 4.3 实现同步

- 新增与每种 `PreparedEnemyResolutionStep` 对应的显式 sync record；禁止用运行时类型名或任意 object 序列化。
- `CurrentSchemaVersion` 递增。
- `CapturePreparedAction` 递归捕获。
- `RestorePreparedAction` 先递归验证到临时树，再构造运行时计划。
- 用现有执行 step limit 同时约束最大递归深度和总节点数。
- 删除旧扁平 `RandomResults` 的恢复分支，不接受旧 schema。

### 4.4 运行聚焦测试

```powershell
dotnet test tests/CardIntentHarness/CardIntentHarness.csproj --nologo --verbosity minimal --filter "FullyQualifiedName~ReconnectStateTests"
```

## Task 5：扩展完整 LiveActionProjection

**文件：**

- 修改 `Scripts/Enemy/CardIntents/EnemyCardSimulationContext.cs`
- 修改 `Scripts/Enemy/CardIntents/EnemyActionProjectionService.cs`
- 修改 `Scripts/Enemy/CardIntents/CardIntentMoveState.cs`
- 修改 `Scripts/Enemy/CardIntents/CardIntentMoveRuntime.cs`
- 修改 `Scripts/Enemy/CardIntents/Intents/CardListIntent.cs`
- 修改 `tests/CardIntentHarness/LiveProjectionTests.testcs`

### 5.1 先写失败测试

- `EnemyDamageHitProjection` 同时保存标准攻击效果的基础伤害与模拟后伤害。
- 每个 replay 同时具有 `RootSourceKey` 与 `ExecutingCardKey`。
- 灵感、收藏品、即时抽牌、回收牌与 Immediate 作词牌的结果全部归到正确根来源。
- 当前测试目录所有已知效果使 `IsComplete` 为真。
- 未知修改器、未知程序、非法计划或步骤限制使 `IsComplete` 为假并含稳定诊断。
- 相同计划与输入复用缓存；计划任一子步骤变化会改变指纹。
- 投影不调用 RNG/战斗命令，不修改牌区、ReplayCount、Power 或收藏品。

### 5.2 确认红灯

```powershell
dotnet test tests/CardIntentHarness/CardIntentHarness.csproj --nologo --verbosity minimal --filter "FullyQualifiedName~LiveProjectionTests"
```

### 5.3 实现投影与显示读取入口

- 按规格修改投影 DTO。
- 投影服务递归遍历 `PreparedEnemyCardUnitPlan.OrderedSteps`。
- `EnemyCardSimulationContext.BeginUnit` 接受根来源键和实际执行键。
- `BuildFingerprint` 递归包含所有计划字段。
- 在 `CardIntentMoveState` 增加不触发事件递归的 `GetLiveProjectionForDisplay(IReadOnlyList<Creature> targets)`：用传入目标顺序创建结构投影输入，取得/复用缓存，但不把本地最终攻击伤害写入投影。
- `CardIntentMoveRuntime` 与 `CardListIntent` 只读转发该入口。
- `NotifyStateChanged` 继续清除投影缓存；Power 变化不重建结构投影。

### 5.4 运行聚焦测试

```powershell
dotnet test tests/CardIntentHarness/CardIntentHarness.csproj --nologo --verbosity minimal --filter "FullyQualifiedName~LiveProjectionTests"
```

## Task 6：增加描述覆写数据契约与纯展示器

**文件：**

- 修改 `Scripts/Enemy/CardIntents/EnemyCardDefinition.cs`
- 修改 `Scripts/Enemy/CardIntents/BaseEnemyCard.cs`
- 修改 `Scripts/Enemy/CardIntents/Test/CardIntentTestCardCatalog.cs`
- 新增 `Scripts/Enemy/CardIntents/View/EnemyCardDescriptionPresenter.cs`
- 新增 `tests/CardIntentHarness/DescriptionOverrideTests.testcs`

### 6.1 先写失败测试

- 未传入覆写时得到非 null 空字符串。
- 非空文本由 `BaseEnemyCard` 只读转发。
- 覆写不改变 `CardDefinitionFingerprint`、卡牌身份、分数、效果或重连实例 DTO。
- presenter 对空字符串返回“不覆写”，对非空可信 BBCode 返回统一居中文本。
- 使用两张池化模拟卡连续绑定时，第二张空覆写不会沿用第一张文本。

测试目录选择一张现有测试卡配置临时写死描述，内容不参与数值断言。

### 6.2 确认红灯

```powershell
dotnet test tests/CardIntentHarness/CardIntentHarness.csproj --nologo --verbosity minimal --filter "FullyQualifiedName~DescriptionOverrideTests"
```

### 6.3 实现契约

- `EnemyCardDefinition` 构造器增加尾部可选参数 `string descriptionOverride = ""`，立即拒绝 null。
- `BaseEnemyCard.DescriptionOverride` 只读转发。
- 不修改 `CardDefinitionFingerprint`。
- `EnemyCardDescriptionPresenter` 只负责判断与居中包装，不直接修改 `CardModel`。

### 6.4 运行聚焦测试

```powershell
dotnet test tests/CardIntentHarness/CardIntentHarness.csproj --nologo --verbosity minimal --filter "FullyQualifiedName~DescriptionOverrideTests"
```

## Task 7：建立逐牌展示模型与映射测试

**文件：**

- 新增 `Scripts/Enemy/CardIntents/Presentation/EnemyCardIntentPresentation.cs`
- 新增 `Scripts/Enemy/CardIntents/Presentation/EnemyCardIntentPresentationBuilder.cs`
- 新增 `tests/CardIntentHarness/IntentPresentationTests.testcs`

### 7.1 先写失败测试

- 输出顺序与 `CardList` 一致，重复 CardId 按实例键分开。
- 子步骤按 `RootSourceKey` 归属。
- 相同基础单次伤害的全部 hit/replay 合并，命中数来自投影元素数量；不同基础伤害保持多个攻击展示并按首次出现顺序排列。
- 格挡、多个敌方 Power、多个玩家 Power 分别折叠为单一 Defend/Buff/Debuff。
- 最终顺序为 Attack、Defend、Buff、Debuff、Unknown。
- 不完整投影保留已知效果、设置 `RequiresGlobalUnknown` 并返回诊断。
- 投影缺卡、重复根键、非法伤害只让相关卡 Unknown，并返回错误。

### 7.2 确认红灯

```powershell
dotnet test tests/CardIntentHarness/CardIntentHarness.csproj --nologo --verbosity minimal --filter "FullyQualifiedName~IntentPresentationTests"
```

### 7.3 实现纯构建器

实现规格第 7 节的 record 与 `EnemyCardIntentPresentationBuilder.Build(...)`。构建器不得引用 Godot、`NCard`、`NIntent`、战斗命令或 LocalContext。

### 7.4 运行聚焦测试

```powershell
dotnet test tests/CardIntentHarness/CardIntentHarness.csproj --nologo --verbosity minimal --filter "FullyQualifiedName~IntentPresentationTests"
```

## Task 8：实现动态槽位与逐牌原版 Intent

**文件：**

- 新增 `Scripts/Enemy/CardIntents/View/NEnemyIntentCardSlot.cs`
- 重写 `Scripts/Enemy/CardIntents/View/NCardListIntentView.cs`
- 修改 `Scripts/Enemy/CardIntents/Intents/CardListIntent.cs`
- 删除 `Scripts/Enemy/CardIntents/Intents/CardAggregateAttackIntent.cs`
- 修改 `tests/CardIntent.Tests.ps1`

### 8.1 先写静态契约失败测试

在 `tests/CardIntent.Tests.ps1` 增加：

- 新槽位和展示构建器文件存在。
- `CardAggregateAttackIntent.cs` 不存在，源码不再引用该类型。
- `NCardListIntentView` 不含 `CardSlotCount`、`MaxDesignWidth` 或总宽度缩放分支。
- 视图按 `EnemyCardInstanceKey` 保存槽位映射。
- 视图存在 `HBoxContainer`、右锚、逐槽 `EffectRow` 和全局 Unknown Host。
- `CardListIntent.AssetPaths` 覆盖 Single/Multi/Defend/Buff/Debuff/Unknown 所需资源。

### 8.2 确认红灯

```powershell
pwsh -NoProfile -File tests/CardIntent.Tests.ps1
```

实施时预期：新 UI 契约在生产代码落地前先失败；领域 Harness 单独成功不代表该阶段通过。

### 8.3 实现槽位与 keyed reconciliation

- `NEnemyIntentCardSlot` 按展示模型绑定一个缩略 `NCard` 和自己的效果 `NIntent` 行。
- 攻击展示实例化原版 `SingleAttackIntent` / `MultiAttackIntent`；格挡、Buff、Debuff、Unknown 使用原版类型。
- 防御不增加任何数值 Label。
- 每个效果 `NIntent` 设为 `MouseFilter.Ignore`。
- `NCardListIntentView` 使用 `Dictionary<EnemyCardInstanceKey, NEnemyIntentCardSlot>` diff，按 `CardList` 重排 HBox 子节点。
- 右锚固定，容器宽度只向左增长；不添加超屏或换行分支。
- 对投影不完整调用 `Owner.ReportCardIntentError`，按投影指纹去重，并显示全局 Unknown。

### 8.4 运行静态契约与编译

```powershell
pwsh -NoProfile -File tests/CardIntent.Tests.ps1
dotnet build
```

## Task 9：实现共享 Hover 预览与描述应用

**文件：**

- 新增 `Scripts/Enemy/CardIntents/View/NEnemyCardHoverPreview.cs`
- 修改 `Scripts/Enemy/CardIntents/View/NEnemyIntentCardSlot.cs`
- 修改 `Scripts/Enemy/CardIntents/View/NCardListIntentView.cs`
- 修改 `Scripts/Enemy/CardIntents/View/EnemyCardDescriptionPresenter.cs`
- 修改 `tests/CardIntent.Tests.ps1`

### 9.1 先写静态契约失败测试

- 只存在一个 `NEnemyCardHoverPreview` 字段/节点，不为每槽持有放大卡。
- 有缩略矩形优先、预览矩形保持和离开两者清理的中央命中路径。
- 预览不进入 `CardRow`，并设置输入穿透。
- 描述应用发生在原版 `UpdateVisuals` 之后，使用 `%DescriptionLabel`。
- 空覆写路径不写 Label，复用前清理状态。

### 9.2 确认红灯

```powershell
pwsh -NoProfile -File tests/CardIntent.Tests.ps1
```

### 9.3 实现 Hover

- `NEnemyCardHoverPreview` 懒取得一个池化 `NCard`，切牌时换绑，隐藏/解绑时归还。
- `NCardListIntentView` 统一进行全局鼠标命中；缩略牌优先于预览。
- 放大牌以缩略牌中心为基准放入前景层，使用规格参数，不改变 HBox。
- 缩略牌和放大牌都在原版视觉更新后调用描述 presenter。
- `_ExitTree` 与 `Unbind` 清理预览和所有卡槽。

### 9.4 运行静态契约与编译

```powershell
pwsh -NoProfile -File tests/CardIntent.Tests.ps1
dotnet build
```

## Task 10：接入本地玩家实时伤害刷新和 Patch 降级

**文件：**

- 修改 `Scripts/Enemy/CardIntents/View/NEnemyIntentCardSlot.cs`
- 修改 `Scripts/Enemy/CardIntents/View/NCardListIntentView.cs`
- 修改 `Scripts/Patch/NIntentCardListPatch.cs`
- 修改 `tests/CardIntent.Tests.ps1`

### 10.1 先写静态/可执行失败测试

- 源码订阅 owner 与 `LocalContext.GetMe(owner.CombatState).Creature` 的四类 Power 事件。
- 刷新使用 deferred/coalesced 标记与绑定世代号，不在事件栈直接递归调用。
- `Unbind` 和 owner/local player 切换解除全部订阅。
- Power 刷新只调用攻击 `NIntent.UpdateIntent`，不修改 `PreparedEnemyCardAction` 或牌区。
- Patch 对普通 Intent、空卡列、Faulted、投影不完整和视图异常走规格定义的 Holder 显隐路径。

### 10.2 确认红灯

```powershell
pwsh -NoProfile -File tests/CardIntent.Tests.ps1
```

### 10.3 实现事件桥

- 绑定时解析 owner 和本地玩家并订阅四类事件。
- 事件处理器只调用 `QueueAttackIntentRefresh()`。
- 下一帧校验绑定世代和节点有效性后，对所有现存攻击节点调用原版 `UpdateIntent`。
- Patch 继续负责整视图失败回退；投影不完整不隐藏已知卡列，只显示全局 Unknown。
- 描述单卡失败只记录错误并保留原版描述。

### 10.4 运行静态契约与编译

```powershell
pwsh -NoProfile -File tests/CardIntent.Tests.ps1
dotnet build
```

## Task 11：全量自动化验证与文档收尾

**文件：**

- 修改 `docs/TODO.md`
- 修改 `docs/2026-08-29-card-list-intent-ui-revision-design.md`（只记录实际验证结果，不改已确认行为）

### 11.1 全量测试

```powershell
pwsh -NoProfile -File tests/CardIntent.Tests.ps1
dotnet test tests/CardIntentHarness/CardIntentHarness.csproj --nologo --verbosity minimal
dotnet build
```

必须逐项确认：

- PowerShell 静态契约通过。
- Harness 全部领域测试通过。
- 主项目编译通过且没有新增警告被忽略。
- `rg -n "CardAggregateAttackIntent|CardSlotCount|MaxDesignWidth" Scripts/Enemy/CardIntents Scripts/Patch` 无旧结构命中。
- `rg -n "NextIndex" Scripts/Enemy/CardIntents/EnemyCardExecutionEngine.cs Scripts/Enemy/CardIntents/EnemyActionProjectionService.cs` 不再命中即时抽取/回收或投影路径。

### 11.2 Spec 自审

按以下三项做实现自审：

1. 覆盖检查：规格每个完成条件都能映射到代码改动、自动化测试或实机验收。
2. 占位检查：新增代码不存在未完成标记、stub、空实现、主动抛出“尚未实现”异常或仅为通过静态正则的死代码。
3. 类型检查：计划、投影、展示模型、Godot 视图和 sync DTO 对同一字段使用一致类型与命名。

### 11.3 更新 TODO

仅在自动化验证真实通过后勾选对应实施项；以下项目在实机完成前保持未勾选：

- 单机布局/Hover/点击穿透验收。
- 多人不同本地玩家伤害验收。
- 重连中断恢复与池化生命周期验收。
- 描述本地化迁移和游戏版本更新复核。

## Task 12：实机验收

本任务必须由能够启动游戏的执行环境完成，结果记录到设计文档“验证记录”与 `docs/TODO.md`。

### 12.1 单机

- 启动显式 `CardIntentTestEncounter`。
- 验证卡列右锚不动、数量增加只向左扩展、无固定五张限制。
- 验证每张牌只显示自己的效果；防御无数值，多段攻击为单次伤害乘次数。
- 快速跨牌 Hover、移入预览、移出两者，确认共享预览无残留且不重排。
- 点击被放大卡覆盖区域下的合法战斗目标，确认输入未被卡牌吞掉。
- 触发写死描述牌与普通描述牌，确认池化切换无文本泄漏。
- 改变怪物和本地玩家相关 Power，确认攻击标签刷新。

### 12.2 多人与重连

- 两个客户端观察同一行动，分别改变各自玩家防御/受伤修正，确认伤害标签可以不同而卡牌结构相同。
- 在安全原子步骤边界断线重连，确认递归计划、牌区、收藏品、ReplayCount 与下一执行游标一致。
- 重连后再次改变 Power，确认旧订阅没有重复刷新，新视图正常刷新。

### 12.3 故障演练

- 注入测试用未知投影节点，确认记录错误、保留已知逐牌图标并出现全局 Unknown。
- 模拟 `%DescriptionLabel` 缺失，确认只回退该牌原版描述。
- 模拟根 Holder 兼容失败，确认整组恢复原版 Unknown，逻辑行动仍可结算。

## 交付检查表

- [x] 递归冻结计划是执行与投影唯一结构来源。
- [x] 执行阶段的即时抽取/回收不再推进 RNG。
- [x] 重连 DTO 已提升版本并完整验证递归计划。
- [x] 当前已知测试牌投影完整。
- [x] 展示模型按实例键逐牌归属并正确归并效果。
- [x] 固定五槽与聚合攻击已经删除。
- [x] 动态左扩布局、逐牌 Intent、共享 Hover 和描述覆写完成。
- [x] 原版攻击 Intent 按本地玩家实时刷新。
- [x] 自动化测试与 `dotnet build` 通过。
- [ ] 单机、多人、重连和故障实机验收完成。
- [x] `docs/TODO.md` 与设计验证记录已按事实更新。

## 自动化执行结果

- `tests/CardIntent.Tests.ps1`：Windows PowerShell 下通过，并继续执行 Harness 与主项目构建。
- `dotnet test tests/CardIntentHarness/CardIntentHarness.csproj --nologo --verbosity minimal`：54/54 通过，0 失败、0 跳过。
- `dotnet build`：0 错误；输出仍包含仓库既有的可空性、Godot 源生成器和本地同名类型警告。
- 旧 `CardAggregateAttackIntent`、固定槽位／总宽缩放字段、旧随机结果与平面游标引用均已移除；执行和投影路径不调用 `NextIndex`。
- 未改动 Godot 资源，不需要为本次代码修订重新导出 `.pck`。
- Task 12 保持未完成，必须在可启动游戏并具备多人／重连条件的环境中执行。
