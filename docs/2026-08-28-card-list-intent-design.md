# 卡牌列表 Intent 与敌人牌库循环设计规格

> 状态：修正版已实现，自动化验证通过；游戏内集成验收待执行
>
> **UI 修订提示（2026-08-29）：** 本文关于“固定五槽、无 Hover、聚合攻击 Intent”的表现层结论已经被 [敌人卡牌逐牌 Intent 与 Hover 预览修订规格](./2026-08-29-card-list-intent-ui-revision-design.md) 取代。领域模型与牌库循环章节仍作为已实现基线。
>
> 设计日期：2026-08-28
>
> 关键词标题：**BaseEnemyCard｜CardIntentMoveState｜真实牌库循环｜复合卡牌 Intent｜聚合攻击预览｜稳定 ID 快照**

## 1. 文档目的

本规格设计一种可复用的敌人卡牌行动基础设施：需要该能力的敌人会在玩家方回合开始时，从固定牌组中无放回抽取一手牌；玩家在敌人头顶看到整手牌的缩略图，以及原版整体攻击和防御 Intent；敌人回合按同一列表从左到右执行这些牌。

本规格是影灯第四层 Boss 的前置基础设施设计。本次只实现通用框架、基础攻击/防御模拟牌和显式启动的测试敌人，不设计影灯的正式牌组、阶段、强度或 Boss 遭遇。

本次设计未读取或写入 Basic Memory。引用任务“评估影灯第四层Boss方案”的结论已经通过 Codex 任务读取，并按本轮用户修正重新设计。

## 2. 已确认需求

- 不为单张缩略牌提供 Hover、放大、拖拽或点击。
- UI 只显示当前整手牌的整体缩略图。
- 保留原版攻击图标，并显示当前手牌的整体预计伤害。
- 当前手牌存在防御牌时显示原版 DefendIntent，但与原版一致，不显示格挡数值。
- 使用截图所示的上下两层布局：上层横排卡牌，下层原版 Intent，整体位于敌人头顶。
- 需要卡牌 Intent 的行动使用 CardIntentMoveState : MoveState。
- CardIntentMoveState 直接拥有 DeckId、DeckList、CardList 和 DiscardList。
- 每个玩家方整体回合只抽一次，不按多人中的每名玩家分别重抽。
- 只为怪物当前 NextMove 抽牌；玩家回合中强制切换 NextMove 时，为新状态补抽。
- 使用抽牌堆、当前手牌、弃牌堆和洗牌构成的真实牌库循环。
- UI 和实际行动必须读取同一个冻结 CardList。
- 本次模拟牌必须实际执行，而不只是显示。
- 新增 BaseEnemyCard，统一处理基础攻击、防御和子类自定义效果。
- BaseEnemyCard.CardModel 只提供牌面与本地化信息，不调用玩家卡牌 OnPlay。
- CustomExecuteAsync 可以选择在基础攻防之前或之后执行，默认在之后。
- 使用无交互的原版 NCard 显示缩略牌。
- 使用单个复合 CardListIntent，在其原版 NIntent 根节点内绘制上层牌列和下层原版攻防 Intent。
- 牌库快照使用稳定 ID 捕获、全量校验并支持显式重建；正常多人战斗使用战斗 RNG 的确定性模拟。
- 当前游戏不保存完整战斗中状态，因此本次不宣称支持战斗中续档或断线重连。
- 增加不进入正常游戏流程、只能显式启动的测试敌人与测试 Encounter。
- 测试不得断言配置属性等于具体数值；必须使用关系和不变量。

## 3. 非目标

- 不实现影灯正式 Boss。
- 不确定影灯正式牌组、阶段、AI、生命、伤害上限或强度曲线。
- 不直接执行玩家 CardModel 的 OnPlay、能量、手牌、弃牌或玩家 Hook 流程。
- 不支持逐张 Hover、逐张放大、拖拽、选择或出牌飞行动画。
- 不在敌人行动期间保留牌列并逐张做消失动画；原版 Intent 开始行动时可整体淡出。
- 不为 DefendIntent 添加数值标签。
- 不为自定义 Buff、Debuff 或跳过回合效果自动生成额外原版 Intent 图标；当前牌面本身承担预告作用。
- 不增加新的 Godot 场景、图片、材质或动画资源。
- 不把测试敌人加入章节、地图、Boss 池或正常遭遇池。
- 不在本规格中支持带独立可变状态的单张敌人卡牌；当前 BaseEnemyCard 实例在战斗中是无内部计数的执行对象。

## 4. 已核对的项目与游戏事实

### 4.1 当前项目模式

- 现有敌人通过 GenerateMoveStateMachine 创建 MoveState，并将 SingleAttackIntent、MultiAttackIntent、DefendIntent 等原版 Intent 传入状态。
- 项目已有 BeforeSideTurnStart、BeforeHandDraw 和 AfterPlayerTurnStart Hook 用法。
- 现有随机抽取使用 RunState.Rng 下的战斗 RNG；从已有卡牌集合选择时可使用 CombatCardSelection。
- 现有敌人卡牌目录 Scripts/Cards/EnemyCards 里的 Taki 卡牌是玩家选择界面的载体，不适合作为本需求的敌人可执行卡牌模型。
- StrikeTomorin 的基础伤害变量为 6，DefendTomorin 的基础格挡变量为 5。
- 当前仓库测试主要位于 tests 下，以 PowerShell 聚焦检查配合游戏内验收。

