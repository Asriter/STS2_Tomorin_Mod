# 敌人卡牌逐牌 Intent 与 Hover 预览修订规格

> 状态：代码与自动化验证已完成；单机、多人和真实重连实机验收待人工
>
> 适用代码基线：2026-08-29 当前工作区
>
> 关键词标题：**敌人卡牌逐牌 Intent｜动态左扩卡列｜共享 Hover 预览｜原版实时伤害｜描述覆写｜冻结 DFS 投影**
>
> 本文是 `2026-08-28-card-list-intent-design.md` 的增量修订。旧文档中的敌人卡牌领域模型、牌区、规划、执行、重连和测试敌人设计继续有效；其“固定五槽、卡牌无 Hover、聚合攻击 Intent”UI 结论由本文取代。

## 1. 目标与范围

本次只修改敌人卡牌 Intent 的投影与表现层，使当前已经完成的卡牌逻辑可以被玩家逐牌读懂：

- 当前行动中的每张敌人卡牌都显示自己的缩略卡图。
- 每张卡牌下方独立显示该牌预期造成的攻击、防御、Buff、Debuff 等类别。
- 鼠标悬停缩略牌时，在前景显示一张共享的放大卡牌，展示完整卡面与描述。
- 卡列不再限制五张；保持右边缘固定，按照 `CardList` 顺序向左扩展，不缩放、不换行、不裁剪。
- 不再显示整组行动的聚合攻击或聚合防御层。
- 攻击图标继续复用原版 `SingleAttackIntent` / `MultiAttackIntent`，并按本地玩家当前 Power 实时显示该玩家将受到的伤害。
- 对当前已知的素材、重放、收藏品、即时牌、作词结果和 DFS 子步骤建立完整投影；不完整投影视为 Bug，记录错误后使用 Unknown 提示兜底。
- 敌人卡牌允许提供暂时写死的描述覆写文本；非空时仅替换敌人 Intent 卡面的描述，空字符串继续显示原 `CardModel` 描述。

## 2. 明确非目标

- 不修改敌人卡牌的评分、抽牌配方、牌区归属、执行顺序、伤害结算、Power 结算或卡牌数值。
- 不让缩略牌或 Intent 图标可点击、可拖拽、可选择或可聚焦。
- 不实现卡列超出屏幕后的压缩、滚动、裁剪、换行或自动避让。
- 不显示格挡数值；防御仍与原版 `DefendIntent` 表现一致。
- 不为 Buff/Debuff 的每一种 Power 制作独立图标。
- 不在本次把写死描述迁移到本地化文件；迁移任务进入 TODO。
- 不把 `LiveActionProjection` 作为新的战斗权威状态，也不把本地实时数值写入重连 DTO。
- 不实现逐牌消失、素材移动或结算回放动画。

## 3. 当前代码核对结论

### 3.1 当前 UI 与数据通路

- `CardListIntent` 是 `UnknownIntent` 子类，负责把 `CardList`、`DeckId`、故障状态、`LiveProjection` 和刷新事件暴露给 UI。
- `NIntentCardListPatch` 在原版 `NIntent.UpdateIntent` 完成后隐藏原 Holder，创建或复用 `NCardListIntentView`；失败时恢复原版 Unknown。
- `NCardListIntentView` 当前写死 `CardSlotCount = 5`，使用整体最大宽度缩放，并只创建一个聚合攻击和一个防御 Intent。
- `CardAggregateAttackIntent` 当前逐牌调用原版伤害预览后求和；该聚合层与新需求冲突，应删除。
- `CardIntentMoveState.RefreshLiveProjection` 已存在，但生产 UI 尚未负责构造并刷新投影。

### 3.2 当前投影缺口

- `EnemyActionProjectionService` 当前只模拟 `Definition.Effects`。
- 素材绑定缺失、收藏品生成、即时 Token 会把投影标记为不完整。
- `EnemyPreparedRandomResult` 当前只冻结收藏品生成结果。
- `DrawAndExecuteImmediateAsync` 与 `RecoverAndResolveAsync` 当前仍在执行时调用战斗 RNG；这会使 UI 无法在行动公开后完整预测 DFS 子步骤，也与“准备后随机结果冻结”的既有设计目标不一致。
- `PreparedEnemyCardSource.RandomResults` 已经以稳定文本进入 `PreparedEnemyCardSourceSyncState.RandomResults`，说明现有同步边界只接受稳定数据；新递归计划将以显式 DTO 取代该扁平字段，同样不传输对象引用。

### 3.3 原版攻击 Intent 与多人显示

已核对当前游戏程序集：

