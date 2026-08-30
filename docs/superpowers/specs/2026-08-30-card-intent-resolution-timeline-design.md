# Card Intent 结算时间线设计

## 背景

当前 `CardListIntent` 直接公开 `PreparedEnemyCardAction` 的保留前缀与指标牌，`EnemyCardIntentPresentationBuilder` 再按 `RootSourceKey` 把全部重放和 DFS 子步骤归回顶层来源。这造成三个展示错误：

1. 合法但素材不足的顶层来源没有投影单元，被错误显示为 `UnknownIntent`。
2. 即时作词 Token 的效果被合并到母牌，Token 自身没有独立卡槽。
3. 被前序来源消耗的后续候选仍保留在顶层卡列原位置，并因为没有投影单元显示问号。

已观察场景的顶层来源日志为：

```text
HARUHIKAGE[RUNTIME:1]
-> ATTACK[TEMPLATE:6]
-> HITOSHIZUKU[TEMPLATE:23]
-> HOPE[TEMPLATE:17]
-> HITOSHIZUKU[TEMPLATE:22]
```

其中 `HITOSHIZUKU[TEMPLATE:22]` 是初始牌素材，不是 Token。真正的 Token 位于冻结 DFS 子计划中，身份应为 `HITOSHIZUKU_TOKEN[RUNTIME:n]`。

## 目标

从已经冻结的 `PreparedEnemyCardAction` 派生一条只读结算时间线，并让 UI、效果归属与 `[CardIntentOrder]` 日志共同使用该时间线。

目标行为：

- 素材不足且完全没有成功执行单元的顶层来源保留原位置、整体置灰、效果行为空。
- 被消耗的卡牌紧跟消费者、整体置灰，并且不会在后续顶层位置重复出现。
- 被消耗的收藏品使用映射卡面紧跟消费者、整体置灰。
- 多个素材严格按冻结支付顺序排列。
- Token、即时抽牌与回收牌排列在消费者的全部素材之后。
- 每个实际执行实例只显示自己的效果；Token 效果不再归到母牌。
- 合法的素材不足或来源被消费都不显示问号。

确认后的示例时间线为：

```text
HARUHIKAGE [INSUFFICIENT_MATERIAL, Gray, No Intent]
-> ATTACK [6]
-> HITOSHIZUKU[TEMPLATE:23] [6]
-> HITOSHIZUKU[TEMPLATE:22] [CONSUMED_CARD, Gray]
-> HITOSHIZUKU_TOKEN[RUNTIME:n] [9 x 2]
-> HOPE [Debuff]
```

## 非目标

- 不改变行动指标选择、素材优先级、素材预留或 RNG 推进。
- 不改变 `PreparedEnemyCardAction`、执行引擎、五牌区或收藏品库存的权威语义。
- 不改变重连 schema；时间线始终由已同步的冻结计划派生。
- 不把生成但未作为素材消费的收藏品加入本次卡列。
- 不新增 Godot 场景、图片或材质资源。

## 方案选择

采用独立的冻结计划派生时间线，而不是从已经聚合的 `LiveActionProjection` 反推顺序，也不把展示条目写入权威 `CardList`。

原因：

- 冻结计划保留素材、收藏品、Token、即时牌与回收牌的完整 DFS 身份和顺序。
- 派生模型可以与战斗状态、RNG 和重连保持隔离。
- UI 和日志消费同一个派生结果，不会出现两套顺序定义。

## 时间线领域模型

新增不可变的结构时间线 `EnemyIntentTimeline` 与条目模型。结构时间线只由冻结计划生成，不依赖玩家目标、实时 Power 或 Godot。条目至少包含以下字段：

- `DisplayKey`：用于 UI keyed reconciliation 的稳定展示键。
- `Kind`：`Card` 或 `Collection`。
- `Role`：
  - `RetainedSource`
  - `NormalSource`
  - `InsufficientMaterial`
  - `ConsumedCard`
  - `ConsumedCollection`
  - `GeneratedChild`
  - `ImmediateChild`
  - `RecoveredChild`
- `CardModel`：原版 `NCard` 渲染使用的只读模型。
- `DescriptionOverride`：仅卡牌条目使用；收藏品为空。
- `IsDimmed`：`InsufficientMaterial`、`ConsumedCard` 与 `ConsumedCollection` 为真。
- `Effects`：只归属于该实际执行实例的 Attack、Defend、Buff、Debuff 或 Unknown 展示集合。