### 4.2 已核对的原版 API

- MoveState 不是 sealed，可以继承。
- MoveState.PerformMove 不可重写，因此 CardIntentMoveState 需要在构造时向原版 MoveState 提供运行时执行委托。
- MoveState.Intents 的 setter 是 private，不应在每次抽牌后用反射替换整个 Intent 列表。
- SingleAttackIntent 提供 Func<decimal> 构造，并允许重写 GetTotalDamage。
- DefendIntent 不是 sealed，但没有防御数值构造参数。
- NIntent.UpdateIntent 是公开方法，适合作为窄范围 Harmony Postfix 接入点。
- NIntent.Create 是公开方法，可创建下层原版攻防 Intent 节点。
- NCard.Create 是公开方法。
- NCard.Model 有公开 setter，可以保留固定卡牌槽位并在刷新时换绑模型。
- MonsterModel.NextMove 是公开 getter。
- MonsterModel.SetMoveImmediate(MoveState, bool) 是可精确 Patch 的实例方法。
- Hook.BeforeSideTurnStart(ICombatState, CombatSide, IReadOnlyList<Creature>) 返回 Task。
- ICombatState 提供 RoundNumber 和 CurrentSide。
- 现有怪物场景使用 IntentPos 作为原版 Intent 锚点。

## 5. 总体架构

```text
EnemyCardDeckRegistry
  └─ DeckId → 新牌组实例工厂
               │
               ▼
BaseCardIntentMonsterModel
  └─ CardIntentMoveState
       ├─ DeckList
       ├─ CardList       ← 唯一的当前手牌权威快照
       ├─ DiscardList
       ├─ CardListIntent ───────────────┐
       └─ ExecuteCardsAsync             │
               │                        │
               ▼                        ▼
       BaseEnemyCard.ExecuteAsync   NIntentCardListPatch
               │                        │
        Attack / Defend / Custom        ▼
                                  NCardListIntentView
                                  ├─ 上层五张 NCard
                                  └─ 下层原版攻防 NIntent
```

核心不变量是：CardList 是当前玩家方回合内唯一的手牌快照。抽牌、显示、聚合攻击预览、实际执行、弃牌和显式快照都读取同一份顺序。

## 6. BaseEnemyCard

### 6.1 职责与接口

目标文件：Scripts/Enemy/CardIntents/BaseEnemyCard.cs。

BaseEnemyCard 是敌人卡牌的抽象基类，至少包含：

| 成员 | 类型 | 说明 |
|---|---|---|
| CardId | EnemyCardId | 稳定定义标识，用于牌组注册与快照 |
| CardModel | CardModel | 只读牌面、本地化和原版 NCard 渲染来源 |
| Atk | decimal | 本牌基础攻击贡献；非攻击牌为零 |
| Def | decimal | 本牌基础防御贡献；非防御牌为零 |
| CustomExecutionTiming | enum | BeforeBaseEffects 或 AfterBaseEffects，默认后置 |
| ExecuteAsync | Task | public 且不可重写的统一执行入口 |
| CustomExecuteAsync | Task | protected virtual，默认完成任务 |

ExecuteAsync 接收 EnemyCardExecutionContext。上下文包含：

- BaseCardIntentMonsterModel Owner；
- CardIntentMoveState State；
- PlayerChoiceContext ChoiceContext；
- IReadOnlyList<Creature> Targets；
- 当前战斗状态及必要的取消/终止查询。

### 6.2 模板执行顺序

```text
验证上下文与重入状态
  └─ CustomExecutionTiming == BeforeBaseEffects？
       └─ CustomExecuteAsync
  └─ Atk > 0？
       └─ 使用 DamageCmd 与敌人来源执行一次攻击
  └─ Def > 0？
       └─ 使用 CreatureCmd 为敌人获得格挡
  └─ CustomExecutionTiming == AfterBaseEffects？
       └─ CustomExecuteAsync
```

基础攻击使用敌人来源和 ValueProp.Move 语义；基础防御作用于敌人自身。CustomExecuteAsync 可实现 Buff、Debuff、跳过玩家回合等效果，但不得直接修改 DeckList、CardList 或 DiscardList。

CardModel 从 ModelDb 取得并作为只读原型引用。BaseEnemyCard 不创建玩家、不消耗玩家能量，也不调用 CardModel.OnPlay。

## 7. 牌组标识与注册

### 7.1 EnemyCardId 与 EnemyCardDeckId

目标文件：

- Scripts/Enemy/CardIntents/EnemyCardId.cs
- Scripts/Enemy/CardIntents/EnemyCardDeckId.cs

