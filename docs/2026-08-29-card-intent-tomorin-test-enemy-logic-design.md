# Tomorin 测试敌人卡牌逻辑 Implementation Plan

> **实现状态（2026-08-29）：** 逻辑层、自动化测试、双语 Power 本地化和资源发布已经完成。实际游戏内 UI、多人联机与断线重连仍列为人工验收项；当前实现提供主机权威捕获／恢复 API，不包含网络传输补丁。

**Goal:** 完全替换 `CardIntentTestMonster` 的旧基础攻防逻辑，建立固定敌人牌组、行动指标软锁、作词与重放、收藏品队列、动态数值投影及断线重连所需的完整逻辑层。

**Architecture:** 以一份战斗级 `EnemyCardCombatState` 作为全部牌区和收藏品的唯一权威状态；`CardIntentMoveState` 只负责接入原版 MoveState 生命周期，并把规划、素材、执行、投影分别委托给可测试服务。行动准备时冻结卡牌结构、素材和随机结果，玩家行动只使数值投影失效并重算，不改变已经公开的行动。

**Tech Stack:** .NET 9、C#、Slay the Spire 2 战斗 API、Godot 4.5.1、BaseLib、xUnit、PowerShell 结构检查。

**Spec:** `docs/2026-08-29-card-intent-tomorin-test-enemy-logic-design.md`（本文同时承载已确认规范和实施计划）。

## Global Constraints

- 本任务只实现逻辑层；不得修改 `CardListIntent`、`CardAggregateAttackIntent`、`NCardListIntentView` 或 NIntent 相关 Patch。
- Intent 和 UI 表现由其他任务负责；本逻辑必须提供冻结行动、实时投影和确定性事件作为接入数据。
- 不调用玩家 `CardModel.OnPlay`，只复用 CardModel 的显示资源、规范变量和底层战斗命令。
- 敌人不存在费用概念；除素材需求、敌人死亡/离场或结构故障外，进入结算的牌一定能够执行。
- 敌人的攻击与负面 Power 面向我方全部存活且有效的玩家；自身格挡和增益只作用于敌人。
- 所有随机选择只使用战斗 RNG；禁止引入 `System.Random`。
- 旧测试牌组和旧运行时 Snapshot 语义完全弃用，不提供迁移路径。
- 普通重新进入战斗从第一回合全新初始化；只有断线重连同步当前运行时状态。
- 自动测试不得断言配置中的固定数值；预期必须从规则对象、卡牌定义、规范变量或输入集合推导。
- 当前工作树包含使用者的其他改动；实施时不得还原、覆盖或格式化无关文件。
- 修改代码前遵循仓库 `AGENTS.md`，优先使用 CodeGraph 确认符号和影响范围。

---

## 一、范围与非目标

### 1.1 本次范围

- 完全替换 `CardIntentTestMonster` 当前的 `BasicEnemyAttackCard` / `BasicEnemyDefendCard` 行为。
- 保留 `CardIntentTestMonster`、`CardIntentTestEncounter` 及显式开发入口，不创建正式 Boss。
- 建立五个牌区：Draw、Current、Retained、Discard、Exhaust。
- 建立有序收藏品可用队列、已消耗收藏品区和收藏品展示 Power 投影。
- 实现三类行动指标、双软锁和最多三次候选评估。
- 实现作词素材优先级、Token 时机、重放、灵感受控执行、收藏品效果和即时结算。
- 实现与当前 Power 状态一致的逐牌、逐重放、逐目标实时预期值。
- 实现普通重开初始化和主机权威的断线重连同步。

### 1.2 明确不做

- 不改 Intent 模型、Intent 图标或聚合 Intent。
- 不改 NCard/NIntent 视图，不实现灰色不可打出显示或即时牌动画。
- 不把测试敌人加入正常 Act、地图或随机遭遇池。
- 不制作新的 Godot 场景、卡面、Power 图标或动画资源。
- 不兼容旧 Snapshot，不恢复普通存档中的敌人牌区和回合进度。
- 不在当前任务继续调整作词素材机制或新增补充收藏品的 Buff；这些记录到 `docs/TODO.md`。

## 二、选定架构

### 2.1 唯一权威状态

`EnemyCardCombatState` 唯一拥有：

```text
DrawPile
CurrentCards
RetainedCards
DiscardPile
ExhaustPile
CollectionQueue
ConsumedCollections
ImmediateResolutionStack
LastMetric
PreparedAction
RuntimePhase
NextGeneratedCardSequence
NextCollectionSequence
FaultDiagnostic
```

`CardIntentMoveState` 不再分别拥有可变 `_deckList`、`_cardList`、`_discardList`；它保留 `DeckList`、`CardList`、`DiscardList` 只读兼容入口，并增加其他区域、准备行动和实时投影的只读入口。

### 2.2 服务边界

```text
EnemyActionMetricPlanner       指标选择、槽位抽取、软锁和候选提交
EnemyCardMaterialResolver      素材资格、优先级和单次原子预留
EnemyCardExecutionEngine       深度优先结算、重放、生命周期和事件
EnemyActionProjectionService   只读模拟与实时预期值
EnemyCollectionInventory       收藏品队列的唯一写入口
EnemyAbilityHookDispatcher     敌人版能力触发顺序
EnemyCardRandomSource          战斗 RNG 的唯一包装与测试替换点
```

所有服务通过构造参数注入 `CardIntentMoveState`，生产路径使用战斗实现，领域 Harness 使用确定性替身。

### 2.3 卡牌定义与实例

- `EnemyCardDefinition` 是不可变语义模板，保存 CardModel、Tag、素材、效果、评分和生命周期。
- `BaseEnemyCard` 是战斗实例，保存 `CardId`、`TemplateSlot`、可选 `RuntimeInstanceId`、`ReplayCount` 和定义引用。
- 初始牌使用 `TemplateSlot` 区分同名副本。
- 战斗生成牌使用由单调序号构造的 `RuntimeInstanceId`。
- `CardInstanceKey = TemplateSlot ?? RuntimeInstanceId`，所有计划、素材和重连引用都使用该键。
- 注册指纹覆盖 CardId、CardModel 类型、Tag、评分档案、素材需求、生命周期、Token 时机和有序效果程序 ID；任一项变化都视为不兼容定义。

## 三、Tag、卡牌目录与数值

### 3.1 七类 Tag

```csharp
[Flags]
public enum EnemyCardTag
{
    None = 0,
    Ability = 1 << 0,
    Buff = 1 << 1,
    Gain = 1 << 2,
    CollectionGenerator = 1 << 3,
    Defense = 1 << 4,
    Attack = 1 << 5,
    Compose = 1 << 6
}
```

通用规则：任何非能力牌，只要直接对任一方施加力量、敏捷、心之壁以外的 Power，就带 `Buff`。如果同一张牌也获得三种增益属性，则同时带 `Gain` 和 `Buff`。能力牌只带 `Ability`，不因施加自身能力 Power 自动增加 `Buff`。收藏品不参与七类 Tag 或行动指标。