展示键命名空间必须避免卡牌和收藏品碰撞：

```text
CARD:<EnemyCardInstanceKey>
COLLECTION:<CollectionInstanceId>
```

收藏品通过 `EnemyCollectionDefinition.CardModelType` 从 `ModelDb` 解析显示模型。Token 与其他子牌通过其冻结 `ExecutingCardId` 解析不可变卡牌定义和显示模型。

## 时间线构建算法

`EnemyIntentTimelineBuilder` 输入冻结行动与卡牌／收藏品定义解析器，输出一条完整或带诊断的只读结构时间线。它不得调用战斗命令、推进 RNG、读取本地玩家 Power 或修改任何牌区。

`EnemyIntentTimelinePresentationBuilder` 再把结构时间线与实时效果投影组合成 UI 展示模型。该两阶段边界保证：

- `PrepareCards()` 可以在没有玩家目标和实时 Power 输入时立即记录确定性顺序日志。
- UI 可以在目标或 Power 变化后只刷新效果数值，不重建权威顺序。
- 重连后可以从恢复的冻结计划重新派生相同结构时间线，无需扩展同步 schema。

### 预索引

构建前递归扫描所有冻结步骤，收集：

- 被 `PreparedConsumedCardStep` 引用的卡牌实例键。
- 被 `PreparedConsumedCollectionStep` 引用的收藏品实例 ID。
- 所有实际执行单元的 `ExecutingCardKey`、`ExecutingCardId` 与首次 DFS 位置。
- 实际执行单元对应的实时投影。

预索引用于区分两种同为零成功单元的顶层来源：

- 已被前序来源消费：不在原顶层位置重复显示。
- 未被消费但首个尝试素材不足：显示 `InsufficientMaterial` 条目。

### 顶层来源

严格按 `PreparedEnemyCardAction.Sources` 顺序处理：

1. 来源实例已在消费预索引中且没有成功单元：跳过顶层重复条目；它会在消费者步骤处显示为置灰素材。
2. 来源没有成功单元且未被消费：生成 `InsufficientMaterial` 条目，`IsDimmed = true`，`Effects` 为空。
3. 来源至少有一个成功单元：生成或复用该实际执行实例的正常条目，再递归展开其步骤。

如果来源至少成功执行一次，只是后续重放因素材耗尽而截断，则保持正常条目，并只合并成功重放的效果；不额外生成素材不足条目，也不置灰来源。

### 单元与步骤

同一实际执行实例的连续重放合并为一个条目，攻击命中数只累计成功重放。不同素材实例不得合并。

每个成功单元先确保消费者条目存在，然后按 `OrderedSteps` 遍历：

- `PreparedConsumedCardStep`
  - 立即在当前消费者后追加 `ConsumedCard` 条目。
  - `IsDimmed = true`。
  - 普通素材的效果集合为空。
  - 如果存在 Inspiration `ControlledChild`，其效果归到该置灰素材条目，并递归处理该子单元允许的步骤。
- `PreparedConsumedCollectionStep`
  - 立即追加 `ConsumedCollection` 条目。
  - `IsDimmed = true`。
  - 收藏品 `Children` 产生的效果归到该置灰收藏品条目。
- `PreparedDirectEffectsStep`
  - 效果归到当前实际执行实例，不创建额外条目。
- `PreparedComposeResultStep`
  - 在本单元全部前置素材之后追加 Token 条目。
  - `ImmediateChild` 与 `AdditionalReplayUnits` 的效果归到 Token 自己。
- `PreparedImmediateCardStep`
  - 在全部前置素材之后追加即时牌条目，并处理全部连续重放。
- `PreparedRecoveryStep`
  - 回收卡牌时追加回收牌条目；回收收藏品本身不作为“被消费素材”展示。
- `PreparedGeneratedCollectionStep`
  - 记录投影与诊断，但不加入本次时间线。

规划器当前先写入全部素材步骤，再写直接效果、生成收藏品与作词步骤，因此按 `OrderedSteps` 遍历天然满足“消费者 -> 全部素材 -> Token／即时子牌”。

## 效果归属

现有展示按 `RootSourceKey` 分组。新展示必须按 `ExecutingCardKey` 分组，同时保留 DFS 首次出现顺序。