两个标识都使用带命名空间校验的只读字符串值对象。有效标识必须非空，并统一使用 STS2_TOMORIN_MOD:NAME 形式；禁止使用本地化名称作为身份。

### 7.2 EnemyCardDeckRegistry

目标文件：Scripts/Enemy/CardIntents/EnemyCardDeckRegistry.cs。

注册表保存：

```text
DeckId → IReadOnlyList<Func<BaseEnemyCard>>
```

每个工厂调用都创建新的 BaseEnemyCard 实例。这样同一牌组可以包含重复定义，但运行时每张牌仍是独立对象。

注册阶段必须校验：

- DeckId 唯一；
- 卡牌工厂和生成结果非空；
- CardId 可解析；
- CardModel 非空；
- 牌组实例数量不小于状态配置的手牌容量；
- 同一工厂不得返回已经被其他槽位使用的对象实例。

注册失败属于开发配置错误，应尽早抛出并阻止该牌组进入状态机。存档恢复阶段的未知 ID 使用第 14 节的安全故障规则。

## 8. CardIntentMoveState

### 8.1 数据所有权

目标文件：Scripts/Enemy/CardIntents/CardIntentMoveState.cs。

CardIntentMoveState : MoveState 内部持有：

```text
EnemyCardDeckId DeckId
List<BaseEnemyCard> _deckList
List<BaseEnemyCard> _cardList
List<BaseEnemyCard> _discardList
bool IsPrepared
bool IsExecuting
bool IsFaulted
```

对外暴露 DeckList、CardList、DiscardList 的 IReadOnlyList 视图。只有本状态的 PrepareCards、ExecuteCardsAsync、CancelPreparedHand 和 RestoreSnapshot 可以修改内部列表。

派生统计：

- TotalRawAttack：CardList 中 Atk 的和，仅供诊断；
- TotalRawDefense：CardList 中 Def 的和，仅供诊断和是否显示防御图标；
- HasAttack：存在 Atk 大于零的牌；
- HasDefense：存在 Def 大于零的牌。

### 8.2 构造方式

由于原版 PerformMove 不可重写，使用私有 CardIntentMoveRuntime 预先创建执行委托与 CardListIntent，再交给 MoveState 构造函数。CardIntentMoveState 对外仍是运行时唯一所有者；CardIntentMoveRuntime 只是解决基类构造顺序的实现细节，不能被 UI 或怪物直接修改。

构造统一由静态 Create 工厂完成：

```text
创建 CardIntentMoveRuntime
  ├─ runtime.ExecuteCardsAsync 传给 MoveState onPerform
  └─ new CardListIntent(runtime) 传给 MoveState intents
返回拥有该 runtime 的 CardIntentMoveState
```

CardIntentMoveState 的 Intent 列表固定为单个 CardListIntent，不在每回合用反射替换 Intents。

### 8.3 抽牌算法

固定手牌容量为 5。

PrepareCards 的正常流程：

1. IsFaulted 时拒绝准备。
2. IsPrepared 为真时直接返回，不消耗 RNG。
3. 首次使用时按 DeckId 从注册表创建 DeckList。
4. 从 DeckList 使用 CombatCardSelection 随机选择一个实例，移除并追加到 CardList。
5. DeckList 为空且仍需抽牌时，将 DiscardList 全部移回 DeckList，再继续随机选择。
6. CardList 达到手牌容量后设置 IsPrepared。
7. 保存快照并触发 CardListChanged。

每次随机选择都是无放回的。使用随机索引选择并移除，与先洗牌再从顶部抽牌具有相同的无放回分布；实现应只调用战斗 RNG，不使用 System.Random。

### 8.4 执行与弃牌

ExecuteCardsAsync 按 CardList 从左到右执行：

1. 检查 IsPrepared、IsExecuting、IsFaulted 和怪物存活状态。
2. 设置 IsExecuting。
3. 取 CardList 首项并调用 BaseEnemyCard.ExecuteAsync。
4. 成功后从 CardList 移除并追加到 DiscardList。
5. 立即更新快照。
6. 重复直到 CardList 为空或战斗终止。
7. 正常完成后清除 IsPrepared 和 IsExecuting。

使用“每次总是取首项”的方式，避免遍历期间修改集合造成跳项。

### 8.5 强制换招

MonsterModel.SetMoveImmediate(MoveState, bool) 使用 Prefix 保存旧 NextMove，并使用 Postfix 比较新旧状态。

只有 CombatState.CurrentSide == CombatSide.Player 时执行卡牌协调：

- 新旧状态相同：不弃牌、不重抽。
- 旧状态为已准备但尚未执行的 CardIntentMoveState：调用 CancelPreparedHand，把整手移入旧状态弃牌堆。
- 新状态为 CardIntentMoveState：立即 PrepareCards。
- 新状态为普通 MoveState：不创建牌列。

敌人方回合内的 SetMoveImmediate 不触发补抽，避免干扰正在执行的 CardList；下一次玩家方回合统一准备。

## 9. 玩家方回合协调

目标文件：Scripts/Patch/CardIntentTurnCoordinatorPatch.cs。