### 3.2 初始牌组

每种初始牌由 `CardIntentTestDeck` 配置两份；实现和测试不得另写总张数常量。用户名称到实际玩家模型的映射及逻辑如下：

测试牌组的稳定 ID 为 `STS2_TOMORIN_MOD:CARD_INTENT_TOMORIN_TEST`；测试怪物的唯一自循环状态 ID 保持 `CARD_INTENT_TEST_LOOP`。

| 用户名称 | 玩家模型 | Tag | 敌人效果 | 评分档案 |
|---|---|---|---|---|
| Rain | `SorrowfulRain` | Ability | 获得 3 层敌人版悲伤之雨；每次成功作词按 Power 层数获得心之壁 | 15 |
| Adayume | `Adayume` | Ability | 获得 1 层敌人版迷星叫；每次成功执行单元按层数获得格挡和心之壁 | 5 |
| NameOfTears | `NameOfTear` | Ability | 获得对应标记，继续由现有心之壁规则读取 | 5 |
| Attack | `StrikeTomorin` | Attack | 对全部有效玩家造成 6 点伤害 | 6 |
| WhyPlayHaruhikage | `WhyPlayHaruhikage` | Attack, CollectionGenerator | 对全部有效玩家造成 16 点伤害；每次执行生成两种互不重复的随机收藏品 | 16 |
| ThisIdNoNeed | `ThisNoNeed` | Attack, Defense | 按非作词规则消耗一张合法素材；对全部有效玩家造成 5 点伤害并获得 5 点格挡 | 10 |
| Defend | `DefendTomorin` | Defense | 获得 5 点格挡 | 5 |
| ATField | `AtField` | Defense, Gain | 消耗一张状态素材；获得 13 点格挡和 5 点心之壁 | 23 |
| HopeOnTheVoice | `HopeOnTheVoice` | Buff, CollectionGenerator | 对全部有效玩家施加虚弱和易伤；生成午夜咖啡；消耗 | 10 |
| CannotBeingHuman | `CannotBeingHuman` | Gain | 获得 1 点敏捷和 4 点心之壁 | 10 |
| Woodlouse | `Woodlouse` | Defense, CollectionGenerator | 获得 8 点格挡；生成残缺音符 | 8 |
| Hitoshizuku | `Hitoshizuku` | Attack, Compose | 消耗一张攻击素材，生成 HitoshizukuToken；对全部有效玩家造成 6 点伤害；作词来源消耗 | 6 |
| NamelessPaper | `NamelessPaper` | Attack, Buff, Compose | 消耗一张攻击素材，生成 SongOfBeHuman；对全部有效玩家造成 9 点伤害并施加易伤；作词来源消耗 | 14 |

未明确改动的牌从对应玩家 CardModel 的规范变量读取数值，但不得调用玩家出牌流程。`NameOfTearPower` 与 `AtFieldPower` 的交互以当前运行时代码的实际实现为准，不在本任务修正文档注释与实现之间的倍率差异。

### 3.3 Token

| Token | Tag | 时机与效果 | 生命周期 | 评分档案 |
|---|---|---|---|---|
| `HitoshizukuToken` | Attack | 来源牌后立即执行；对全部有效玩家执行 9×2 | 未标注消耗，成功后进入弃牌堆 | 18 |
| `SongOfBeHuman` | Compose, Gain, Defense | 保留到下一回合；消耗两张技能素材并生成 Haruhikage；获得 5 敏捷和 20 格挡 | 成功作词后消耗；素材不足则继续保留 | 30 |
| `Haruhikage` | Compose, Gain | 保留到下一回合；消耗两张状态素材并生成 PrideManSaki；获得 20 心之壁 | 成功作词后消耗；素材不足则继续保留 | 40 |
| `PrideManSaki` | Attack | 保留到下一回合；对全部有效玩家执行 5×10 | 牌本身带 Exhaust，成功后消耗 | 50 |

保留 Token 在下一回合形成强制前缀，先于指标牌执行；它们不占指标槽位，也不参与软锁。HitoshizukuToken 为同回合即时步骤，同样绕过指标和软锁。

## 四、行动指标与软锁

### 4.1 指标配置

```text
增益：Ability + Gain + Defense
攻击：Attack + Attack + Random + Random
作词测试：CollectionGenerator + Defense + Compose
```

第一回合从三种指标等概率选择；后续从不等于 `LastMetric` 的其他指标中等概率选择。指标槽位数量必须严格按配方，不补齐额外牌。

### 4.2 槽位抽取

按左到右顺序处理：

1. 一个实例最多填一个槽位，多 Tag 不允许复用。
2. 从当前抽牌堆所有匹配 Tag 的牌中随机选择。
3. 当前抽牌堆没有匹配牌但仍有牌时，从当前抽牌堆随机选一张兜底。
4. 抽牌堆非空时不得提前回洗弃牌堆寻找 Tag。
5. 抽牌堆为空时回洗弃牌堆。
6. 两者都为空时该槽位为空。

每个候选从相同权威牌区的事务副本开始；候选被拒绝时只保留 RNG 推进，不提交任何牌区变化。第三个候选无论是否超过锁都提交。

### 4.3 评分

```text
攻击锁 = 80
总评分锁 = 100
总评分 = 伤害 + 格挡 + 普通 Buff Power 层数 × 5
         +（力量 + 敏捷 + 心之壁）× 2
```

- 任一候选的总攻击伤害或总评分超过对应阈值时重新抽取。
- 评分只计算每张选中牌的一次本体直接效果，完全忽略 `ReplayCount`。
- 不计算保留牌、即时牌、未来 Token、收藏品、灵感受控执行或当前能力触发的额外收益。
- 使用准备时的当前战斗修正，但不会把候选牌将要施加的 Buff 回灌到同一候选的其他牌。
- 行动公开后，玩家施加虚弱等效果不会重新评分、重抽或改变指标。

## 五、结构冻结与实时数值投影

### 5.1 冻结内容

`PreparedEnemyCardAction` 冻结：

- 行动指标、保留前缀和指标牌实例；
- 每张来源牌的最大尝试次数；
- 每次尝试的素材绑定与截断位置；
- 收藏品生成、即时抽牌和回收随机结果；
- 深度优先子步骤顺序；
- 来源区域与预期生命周期；
- 准备时软锁诊断。

玩家行动不得改变以上内容。

### 5.2 动态投影

`EnemyActionProjectionService` 从当前战斗状态重新生成 `LiveActionProjection`：

- 每个来源实例、每次重放分别记录；
- 每名有效玩家分别记录每击伤害、攻击次数和总伤害；
- 记录当前实际格挡、Power 层数和三种增益变化；
- 按行动顺序模拟，使前一步的力量、敏捷、心之壁和能力触发影响后续牌；
- 玩家给敌人施加虚弱，或给某名玩家施加易伤/减伤后，相关投影随即变化；
- 投影不得移动牌、调用命令、触发钩子或推进 RNG。