现有 `EnemyCardReplayProjection` 会把收藏品子效果聚合进消费者单元，无法表达收藏品自己的展示归属。为避免展示层从合计值反推步骤，实时投影新增只读的归属切片，例如 `EnemyTimelineEffectProjection`：

- `DisplayKey`：效果应归属的结构时间线条目。
- `RootSourceKey`：保留原有诊断关联。
- `ExecutingCardKey`／`ExecutingCardId`：卡牌效果存在时保留实际执行身份；收藏品效果允许为空。
- `ReplayIndex`：卡牌重放索引；收藏品沿用所属消费者单元索引。
- 逐目标基础／投影伤害、目标 Power 变化、敌人格挡与敌方 Power 变化。

投影遍历在进入效果步骤时设置明确归属作用域：

- 普通来源直接效果归到来源卡牌展示键。
- Token、即时牌、回收牌和 Inspiration 受控子单元归到各自卡牌展示键。
- 收藏品 `Children` 中的直接效果归到收藏品展示键。

`LiveActionProjection` 可以保留现有逐单元数据供诊断和既有消费者使用，同时增加时间线效果切片；两者都属于派生只读数据，不进入重连 DTO。新展示构建器只消费带归属切片，禁止再次按 `RootSourceKey` 猜测归属。

- 一滴泪母牌只获得自身 `6` 点攻击。
- 一滴泪 Token 独立获得 `9 x 2`。
- Inspiration 素材的受控效果归到置灰素材。
- 收藏品子效果归到置灰收藏品。
- 同一执行实例的相同基础伤害跨成功重放合并为原版 `MultiAttackIntent`。
- 不同基础伤害保持首次出现顺序，继续生成多个攻击图标。

`UnknownIntent` 只表示非法或不可解析结构。以下状态均为合法，不得产生 Unknown：

- 顶层来源素材不足且零成功单元。
- 顶层来源已被前序来源消费。
- 后续重放因素材耗尽正常截断。

## UI 与置灰

现有逐牌槽位改为绑定统一时间线条目，但继续复用原版 `NCard` 与 `NIntent`：

- 卡牌与收藏品都通过 `NCard.Create(CardModel, ModelVisibility.Visible)` 创建缩略图。
- 时间线列表使用 `DisplayKey` 复用、移动和释放槽位。
- `IsDimmed` 为真时，对整个槽位应用灰色调制，包括缩略牌和效果行。
- 不降低透明度，保证缩略图和图标可读。
- 普通消耗素材与素材不足来源的效果行为空。
- Inspiration 素材与收藏品子效果继续显示，但随槽位一起置灰。
- Hover 放大预览保持原始颜色，以便阅读卡面。
- 换绑、解绑或归还池化节点前恢复默认调制，禁止灰色状态泄漏。

收藏品不需要新的视觉资源。其映射玩家卡牌模型已经提供标准卡面与 Hover 数据。

## 资源预加载

`CardListIntent.AssetPaths` 必须覆盖时间线可能显示的全部模型：

- 初始敌人牌组模型。
- 全部已注册 Token／衍生牌模型。
- 全部已注册收藏品 `CardModelType` 对应模型。
- 既有 Single／Multi Attack、Defend、Buff、Debuff 与 Unknown Intent 资源。

资源集合在牌组或目录注册阶段解析并去重；首次绑定 UI 时不得临时修改注册状态。

## 日志

保留 `[CardIntentOrder]` 标签，但内容改为时间线顺序，并为每项输出角色：

```text
1:...HARUHIKAGE[RUNTIME:1,INSUFFICIENT_MATERIAL]
-> 2:...ATTACK[TEMPLATE:6,NORMAL_SOURCE]
-> 3:...HITOSHIZUKU[TEMPLATE:23,NORMAL_SOURCE]
-> 4:...HITOSHIZUKU[TEMPLATE:22,CONSUMED_CARD]
-> 5:...HITOSHIZUKU_TOKEN[RUNTIME:2,GENERATED_CHILD]
-> 6:...HOPE[TEMPLATE:17,NORMAL_SOURCE]
```

收藏品格式为：

```text
COLLECTION_ID[COLLECTION_INSTANCE_ID,CONSUMED_COLLECTION]
```

日志必须消费 UI 使用的同一份结构 `EnemyIntentTimeline`，不得单独重建顺序。实时伤害与 Power 图标由后续展示构建阶段附加，不参与顺序日志。