Patch 目标为 Hook.BeforeSideTurnStart(ICombatState, CombatSide, IReadOnlyList<Creature>)。使用 Postfix 包装原 Task，并在原逻辑完成后执行：

1. side 不是 CombatSide.Player 时直接返回。
2. 遍历 combatState.Enemies 中存活且未逃跑的 Creature。
3. 读取 creature.Monster。
4. 只处理 BaseCardIntentMonsterModel 且 NextMove 为 CardIntentMoveState 的对象。
5. 调用 PrepareCards。

BeforeHandDraw 不承担本功能，因为它按玩家触发，多人环境可能为同一敌人重复抽牌。CardIntentMoveState.IsPrepared 同时防止 Hook 重入、状态刷新或网络重复回调造成再次抽牌。

## 10. BaseCardIntentMonsterModel 与快照

### 10.1 怪物基类

目标文件：Scripts/Enemy/CardIntents/BaseCardIntentMonsterModel.cs。

BaseCardIntentMonsterModel : CustomMonsterModel 统一提供：

- 注册本怪物的 CardIntentMoveState；
- CaptureCardIntentSnapshot；
- RestoreCardIntentSnapshot；
- 状态变更后刷新稳定 ID 快照投影；
- 战斗结束时解除运行时事件关系。

派生怪物在 GenerateMoveStateMachine 中通过 RegisterCardIntentState 注册状态。状态机重新生成时，同 StateId 的旧状态会被解绑并替换；BaseCardIntentMonsterModel.BeforeRemovedFromRoom 幂等解除事件关系。

运行时 List 是权威状态；快照只是稳定 ID 投影，只有显式 RestoreCardIntentSnapshot 才能驱动恢复。当前游戏版本没有 MonsterModel 的 Run 存档序列化入口，并且不会保存完整战斗中状态，因此本次不使用 Monster SavedProperty，也不宣称战斗中续档或断线重连。

### 10.2 快照结构

目标文件：Scripts/Enemy/CardIntents/CardIntentRuntimeSnapshot.cs。

```text
CardIntentOwnerSnapshot
├─ SchemaVersion = 1
└─ States[StateId]
   ├─ DeckId
   ├─ DrawPileCardIds[]
   ├─ CurrentCardIds[]
   ├─ DiscardPileCardIds[]
   ├─ IsPrepared
   └─ IsFaulted
```

重复 CardId 按数组位置保存，以保留副本数量与顺序。CardModel、委托、Godot 节点、事件订阅和对象地址不进入快照。

恢复时进行全量校验：

- SchemaVersion 支持；
- StateId 在当前状态机中存在且唯一；
- DeckId 与该状态声明一致；
- 所有 CardId 可由该 DeckId 的模板解析；
- 三个牌堆合并后的 CardId 多重集合与模板一致；
- 非故障状态下，IsPrepared 与 CurrentCardIds 是否为空保持一致；
- IsFaulted 恢复后禁止再次抽牌或执行。

任一检查失败时不接受部分数据，也不重新抽牌。若无法确定单一对应状态（例如缺失或未知 StateId），则将该怪物的全部 CardIntentMoveState 标记为 Faulted。

## 11. 复合 CardListIntent

### 11.1 数据 Intent

目标文件：Scripts/Enemy/CardIntents/Intents/CardListIntent.cs。

CardListIntent : UnknownIntent 固定持有 CardIntentMoveRuntime 暴露的只读视图，并满足：

- HasIntentTip 为 false；
- 不提供自定义单图标；
- 作为原版 NIntent 生命周期根的数据标记；
- 本身不抽牌、不执行牌、不修改状态。

### 11.2 聚合攻击 Intent

目标文件：Scripts/Enemy/CardIntents/Intents/CardAggregateAttackIntent.cs。

CardAggregateAttackIntent : SingleAttackIntent 保留原版攻击贴图、动画、标签格式和 HoverTip，重写 GetTotalDamage：

1. 过滤 CardList 中 Atk 大于零的牌。
2. 对每张牌分别按原版单次攻击 Intent 的计算路径取得预览伤害。
3. 汇总每张牌的预览结果。

不能把 Atk 原始总和只交给一个普通 SingleAttackIntent，因为 Strength 等加法修正应对每次独立攻击分别生效。基础测试牌的实际攻击也是逐牌执行，因此逐牌预览再求和才能保持一致。

原版预览仍表示玩家观察时的当前战斗状态。未来若自定义牌在同一手内部先改变攻击修正再影响后续牌，需要另行设计顺序模拟；本规格不对未实现的状态变更做预测。

## 12. Godot 视图与 Harmony 渲染桥

### 12.1 视图层级

目标文件：Scripts/Enemy/CardIntents/View/NCardListIntentView.cs。