Power、目标有效性或玩家行动变化时主动失效缓存；读取时再比较 `ProjectionInputFingerprint`，防止第三方 Mod 绕过事件。准备时软锁诊断保持不变。

## 六、素材、灵感与重放

### 6.1 资格先于优先级

- 普通素材必须满足卡牌要求的 CardType。
- Epiphany（灵光乍现）是作词通配素材。
- 来源牌永远不允许消费自己。
- 同一卡同时具有 Inspiration 和 Epiphany 时，只进入当前规则中优先级最高的层级。

### 6.2 最终素材优先级

作词消耗攻击或技能：

```text
收藏品队列中最早的合法 Epiphany
→ 手牌中类型匹配的 Inspiration
→ 手牌中的 Epiphany
→ 手牌中普通的类型匹配牌
```

作词消耗状态：

```text
收藏品队列中最早的合法收藏品
→ 手牌中的 Epiphany
→ 手牌中普通状态牌
```

普通 Inspiration 状态牌在状态作词中受到保护，除非它同时是 Epiphany。

非作词消耗：

```text
手牌中符合要求的 Inspiration
→ 收藏品队列中最早的合法项
→ 手牌中的 Epiphany
→ 手牌中普通合法牌
```

### 6.3 单次原子支付与逐次重放

- `ReplayCount = 4` 表示本体一次加四次重放，最多执行五次。
- 每次尝试单独支付一组素材。
- 当前尝试先完整预留全部素材；不足时不消费任何素材，并立即停止该牌后续重放。
- 已成功次数不回滚。
- 至少成功一次时，来源牌按成功出牌完成一次最终生命周期。
- 一次都未成功时，按不可打出规则处理。
- 成功次数不会减少或清除 `ReplayCount`；实例以后再次抽到时仍按 `1 + ReplayCount` 重新尝试。
- 软锁始终忽略重放。

当前尝试预留完成后才移除素材。素材附带效果产生的新牌或收藏品只能供下一次重放或后续来源牌使用，不能补足已经开始的当前支付。

### 6.4 灵感受控执行

只有从手牌消费的 Inspiration 牌触发受控执行：

- 执行其敌人适配后的直接效果，并按该实例的 `1 + ReplayCount` 重复；
- 禁止再次支付素材、发起作词或生成下一级作词 Token；
- 直接攻击、格挡、Power 和收藏品生成仍生效；
- 每次实际成功执行都属于成功出牌单元，会触发 Adayume；
- 绕过指标和软锁。

## 七、收藏品系统

### 7.1 显式目录

`CardIntentTestCollectionCatalog` 通过通用 `EnemyCollectionCatalog` 显式注册且只注册：

| 收藏品 | 敌人适配效果 |
|---|---|
| `BrokenNote` | 获得玩家版对应格挡并获得对应 Power |
| `ColdRedTea` | 对全部有效玩家施加玩家版虚弱与 CustomConstrict |
| `CrumpledPaper` | 随机抽取一张敌人牌并立即处理 |
| `LeftoverBuffet` | 从统一可回收消耗视图中随机回收一个对象 |
| `MidnightCoffee` | 随机抽取一张敌人牌并立即处理 |
| `StarStone` | 仅作为 Epiphany 通配素材，没有附加效果 |

不得依赖当前为空的 `CollectionsCardPool.GenerateAllCards()`。

### 7.2 生成与队列

- 测试战斗初始化时，队列按配置放入五张 StarStone。
- WhyPlay 每次实际执行从完整目录生成两种互不重复的类型，按抽取顺序追加；不同执行之间允许重复。
- Hope 固定生成 MidnightCoffee，Woodlouse 固定生成 BrokenNote。
- 受控灵感执行和重放中的生成节点照常生成。
- 消耗始终选择队列中最早的合法实例。

### 7.3 即时抽取与回收

CrumpledPaper 和 MidnightCoffee：

1. 使用冻结的战斗 RNG 结果从抽牌堆随机取一张；抽牌堆为空时先回洗弃牌堆。
2. 在当前素材效果位置插入即时步骤，深度优先结算。
3. 可执行则正常执行效果、重放和生命周期。
4. 不可执行则直接进入弃牌堆，即使该牌具有 Retain。

LeftoverBuffet：

- 候选集合统一包含普通 `ExhaustPile` 和 `ConsumedCollections`。
- LeftoverBuffet 先进入 `ConsumedCollections` 再选目标，不能立即选中自己。
- 回收普通牌后立即尝试执行；不可执行则进入弃牌堆。
- 回收收藏品只追加到可用队列尾部，不立即触发效果。

### 7.4 收藏品 Power

`EnemyCollectionInventoryPower` 是权威队列的只读投影：

- `Amount` 等于当前可用收藏品总数；
- 数量为零时仍保留 Power；
- 描述列出实际队列顺序，只压缩连续同类项；
- 已消耗收藏品不显示；
- 初始化、生成、消耗、回收和重连应用后同步；
- 不参与 Tag、评分、Buff 统计或能力钩子；
- 暂时复用现有 StarStone/收藏品资源，不增加自定义 UI。

## 八、结算顺序与生命周期

### 8.1 深度优先顺序

```text
预留并移除当前尝试全部素材
  → 依绑定顺序处理每个素材效果
    → 深度优先处理素材产生的即时牌或回收
  → 执行来源牌直接效果
  → 完成本次作词结果
  → 触发 AfterCompose / SorrowfulRain
  → 触发成功出牌 / Adayume
  → 尝试下一次重放
```

每个成功执行单元重新读取真实战斗修正。所有攻击和负面 Power 遍历全部存活且有效的玩家；死亡、逃离或无效目标跳过。

### 8.2 生命周期

- 不检查能量或费用；“无法打出”在正常玩法中只表示当前尝试无法完整支付所需素材。
- 普通攻击和技能成功后进入弃牌堆。
- 能力牌成功后进入消耗堆。
- 作词来源牌成功作词后进入消耗堆。
- 普通素材进入消耗堆，收藏品素材进入 `ConsumedCollections`。
- 玩家定义带 Exhaust 的敌人牌进入消耗堆。
- Token 身份本身不意味着消耗。
- 素材不足导致完全不可打出：保留牌继续保留，普通牌进入弃牌堆，即时牌直接进入弃牌堆。
- 至少成功一次后才在整组重放结束时移动来源牌一次。

### 8.3 能力钩子

- `CardIntentSorrowfulRainPower` 使用标准 Power 模型显示层数；每次成功作词后由 `EnemyAbilityHookDispatcher` 获得相应心之壁。
- `CardIntentAdayumePower` 使用标准 Power 模型显示层数；普通执行、每次重放、即时执行和受控灵感执行成功后分别触发。
- 无法打出的牌和被截断的重放不触发能力。
- `NameOfTearPower` 保持标记语义。