- `AttackIntent.GetSingleDamage()` 通过 `LocalContext.GetMe(owner.CombatState)` 获取本机玩家，再调用原版 `Hook.ModifyDamage`。
- `SingleAttackIntent` 与 `MultiAttackIntent` 因而天然按每个客户端的本地玩家状态计算不同伤害。
- `NIntent.UpdateVisuals()` 会重新调用攻击 Intent 并刷新图标与标签。
- 原版只会在怪物自身 Power 变化时自动刷新该怪物 Intent；本地玩家 Power 变化不会可靠触发所有怪物 Intent 刷新。
- `Creature` 提供 `PowerApplied`、`PowerIncreased`、`PowerDecreased`、`PowerRemoved` 事件，可用于补齐本地玩家 Power 变化刷新。

因此本规格采用混合模型：

- `LiveActionProjection` 决定卡牌归属、重放、截断、DFS 子步骤和效果结构。
- 攻击 Intent 只接收标准攻击效果的基础单次伤害与命中次数。
- 最终显示数值继续由原版攻击 Intent 针对本地玩家实时计算。

## 4. 总体架构

```text
PreparedEnemyCardAction
  -> 冻结 DFS 行动计划
  -> EnemyActionProjectionService
  -> LiveActionProjection（结构权威、非战斗权威）
  -> EnemyCardIntentPresentationBuilder
  -> 每张卡的展示描述
  -> NCardListIntentView
       |- 动态卡列
       |- 每卡 Intent 行
       |- 全局 Unknown 兜底
       `- 共享 Hover NCard

owner / 本地玩家 Power 事件
  -> 合并到下一帧的 UpdateIntent
  -> 原版 AttackIntent 重新计算本地伤害
```

核心约束：

1. `EnemyCardCombatState` 和 `PreparedEnemyCardAction` 仍是执行所依据的领域状态。
2. `LiveActionProjection` 是从冻结行动派生的只读结果；投影失败不能修改牌区或停止已合法准备的行动。
3. UI 只消费 `CardList`、投影和原版 Intent，不自行猜测卡牌执行结果。
4. 所有卡牌与投影的关联使用 `EnemyCardInstanceKey`，禁止按数组索引或 `CardId` 关联。

## 5. 冻结 DFS 行动计划

### 5.1 采用显式递归计划，而不是继续堆叠松散随机值

为保证实际执行与投影遍历同一结构，在 `PreparedEnemyCardSource` 外壳下为每次成功重放加入不可变 `PreparedEnemyCardUnitPlan`。建议契约：

```csharp
public sealed record PreparedEnemyCardUnitPlan(
    EnemyCardInstanceKey RootSourceKey,
    EnemyCardInstanceKey ExecutingCardKey,
    EnemyCardId ExecutingCardId,
    int ReplayIndex,
    EnemyPreparedExecutionMode Mode,
    IReadOnlyList<EnemyMaterialReservation> MaterialReservations,
    IReadOnlyList<PreparedEnemyResolutionStep> OrderedSteps);

public enum EnemyPreparedExecutionMode
{
    Normal,
    ControlledDirectOnly
}

public abstract record PreparedEnemyResolutionStep;

public sealed record PreparedDirectEffectsStep(
    IReadOnlyList<string> EffectProgramIds) : PreparedEnemyResolutionStep;

public sealed record PreparedConsumedCardStep(
    EnemyCardInstanceKey MaterialKey,
    PreparedEnemyCardUnitPlan? ControlledChild) : PreparedEnemyResolutionStep;

public sealed record PreparedConsumedCollectionStep(
    string CollectionInstanceId,
    string CollectionId,
    IReadOnlyList<PreparedEnemyResolutionStep> Children) : PreparedEnemyResolutionStep;

public sealed record PreparedGeneratedCollectionStep(
    string CollectionId,
    long ExpectedSequence) : PreparedEnemyResolutionStep;

public sealed record PreparedComposeResultStep(
    EnemyCardId ResultCardId,
    EnemyCardInstanceKey ResultInstanceKey,
    EnemyCardTokenTiming Timing,
    bool IncreasesExistingReplay,
    PreparedEnemyCardUnitPlan? ImmediateChild) : PreparedEnemyResolutionStep;

public sealed record PreparedImmediateCardStep(
    EnemyCardInstanceKey SelectedCardKey,
    PreparedEnemyCardUnitPlan Child) : PreparedEnemyResolutionStep;

public sealed record PreparedRecoveryStep(
    EnemyPreparedRecoveryKind Kind,
    string SelectedInstanceId,
    PreparedEnemyCardUnitPlan? ImmediateCardChild) : PreparedEnemyResolutionStep;