```text
原版 NIntent（CardListIntent 根）
├─ 原版 IntentHolder：自定义模式时隐藏
└─ NCardListIntentView
   └─ VBoxContainer
      ├─ CardRow
      │  ├─ CardSlot 1 → NCard
      │  ├─ CardSlot 2 → NCard
      │  ├─ CardSlot 3 → NCard
      │  ├─ CardSlot 4 → NCard
      │  └─ CardSlot 5 → NCard
      └─ IntentRow
         ├─ NIntent → CardAggregateAttackIntent
         └─ NIntent → DefendIntent
```

上层和下层都以 IntentPos 为整体中心。CardRow 位于 IntentRow 上方，符合用户提供截图。

### 12.2 NCard 槽位复用

- 首次绑定时创建固定数量的 CardSlot 和 NCard。
- 后续刷新只设置 NCard.Model 和槽位 Visible。
- CardListChanged、视图重绑和首次 Ready 都走同一个 BindCards 方法。
- 视图从旧状态解绑时取消 CardListChanged 订阅。
- 退出场景树时再次做幂等解绑。
- 不在每次刷新时 QueueFree 并重建五张卡。

NCard 及其所有 Control 子节点递归设置：

- MouseFilter.Ignore；
- FocusMode.None；
- 不处理输入与未处理输入；
- 禁用所有可用的 Highlight、Glow 和手牌交互状态；当前游戏版本没有对应开关时不新增替代动画。

缩略牌不响应 Hover、放大、拖拽或点击，且不得遮挡敌人的选择区域。

### 12.3 下层原版 Intent

- HasAttack 为真时显示一个通过 NIntent.Create 创建的原版 NIntent，并绑定 CardAggregateAttackIntent。
- HasAttack 为假时隐藏攻击节点。
- HasDefense 为真时显示一个原版 NIntent 并绑定 DefendIntent。
- HasDefense 为假时隐藏防御节点。
- DefendIntent 不增加数值标签。
- CardListChanged 后两个节点调用 UpdateIntent 更新显示。

### 12.4 NIntentCardListPatch

目标文件：Scripts/Patch/NIntentCardListPatch.cs。

Patch 目标：NIntent.UpdateIntent(AbstractIntent, IEnumerable<Creature>, Creature)。

Postfix 行为：

- intent 是 CardListIntent：安全取得根 NIntent 的私有 IntentHolder，隐藏 Holder，查找或创建唯一 NCardListIntentView，并绑定状态、owner 与 targets。
- intent 不是 CardListIntent：恢复 Holder，隐藏并解绑可能存在的自定义视图。
- CardListIntent 对应状态为 Faulted，或自定义视图创建/绑定失败：恢复 Holder，使根 Unknown Intent 可见，并隐藏、解绑不完整视图。
- UpdateIntent 重复调用保持幂等，不增加额外视图或 NCard。

私有 Holder 使用缓存的安全反射访问。字段缺失时不让 Patch 抛错，使用第 14 节的 Unknown Intent 回退。

## 13. 确认的测试内容与显示参数

### 13.1 模拟牌

目标文件：

- Scripts/Enemy/CardIntents/Test/BasicEnemyAttackCard.cs
- Scripts/Enemy/CardIntents/Test/BasicEnemyDefendCard.cs

基础攻击牌：

- CardModel：ModelDb.Card<StrikeTomorin>()；
- Atk：6；
- Def：0；
- 无自定义效果。

基础防御牌：

- CardModel：ModelDb.Card<DefendTomorin>()；
- Atk：0；
- Def：5；
- 无自定义效果。

这些值直接沿用当前玩家基础牌，仅用于验证基础设施，不代表影灯正式平衡。

### 13.2 测试牌组

- DeckId：STS2_TOMORIN_MOD:CARD_INTENT_BASIC_TEST。
- 牌组实例：5 张基础攻击牌和 5 张基础防御牌。
- 手牌容量：5。
- 抽牌方式：战斗 RNG 无放回随机。

测试不得断言上述配置值等于字面值；应验证 BaseEnemyCard 的攻防值来自引用 CardModel 的规范动态变量，以及牌库行为满足状态配置。

### 13.3 UI 参数

- NCard 缩放：0.24。
- 卡牌间距：6 像素。
- 上下层间距：8 像素。
- 整体最大设计宽度：480 像素；超出时按可用宽度等比缩小整行，而不是换行。
- 卡牌顺序：从左到右等于 CardList 与执行顺序。

这些参数属于可自行确定的具体属性值。实机验收可在不改变架构和交互语义的前提下微调。

## 14. 错误处理与兼容性

### 14.1 配置错误

- 重复 DeckId、缺失 CardModel、牌组容量不足或工厂复用实例：注册阶段失败。
- 未知 DeckId 或 CardId 的存档：拒绝整个对应状态快照并标记 Faulted。
- 不使用随机卡牌或复制最后一张牌作为恢复兜底。

### 14.2 执行异常

- ExecuteCardsAsync 有 IsExecuting 重入保护。
- 已成功执行并移入弃牌堆的牌绝不重试。
- 单张牌部分生效后抛错时无法回滚游戏命令，因此记录怪物 ID、StateId、CardId 和异常。
- 当前手牌按固定规则整体移入弃牌堆，状态标记 Faulted，余下牌停止执行。
- Faulted 状态不再抽牌或执行，显示 Unknown Intent。
- 怪物死亡、逃跑或战斗结束导致的停止不标记为实现故障；只清空当前运行标记并终止后续命令。