## 九、故障、事件与安全上限

素材不足是正常结果，不进入 `Faulted`。结构引用丢失、冻结结果不可恢复、效果处理器异常或非法递归才进入 `Faulted`。

逻辑层按确定性顺序发布：

```text
ActionPrepared
CardMarkedUnplayable
MaterialReserved
CardConsumed
CollectionConsumed
CollectionGenerated
ImmediateCardQueued
ReplayTruncated
CardResolved
ActionInterrupted
ExecutionFaulted
ActionCompleted
```

事件只携带稳定实例身份、步骤序号、状态和投影引用，不保存或调用 UI 对象。

故障策略：

- 已提交战斗效果不回滚；
- 当前来源牌按已经成功的次数整理生命周期；
- 尚未执行的保留牌返回 Retained；
- 尚未执行的指标牌进入 Discard；
- 停止后续步骤并记录 `ExecutionFaulted`；
- 不重新抽牌或临时改变公开行动。

敌人死亡或离场属于正常 `ActionInterrupted`，停止后续牌和重放，不标记程序故障。

准备模拟和实际执行都使用同一可配置的有限步骤上限，初始建议值为 256。测试只读取规则配置验证有限终止，不断言该字面值。

- 准备模拟超过上限或违反结构不变量时，丢弃整个准备事务，不提交候选牌区或半成品计划，并将状态标记为 `Faulted`。
- 实际执行超过上限时，保留已经提交的战斗结果，按上述故障策略整理当前牌并停止后续步骤。

## 十、新战斗初始化与断线重连

### 10.1 普通重新开始

`InitializeFreshCardCombat()` 必须幂等地：

- 从当前 Deck 配置重建全部初始牌实例；
- 清空 Current、Retained、Discard、Exhaust 和即时队列；
- 清空重放、生成序号、LastMetric、PreparedAction 和 Fault；
- 重建配置中的初始收藏品队列并清空已消耗收藏品；
- 同步收藏品 Power；
- 从第一回合开始。

普通存档不保存运行中牌区和回合。

### 10.2 重连同步

以主机为唯一权威，`EnemyCardRuntimeSyncState` 传输：

- 五个牌区的实例身份、顺序和 ReplayCount；
- 收藏品可用/已消耗队列与实例身份；
- 下一生成序号、LastMetric、RuntimePhase；
- 冻结行动、素材绑定、随机结果、即时子步骤和下一个未执行游标；
- Fault 状态与诊断。

客户端先在临时对象中完成唯一性、区域互斥、定义、素材引用和游标校验，再一次性应用。失败时请求主机重发，不执行本地随机初始化。实时投影不作为权威数据传输，应用后根据当前 Power 重算。

敌人行动中发生重连时，主机等待当前最小原子步骤完成，再发送下一个未执行游标；不序列化执行到一半的异步命令。

## 十一、文件映射

### 11.1 通用逻辑

| 文件 | 操作 | 职责 |
|---|---|---|
| `Scripts/Enemy/CardIntents/BaseEnemyCard.cs` | 修改 | 定义引用、实例身份和 ReplayCount |
| `Scripts/Enemy/CardIntents/EnemyCardTag.cs` | 新增 | 七类 Tag |
| `Scripts/Enemy/CardIntents/EnemyCardDefinition.cs` | 新增 | 不可变卡牌语义 |
| `Scripts/Enemy/CardIntents/EnemyCardScoreProfile.cs` | 新增 | 软锁计分档案 |
| `Scripts/Enemy/CardIntents/EnemyCardInstanceKey.cs` | 新增 | 初始/生成实例统一身份 |
| `Scripts/Enemy/CardIntents/EnemyCardCombatState.cs` | 新增 | 五牌区、收藏品和运行阶段唯一权威 |
| `Scripts/Enemy/CardIntents/EnemyActionMetric.cs` | 新增 | 指标与槽位定义 |
| `Scripts/Enemy/CardIntents/EnemyActionRecipe.cs` | 新增 | 三种指标的有序槽位配方 |
| `Scripts/Enemy/CardIntents/PreparedEnemyCardAction.cs` | 新增 | 冻结计划及步骤 DTO |
| `Scripts/Enemy/CardIntents/EnemyActionMetricPlanner.cs` | 新增 | 选择、软锁和原子提交 |
| `Scripts/Enemy/CardIntents/EnemyCardScoreCalculator.cs` | 新增 | 双锁公式和准备时评分 |
| `Scripts/Enemy/CardIntents/EnemyCardRandomSource.cs` | 新增 | 战斗 RNG 包装与测试 seam |
| `Scripts/Enemy/CardIntents/EnemyCardMaterialResolver.cs` | 新增 | 素材资格、优先级和预留 |
| `Scripts/Enemy/CardIntents/EnemyMaterialRequest.cs` | 新增 | 单次执行的素材需求 |
| `Scripts/Enemy/CardIntents/EnemyMaterialReservation.cs` | 新增 | 完整且有序的素材绑定 |
| `Scripts/Enemy/CardIntents/EnemyCardExecutionEngine.cs` | 新增 | 深度优先执行与生命周期 |
| `Scripts/Enemy/CardIntents/EnemyCardEffectNode.cs` | 新增 | Simulate/Execute 共享语义节点 |
| `Scripts/Enemy/CardIntents/EnemyCardExecutionContext.cs` | 修改 | 真实结算上下文与冻结结果访问 |
| `Scripts/Enemy/CardIntents/EnemyCardSimulationContext.cs` | 新增 | 无副作用的顺序模拟状态 |
| `Scripts/Enemy/CardIntents/EnemyCardExecutionCursor.cs` | 新增 | 深度优先步骤和重连边界游标 |
| `Scripts/Enemy/CardIntents/EnemyAbilityHookDispatcher.cs` | 新增 | 敌人版能力钩子顺序 |
| `Scripts/Enemy/CardIntents/EnemyActionProjectionService.cs` | 新增 | 实时逐目标投影 |
| `Scripts/Enemy/CardIntents/LiveActionProjection.cs` | 新增 | 逐牌、逐重放、逐目标投影 DTO |
| `Scripts/Enemy/CardIntents/EnemyCollectionInventory.cs` | 新增 | 收藏品权威队列 |
| `Scripts/Enemy/CardIntents/EnemyCollectionCatalog.cs` | 新增 | 可复用的收藏品定义注册表 |
| `Scripts/Enemy/CardIntents/EnemyCollectionDefinition.cs` | 新增 | 收藏品稳定定义和效果程序 |
| `Scripts/Enemy/CardIntents/EnemyCollectionInstance.cs` | 新增 | 收藏品运行时实例身份 |
| `Scripts/Enemy/CardIntents/EnemyCardResolutionEvent.cs` | 新增 | 逻辑事件 |
| `Scripts/Enemy/CardIntents/EnemyCardRuntimeSyncState.cs` | 新增 | 当前版本重连 DTO |
| `Scripts/Enemy/CardIntents/CardIntentRuntimeSnapshot.cs` | 删除 | 旧版语义完全弃用 |
| `Scripts/Enemy/CardIntents/CardIntentMoveState.cs` | 修改 | 原版状态机适配器 |
| `Scripts/Enemy/CardIntents/CardIntentMoveRuntime.cs` | 修改 | 向现有 Intent 暴露逻辑只读投影和变更事件 |
| `Scripts/Enemy/CardIntents/EnemyCardDeckRegistry.cs` | 修改 | 定义、模板槽位和完整指纹 |
| `Scripts/Enemy/CardIntents/BaseCardIntentMonsterModel.cs` | 修改 | 新战斗初始化与重连接口 |