```

`PreparedEnemyCardSource` 保留 `SourceCard`、`MaximumAttempts` 和 `TruncationAttemptIndex`，并把当前扁平的 `MaterialBindings` / `RandomResults` 迁移为 `Units`。不保留两套并行的执行结构，避免投影和执行再次漂移。

### 5.2 准备阶段

新增 `EnemyPreparedResolutionPlanner`，在候选事务副本上按与执行引擎一致的深度优先顺序构建计划：

1. 按来源牌和重放顺序解析素材。
2. 把被消费的卡牌或收藏品立即移动到事务副本中的对应区域。
3. 灵感素材生成 `ControlledDirectOnly` 子单元。
4. 收藏品效果按当前目录解析；即时抽牌和回收只在此处推进战斗 RNG，并记录选中的稳定实例键。
5. 收藏品生成记录定义 ID 和预期序号。
6. 作词结果记录“增加现有实例 ReplayCount”或“创建指定运行时实例”；Immediate 结果继续递归生成子单元。
7. 达到执行步骤上限、出现循环、未知收藏品程序、未知第三方效果或非法实例引用时，准备失败并进入现有故障通路，不提交半成品计划。

候选被软锁拒绝时，其 RNG 消耗保持既有规则；只有最终提交候选的 DFS 计划进入权威状态。

### 5.3 执行阶段

`EnemyCardExecutionEngine` 改为消费 `PreparedEnemyCardUnitPlan.OrderedSteps`：

- 不再在即时抽取或回收时调用 RNG。
- 每一步验证计划中的稳定实例仍处于预期区域；不一致属于结构 Bug，沿现有 `MarkFault` 路径停止后续结算。
- 直接效果仍调用现有 `IEnemyCardEffectNode.ExecuteAsync`，不把伤害或 Power 规则复制进计划。
- 计划只冻结选择与遍历结构，不冻结 Power 修正后的数值。
- 每个原子步骤继续发布现有分辨事件和安全游标。

### 5.4 重连

同步 DTO 增加与递归计划同构、但只含稳定字符串/数值/枚举的 DTO；`EnemyCardRuntimeSyncState.CurrentSchemaVersion` 递增。恢复时必须：

- 验证所有卡牌、收藏品和生成序号引用。
- 验证 `RootSourceKey` 属于公开来源，`ExecutingCardKey` 与 CardId 一致。
- 验证步骤类型与字段互斥关系、重放范围、递归深度和步骤总量。
- 将现有单层 `ChildStepIndex` 游标替换为 `IReadOnlyList<int> StepPath`；路径每一项都是该递归层的下一步骤索引。
- 先在临时状态完整恢复并验证，再原子替换。
- 拒绝旧版本或非法递归计划并要求主机重发；不做静默兼容。

实时投影本身仍不进入重连 DTO；客户端从恢复后的冻结计划重新派生。

## 6. LiveActionProjection 修订

### 6.1 DTO

```csharp
public sealed record EnemyDamageHitProjection(
    decimal BaseDamage,
    decimal ProjectedDamage);

public sealed record EnemyTargetProjection(
    string TargetId,
    IReadOnlyList<EnemyDamageHitProjection> DamageHits,
    IReadOnlyDictionary<string, decimal> PowerDeltas);

public sealed record EnemyCardReplayProjection(
    EnemyCardInstanceKey RootSourceKey,
    EnemyCardInstanceKey ExecutingCardKey,
    EnemyCardId ExecutingCardId,
    int ReplayIndex,
    IReadOnlyList<EnemyTargetProjection> Targets,
    decimal EnemyBlockDelta,
    IReadOnlyDictionary<string, decimal> EnemyPowerDeltas,
    IReadOnlyList<EnemyCollectionProjection> CollectionDeltas,
    IReadOnlyList<EnemyGeneratedCardProjection> GeneratedCards);