### 14.3 UI 失败

- 找不到 IntentHolder、无法创建 NCard、CardModel 无法渲染或场景结构不兼容时，恢复根 NIntent 的 Unknown 显示。
- UI 失败不改变 DeckList、CardList、DiscardList 或 RNG。
- 战斗逻辑仍按确定性状态执行。
- 相同兼容错误每场战斗只记录一次。

### 14.4 游戏版本更新

实现和升级时必须重新核对：

- NIntent.UpdateIntent 签名；
- NIntent 私有 IntentHolder 字段；
- NCard.Model setter；
- NIntent.Create；
- MonsterModel.SetMoveImmediate；
- Hook.BeforeSideTurnStart。

任何接入点失效时使用安全关闭，不猜测字段或继续执行半套 UI。

## 15. 测试敌人与显式测试 Encounter

目标文件：

- Scripts/Enemy/CardIntents/Test/CardIntentTestMonster.cs
- Scripts/Encounters/CardIntentTestEncounter.cs

CardIntentTestMonster：

- 继承 BaseCardIntentMonsterModel；
- 只有一个循环到自身的 CardIntentMoveState；
- 使用基础测试牌组；
- 复用现有敌人视觉资源；
- 使用仅供开发验证的生命值，不增加伤害上限、阶段或其他机制。

CardIntentTestEncounter：

- 具有稳定模型 ID；
- 只生成 CardIntentTestMonster；
- 不被任何 Act、地图、BossDiscoveryOrder、随机 Encounter 池或正常房间解析器引用；
- 只能通过开发控制台或明确的测试入口按 ID 启动。

测试内容保留在发布 DLL 中，不使用条件编译，以保证开发构建和发布构建的运行逻辑一致。

## 16. 文件级实施方案

### 16.1 新增领域与运行时文件

- Scripts/Enemy/CardIntents/EnemyCardId.cs
- Scripts/Enemy/CardIntents/EnemyCardDeckId.cs
- Scripts/Enemy/CardIntents/EnemyCardExecutionContext.cs
- Scripts/Enemy/CardIntents/BaseEnemyCard.cs
- Scripts/Enemy/CardIntents/EnemyCardDeckRegistry.cs
- Scripts/Enemy/CardIntents/CardIntentMoveRuntime.cs
- Scripts/Enemy/CardIntents/CardIntentMoveState.cs
- Scripts/Enemy/CardIntents/CardIntentRuntimeSnapshot.cs
- Scripts/Enemy/CardIntents/BaseCardIntentMonsterModel.cs

### 16.2 新增 Intent 与视图文件

- Scripts/Enemy/CardIntents/Intents/CardListIntent.cs
- Scripts/Enemy/CardIntents/Intents/CardAggregateAttackIntent.cs
- Scripts/Enemy/CardIntents/View/NCardListIntentView.cs

### 16.3 新增 Patch 文件

- Scripts/Patch/CardIntentTurnCoordinatorPatch.cs
- Scripts/Patch/CardIntentImmediateMovePatch.cs
- Scripts/Patch/NIntentCardListPatch.cs

### 16.4 新增测试内容

- Scripts/Enemy/CardIntents/Test/BasicEnemyAttackCard.cs
- Scripts/Enemy/CardIntents/Test/BasicEnemyDefendCard.cs
- Scripts/Enemy/CardIntents/Test/CardIntentTestMonster.cs
- Scripts/Encounters/CardIntentTestEncounter.cs
- tests/CardIntent.Tests.ps1

### 16.5 不应修改的现有内容

- 不修改 StrikeTomorin 或 DefendTomorin 的实现。
- 不修改现有 Taki 敌人卡牌。
- 不修改现有 Boss 的 MoveState。
- 不修改任何 Act、地图或正常 Encounter 池来加入测试敌人。
- 不新增 Godot 资源，因此不修改 project.godot、export_presets.cfg 或现有场景。

## 17. 推荐实现顺序

1. 新增稳定 ID、BaseEnemyCard、执行上下文和两张基础模拟牌。
2. 新增牌组注册表与纯牌堆运行时，先验证无放回、弃牌和洗牌不变量。
3. 新增 CardIntentMoveState 与构造运行时，完成左到右执行和故障状态。
4. 新增 BaseCardIntentMonsterModel、快照 DTO、捕获与恢复。
5. 新增玩家方回合协调 Patch 和强制换招 Patch。
6. 新增 CardListIntent 与 CardAggregateAttackIntent。
7. 新增 NCardListIntentView 和 NIntent.UpdateIntent 渲染桥。
8. 新增测试牌组、测试敌人和显式测试 Encounter。
9. 新增 tests/CardIntent.Tests.ps1。
10. 运行聚焦测试、完整现有测试集和 dotnet build。
11. 进行游戏内单人、多人确定性、显式快照恢复、强制换招和 UI 验收。