### 11.2 测试敌人与 Power

| 文件 | 操作 | 职责 |
|---|---|---|
| `Scripts/Enemy/CardIntents/Test/CardIntentTestRules.cs` | 新增 | 锁、尝试、步骤上限、指标和初始收藏品配置 |
| `Scripts/Enemy/CardIntents/Test/CardIntentTestCardCatalog.cs` | 新增 | 初始牌和 Token 定义 |
| `Scripts/Enemy/CardIntents/Test/CardIntentTestCollectionCatalog.cs` | 新增 | 六种测试收藏品的敌人适配效果注册 |
| `Scripts/Enemy/CardIntents/Test/CardIntentTestDeck.cs` | 修改 | 新 Deck 模板和幂等注册 |
| `Scripts/Enemy/CardIntents/Test/CardIntentTestMonster.cs` | 修改 | 注入新运行时并维持单状态自循环 |
| `Scripts/Enemy/CardIntents/Test/BasicEnemyAttackCard.cs` | 删除 | 旧测试牌弃用 |
| `Scripts/Enemy/CardIntents/Test/BasicEnemyDefendCard.cs` | 删除 | 旧测试牌弃用 |
| `Scripts/Encounters/CardIntentTestEncounter.cs` | 保留 | 显式测试入口，不进入 Act |
| `Scripts/Powers/EnemyPowers/CardIntentSorrowfulRainPower.cs` | 新增 | 敌人版能力层数模型 |
| `Scripts/Powers/EnemyPowers/CardIntentAdayumePower.cs` | 新增 | 敌人版能力层数模型 |
| `Scripts/Powers/EnemyPowers/EnemyCollectionInventoryPower.cs` | 新增 | 收藏品队列标准 Power 投影 |
| `STS2_Tomorin_Mod/localization/eng/powers.json` | 修改 | 新 Power 英文文本 |
| `STS2_Tomorin_Mod/localization/zhs/powers.json` | 修改 | 新 Power 中文文本 |

### 11.3 测试

| 文件 | 操作 | 职责 |
|---|---|---|
| `tests/CardIntentHarness/CardIntentHarness.csproj` | 修改 | 编译全部 `*.testcs` |
| `tests/CardIntentHarness/DomainIdentityTests.testcs` | 新增 | 定义、实例和牌区守恒 |
| `tests/CardIntentHarness/ActionPlannerTests.testcs` | 新增 | 指标、抽取和软锁 |
| `tests/CardIntentHarness/MaterialResolverTests.testcs` | 新增 | 三套素材优先级和原子支付 |
| `tests/CardIntentHarness/ExecutionEngineTests.testcs` | 新增 | 重放、生命周期、作词和全体目标 |
| `tests/CardIntentHarness/CollectionInventoryTests.testcs` | 新增 | 收藏品生成、消费、回收和 Power |
| `tests/CardIntentHarness/LiveProjectionTests.testcs` | 新增 | Power 变化后的实时数值 |
| `tests/CardIntentHarness/ReconnectStateTests.testcs` | 新增 | 初始化和主机权威重连 |
| `tests/CardIntentHarness/CardIntentDomainTests.testcs` | 删除 | 由职责化测试文件替代 |
| `tests/CardIntent.Tests.ps1` | 修改 | 新结构契约与 Harness 入口 |

## 十二、TDD 实施任务

### Task 1: 定义、实例身份与唯一战斗状态

**Files:**
- Create: `Scripts/Enemy/CardIntents/EnemyCardTag.cs`
- Create: `Scripts/Enemy/CardIntents/EnemyCardDefinition.cs`
- Create: `Scripts/Enemy/CardIntents/EnemyCardScoreProfile.cs`
- Create: `Scripts/Enemy/CardIntents/EnemyCardInstanceKey.cs`
- Create: `Scripts/Enemy/CardIntents/EnemyCardCombatState.cs`
- Modify: `Scripts/Enemy/CardIntents/BaseEnemyCard.cs`
- Modify: `Scripts/Enemy/CardIntents/EnemyCardDeckRegistry.cs`
- Test: `tests/CardIntentHarness/DomainIdentityTests.testcs`

**Interfaces:**
- Produces: `EnemyCardDefinition`, `EnemyCardInstanceKey`, `EnemyCardCombatState`, `EnemyCardDeckRegistry.CreateCombatState(EnemyCardDeckId)`。
- Consumers: Tasks 2–8。

- [x] **Step 1: 写实例身份和五牌区守恒的失败测试**

```csharp
[Fact]
public void RegisteredDeckCreatesUniqueInstancesMatchingItsTemplate()
{
    EnemyCardCombatState state = CreateStateFromHarnessDefinition();
    Assert.Equal(state.Definition.TemplateSlots, state.DrawPile.Select(card => card.TemplateSlot));
    Assert.Equal(AllCards(state).Count(), AllCards(state).Select(card => card.InstanceKey).Distinct().Count());
}
```

- [x] **Step 2: 运行领域测试并确认因新类型缺失而失败**

Run: `dotnet test tests/CardIntentHarness/CardIntentHarness.csproj --filter FullyQualifiedName~DomainIdentityTests`

- [x] **Step 3: 实现最小不可变定义、实例键和状态所有权**

```csharp
public sealed record EnemyCardInstanceKey(string Value);

public sealed class EnemyCardCombatState
{
    public IReadOnlyList<BaseEnemyCard> DrawPile => _drawPile;
    public IReadOnlyList<BaseEnemyCard> CurrentCards => _currentCards;
    public IReadOnlyList<BaseEnemyCard> RetainedCards => _retainedCards;
    public IReadOnlyList<BaseEnemyCard> DiscardPile => _discardPile;
    public IReadOnlyList<BaseEnemyCard> ExhaustPile => _exhaustPile;
}
```

- [x] **Step 4: 扩展注册校验，确保模板槽位、实例和定义指纹稳定**
- [x] **Step 5: 重跑测试并确认实例多重集合、唯一性和只读边界通过**
- **Step 6：未创建提交；共享工作区按交付规则保留未提交修改。**

```bash
git add Scripts/Enemy/CardIntents tests/CardIntentHarness
git commit -m "refactor: add authoritative enemy card combat state"
```