## 故障降级

时间线构建器保留已知条目并累计诊断：

- 未知卡牌或收藏品定义。
- 悬空实例引用。
- 重复展示键。
- 非法递归身份、循环或步骤上限。
- 预期执行实例缺少实时投影。

能够定位到单条目的故障只为该条目追加 `EnemyUnknownPresentation`。时间线整体不完整时继续显示已知条目，并追加全局 Unknown。Godot 视图自身异常时沿用现有安全路径，恢复原版整体 Unknown，且不得回写逻辑状态。

## 测试策略

### 时间线领域测试

新增精确复现观察场景的测试，断言：

- 春日影位于首位，角色为 `InsufficientMaterial`，置灰且效果集合为空。
- Attack 紧随其后并显示单次攻击。
- 一滴泪母牌只显示 `6`。
- 被消费的一滴泪紧跟母牌，角色为 `ConsumedCard`，置灰且不会在后续重复出现。
- Token 位于全部素材之后，并独立显示 `9 x 2`。
- Hope 位于 Token 之后并显示 Debuff。

其他领域测试覆盖：

- 多素材按冻结支付顺序排列。
- 收藏品素材的稳定键、映射模型、置灰与子效果归属。
- Inspiration 素材的受控效果归属。
- Token、即时抽牌、回收牌与附加重放顺序。
- 同实例成功重放合并；不同素材实例不合并。
- 首次成功、后续素材耗尽只保留正常条目。
- 生成但未消费的收藏品不进入时间线。

### 展示与故障测试

- 卡牌和收藏品展示键命名空间不碰撞。
- 效果按 `ExecutingCardKey` 而不是 `RootSourceKey` 归属。
- 合法素材不足、合法消费与正常截断不产生 Unknown。
- 未知定义、悬空引用和非法递归产生局部或全局 Unknown。
- 置灰槽位换绑和释放后恢复默认颜色。
- Hover 不继承缩略槽位置灰。
- `[CardIntentOrder]` 与时间线条目逐项一致。

### 回归验证

必须通过：

```powershell
dotnet test tests/CardIntentHarness/CardIntentHarness.csproj
powershell -ExecutionPolicy Bypass -File tests/CardIntent.Tests.ps1
dotnet build --no-restore
```

执行、同步、牌区、收藏品库存和重连往返测试必须保持通过。由于不修改 Godot 资源，本任务不要求 `dotnet publish`。

## 实机验收

自动化通过后，在测试 Encounter 验证：

1. 素材不足来源置灰且没有任何 Intent 图标。
2. 消耗卡牌与消耗收藏品紧跟消费者并置灰。
3. Token 位于全部素材之后并显示自己的伤害。
4. 后续未被消费的来源保持原顺序。
5. Hover 卡面可读且不被灰色调制污染。
6. 日志顺序、稳定身份和角色与画面一致。

## 预计代码边界

主要修改与新增范围：

- `Scripts/Enemy/CardIntents/Presentation/`
  - 新增时间线领域／展示模型与构建器。
  - 调整现有效果构建器为按实际执行身份归属。
- `Scripts/Enemy/CardIntents/CardIntentMoveState.cs`
  - 在准备后生成并记录统一时间线日志。
- `Scripts/Enemy/CardIntents/CardIntentMoveRuntime.cs`
  - 向 `CardListIntent` 只读转发时间线输入或结果。
- `Scripts/Enemy/CardIntents/Intents/CardListIntent.cs`
  - 暴露时间线并扩展完整资源预加载。
- `Scripts/Enemy/CardIntents/View/NCardListIntentView.cs`
  - 改为按统一展示键协调时间线槽位。
- `Scripts/Enemy/CardIntents/View/NEnemyIntentCardSlot.cs`
  - 绑定统一条目并实现可恢复置灰。
- `Scripts/Enemy/CardIntents/View/NEnemyCardHoverPreview.cs`
  - 接受统一显示模型并保持原色预览。
- `tests/CardIntentHarness/`
  - 新增时间线、素材、收藏品、效果归属与故障测试。
- `tests/CardIntent.Tests.ps1`
  - 更新静态契约与资源预加载检查。

任何实现阶段发现需要修改执行引擎、权威状态或重连 schema 的情况，都视为超出本设计边界，必须停止并重新评审。