测试驱动要求：每个行为步骤先加入会失败的聚焦测试或可重复的测试 Encounter 验收条件，再实现最小代码使其通过。UI 节点与原版私有字段接入必须在当前游戏版本实机验证。

## 18. 测试用例

### 18.1 自动化领域测试

所有测试使用状态配置、CardModel 规范变量、集合关系和调用顺序作为期望，不断言具体配置字面值。

#### BaseEnemyCard

- 基础攻击牌 Atk 与引用 CardModel 的规范伤害变量一致。
- 基础防御牌 Def 与引用 CardModel 的规范格挡变量一致。
- Atk 为零时不调用攻击步骤；Def 为零时不调用防御步骤。
- BeforeBaseEffects 时 CustomExecuteAsync 早于基础攻防。
- AfterBaseEffects 时 CustomExecuteAsync 晚于基础攻防。
- 子类不能绕过公开 ExecuteAsync 的上下文校验。

#### 牌组注册与实例

- 同一 DeckId 两次创建的卡牌对象引用互不相同。
- 两次创建得到相同的 CardId 多重集合。
- 重复 CardId 的副本数量与模板一致。
- 非法注册被拒绝，且不产生可用的半注册牌组。

#### 抽牌、弃牌与洗牌

- PrepareCards 后 CardList 数量等于状态配置的手牌容量。
- 同一 BaseEnemyCard 实例不会同时存在于两个牌堆。
- DeckList、CardList、DiscardList 的实例多重集合始终等于初始牌组实例全集。
- DeckList 不足时从 DiscardList 补充，且不丢失、不复制实例。
- 重复 PrepareCards 不改变 CardList 顺序，也不额外调用 RNG。
- CardList 顺序等于抽取顺序。

#### 执行与强制换招

- 执行记录顺序等于 CardList 从左到右顺序。
- 每张成功牌只进入一次 DiscardList。
- 正常完成后 CardList 为空且可在下一玩家方回合重新准备。
- 玩家方回合强制换招时，旧手牌完整进入旧状态弃牌堆。
- 新 NextMove 只准备一次。
- 切换到普通 MoveState 不创建卡牌快照。
- 敌人方回合 SetMoveImmediate 不提前抽下一手。

#### 攻防 Intent

- 聚合攻击预览等于每张攻击牌分别经过原版预览后的总和。
- CardList 不含攻击时攻击 NIntent 不可见。
- CardList 存在防御牌时 DefendIntent 可见。
- DefendIntent 视图不存在防御数值标签。
- Strength、Weak 等当前修正下，聚合预览与逐牌预览的关系保持一致。

#### 快照与故障

- 捕获并恢复后，三类牌堆 CardId 多重集合、各自顺序与 IsPrepared 一致。
- 快照不包含 CardModel、Godot 节点或对象地址。
- 缺失 StateId、DeckId 或 CardId 时拒绝整个状态快照。
- 单牌部分执行后异常不会重试该牌。
- Faulted 状态不再抽牌或执行。
- 死亡、逃跑和战斗结束停止序列但不误标实现故障。

### 18.2 PowerShell 静态检查

- CardIntentMoveState 继承 MoveState，并存在 DeckId 与三个只读牌堆视图。
- BaseEnemyCard.CardModel 只用于显示，不存在调用 CardModel.OnPlay 的路径。
- Hook.BeforeSideTurnStart 只在 CombatSide.Player 准备当前 NextMove。
- SetMoveImmediate Patch 比较新旧状态，并只在玩家方回合做补抽。
- NIntent.UpdateIntent Patch 只对 CardListIntent 启用自定义视图。
- 测试 Encounter 没有被任何正常内容池引用。
- 新实现不引入 System.Random。
- 本功能没有新增 Godot 资源路径。

### 18.3 游戏内集成验收

#### 基本布局

- 混合手牌显示一排缩略牌；下层同时显示整体攻击与原版防御图标。
- 全攻击手牌隐藏防御图标。
- 全防御手牌隐藏攻击图标，防御图标不带数值。
- 牌列、下层 Intent 与敌人 IntentPos 整体居中。
- 缩略牌不响应 Hover、放大、拖拽、点击，也不遮挡敌人目标选择。

#### 生命周期

- 连续刷新 Intent 不增加 NCard 或 NCardListIntentView 节点。
- CardListIntent 切换到普通 Intent 后原版 Holder 恢复。
- 强制转阶段后旧牌列消失，新牌列与新 CardList 一致。
- 怪物死亡、逃跑或战斗结束后无残留节点和事件订阅。
- 快进模式下不会跳过下一玩家方回合的准备。

#### 行动一致性

- UI 从左到右顺序等于实际执行顺序。
- 整体攻击图标等于逐张攻击预览的总和。
- 基础防御牌执行后敌人获得与各牌 Def 贡献关系一致的格挡。
- 多轮后出现弃牌回洗，且没有卡牌丢失或重复。

#### 快照与多人