### Task 2: 行动指标、抽取与双软锁

**Files:**
- Create: `Scripts/Enemy/CardIntents/EnemyActionMetric.cs`
- Create: `Scripts/Enemy/CardIntents/EnemyActionRecipe.cs`
- Create: `Scripts/Enemy/CardIntents/PreparedEnemyCardAction.cs`
- Create: `Scripts/Enemy/CardIntents/EnemyActionMetricPlanner.cs`
- Create: `Scripts/Enemy/CardIntents/EnemyCardScoreCalculator.cs`
- Create: `Scripts/Enemy/CardIntents/EnemyCardRandomSource.cs`
- Create: `Scripts/Enemy/CardIntents/Test/CardIntentTestRules.cs`
- Test: `tests/CardIntentHarness/ActionPlannerTests.testcs`

**Interfaces:**
- Consumes: `EnemyCardCombatState`, `EnemyCardDefinition`, `EnemyCardScoreProfile`。
- Produces: `PreparedEnemyCardAction Prepare(EnemyCardCombatState, EnemyPlanningContext)`、`EnemyCardScoreCalculator.Calculate` 与唯一战斗 RNG seam。

- [x] **Step 1: 写指标不连续、槽位左到右和候选事务隔离的失败测试**
- [x] **Step 2: 写相对阈值软锁测试，所有边界从 `CardIntentTestRules` 读取**

```csharp
decimal attackLock = rules.AttackLock;
CandidateProfile rejected = CandidateProfile.WithAttackAbove(attackLock);
CandidateProfile accepted = CandidateProfile.WithAttackNotAbove(attackLock);
Assert.Same(accepted, planner.Prepare(state, Sequence(rejected, accepted)).Candidate);
```

- [x] **Step 3: 运行 Planner 测试并确认失败**
- [x] **Step 4: 实现指标排除、匹配抽取、随机兜底和按需回洗**
- [x] **Step 5: 实现双锁、最大候选次数和第三次强制提交**
- [x] **Step 6: 验证拒绝候选不改牌区但推进 RNG，重放不影响评分**
- **Step 7：未创建提交；共享工作区按交付规则保留未提交修改。**

```bash
git add Scripts/Enemy/CardIntents tests/CardIntentHarness/ActionPlannerTests.testcs
git commit -m "feat: plan enemy card actions with soft locks"
```

### Task 3: 素材解析与收藏品权威队列

**Files:**
- Create: `Scripts/Enemy/CardIntents/EnemyCardMaterialResolver.cs`
- Create: `Scripts/Enemy/CardIntents/EnemyMaterialRequest.cs`
- Create: `Scripts/Enemy/CardIntents/EnemyMaterialReservation.cs`
- Create: `Scripts/Enemy/CardIntents/EnemyCollectionCatalog.cs`
- Create: `Scripts/Enemy/CardIntents/EnemyCollectionDefinition.cs`
- Create: `Scripts/Enemy/CardIntents/EnemyCollectionInstance.cs`
- Create: `Scripts/Enemy/CardIntents/EnemyCollectionInventory.cs`
- Test: `tests/CardIntentHarness/MaterialResolverTests.testcs`
- Test: `tests/CardIntentHarness/CollectionInventoryTests.testcs`

**Interfaces:**
- Produces: `TryReserve(EnemyMaterialRequest, EnemyMaterialContext, out EnemyMaterialReservation)`。
- Produces: `EnemyCollectionInventory.Append/Consume/Recover`。

- [x] **Step 1: 为三套优先级分别写同时存在多个合法候选的失败测试**
- [x] **Step 2: 写来源排除、队列稳定顺序和单次完整预留测试**
- [x] **Step 3: 运行相关测试并确认失败**
- [x] **Step 4: 实现资格过滤和三个显式优先级策略**

```csharp
public bool TryReserve(
    EnemyMaterialRequest request,
    EnemyMaterialContext context,
    out EnemyMaterialReservation reservation)
{
    IReadOnlyList<EnemyMaterialCandidate> ordered = BuildPriorityOrder(request, context);
    reservation = EnemyMaterialReservation.TryCreateComplete(request, ordered);
    return reservation.IsComplete;
}
```

- [x] **Step 5: 实现收藏品可用/已消耗区域及 InventoryChanged 事件**
- [x] **Step 6: 重跑测试，确认不完整支付零修改、完整支付保持绑定顺序**
- **Step 7：未创建提交；共享工作区按交付规则保留未提交修改。**

```bash
git add Scripts/Enemy/CardIntents tests/CardIntentHarness
git commit -m "feat: resolve enemy materials and collection inventory"
```

### Task 4: 共享效果节点、执行引擎与能力钩子

**Files:**
- Create: `Scripts/Enemy/CardIntents/EnemyCardEffectNode.cs`
- Create: `Scripts/Enemy/CardIntents/EnemyCardExecutionEngine.cs`
- Create: `Scripts/Enemy/CardIntents/EnemyCardResolutionEvent.cs`
- Modify: `Scripts/Enemy/CardIntents/EnemyCardExecutionContext.cs`
- Create: `Scripts/Enemy/CardIntents/EnemyCardSimulationContext.cs`
- Create: `Scripts/Enemy/CardIntents/EnemyCardExecutionCursor.cs`
- Create: `Scripts/Enemy/CardIntents/EnemyAbilityHookDispatcher.cs`
- Create: `Scripts/Powers/EnemyPowers/CardIntentSorrowfulRainPower.cs`
- Create: `Scripts/Powers/EnemyPowers/CardIntentAdayumePower.cs`
- Test: `tests/CardIntentHarness/ExecutionEngineTests.testcs`

**Interfaces:**
- Produces: `IEnemyCardEffectNode.Simulate` 与 `ExecuteAsync`。
- Produces: `ExecutePreparedActionAsync(PreparedEnemyCardAction, EnemyExecutionContext)`。

- [x] **Step 1: 写全体目标、无效目标跳过和自身增益隔离的失败测试**
- [x] **Step 2: 写逐次重放支付、首次不足截断和 ReplayCount 持久测试**
- [x] **Step 3: 写成功/不可打出生命周期与能力触发次数测试**
- [x] **Step 4: 运行 Execution 测试并确认失败**
- [x] **Step 5: 实现通用效果节点和深度优先步骤游标**

```csharp
public interface IEnemyCardEffectNode
{
    void Simulate(EnemyCardSimulationContext context);
    Task ExecuteAsync(EnemyCardExecutionContext context);
}
```

- [x] **Step 6: 实现每次重放前的素材预留、正常截断和最终一次生命周期移动**
- [x] **Step 7: 实现敌人版能力 Power 与 `EnemyAbilityHookDispatcher`**
- [x] **Step 8: 实现事件、死亡中止和不可回滚的 Fault 收敛**
- [x] **Step 9: 重跑测试并确认实际完成次数不改变 ReplayCount**
- **Step 10：未创建提交；共享工作区按交付规则保留未提交修改。**