```

- `RootSourceKey` 决定结果显示在哪张公开卡牌下。
- `ExecutingCardKey` / `ExecutingCardId` 供诊断和未来动画识别真正执行的子牌。
- `BaseDamage` 用于构造原版攻击 Intent；`ProjectedDamage` 保留领域测试与诊断价值，但 UI 不直接把它当作本地最终伤害。
- 子牌、灵感、收藏品与即时结果都保留同一个 `RootSourceKey`。

### 6.2 投影算法

`EnemyActionProjectionService` 只遍历已冻结计划，不调用 RNG、战斗命令或可变游戏 Hook：

- 按计划的 DFS 顺序模拟直接效果、素材子效果、收藏品效果、即时牌和作词子牌。
- 重放截断直接来自冻结计划，不在 UI 重新判断素材。
- 同时记录生成/消费/回收结果，便于诊断和未来执行动画扩展。
- 缓存指纹包含递归计划、效果程序 ID、重放和截断信息、投影输入以及未知修改器集合。
- 当前目录与标准效果全部可解析时 `IsComplete = true`。
- 只有未知第三方修改器、未知效果程序、非法冻结结构、循环或步骤上限才允许 `IsComplete = false`。

## 7. 逐牌展示模型

新增无 Godot 依赖的 `EnemyCardIntentPresentationBuilder`，输入 `CardList` 与 `LiveActionProjection`，输出保持卡列顺序的不可变展示模型：

```csharp
public sealed record EnemyCardIntentPresentation(
    EnemyCardInstanceKey CardInstanceKey,
    BaseEnemyCard Card,
    IReadOnlyList<EnemyCardEffectIntentPresentation> Effects);

public abstract record EnemyCardEffectIntentPresentation;
public sealed record EnemyAttackPresentation(decimal BaseDamage, int HitCount)
    : EnemyCardEffectIntentPresentation;
public sealed record EnemyDefendPresentation : EnemyCardEffectIntentPresentation;
public sealed record EnemyBuffPresentation : EnemyCardEffectIntentPresentation;
public sealed record EnemyDebuffPresentation : EnemyCardEffectIntentPresentation;
public sealed record EnemyUnknownPresentation(string Diagnostic)
    : EnemyCardEffectIntentPresentation;

public sealed record EnemyCardListPresentation(
    IReadOnlyList<EnemyCardIntentPresentation> Cards,
    bool RequiresGlobalUnknown,
    IReadOnlyList<string> Diagnostics);
```

归并规则：

- 先按 `RootSourceKey` 把所有重放和 DFS 子步骤归到公开卡牌。
- 攻击：同一根来源下相同基础单次伤害的全部命中/重放合并为一个攻击展示；单次使用 `SingleAttackIntent`，多次使用 `MultiAttackIntent`，标签为原版“单次伤害 × 次数”，不显示总伤害。
- 不同基础单次伤害不能错误合并；按首次出现顺序保留多个攻击图标。
- 防御：只要该卡归属结果包含正向敌人格挡变化，显示一个 `DefendIntent`，不显示格挡数值。
- Buff：敌人自身 Power 变化映射为一个 `BuffIntent`。
- Debuff：玩家目标 Power 变化映射为一个 `DebuffIntent`。
- 同一张牌的多个 Buff 合并为一个类别图标；多个 Debuff 同理。完整效果顺序与具体内容由卡牌文本承担。
- 固定类别顺序为：攻击、格挡、Buff、Debuff、Unknown。多个不同攻击图标保留各自首次出现顺序。
- 无法映射到标准攻击基础值、非法负命中、未知效果或投影缺项时，记录诊断并为该卡添加 Unknown。
- `LiveActionProjection.IsComplete == false` 时保留全部已知逐牌图标，并额外在卡列级显示一个全局 Unknown。

投影不完整是 Bug，不是普通玩法状态。每个新投影指纹至少记录一次带 `StateId`、来源键和诊断的错误；Unknown 只是可见提示与安全兜底，不能代替日志。

## 8. Godot 视图

### 8.1 节点层级

```text
NCardListIntentView
|- RightAnchor
|  `- CardRow (HBoxContainer)
|     `- NEnemyIntentCardSlot [0..n]
|        |- ThumbnailHost
|        |  `- NCard
|        `- EffectRow
|           `- NIntent [0..m]
|- ProjectionStatusHost
|  `- NIntent (optional Unknown)
`- HoverLayer
   `- NCard (最多一个共享预览)