- 当前手牌准备后捕获并显式恢复快照，牌序和三类牌堆不变化。
- 主机与客户端看到相同 CardList、攻防 Intent 与执行顺序。
- 多名玩家开始回合时，同一敌人不会按玩家人数重抽。
- 显式快照恢复不额外推进 RNG；战斗中续档和断线重连不属于本次支持范围。

#### 分辨率与缩放

- 常见 16:9 与 16:10 宽高比下保持居中。
- UI 缩放变化后不越出安全区域。
- 超出最大设计宽度时整行等比缩小，不换行、不截断单牌。

## 19. 完成判定

只有以下条件全部满足，其他 Agent 才可宣称实现完成：

- 当前 NextMove 在每个玩家方整体回合只生成一次 CardList。
- CardList 是 UI、聚合攻防 Intent、执行、弃牌和快照的唯一权威顺序。
- 真实牌库循环不会丢失或复制实例。
- BaseEnemyCard 统一处理 Atk、Def 与前后置 CustomExecuteAsync。
- 原版攻击图标显示逐牌预览之和；原版防御图标不显示数值。
- 五张原版 NCard 缩略图无任何单牌交互。
- 强制换招、普通 Intent 恢复、死亡、逃跑与故障路径不会留下 UI 或重复执行牌。
- 快照可恢复稳定 ID 顺序，多人不会按玩家数量重抽。
- 测试敌人不能从正常游戏流程遇到。
- 聚焦测试、现有完整测试集和 dotnet build 通过。
- 游戏内 UI、多人确定性、显式快照恢复和生命周期验收完成。

## 20. 后续工作

第 16 节所列领域模型、运行时、Intent、视图、Patch、测试牌、测试敌人、测试 Encounter 与自动化测试均已实现。本节只记录仍需在游戏运行环境完成的集成验收和明确排除的后续扩展。

- 完成测试 Encounter 的游戏内 UI、多人确定性、显式快照恢复、强制换招和生命周期验收。
- 游戏版本更新后重新核对第 14.4 节的原版接入点。
- 另行设计影灯正式 Boss、正式牌组、阶段、强度和自定义卡牌。
- 若未来敌人卡牌需要自身可变计数，另行扩展单卡实例快照；当前只保存稳定 CardId 与牌堆顺序。
- 若未来需要 Buff、Debuff 等额外原版图标，另行设计 BaseEnemyCard 的附加 Intent 贡献接口。
- 若未来需要敌人行动时逐张消失动画，另行设计不受原版整体淡出影响的执行动画层。

## 21. Spec 自审记录

### 21.1 占位项检查

- 文档不存在任何未决占位标记、待用户补充的选项或并列候选路线。
- 自动化范围已实现；游戏内验收与扩展项集中记录在第 20 节。
- 具体测试数值、布局参数和快照版本均已给出；测试断言明确禁止依赖这些字面值。

### 21.2 一致性检查

- CardIntentMoveState 直接拥有实时牌堆，BaseCardIntentMonsterModel 只保存稳定 ID 投影，两者不存在双权威状态。
- UI、聚合攻击、执行和显式快照都读取同一个 CardList 顺序。
- 玩家方回合只为当前 NextMove 抽牌；多人不会按玩家数量重抽。
- 强制换招发生在玩家方时弃旧手并准备新手；敌人方换招留到下一玩家方回合。
- 攻击显示逐牌预览总和，防御保持原版无数值图标，与已确认需求一致。
- CardModel 只提供视觉信息，战斗效果由 BaseEnemyCard 执行。
- 测试敌人有稳定显式入口，但不进入正常内容池。

### 21.3 范围检查

- 本规格集中于通用卡牌 Intent 基础设施、模拟牌和测试入口，可以作为单个实施计划执行。
- 影灯正式 Boss、状态型单卡、额外自定义 Intent 图标和逐牌动画均明确排除并记录为后续独立设计。
- 没有要求修改现有 Boss、玩家基础牌或 Godot 资源。
- 通用框架、模拟牌、隔离 Encounter、补丁和自动化测试已经落盘；影灯正式 Boss 与正式牌组仍不在本规格范围内。

### 21.4 歧义检查

- “玩家回合开始”明确为 CombatSide.Player 的整体回合开始。
- “随机抽五张”明确为战斗 RNG、无放回、抽牌堆不足时回洗弃牌堆。
- “整体伤害”明确为各攻击牌分别走原版预览后的总和，而不是 Atk 原始值简单求和后只应用一次修正。
- “显示防御”明确为存在防御牌时显示无数值 DefendIntent。
- “卡牌 List”明确为抽取顺序、显示顺序和执行顺序相同的冻结 CardList。
- “保留原版攻击图标”明确为复合视图下层使用原版 NIntent 与 SingleAttackIntent 派生类型。

### 21.5 自审结论

规格未发现占位、内部矛盾或影响实施的双重解释。自动化实现与验证已经完成，游戏内 UI、多人确定性和生命周期仍须通过显式测试 Encounter 验收。当前只提供稳定快照捕获与显式恢复 API，不宣称支持战斗中续档或断线重连。