```bash
git add Scripts/Enemy/CardIntents Scripts/Powers/EnemyPowers tests/CardIntentHarness
git commit -m "feat: execute enemy card effects and replay hooks"
```

### Task 5: Tomorin 卡牌、Token 与收藏品效果目录

**Files:**
- Create: `Scripts/Enemy/CardIntents/Test/CardIntentTestCardCatalog.cs`
- Create: `Scripts/Enemy/CardIntents/Test/CardIntentTestCollectionCatalog.cs`
- Modify: `Scripts/Enemy/CardIntents/Test/CardIntentTestDeck.cs`
- Delete: `Scripts/Enemy/CardIntents/Test/BasicEnemyAttackCard.cs`
- Delete: `Scripts/Enemy/CardIntents/Test/BasicEnemyDefendCard.cs`
- Test: `tests/CardIntentHarness/ExecutionEngineTests.testcs`
- Test: `tests/CardIntentHarness/CollectionInventoryTests.testcs`

**Interfaces:**
- Produces: 完整初始牌与 Token `EnemyCardDefinition`。
- Consumes: Tasks 1–4 的身份、效果节点、素材和执行接口。

- [x] **Step 1: 写目录映射、Tag、玩家变量来源和敌人覆盖关系测试**
- [x] **Step 2: 写作词搜索顺序、Token 时机和保留前缀测试**
- [x] **Step 3: 写六种收藏品、受控灵感、即时抽牌和统一回收测试**
- [x] **Step 4: 运行目录与效果测试并确认失败**
- [x] **Step 5: 用有序效果节点建立十三种初始牌和四种 Token 定义**
- [x] **Step 6: 建立六种收藏品效果，确保抽牌/回收使用冻结结果**
- [x] **Step 7: 替换测试 Deck ID 和模板，删除旧基础牌**
- [x] **Step 8: 重跑测试，预期值只与定义或玩家规范变量比较**
- **Step 9：未创建提交；共享工作区按交付规则保留未提交修改。**

```bash
git add Scripts/Enemy/CardIntents/Test tests/CardIntentHarness
git commit -m "feat: add Tomorin enemy card and collection catalog"
```

### Task 6: 实时数值投影

**Files:**
- Create: `Scripts/Enemy/CardIntents/EnemyActionProjectionService.cs`
- Create: `Scripts/Enemy/CardIntents/LiveActionProjection.cs`
- Test: `tests/CardIntentHarness/LiveProjectionTests.testcs`

**Interfaces:**
- Produces: `LiveActionProjection GetProjection(PreparedEnemyCardAction, EnemyProjectionContext)`。
- Consumes: 共享效果节点的 `Simulate` 路径。

- [x] **Step 1: 写敌人虚弱后全部攻击投影变化且冻结计划不变的失败测试**
- [x] **Step 2: 写不同玩家承伤 Power 产生逐目标差异的失败测试**
- [x] **Step 3: 写前置增益影响后续牌、投影不推进 RNG 的测试**
- [x] **Step 4: 运行 Projection 测试并确认失败**
- [x] **Step 5: 实现顺序模拟、逐重放和逐目标投影 DTO**
- [x] **Step 6: 实现事件失效加输入指纹兜底缓存**
- [x] **Step 7: 重跑测试，确认软锁诊断和牌区均不随投影变化**
- **Step 8：未创建提交；共享工作区按交付规则保留未提交修改。**

```bash
git add Scripts/Enemy/CardIntents/EnemyActionProjectionService.cs tests/CardIntentHarness/LiveProjectionTests.testcs
git commit -m "feat: project live enemy card values"
```

### Task 7: 收藏品 Power、新战斗初始化与重连

**Files:**
- Create: `Scripts/Powers/EnemyPowers/EnemyCollectionInventoryPower.cs`
- Create: `Scripts/Enemy/CardIntents/EnemyCardRuntimeSyncState.cs`
- Delete: `Scripts/Enemy/CardIntents/CardIntentRuntimeSnapshot.cs`
- Modify: `Scripts/Enemy/CardIntents/BaseCardIntentMonsterModel.cs`
- Modify: `STS2_Tomorin_Mod/localization/eng/powers.json`
- Modify: `STS2_Tomorin_Mod/localization/zhs/powers.json`
- Test: `tests/CardIntentHarness/ReconnectStateTests.testcs`
- Test: `tests/CardIntentHarness/CollectionInventoryTests.testcs`

**Interfaces:**
- Produces: `InitializeFreshCardCombat()`。
- Produces: `CaptureReconnectState()` 与 `TryApplyReconnectState(EnemyCardRuntimeSyncState)`。

- [x] **Step 1: 写幂等全新初始化及第一回合状态测试**
- [x] **Step 2: 写重连往返、无 RNG 推进和实时投影重算测试**
- [x] **Step 3: 写损坏输入全量拒绝、当前状态零部分提交和旧格式拒绝测试**
- [x] **Step 4: 写收藏品 Power 数量、顺序压缩和零库存保留测试**
- [x] **Step 5: 运行测试并确认失败**
- [x] **Step 6: 实现当前唯一重连 DTO、临时校验计划和原子应用**
- [x] **Step 7: 实现幂等初始化和收藏品 Power 同步**
- [x] **Step 8: 删除旧 Snapshot API 和 DTO，不保留迁移分支**
- [x] **Step 9: 更新双语本地化并验证 JSON**
- [x] **Step 10: 重跑测试并确认安全边界游标不会重复已提交步骤**
- **Step 11：未创建提交；共享工作区按交付规则保留未提交修改。**

```bash
git add Scripts/Enemy/CardIntents Scripts/Powers/EnemyPowers STS2_Tomorin_Mod/localization tests/CardIntentHarness
git commit -m "feat: sync enemy card reconnect state and inventory power"
```

### Task 8: 接入 CardIntentTestMonster 并替换测试契约

**Files:**
- Modify: `Scripts/Enemy/CardIntents/CardIntentMoveState.cs`
- Modify: `Scripts/Enemy/CardIntents/CardIntentMoveRuntime.cs`
- Modify: `Scripts/Enemy/CardIntents/BaseCardIntentMonsterModel.cs`
- Modify: `Scripts/Enemy/CardIntents/Test/CardIntentTestMonster.cs`
- Modify: `tests/CardIntentHarness/CardIntentHarness.csproj`
- Delete: `tests/CardIntentHarness/CardIntentDomainTests.testcs`
- Modify: `tests/CardIntent.Tests.ps1`

**Interfaces:**
- Consumes: Tasks 1–7 全部公共接口。
- Produces: 可从显式 Encounter 运行的完整测试敌人逻辑。

- [x] **Step 1: 调整 Harness 项目以编译全部职责化 `*.testcs`**

```xml
<ItemGroup>
  <Compile Include="*.testcs" />
</ItemGroup>
```