```

`NEnemyIntentCardSlot` 负责一张缩略牌与它的效果行；`NCardListIntentView` 负责列表 diff、全局 Unknown、Power 订阅和中央 Hover 命中测试；`NEnemyCardHoverPreview` 负责共享放大牌的取得、绑定、覆写描述和归还。

### 8.2 动态布局

- `CardRow` 的右边缘固定在现有 Intent 根的设计锚点。
- 槽位的视觉顺序严格等于 `CardList`：列表第一张位于最左，最后一张位于最右。
- 数量变化时只改变容器最小宽度，增长方向只向左。
- 不保留 `CardSlotCount`、`MaxDesignWidth` 或按总宽度缩放逻辑。
- 不换行、不滚动、不裁剪、不处理超出屏幕。
- `EffectRow` 位于各自卡牌正下方，与邻牌效果互不聚合。

### 8.3 按实例键增量复用

刷新时执行 keyed reconciliation：

1. 以 `EnemyCardInstanceKey` 建立旧槽位索引。
2. 按新 `CardList` 顺序复用并移动仍存在的槽位。
3. 为新增键创建槽位并从原版池取得 `NCard` / `NIntent`。
4. 对移除键解除事件、清理描述覆写并归还所有池化节点。
5. 同一键但展示效果变化时，只 diff 该槽位的效果节点。

禁止按 `CardId` 复用，因为同一牌组可包含同定义的多个实例。

### 8.4 输入与 Hover

- 缩略牌和效果图标不使用各自的点击/拖拽行为；效果 `NIntent.MouseFilter = Ignore`。
- `NCardListIntentView` 在 `_GuiInput` / 全局鼠标位置下统一命中测试缩略牌矩形。
- 缩略牌命中优先于当前放大预览；指针从一张缩略牌直接移动到另一张时立即切换共享预览。
- 当指针离开缩略牌但仍位于当前预览矩形内时，预览继续保持。
- 离开两者后归还预览节点并清空当前 Hover 键。
- 放大预览自身 `MouseFilter = Ignore`，只作为命中区域参与中央测试，不阻挡敌人、玩家或其他战斗点击。
- 不响应点击、拖拽、键盘焦点或手柄焦点。
- 放大预览使用独立前景层，不进入 `HBoxContainer`，因此不会引发布局重排。

### 8.5 本次采用的显示参数

以下值是实现初始值，可以在实机验收后只调整配置，不改变契约：

- 缩略牌缩放：`0.24`
- Hover 预览缩放：`0.72`
- 卡牌水平间距：`6`
- 卡牌与效果行间距：`8`
- 效果行预留高度：`72`
- Hover 前景 `ZIndex`：`100`

Hover 预览以被悬停缩略牌的中心为基准放大并进入前景，视觉上覆盖邻牌但不移动它们，符合参考图的局部放大效果。

## 9. 卡牌描述覆写

### 9.1 数据契约

`EnemyCardDefinition` 增加：

```csharp
public string DescriptionOverride { get; }
```

- 构造参数非空，默认 `string.Empty`。
- `BaseEnemyCard.DescriptionOverride => Definition.DescriptionOverride` 只读转发。
- 非空文本是可信 MegaRichText/BBCode，可包含换行、颜色等标签。
- 视图统一增加居中包装；不自动做本地化查找或 DynamicVar 替换。
- 该字段只影响显示，不加入 `CardDefinitionFingerprint`，不进入 RNG、评分、执行、牌区或重连实例状态。

### 9.2 应用方式

原版 `NCard.UpdateVisuals()` 会重写 `%DescriptionLabel`。因此：

1. 先正常绑定 `CardModel` 并调用原版更新。
2. 若 `DescriptionOverride` 非空，再通过 `GetNode<MegaRichTextLabel>("%DescriptionLabel")` 写入居中后的覆写文本。
3. 若为空，不写入，让原版描述保持不变。
4. 每次绑定、模型更新、缩略/放大预览复用后都重新应用，避免池化残留。
5. 只对本功能创建的敌人 Intent `NCard` 生效，绝不修改共享 `CardModel` 或玩家手牌节点。

无法取得描述节点属于 UI 兼容错误：记录错误，保留原版描述并继续显示卡牌。

## 10. 原版攻击 Intent 与实时刷新

### 10.1 Intent 创建

- `EnemyAttackPresentation(BaseDamage, 1)` 创建 `new SingleAttackIntent(BaseDamage)`。
- `EnemyAttackPresentation(BaseDamage, HitCount > 1)` 创建 `new MultiAttackIntent(BaseDamage, HitCount)`；禁止复制原版伤害计算。
- 所有 `NIntent` 继续通过原版 `UpdateIntent(intent, targets, owner)` 绑定。
- `CardListIntent.AssetPaths` 增加 `MultiAttackIntent`、`BuffIntent`、`DebuffIntent` 和 Unknown 兜底资源，并移除聚合 Intent 依赖。

### 10.2 Power 事件

视图绑定时订阅：

- 敌人 owner 的四类 Power 事件。
- `LocalContext.GetMe(owner.CombatState).Creature` 的四类 Power 事件。

事件只请求刷新，不立即递归刷新：

- 使用一帧一次的 deferred/coalesced refresh。
- 刷新前确认视图仍绑定同一个 `CardListIntent`、owner 和本地玩家。
- 只更新现存攻击 `NIntent`；结构改变仍走完整 keyed reconciliation。
- 具有重入保护；刷新期间再次收到事件只保留下一次请求。
- `Unbind`、换 owner、换本地玩家、退出树和池化复用时必须解除全部订阅。

这样每个客户端会根据自己的本地玩家 Power 得到不同伤害标签，且怪物 Strength/Weak 等变化也能实时反映。

## 11. Patch 与失败降级

继续使用 `NIntentCardListPatch` 作为唯一桥接点：

- 普通 Intent：恢复原版 Holder，解绑并隐藏自定义视图。
- `CardListIntent` 且卡列非空：隐藏原版 Holder，显示自定义视图。
- 卡列为空或状态 Faulted：隐藏/解绑自定义视图，显示原版 Unknown。
- 投影完整：显示逐牌效果，不显示全局 Unknown。
- 投影不完整：显示已知逐牌效果，同时显示全局 Unknown，并记录错误。
- 自定义视图自身异常：恢复原版 Unknown，不修改逻辑状态。
- 单张描述覆写失败：仅该牌保留原版描述，不应使整列消失。
- 单个标准效果映射失败：该卡显示 Unknown；其他卡正常显示。

## 12. 生命周期与健壮性

- `Bind` 幂等：相同 intent/owner/targets 可以安全重复调用。
- `Unbind` 幂等：可在未绑定或节点已失效时调用。
- 所有延迟回调先验证 Godot 对象有效性和绑定世代号，旧世代回调直接丢弃。
- 所有池化 `NCard` / `NIntent` 在归还前清理模型引用、描述覆写、可见性和事件。
- 视图不得持有玩家或怪物的长期强引用超过绑定生命周期。
- UI 日志包含 `StateId`、`EnemyCardInstanceKey` 和投影诊断；对同一投影指纹去重，避免每帧刷屏。
- `LiveActionProjection`、展示模型和槽位列表均对外只读。

## 13. 文件级变更

### 13.1 修改

- `Scripts/Enemy/CardIntents/EnemyCardDefinition.cs`
- `Scripts/Enemy/CardIntents/BaseEnemyCard.cs`
- `Scripts/Enemy/CardIntents/PreparedEnemyCardAction.cs`
- `Scripts/Enemy/CardIntents/EnemyActionMetricPlanner.cs`
- `Scripts/Enemy/CardIntents/EnemyCardExecutionEngine.cs`
- `Scripts/Enemy/CardIntents/EnemyCardSimulationContext.cs`
- `Scripts/Enemy/CardIntents/EnemyActionProjectionService.cs`
- `Scripts/Enemy/CardIntents/EnemyCardRuntimeSyncState.cs`
- `Scripts/Enemy/CardIntents/CardIntentMoveState.cs`
- `Scripts/Enemy/CardIntents/CardIntentMoveRuntime.cs`
- `Scripts/Enemy/CardIntents/Intents/CardListIntent.cs`
- `Scripts/Enemy/CardIntents/View/NCardListIntentView.cs`
- `Scripts/Patch/NIntentCardListPatch.cs`
- `Scripts/Enemy/CardIntents/Test/CardIntentTestCardCatalog.cs`
- `tests/CardIntent.Tests.ps1`

### 13.2 新增

- `Scripts/Enemy/CardIntents/PreparedEnemyResolutionPlan.cs`
- `Scripts/Enemy/CardIntents/EnemyPreparedResolutionPlanner.cs`
- `Scripts/Enemy/CardIntents/Presentation/EnemyCardIntentPresentation.cs`
- `Scripts/Enemy/CardIntents/Presentation/EnemyCardIntentPresentationBuilder.cs`
- `Scripts/Enemy/CardIntents/View/NEnemyIntentCardSlot.cs`
- `Scripts/Enemy/CardIntents/View/NEnemyCardHoverPreview.cs`
- `Scripts/Enemy/CardIntents/View/EnemyCardDescriptionPresenter.cs`
- `tests/CardIntentHarness/FrozenResolutionPlanTests.testcs`
- `tests/CardIntentHarness/IntentPresentationTests.testcs`
- `tests/CardIntentHarness/DescriptionOverrideTests.testcs`

### 13.3 删除

- `Scripts/Enemy/CardIntents/Intents/CardAggregateAttackIntent.cs`
- `Scripts/Enemy/CardIntents/EnemyPreparedRandomResult.cs`

## 14. 测试用例

所有自动化测试都以定义关系、动态变量来源、集合、顺序和不变量为期望；不得断言游戏配置属性等于某个固定字面数值。第 8.5 节的 UI 参数只做实机观感配置，不作为数值断言。

### 14.1 冻结计划与投影

- 同一冻结行动无论投影多少次都不推进 RNG，也不修改任何牌区或收藏品区域。
- 即时抽牌与回收的随机选择只在准备阶段发生；执行阶段和投影阶段读取同一稳定实例。
- 素材牌、灵感直接执行、收藏品效果、作词结果和即时子牌按实际 DFS 顺序进入投影。
- 子步骤的 `RootSourceKey` 始终指向公开卡列中的来源牌，`ExecutingCardKey` 指向实际子牌。
- 重放成功次数与冻结计划一致；素材截断后的重放不进入展示。
- 标准攻击投影同时保留基础伤害与投影伤害，两者来源正确。
- 当前测试目录的所有已知效果得到完整投影。
- 未知效果、非法引用、循环或步骤超限返回不完整投影与诊断，不产生部分状态写入。
- 执行、投影和重连恢复后的执行产生相同的结构事件顺序。
- 新同步版本可完整往返递归计划；旧版本、缺项、重复实例或越界步骤被整体拒绝。

### 14.2 展示模型

- 输出卡牌顺序等于 `CardList`，重复 `CardId` 仍按不同 `EnemyCardInstanceKey` 独立展示。
- 每张卡只收到以自己为 `RootSourceKey` 的效果。
- 相同基础单次伤害的全部命中和重放合并为一次多段展示；不同基础伤害不合并。
- 防御只产生无数值 `DefendPresentation`。
- 多个敌方 Power 变化折叠为一个 Buff，多个玩家 Power 变化折叠为一个 Debuff。
- 类别顺序稳定为攻击、格挡、Buff、Debuff、Unknown。
- 不完整投影保留已知卡牌效果，同时设置全局 Unknown 和错误诊断。
- 无法映射的攻击只影响所属卡牌，其他卡牌展示保持完整。

### 14.3 动态卡列与复用

- 空列表、单牌、等于当前规则容量和超过当前规则容量的列表都能刷新。
- 追加、删除、重排后，仍存在的实例键复用原槽位，新增/移除节点数量正确。
- 卡列右边缘在数量变化前后保持相同设计锚点，新增卡只向左扩展。
- 不存在固定槽位上限、总宽度缩放、换行或屏幕边界分支。
- 每个槽位的效果节点只属于该槽位，不存在聚合攻击节点。
- 普通 Intent、空列、故障和 UI 异常都恢复原版 Holder。

### 14.4 Hover 与描述

- 鼠标进入缩略牌后只创建/取得一个共享预览；从一张缩略牌移动到另一张时复用并换绑。
- 指针位于预览矩形内时预览保持，离开缩略牌和预览后归还。
- Hover 不改变 HBox 子节点顺序、尺寸或卡牌槽位位置。
- 缩略牌、预览和效果图标不消费战斗点击。
- 非空覆写文本只出现在敌人 Intent 卡节点，原 `CardModel` 描述不变。
- 空覆写显示原版描述；池化复用后不会泄漏上一张牌的覆写。
- 描述节点缺失时记录错误并保留原版描述。

### 14.5 原版实时伤害与多人

- 展示构建器把基础单次伤害与命中次数交给原版攻击 Intent，不传聚合总伤害。
- owner Power 事件与本地玩家 Power 事件都只合并触发一次延迟刷新。
- 解除绑定后 Power 事件不再刷新旧视图。
- 两个客户端使用不同本地玩家上下文时，各自的原版攻击 Intent 得到各自伤害标签，冻结投影结构保持一致。
- 玩家 Power 改变只更新显示，不改变冻结计划、牌区、RNG 或重连 DTO。

### 14.6 实机验收

- 使用显式 `CardIntentTestEncounter` 检查多张卡向左展开、每牌效果图标与参考图一致。
- Hover 任意缩略牌时放大卡完整显示，切牌和快速移出无残影、无闪烁、无布局抖动。
- 防御牌只显示原版防御图标，不显示格挡数值。
- 多段攻击显示单次伤害乘命中次数，不显示错误总伤害。
- 给怪物或本地玩家增减相关 Power 后，伤害标签立即更新。
- 多人实机中不同玩家看到各自将受到的伤害。
- 投影 Bug 时日志可定位来源牌，已知图标仍显示，卡列级 Unknown 明确可见。
- 在不同分辨率和 UI 缩放下只检查锚点、Hover 和点击穿透；按需求不验收超屏处理。

## 15. 完成判定

同时满足以下条件才可声明实现完成：

- 聚合攻击类和固定五槽逻辑已移除。
- 动态卡列、逐牌多 Intent、Hover 预览和描述覆写均工作。
- 当前已知测试牌及收藏品的冻结 DFS 投影完整，执行阶段不再为即时抽取/回收推进 RNG。
- 攻击数字由原版 Intent 针对本地玩家实时计算。
- 所有新增领域测试、静态契约测试、`dotnet build` 与需要 Godot 资源变化时的 `dotnet publish` 通过。
- 单机、多人、重连、Power 刷新、池化生命周期和故障降级完成实机验收。

## 16. 后续 TODO

- 将 `DescriptionOverride` 从写死文本迁移到 `eng/zhs` 本地化与 DynamicVar 体系。
- 正式引入非标准伤害或新效果节点时，为其增加显式展示映射，不允许依赖 Unknown 长期兜底。
- 游戏版本更新后复核 `AttackIntent`、`NIntent`、`NCard` 私有节点和 Creature Power 事件。
- 若未来需要超屏、滚动、换行或逐牌执行动画，单独设计，不在本次实现中预埋隐式行为。

## 17. Spec 自审记录

### 17.1 需求覆盖

- 已覆盖逐牌效果、Hover 放大、动态横向左扩、取消聚合层、原版实时伤害、多人差异和描述覆写。
- 已保留“不显示格挡数值”的原版表现。
- 已覆盖投影不完整必须报错，Unknown 仅作提示和兜底。

### 17.2 框架一致性

- 继续以 `CardListIntent` + `NIntentCardListPatch` 接入原版生命周期。
- 继续使用原版 `NCard`、`NIntent`、`SingleAttackIntent`、`MultiAttackIntent`、`DefendIntent`、`BuffIntent` 和 `DebuffIntent`。
- 不建立第二套牌区权威状态；递归计划属于已准备行动的一部分，投影仍为派生只读数据。
- 重连只同步稳定 ID 与冻结计划，不同步本地 Power 数值。

### 17.3 健壮性

- 卡牌、子牌、槽位和同步引用均使用稳定实例键。
- UI 错误与投影错误不会写入战斗状态；结构执行错误沿现有 Faulted 通路处理。
- Power 刷新、延迟回调、池化节点和事件订阅均定义了解绑与重入规则。

### 17.4 测试约束

- 自动化测试不要求任何配置属性等于固定数值。
- UI 数值参数仅作为显示初始值展示，测试验证关系和行为。
- 明确区分可自动化领域测试、静态契约检查和必须实机验收的 Godot/多人行为。

### 17.5 占位与歧义检查

- 文档不含“待定”“任选其一”“视情况”等未决实现分支。
- 已确认采用共享 Hover 预览、固定右锚左扩、按实例键 diff、逐牌多 Intent、固定类别顺序、混合投影/原版攻击数值和全局 Unknown 兜底。
- 后续事项均已明确列入 TODO，不作为本次完成条件的隐藏缺口。

### 17.6 自审结论

规格覆盖当前确认需求，与现有 Card Intent 领域框架和原版多人伤害显示方式相容；在进入代码实现前不存在需要再次猜测的产品决策。

## 18. 实现与验证记录

2026-08-29 已完成规格对应的代码和自动化验证：

- 冻结行动已改为不可变递归单元与显式步骤；即时抽牌、回收、收藏品、灵感与作词结果都在准备阶段冻结，执行、投影和重连共同消费同一结构。
- 即时卡牌自身的全部重放以首单元和连续附加重放单元表达；规划期虚拟增加的 `ReplayCount` 会参与后续冻结，但不会提前修改权威状态。
- `LiveActionProjection` 保留根来源、实际执行牌、重放索引、基础／投影伤害、收藏品变化和生成牌结果；未知修改器、非法结构与步骤超限返回不完整诊断。
- 展示层按公开实例键逐牌归并 Attack、Defend、Buff、Debuff、Unknown；攻击命中只以首目标计数并校验其他目标结构，不按玩家数量重复累加。
- Godot 视图已改为固定右锚、动态向左增长、每牌独立原版 Intent 和唯一共享 Hover；owner／本地玩家 Power 变化按绑定世代合并刷新。
- `DescriptionOverride` 为空时不覆写，非空可信 BBCode 在原版 `NCard.UpdateVisuals` 后居中写入，并在池化换绑时清理。
- Windows PowerShell 执行 `tests/CardIntent.Tests.ps1` 通过；`CardIntentHarness` 54/54 通过；`dotnet build` 0 错误。新增测试只校验身份、关系、顺序、集合和状态不变量，不固定游戏平衡配置数值。
- 本次只修改 C#、测试和文档，没有新增或修改 Godot 资源，因此未执行 `dotnet publish`。

当前环境未启动游戏，14.6 所列单机布局、Hover、输入穿透、多人本地伤害差异、真实断线重连、池化生命周期和故障注入仍保留为人工实机验收项；本记录不把这些项目标记为已完成。