- [x] **Step 2: 更新 PowerShell 契约，移除旧基础牌和旧 Snapshot 断言**
- [x] **Step 3: 写测试敌人单状态自循环、新 Deck ID 和正常内容隔离检查**
- [x] **Step 4: 运行 `tests/CardIntent.Tests.ps1` 并确认接入测试先失败**
- [x] **Step 5: 将 `CardIntentMoveState` 改为注入统一状态和各策略服务**
- [x] **Step 6: 在 `CardIntentTestMonster` 幂等注册目录并调用全新初始化**
- [x] **Step 7: 保持 `CardIntentTestEncounter` 显式入口，不修改 Intent/View/Patch**
- [x] **Step 8: 运行领域 Harness、PowerShell 契约和 `dotnet build`**
- [x] **Step 9: 因 Power 本地化发生变化，运行 `dotnet publish` 生成资源包**
- **Step 10：未创建提交；共享工作区按交付规则保留未提交修改。**

```bash
git add Scripts/Enemy/CardIntents Scripts/Powers/EnemyPowers STS2_Tomorin_Mod/localization tests
git commit -m "feat: replace card intent test monster logic"
```

### Task 9: 显式 Encounter 验收与回归

**Files:**
- Modify only if a verified logic defect is found; do not modify UI/Intent files.

**Interfaces:**
- Consumes: 完整测试敌人和 `EnemyCardResolutionEvent`。
- Produces: 验收记录与无回归构建结果。

自动化已覆盖初始化、指标规划、回洗、素材不足、重放截断、虚弱等输入变化后的纯投影、当前格式重连往返与损坏输入全量拒绝。以下内容需要在游戏客户端中人工验收，本次未将其误记为自动化完成：

- 通过稳定 Encounter ID 启动 `CardIntentTestEncounter`，检查收藏品 Power、保留前缀、同回合 Token 和旧五槽 UI 回退。
- 在多人环境验证全部有效玩家受击、逐目标显示以及主机权威同步。
- 在安全原子步骤边界进行真实断线重连，确认牌区、收藏品、ReplayCount 和行动游标一致且不重复效果。
- 验证怪物死亡／离场、不同分辨率和 UI 缩放下的表现。

## 十三、测试设计约束与用例矩阵

### 13.1 禁止固定数值断言

测试必须使用下列关系：

```csharp
Assert.Equal(definition.InitialDeckEntries, state.AllInitialInstancesByTemplate());
Assert.Equal(rules.InitialCollections, inventory.Available.Select(item => item.CollectionId));
Assert.Equal(effectDefinition.Expected(context), projection.Actual(context));
Assert.True(overLock.Score > rules.ScoreLock);
Assert.True(withinLock.Score <= rules.ScoreLock);
```

不得在测试中重新写入攻击锁、评分锁、牌组份数、初始收藏品数量、卡牌伤害、格挡、Power 层数或最大候选次数。

### 13.2 必须覆盖的行为

- 定义/实例：模板多重集合、实例唯一、同名副本不同 ReplayCount、跨区域身份保持。
- 指标：首回合全集、后续排除上次、左到右、无重复实例、随机兜底、按需回洗、空槽。
- 软锁：双锁独立、拒绝事务隔离、RNG 保留、最后候选提交、忽略重放/保留/即时。
- 素材：三套优先级、来源排除、队列最早、完整预留、当前支付不被新素材补齐。
- 重放：`1 + ReplayCount`、逐次支付、首次不足截断、已完成不回滚、标记不减少。
- 生命周期：能力/作词/Exhaust、普通弃牌、保留失败、即时失败、HitoshizukuToken 回收循环。
- 作词：搜索四区域稳定顺序、排除消耗/收藏品/来源、既有实例加重放、Token 时机。
- 能力：SorrowfulRain 只响应成功作词；Adayume 响应每个实际成功执行单元。
- 收藏品：目录、WhyPlay 单次去重、队列顺序、即时抽牌、统一回收、Leftover 自排除。
- 目标：全部有效玩家、无效目标跳过、自身增益隔离、逐目标差异。
- 投影：虚弱/力量/易伤变化、顺序增益、逐重放、无副作用、不重新软锁。
- 重连：新战斗重置、幂等、往返、损坏全拒绝、旧格式拒绝、安全边界游标。
- 故障：正常不足不 Fault、结构异常停止、已提交不回滚、死亡正常中止、有限步骤上限。

## 十四、验收命令

实施 Agent 应按顺序执行：

```powershell
dotnet test tests\CardIntentHarness\CardIntentHarness.csproj --nologo --verbosity minimal
powershell -ExecutionPolicy Bypass -File tests\CardIntent.Tests.ps1
dotnet build
dotnet publish
```

若完整 PowerShell 回归入口另有仓库约定，先用 CodeGraph/仓库测试脚本确认后补跑；不得以聚焦测试通过替代完整回归。

## 十五、后续任务边界

以下事项不属于本实施计划，统一记录在 `docs/TODO.md`：

- UI 灰色显示不可打出的牌；
- UI 显示收藏品即时抽取、回收与深度优先处理；
- Intent/UI 消费逐目标实时投影和超过原固定槽位的保留前缀；
- 继续调整作词素材策略，使作词更倾向消费收藏品；
- 设计额外 Buff，为收藏品队列提供补充来源；
- 将测试逻辑迁移到未来正式 Boss、正式牌组和正式 Encounter；
- 游戏版本变化后复核战斗 RNG、Power 修正、MoveState 和联机同步接入点。

## 十六、Spec 自审清单

- [x] 所有用户明确要求均可定位到规范章节和实施任务。
- [x] UI/Intent 非目标与逻辑输出边界一致。
- [x] 初始牌、Token、Tag、效果、时机和生命周期均有唯一描述。
- [x] 软锁公式、阈值、重抽次数和重放忽略规则一致。
- [x] 素材优先级、单次原子支付和逐次重放没有相互冲突。
- [x] 收藏品队列、受控灵感、即时抽牌与回收顺序闭合。
- [x] 冻结行动和实时数值投影明确分层。
- [x] 普通重开与断线重连明确分层，旧 Snapshot 无迁移路径。
- [x] 测试只断言配置关系和领域不变量，不固定配置数值。
- [x] 所有后续 UI、平衡和正式内容工作已外移至 TODO List。

## 十七、执行交接

本计划已经实现。生产代码位于 `Scripts/Enemy/CardIntents/`、`Scripts/Powers/EnemyPowers/` 及测试 Encounter 接入文件；领域与恢复测试位于 `tests/CardIntentHarness/`，静态与本地化契约位于 `tests/CardIntent.Tests.ps1`。

实现采用一份 `EnemyCardCombatState` 作为唯一权威状态，准备阶段冻结结构、素材和随机结果，执行阶段只消费冻结计划；`EnemyCardRuntimeSyncState` 只接受当前版本并通过临时状态全量校验后原子替换。网络消息发送、客户端可视 UI 和真实断线流程不在本实现中伪造，保留为上述人工验收边界。
