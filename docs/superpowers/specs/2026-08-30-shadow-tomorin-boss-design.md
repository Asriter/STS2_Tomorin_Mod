# 影灯三阶段卡牌首领设计

**状态：** 已通过玩法设计确认，等待实现计划  
**日期：** 2026-08-30  
**正式首领：** 影灯（Shadow Tomorin）  
**替换对象：** 第四层临时占位首领 `CrychicPhatom`  
**依赖：** 敌人卡牌 Intent、作词、收藏品、Replay、实时投影与五牌区权威状态

## 1. 目标

本设计定义第四层正式首领影灯的：

- 玩家卡组数值门槛；
- 三阶段连续血条与锁血时序；
- 每阶段行为指标、活跃牌池和敌人特供卡牌效果；
- `CarryAcrossPhase` 跨阶段契约；
- P2 收藏品供给与 P3 作词终结链；
- X 费牌与 Replay 的统一结算语义；
- 静态牌面评分和完整冻结行动风险评分；
- 第 7～8 回合仅由成长、Replay、Token 与牌组压缩自然产生的软狂暴；
- 断线恢复、诊断与验收边界。

影灯必须使用一套持续存在的敌人卡牌战斗状态。阶段切换不得重建整套 `EnemyCardCombatState`，不得取消已经公开的旧阶段行动，也不得通过固定行动序列实现阶段体验。

## 2. 非目标与禁止项

本设计明确禁止：

- 将 `CrychicPhatom` 继续作为正式第四层首领；
- 把开发测试敌人 `CardIntentTestMonster` 直接接入正式路线；
- 使用 `Utakotoba / 诗超绊` 或其 Token；
- 固定第 N 回合必定打出某张牌；
- 固定 P1、P2、P3 的逐手牌序；
- 第 7 回合直接乘伤害、直接加固定属性或强制插入斩杀牌；
- 第 8 回合切换到不经过牌组的固定大招；
- 阶段切换时调用 `InitializeFreshCardCombat()`；
- 阶段切换时通过 `SetMoveImmediate` 取消或替换已经公开的行动；
- 阶段切换时移除任何 `CarryAcrossPhase` 卡牌，无论其当前牌区；
- 用静态单卡评分代替完整 Replay、Token、收藏品和能力连锁投影；
- 在投影不完整时通过 `ForcedOverLock` 强制执行未知行动。

整个流程允许的固定阶段行为只有：

1. 在安全阶段迁移点移除旧模板、加入新模板；
2. 在阶段迁移点施加或保留阶段状态。

所有实际攻击、防御、作词、成长和终结行为均由随机行为指标、牌组、素材与权威运行时状态产生。

## 3. 玩家卡组数值模型

### 3.1 前置流程模型

完成“Oblivionis 关卡中不击杀任何队友，直接击杀 Oblivionis，再击败 FullPowerOblivionis”的玩家卡组基准为：

| 维度 | 前置流程基准 |
|---|---:|
| 持续净有效单体输出 | 180 / 玩家回合 |
| 爆发净有效单体输出 | 240 / 玩家回合 |
| 平均防御能力 | 35 / 敌方攻击回合 |
| 峰值防御能力 | 60 |
| 有效能量 | 5 / 玩家回合 |
| 每回合可见牌数 | 9 |
| 特殊资源运转窗口 | 12 |

### 3.2 影灯第四层模型

影灯模型在前置流程平均门槛上提高 20%。连续数值直接乘 1.2；牌数、资源窗口等离散运转指标向上取整，保证取整后不低于 20% 门槛：

| 维度 | 影灯门槛 |
|---|---:|
| 持续净有效单体输出 | 216 / 玩家回合 |
| 爆发净有效单体输出 | 288 / 玩家回合 |
| 平均防御能力 | 42 / 敌方攻击回合 |
| 峰值防御能力 | 72 |
| 有效能量 | 6 / 玩家回合 |
| 每回合可见牌数 | 11 |
| 特殊资源运转窗口 | 15 |
| 状态牌处理能力 | 3 个稳定处理位 |

这里的输出是影灯格挡与心之壁生效后，最终实际扣除影灯 HP 的净有效伤害，而不是玩家牌面伤害之和。

## 4. 总体战斗架构

### 4.1 单一权威状态

影灯从入场到死亡只使用一个持续存在的卡牌 Intent 状态和一个 `EnemyCardCombatState`。

状态至少包含：

- `ActivePhase`：当前真正生效的阶段；
- `PendingPhase`：已经显示、等待安全迁移的下一阶段；
- Draw、Current、Retained、Discard、Exhaust 五个权威牌区；
- Available、Consumed 两个收藏品库存区；
- `LastMetric`；
- `PreparedAction`、即时结算栈与执行游标；
- 每个实例的 `InstanceKey` 与 `ReplayCount`；
- 本敌方行动的有效牌计数和逐实例冻结 X 数据；
- 阶段迁移修订号与同步诊断。

影灯使用一个正式 DeckId。P1、P2、P3 是同一 DeckId 下的阶段模板配置，不通过替换 DeckId 表示阶段切换。

### 4.2 锁血触发与行动连续性

P1、P2 使用 `EnemyMaxDamageReceivedPower` 限制该阶段累计可失去的 HP。

玩家伤害达到本阶段额度时：

1. 伤害在阶段额度处截断，溢出伤害不进入下一阶段；
2. 立即设置 `PendingPhase` 并播放阶段变化提示；
3. 不调用 `SetMoveImmediate`；
4. 不取消当前已经公开的旧阶段行动；
5. 玩家结束回合后，影灯完整执行旧阶段冻结行动；
6. 旧行动所有来源、Replay、Immediate Token、素材、生命周期和能力触发提交完毕；
7. 在 Idle 安全点执行原子阶段迁移；
8. 下一次行动准备才使用新阶段牌池和指标规则。

视觉上可以立即显示“即将进入下一阶段”，但 `ActivePhase` 只有在原子迁移完成后才改变。

`PendingPhase != None` 时，旧阶段伤害额度的剩余值必须已经为 0；迁移完成前受到的后续伤害仍被截断，不得提前写入下一阶段额度。

### 4.3 安全迁移前置条件

阶段迁移只能在以下条件全部成立时执行：

```text
PendingPhase != None
RuntimePhase == Idle
PreparedAction == null
ImmediateResolutionStack.Count == 0
旧行动的所有生命周期与素材变更已经提交
```

## 5. 血量、阶段阈值与目标回合

### 5.1 单人基准

影灯单人基准总 HP 为 1200，且不拥有直接回复 HP 的卡牌效果。

| 阶段 | HP 区间 | 本阶段累计伤害额度 | 目标体验 |
|---|---:|---:|---|
| P1 启动 | 1200 → 984 | 216 | 达标构筑经历 1 次低烈度行动 |
| P2 积累与检测 | 984 → 552 | 432 | 达标构筑经历 2 次资源与防御检测 |
| P3 NamelessPaper | 552 → 0 | 552 | 持续输出需 3 个玩家回合，爆发输出只需 2 个 |

多人模式必须对最大 HP、P1 额度和 P2 额度使用同一套遭遇缩放系数，保持 216 : 432 : 552 的阶段比例。所有攻击软锁仍按单名玩家承受的最大伤害核算，而不是把多人总伤害相加。

上表与下方回合公式是单人验收基准。多人验收中，`D_team` 表示一个完整玩家方循环内所有存活玩家造成的团队净有效伤害；最大 HP 与两个阶段额度必须使用同源缩放函数。单人的击杀回合表不直接承诺多人击杀轮次，需另设多人矩阵验证。

### 5.2 净输出回合验证

每阶段独立截断溢出伤害，因此：

```text
击杀玩家回合数
= ceil(216 / D)
+ ceil(432 / D)
+ ceil(552 / D)
```

其中 `D` 是每玩家回合净有效输出。

| 净输出 | P1 用时 | P2 用时 | P3 用时 | 击杀回合 | 实际承受敌方行动 |
|---:|---:|---:|---:|---:|---:|
| 288 | 1 | 2 | 2 | 第 5 回合 | 4 次 |
| 216 | 1 | 2 | 3 | 第 6 回合 | 5 次 |
| 200 | 2 | 3 | 3 | 第 8 回合 | 7 次 |
| 180 | 2 | 3 | 4 | 第 9 回合 | 8 次 |

目标节奏为：

- 第 5 回合：爆发构筑奖励窗口；
- 第 6 回合：推荐击杀回合；
- 第 7 回合：必须已经明显进入成长惩罚区；
- 第 8 回合：通过累计状态、Replay 与压缩自然进入软狂暴；
- 不存在基于回合号直接执行的额外强化。

## 6. 阶段压力预算

下表伤害为单名玩家承受的完整行动总伤害，包含本行动中的来源牌、Replay、保留前缀、Immediate Token、力量和易伤，尚未扣除玩家格挡。

| 阶段 | 常规完整伤害 | 条件高点 | 单行动生存收益 | 定位 |
|---|---:|---:|---:|---|
| P1 | 24～36 | 最高约 48 | 8～18 格挡等价 | 平均 42 防御基本可以完整处理 |
| P2 | 42～60 | 最高约 72 | 18～32 格挡等价 | 平均防御开始漏血，峰值 72 可以覆盖高点 |
| P3 | 60～84 | 90～108 | 24～42 格挡等价 | 常态超过平均防御，允许条件性超模 |
| 超时成长后 | 90～120 | 120～150 | 由累计状态决定 | 第 7～8 回合软狂暴 |

同一行动不能同时免费取得攻击和生存预算的两个上限。是否越界由完整总风险软锁判断，而不是单独为牌写固定互斥顺序。

这里的区间是实战调参观察带，不是额外硬锁。P3 `AttackRisk <= 96` 才能作为普通候选提交；96 以上只能由第三个、投影完整的候选以 `ForcedOverLock` 提交。设计不再设置第二层绝对伤害上限，因为未封顶的心之壁转伤与 Replay 正是长线惩罚的一部分；超过观察带的完整行动必须记录遥测，但不得因此改成固定保底行为。

达标路径五次敌方行动的中位裸伤参考为：

```text
30 + 50 + 50 + 72 + 72 = 274
```

玩家若五回合都只提供 42 防御，会承受约 64 点穿透压力；若将两次 72 峰值防御留给 P3，实际损失显著下降。

## 7. 长线惩罚原则

第 7～8 回合不读取回合号施加额外数值。低输出玩家之所以进入惩罚区，是因为影灯比达标路径多执行了两到三次随机卡牌行动：

- P1 已生效能力（若已建立）继续被有效牌、正常 Exhaust 和成功作词触发；P2/P3 成长不以某个 P1 能力必然出现为前提；
- P2/P3 收藏品状态继续提供素材；
- 力量、敏捷与心之壁继续通过牌组内卡牌累积；
- 普通来源牌自然 Exhaust，使活跃牌组进一步压缩；
- `CarryAcrossPhase` Token 保留身份与 Replay；
- 同类作词结果再次生成时增加现有 Token 的 ReplayCount；
- 第三次 P3 行动通常进入九张紧凑模板与既有 Carry Token 的小循环；只有所需槽位超过当前 Draw 余量时才按既有规则回洗；
- 越来越多完整候选自然超过 P3 软锁，第三候选被标记为 `ForcedOverLock`。

下表是调参观察区间，不是固定发放或硬上限：

| 战斗状态 | 力量 | 敏捷 | 心之壁 | 高效 Token / Replay 状态 |
|---|---:|---:|---:|---:|
| 正常进入 P3 | 0～2 | 1～3 | 10～20 | 0～1 个 |
| 第 6 回合击杀窗口 | 2～4 | 3～6 | 20～36 | 1～2 个 |
| 进入第 7 回合惩罚区 | 4～6 | 5～8 | 32～50 | 2～3 个 |
| 进入第 8 回合软狂暴 | 6～9 | 8～12 | 48～72 | 3 个以上或核心 Token 已重放 |

## 8. P1：启动阶段牌池

P1 活跃模板数为 12，不包含任何 Compose 来源。

| 卡牌 | 数量 | 影灯特供效果 | 标签 | 生命周期 | Carry |
|---|---:|---|---|---|---|
| `SorrowfulRain` | 1 | 获得能力：以后每次成功作词获得 3 心之壁 | Ability, Gain | Exhaust | 否 |
| `Adayume` | 1 | 获得能力：每个成功结算的执行单元给予 1 格挡和 1 心之壁；本体与每次 Replay 分别触发，但这不改变有效牌计数 `N` | Ability, Gain, Defense | Exhaust | 否 |
| `HeartBeat` | 1 | 获得能力：卡牌因正常生命周期进入 Exhaust 时获得 2 格挡 | Ability, Defense | Exhaust | 否 |
| `DuckAndCover` | 1 | 获得能力：每次行动准备前获得等同当前心之壁的格挡 | Ability, Defense | Exhaust | 否 |
| `NameOfTear` | 1 | 获得不可叠加能力：心之壁反击伤害变为 1.5 倍 | Ability, Gain | Exhaust | 否 |
| `BuildAtField` | 2 | 敌人版取消抽牌，改为获得 2 心之壁 | Gain, Defense | Discard | 否 |
| `DefendTomorin` | 2 | 获得 5 格挡 | Defense | Discard | 否 |
| `StrikeTomorin` | 2 | 对每名玩家造成 6 点伤害 | Attack | Discard | 否 |
| `TomorinPunch` | 1 | 全体 8 伤害，获得 8 格挡和 2 心之壁 | Attack, Gain, Defense | Discard | 否 |

P1 中 5/12 是能力牌，另有 4 张成长或防御模板，只有 3 张直接攻击模板。P1 未打出的能力来源会在迁移时退出；已经打出的能力 Power 保留。

## 9. P2：收藏品、心之壁与作词阶段

### 9.1 阶段状态

P1 → P2 时施加敌人特供 `ShadowTomoriFormPower`。

其唯一固定阶段效果是：每个 P2/P3 **敌方行动准备周期**恰好冻结并提供 1 张随机收藏品。这里的准备周期包含该行动最多三次候选尝试，不是“每个候选一次”。

确定性提交协议：

1. 准备周期开始时用权威战斗 RNG 恰好选择一个 `FrozenPreparationCollection`；
2. 同一周期的全部候选共享这一选择，并在各自候选库存副本中把同一实例增量加入 Available；不得重抽，也不得随候选累计成 2～3 张；
3. 候选冻结计划可以使用该收藏品，完整投影也必须看见它；
4. 只有最终 `PreparedAction` 原子提交时，才把对应 `PreparedPreActionInventoryDelta` 写入真实 Available；
5. 三个候选全部因配置或模拟器 Fault 失败时不写真实库存，但 RNG 推进、冻结选择和故障诊断仍须可同步、可重连；
6. P2 第一份行动准备也执行一次；P3 继续保留该状态；
7. 收藏品只进入敌人库存，不打开玩家 UI，不自动执行玩家卡牌效果。

推荐随机权重：

| 收藏品 | 权重 |
|---|---:|
| `BrokenNote` | 25% |
| `CrumpledPaper` | 20% |
| `MidnightCoffee` | 15% |
| `ColdRedTea` | 15% |
| `LeftoverBuffet` | 15% |
| `StarStone` | 10% |

`StarStone` 是唯一通配作词素材。

### 9.2 P2 活跃模板

P2 活跃模板数为 11。

| 卡牌 | 数量 | 影灯特供效果 | 标签 | 生命周期 | Carry |
|---|---:|---|---|---|---|
| `AtField` | 2 | 消耗 1 张 Status 收藏品，获得 13 格挡和 5 心之壁 | Defense, Gain, CollectionConsumer | Discard | 否 |
| `CannotBeingHuman` | 1 | 获得 1 敏捷和 4 心之壁 | Gain, Defense | Discard | 否 |
| `Woodlouse` | 1 | 获得 8 格挡，向库存加入 1 个 `BrokenNote` | Defense, CollectionGenerator | Discard | 否 |
| `UnwantedSixth` | 1 | 当前完整行动每次独立获得格挡时额外获得 1 心之壁；生成 1 个 `CrumpledPaper` | Ability, Gain, CollectionGenerator | Exhaust | 否 |
| `PoetryOrLyrics` | 1 | 消耗至多 3 张 Available 收藏品，每张给予 1 敏捷和 1 心之壁 | Gain, Defense, CollectionConsumer | Exhaust | 否 |
| `ThisNoNeed` | 1 | 消耗 1 张非 Compose 来源牌，全体 5 伤害并获得 5 格挡 | Attack, Defense | Discard | 否 |
| `HopeOnTheVoice` | 1 | 每名玩家获得 1 虚弱和 1 易伤；生成 1 个 `MidnightCoffee` | Control, CollectionGenerator | Exhaust | 否 |
| `Hitoshizuku` | 1 | 作词消耗 1 Attack；成功时先立即执行 Token，随后无论成功与否全体 6 伤害 | Attack, Compose | Exhaust | 否 |
| `WantBeYourGod` | 1 | 作词消耗 1 Skill；获得 5 心之壁，成功则生成下回合保留的防御 Token | Compose, Gain, Defense | Exhaust | 否 |
| `TomorinPunch` | 1 | 全体 8 伤害，获得 8 格挡和 2 心之壁 | Attack, Gain, Defense | Discard | 否 |

P2 中 10/11 张牌直接产生或放大格挡、敏捷、心之壁或收藏品。P2 每个候选行动最多包含一个 Compose 来源。

### 9.3 P2 作词结果

| Token | 效果 | 时机 | 生命周期 | Carry |
|---|---|---|---|---|
| `HitoshizukuToken` | 对每名玩家造成 9×2 伤害 | Immediate | Discard | 是 |
| `WantBeYourGodToken` | 获得 9 格挡和 1 心之壁 | RetainedNextTurn | Exhaust | 是 |

## 10. P3：NamelessPaper 启动阶段

P2 → P3 时不固定打出 `NamelessPaper`，不把它强制放入 Retained，也不保证 P3 第一手抽到它。

“NamelessPaper 启动”只表示：

- 在原子阶段迁移中加入两张 `NamelessPaper` 新模板；
- 开放完整的保留作词终结链；
- P2 随机收藏品状态继续工作；
- P2 已生成的收藏品和 Carry Token 完整保留。

P3 活跃模板数为 9。

| 卡牌 | 数量 | 影灯特供效果 | 标签 | 生命周期 | Carry |
|---|---:|---|---|---|---|
| `NamelessPaper` | 2 | 作词消耗 1 Attack；全体 9 伤害并给予 1 易伤；成功后生成 `SongOfBeHuman` | Attack, Control, Compose | Exhaust | 否 |
| `Mayoiuta` | 1 | 作词消耗 1 Attack；成功时先立即执行 Token，随后全体 6 伤害并给予 2 易伤 | Attack, Control, Compose | Exhaust | 否 |
| `Hitoshizuku` | 1 | 作词消耗 1 Attack；成功时先立即执行 9×2 Token，随后全体 6 伤害 | Attack, Compose | Exhaust | 否 |
| `Senzaihyoumei` | 1 | 作词消耗 1 Status 收藏品；成功后生成下回合保留的 X 费 Token | Compose | Exhaust | 否 |
| `SingFullPower` | 1 | 对每名玩家造成 `9 + 3 × 当前心之壁` 伤害，不消耗心之壁 | Attack, Finisher | Discard | 否 |
| `WhyPlayHaruhikage` | 1 | 全体 16 伤害，生成 2 个随机收藏品 | Attack, CollectionGenerator | Discard | 否 |
| `TomorinPunch` | 1 | 全体 8 伤害，获得 8 格挡和 2 心之壁 | Attack, Gain, Defense | Discard | 否 |
| `WantToBeingHuman` | 1 | 若至少有 4 心之壁，则移除 4 心之壁并永久获得 1 力量；不足时失败并保留 | Gain, HeartWallConsumer | Discard；失败 Retain | 否 |

P3 中 7/9 张模板实例具有直接攻击效果，另有 1 张生成延迟攻击 Token；5/9 张是 Compose 来源，此外 `SingFullPower` 与 `WantToBeingHuman` 负责资源转化。Compose 来源自然 Exhaust 后，九张来源池会继续压缩到可重复攻击、力量转化和 Carry Token 小循环。

P3 额外 Token 的正式敌人定义：

| Token | 效果 | 时机 | 生命周期 | Carry |
|---|---|---|---|---|
| `MayoiutaToken` | 对每名玩家造成 5×5 伤害 | Immediate | Discard | 是 |
| `SenzaihyoumeiToken` | 对每名玩家造成 `8 × FinalHitCount` 伤害；次数按第 14 节冻结 | RetainedNextTurn | Discard | 是 |

`SingFullPower` 不设置人为伤害上限：

- 10～20 心之壁对应 39～69 伤害；
- 32～50 心之壁对应 105～159 伤害；
- 心之壁不被消费，因此同时保留防御和反伤价值；
- 是否允许该行动通过由完整攻击风险与完整总风险软锁判断。

## 11. NamelessPaper 保留作词链

```text
NamelessPaper
  └─消耗 1 Attack
      → SongOfBeHuman（下回合保留）
          └─消耗 2 Skill
              → +5 敏捷、+20 格挡
              → Haruhikage（下回合保留）
                  └─消耗 2 Status 收藏品
                      → +20 心之壁
                      → PrideManSaki（下回合保留）
                          → 5×10 全体伤害
```

生命周期：

- `SongOfBeHuman`：素材不足时继续 Retain；成功后 Exhaust；
- `Haruhikage`：素材不足时继续 Retain；成功后 Exhaust；
- `PrideManSaki`：成功后 Exhaust；
- 三者全部 `CarryAcrossPhase = true`；
- 同类 Token 再次生成时增加现有实例 `ReplayCount`，不创建第二个实例；
- 已在 Exhaust 的同类实例仍保留身份与新增 Replay，不因阶段或区域丢失。

## 12. CarryAcrossPhase 契约

### 12.1 不变量

若一张卡定义标记 `CarryAcrossPhase = true`，阶段迁移不得移除、替换、重建或改绑该实例。

该规则无条件覆盖：

- Draw；
- Current；
- Retained；
- Discard；
- Exhaust。

正常 Idle 安全点的 Current 应为空；保留 Current 区 Carry 的要求是恢复兼容和防御性不变量，迁移过程不得通过清空 Current 来绕过它。

必须保留：

- 原对象身份；
- `InstanceKey`；
- 当前所在区域及区域顺序；
- `ReplayCount`；
- Token 家族与生成序列；
- 与冻结行动、重连 DTO 相关的稳定身份。

### 12.2 当前明确标记 Carry 的定义

| 定义 | Carry |
|---|---|
| `HitoshizukuToken` | 是 |
| `WantBeYourGodToken` | 是 |
| `MayoiutaToken` | 是 |
| `SenzaihyoumeiToken` | 是 |
| `SongOfBeHuman` | 是 |
| `Haruhikage` | 是 |
| `PrideManSaki` | 是 |
| 六种收藏品的 Available / Consumed 实例 | 独立库存不变量，等价完整保留 |

所有普通阶段来源牌均为非 Carry。它们已经产生的 Power、力量、敏捷、心之壁不会随来源牌移除而消失。

### 12.3 原子迁移算法

```text
TransitionAtSafePoint(nextPhase):
  require PendingPhase == nextPhase
  require RuntimePhase == Idle
  require PreparedAction == null
  require ImmediateResolutionStack empty

  for zone in Draw, Current, Retained, Discard, Exhaust:
    for card in zone snapshot:
      if card.Definition.CarryAcrossPhase:
        preserve card object, zone, order, key and ReplayCount
      else:
        remove card directly without triggering Exhaust hooks

  preserve all Powers and creature stats
  preserve Available and Consumed collection inventory and sequence counters

  replace phase damage cap atomically:
    entering P2 -> close the zeroed P1 cap and install a fresh 432-scaled P2 cap
    entering P3 -> close the zeroed P2 cap and install no further phase cap

  create fresh instances for every next-phase template
  add them to Draw
  shuffle with authoritative combat RNG

  reset LastMetric
  ActivePhase = nextPhase
  PendingPhase = None
  publish one atomic state change
```

迁移不得被 UI、同步或实时投影观察为半完成状态。

## 13. 阶段行为指标

阶段主题是牌池、指标权重、迁移状态和完整评分共同形成的统计倾向，不是逐手演出保证。验收应使用一组确定性 RNG 种子的分布、上下界和失败诊断，不要求单局必然出现某张能力、作词或终结牌。

指标随机选择，不固定逐回合顺序。阶段迁移后重置 `LastMetric`；同一指标不能连续出现，其余权重重新归一化。

| 阶段 | 指标 | 初始权重 | 推荐槽位 |
|---|---|---:|---|
| P1 | `Gain` | 55% | Ability + Ability/Gain + Defense |
| P1 | `Fortify` | 25% | Defense + Gain + 非攻击任意牌 |
| P1 | `Pressure` | 20% | Attack + Attack/Gain + Defense |
| P2 | `Fortify` | 40% | Defense + Gain + CollectionGenerator/Consumer |
| P2 | `Compose` | 35% | 1 Compose + 对应素材 + Defense/Gain |
| P2 | `Pressure` | 25% | Attack + Attack/Control + Defense/Gain |
| P3 | `Burst` | 45% | Attack + Attack + 任意 + 任意 |
| P3 | `Compose` | 40% | Compose + 对应素材 + Attack + Gain/Defense |
| P3 | `Growth` | 15% | Strength/HeartWall 转化 + Attack + 任意 |

抽牌规划器的正式 Tag 仍只有 `Ability / Buff / Gain / CollectionGenerator / Defense / Attack / Compose`。牌池表中的 `CollectionConsumer / Control / Finisher / HeartWallConsumer` 是效果与评分分类，不扩展 Tag 位掩码。需要这些语义的配方通过阶段配置中的显式 DefinitionId 谓词选择；“对应素材”由已选 Compose 来源的冻结 `ComposeMaterialRequest`（类别、数量、库存来源）过滤，也不是一个 Tag。

结构约束：

- P1 不允许 Compose；
- P2 每个候选行动最多一个 Compose 来源；
- P3 最多两个 Compose 来源；若有两个，最多一个生成 Immediate 攻击 Token，另一个必须产生延迟 Token；
- Retained Token 作为下一行动强制前缀；
- 所有前缀、Replay 与 Token 必须进入完整风险评分；
- 槽位匹配失败时的兜底抽取仍须遵守阶段 Compose 数量约束。

## 14. X 费与有效牌计数

### 14.1 通用 X 公式

角色卡池中的敌人版 X 费牌统一把原能量 X 替换为：

```text
BaseX = max(0, 6 - N)
```

其中 `N` 是本敌方行动中，此前已经成功完成的实际卡牌实例数量。

### 14.2 SenzaihyoumeiToken 翻倍

`SenzaihyoumeiToken` 在第一次执行前检查 Exhaust 中不同敌人卡牌定义数量。去重键使用稳定 DefinitionId（当前实现可落为 `EnemyCardId`）；同定义的多个实例只计 1，收藏品库存不参与该计数：

```text
Multiplier = 2  若不同定义数 >= 5
Multiplier = 1  否则
FinalHitCount = BaseX × Multiplier
```

每次命中造成 8 点基础伤害。

### 14.3 Replay 不增加 N

计数单位必须是 `ExecutingCardInstanceKey`，不能使用展示归属的 `RootSourceKey`。

规则：

1. 某张实际卡第一次开始执行时冻结 `FrozenN`；
2. 计算并冻结 `FrozenX` 和翻倍状态；
3. 本体使用该冻结值；
4. 同一实例的全部 Replay 继续使用完全相同的冻结值；
5. Replay 不增加 `N`，不重新计算 X，也不重新检查翻倍；
6. 该卡的本体与全部 Replay 完成后，如果至少一个执行单元成功，`N += 1`；
7. Immediate 子牌拥有自己的 `ExecutingCardInstanceKey`，作为另一张实际卡单独计数；
8. 攻击命中次数不增加 N；
9. 即使 `FinalHitCount = 0`，卡牌仍视为成功结算，并在整张卡结束时使 N 增加 1；
10. 因素材不足一次都未成功的卡不增加 N。

例：`N = 2`、满足翻倍、`ReplayCount = 1`：

```text
BaseX = 6 - 2 = 4
FinalHitCount = 4 × 2 = 8
本体：8 次
Replay：8 次
整张卡总计：16 次命中、128 基础伤害
全部完成后 N 从 2 变为 3
```

父来源与 Immediate 子牌的 DFS 计数顺序：

```text
父来源首次成功开始：冻结父 FrozenN/FrozenX
Immediate 子牌开始：按当时 N 冻结自己的 FrozenN/FrozenX
Immediate 子牌本体及其 Replay 全部结束：子牌使 N +1
返回父来源后续 Replay：仍复用父 FrozenX，不重读 N
父来源本体及其全部 Replay 结束：父来源再使 N +1
下一张独立来源：读取上述两个实际卡实例都完成后的 N
```

因此 Immediate 子牌是另一张有效牌；Replay 只是同一实际卡的执行单元，不是另一张有效牌。

冻结计划、实时投影、真实执行、Intent 展示和重连恢复必须共享同一份 X 元数据。

## 15. 双层评分模型

### 15.1 正式完整软锁

| 阶段 | 完整攻击风险锁 | 完整总风险锁 |
|---|---:|---:|
| P1 | 48 | 90 |
| P2 | 72 | 135 |
| P3 | 96 | 190 |

### 15.2 第一层：来源牌快速评分

第一层只计算当前指标选择的普通来源牌，不展开 Retained、Replay、Immediate、未来 Token 或完整能力连锁。它允许使用定义级的 `AbilityHint` 和单层 `DeferredTokenHint`，但 Hint 不创建 Token、不读取区域系数，也不递归预测连锁。

```text
StaticAttack
= Σ DirectAttack

StaticTotal
= StaticAttack
+ 0.65 × Block
+ 10 × Strength
+ 6 × Dexterity
+ 3 × HeartWall
+ 5 × OtherPersistentPower
+ 6 × Vulnerable
+ 3 × OtherDebuff
+ 3 × NormalCollection
+ 5 × StarStone
+ AbilityHint
+ 0.5 × DeferredTokenHint
```

静态字段口径：

- `DirectAttack(card)`：该来源牌一次本体执行、无力量/易伤/Replay 时，任一单名有效玩家承受的全部基础命中之和；全体攻击不乘玩家数，多段必须相加；
- `AbilityHint`：只对本次打出会激活的能力来源计定义常数，`SorrowfulRain=12`、`Adayume=15`、`HeartBeat=8`，`DuckAndCover` 与 `NameOfTear` 的动态部分留给完整层；
- `DeferredTokenHint`：来源可能生成的第一个非 Immediate Token 的一次本体静态总值，假定作词成功，但排除 Replay、区域系数和后续链；非延迟 Token 来源为 0；
- X Token 的静态 Hint 使用该候选开始时可确定的 `BaseX`，但不计算翻倍与 Replay；无法确定时记 0，并由完整层裁决。

同一持续能力不得同时填入 `OtherPersistentPower` 和 `AbilityHint`；Hint 是第一层对能力来源的唯一占位分。

快速软锁为完整锁的约 80%：

| 阶段 | Static Attack | Static Total |
|---|---:|---:|
| P1 | 38 | 72 |
| P2 | 58 | 108 |
| P3 | 77 | 152 |

当前 `EnemyCardScoreProfile` 构造函数的 `atField = atField` 自赋值会导致心之壁静态分恒为 0。实现前必须改为 `this.atField = atField` 或统一重命名为 `AtField`，并增加回归测试。

### 15.3 第二层：完整冻结行动评分

第二层输入是尚未提交真实牌区的完整 `PreparedEnemyCardAction` 与纯内存 `LiveActionProjection`。

```text
TotalRisk
= AttackRisk
+ SurvivalRisk
+ EngineRisk
+ DeferredRisk
```

不进行整体折扣。

完整层有意使用行动结束后的**总存量**，不是只算本行动增量。已经存在的力量、敏捷、心之壁和能力会在后续每个候选中继续形成局面风险；它们也正是第 7～8 回合让普通候选越来越难通过、第三候选更常进入 `ForcedOverLock` 的自然惩罚来源。不得把本节自动改写成 Delta-only 评分。当前力量同时进入实际 `ProjectedDamage` 和持续引擎风险也是有意的：前者衡量本行动伤害，后者衡量没有及时击杀时留下的局面。

#### AttackRisk

```text
AttackRisk
= max over living player p (
     sum of all ProjectedDamage to p
   )
```

必须包含：

- Retained 前缀；
- 所有成功 Replay；
- Immediate Token；
- 受控灵感与收藏品直接伤害；
- 当前力量、易伤与逐命中修正；
- X 费牌的冻结实际命中数。

不得在 `ProjectedDamage` 上再次乘力量、易伤、命中数或 Replay。

#### SurvivalRisk

使用行动结束状态：

```text
SurvivalRisk
= 0.65 × EndBlock
+ 6 × EndDexterity
+ 3 × EndHeartWall
+ DefensivePowerRisk
```

能力附加：

- `DuckAndCover` 已激活：再加 `0.65 × EndHeartWall`；
- `HeartBeat` 已激活：再加 8；
- 其他普通持续防御 Power：每层 5。

`DefensivePowerRisk` 就是上述三项之和；`DuckAndCover` 使用动态项，`HeartBeat` 固定只出现一次，其他尚未单列的防御 Power 才使用每层 5。

#### EngineRisk

```text
EngineRisk
= 10 × EndStrength
+ AbilityRisk
+ 6 × EndTargetVulnerable
+ 3 × EndOtherTargetDebuff
+ CollectionInventoryRisk
+ CompressionRisk
```

已生效能力基础风险：

| 能力 | AbilityRisk |
|---|---:|
| `SorrowfulRain` | 12 |
| `Adayume` | 15 |
| `HeartBeat` | 0；固定 8 已计入 `DefensivePowerRisk` |
| `DuckAndCover` | 动态心之壁公式，不给固定分 |
| `NameOfTear` | 动态反伤公式，不给固定分 |

除表中能力外，其他普通非防御持续 Power 按每层 5 计入 `AbilityRisk`。

```text
CollectionInventoryRisk
= 3 × EndAvailableNormalCollectionCount
 + 5 × EndAvailableStarStoneCount
```

Consumed 收藏品不计库存分，素材消费也不得抵扣当前攻击风险。

压缩风险：

```text
CompressionRisk
= PhaseCompressionWeight
 × (PhaseInitialTemplateInstanceCount - EndReusablePhaseSourceInstanceCount)
```

| 阶段 | PhaseCompressionWeight |
|---|---:|
| P1 | 0 |
| P2 | 1 |
| P3 | 3 |

压缩按当前阶段的**非 Carry 来源实例**计算，而不是按不同 DefinitionId 计算：两张 `NamelessPaper` 算两个实例；Carry Token、收藏品和迁移直接移除的旧来源都不进入分母；进入 Exhaust 的当前阶段普通来源不再 reusable；失败而 Retain 的普通来源仍算 reusable。阶段迁移直接移除旧卡不计压缩。

#### DeferredRisk

```text
DeferredRisk
= ReactiveRisk
+ CarryTokenRisk
+ ReplayGrowthRisk
```

心之壁反伤折算：

```text
ReactiveRisk
= 0.5 × EndHeartWall
 × (NameOfTear active ? 1.5 : 1)
```

Carry Token 区域系数：

| 行动结束后区域 | 预测价值系数 |
|---|---:|
| Retained | 0.75 |
| Draw / Discard | 0.45 |
| Exhaust | 0.15 |

辅助函数定义：

```text
OneExecutionForecast(token)
= ForecastAttack
 + 0.65 × ForecastBlock
 + 10 × ForecastStrength
 + 6 × ForecastDexterity
 + 3 × ForecastHeartWall
 + 6 × ForecastVulnerable
 + 3 × ForecastOtherDebuff
 + ForecastCollectionValue

CarryTokenRisk
= Σ ZoneCoefficient(token.Zone)
    × (OneExecutionForecast(token) + ChainContinuationForecast(token))

ReplayGrowthRisk
= Σ ZoneCoefficient(token.Zone)
    × token.ReplayCount
    × OneExecutionForecast(token)
  + PendingDeferredReplayIncrementRisk
```

`CarryTokenRisk + ReplayGrowthRisk` 合计必须恰好展开当前 Token 的 `ReplayCount + 1` 次执行，不能把同一 Replay 同时放进两项。`PendingDeferredReplayIncrementRisk` 只计算尚未写入 EndReplayCount、但已经由延迟链保证会发生的增量；已经反映在行动结束 `ReplayCount` 中的增量不得再次计分。X Token 的所有 Replay 使用相同 FrozenX，但每次 Replay 都造成完整的同次数攻击。

Forecast 使用行动结束后的力量、目标状态与已冻结 X；尚未冻结且执行位置不确定的 X Token 采用下一次最早合法执行位置的保守值。`ForecastCollectionValue` 沿用普通收藏品 3、`StarStone` 5。`PendingDeferredReplayIncrementRisk` 使用与对应 Token 相同的区域系数和链深折扣。

延迟作词链最多递归三层，每层乘 0.6，并乘素材可行性：

| 素材状态 | Feasibility |
|---|---:|
| 当前全部具备 | 1.0 |
| 当前部分具备 | 0.5 |
| 当前没有，但收藏品状态仍能提供 | 0.25 |
| 当前阶段不可能获得 | 0 |

`ChainContinuationForecast` 从当前 Token 的直接子链开始，最多三跳；每一跳把该层 Token 的一次执行、现有 Replay 和下一跳继续价值一起乘 0.6 与该层素材可行性。区域系数只在当前已存在 Token 的根节点乘一次，未来子 Token 的额外延迟由每跳 0.6 表示。

### 15.4 评分样例

| 行动 | AttackRisk | 其他风险参考 |
|---|---:|---|
| `AtField`：13 格挡、5 心之壁 | 0 | `0.65×13 + 3×5 = 23.45` |
| `CannotBeingHuman`：1 敏捷、4 心之壁 | 0 | `6 + 3×4 = 18` |
| `Hitoshizuku` 成功并 Immediate Token | 24 | P2 正常 Exhaust 压缩约 1；能力触发按结束状态计 |
| `WantBeYourGod` + Retained Token | 0 | 本体 5 心之壁的风险为 15；Token 基础风险 `0.65×9+3×1=8.85`，保留折算约 6.64 |
| `Mayoiuta` + Immediate Token | `5×5+6=31` | Token 先于本体 6 伤和 2 易伤；易伤风险计 12，P3 压缩计 3，总计约 46 |
| `PrideManSaki`，无力量无 Replay | 50 | 总风险至少 50 |
| `PrideManSaki`，2 力量、1 Replay | `(5+2)×10×2=140` | 力量逐段生效，超过 P3 攻击锁 96 |
| `SingFullPower`，20 心之壁 | 69 | 心之壁仍进入生存与反伤风险 |
| `SenzaihyoumeiToken`，N=2、翻倍、无 Replay | 64 | 8 次命中 |
| 同上，ReplayCount=1 | 128 | 本体和 Replay 都是 8 次；N 只增加一次 |

## 16. 候选生成与 ForcedOverLock

每次最多尝试三个候选：

1. 在候选事务副本中随机选择指标与来源牌；
2. 计算第一层快速评分；
3. 第一、二次若第一层超锁，拒绝，真实牌区不变化，RNG 推进保留；
4. 通过第一层后，冻结 Retained、素材、Replay、Immediate、收藏品与随机结果；
5. 构造完整 `PreparedEnemyCardAction`；
6. 在真实 `CommitPreparedAction` 之前运行纯内存完整投影；
7. 计算完整攻击与总风险；
8. 第一、二次若完整层超锁，拒绝，真实牌区不变化，RNG 推进保留；
9. 第三次无论第一层是否超锁，都必须构造完整投影；
10. 第三次投影完整但超过软锁时允许提交，并标记 `ForcedOverLock`；
11. `IsComplete == false`、未知数值修正或步骤上限截断不得被 ForcedOverLock 豁免；
12. 三次都无法产生完整投影属于配置或模拟器 Fault，不执行固定保底行动。

P3 的 96～108 条件高点和超时 120～150 观察带，均可能由第三个完整候选的 `ForcedOverLock` 产生，不是普通候选可忽略的锁。`ForcedOverLock` 不设第二个硬上限；未封顶的 `SingFullPower` 和 X/Replay 仍需完整投影、记录明确分数和超观察带遥测。缺少逐实例 FrozenX/FrozenN 元数据本身即属于投影不完整，不能强制提交。

第二层插入点必须位于：

```text
完整候选 PreparedAction 已构造
AND 真实牌区尚未 CommitPreparedAction
```

投影阶段禁止：

- 写真实五牌区；
- 发出真实命令；
- 再次抽取 RNG；
- 从实时库存重新选择不同素材；
- 执行带副作用的 Ability Hook。

所有可影响数值的 Ability Hook 必须提供纯模拟适配器；无法适配时投影必须标记不完整。

## 17. 断线恢复与同步字段

正式影灯快照至少包含：

- `ActivePhase`、`PendingPhase`、阶段迁移修订号；
- 单一正式 DeckId；
- 五牌区全部实例与顺序；
- `CarryAcrossPhase` 定义身份；
- 每张实例的 `InstanceKey`、CardId、来源阶段与 `ReplayCount`；
- Available、Consumed 收藏品及 NextCollectionSequence；
- `ShadowTomoriFormPower` 是否激活；
- 当前准备周期的 `FrozenPreparationCollection` 与 `PreparedPreActionInventoryDelta`；
- 力量、敏捷、心之壁、能力 Power 与格挡；
- `LastMetric`；
- PreparedAction、执行游标、即时栈；
- 当前行动 `N`；
- 每个 `ExecutingCardInstanceKey` 的 FrozenN、FrozenX、翻倍状态与是否已计数；
- 第一层、第二层评分快照；
- 候选次数、拒绝原因与 `ForcedOverLock`；
- 投影完整性与诊断；
- RNG 权威状态或与现有主机同步协议一致的恢复信息。

断线恢复不得：

- 重新进行阶段迁移；
- 重新生成收藏品；
- 重新随机牌序；
- 重新计算已经冻结的 X；
- 把 Carry Token 创建为新实例；
- 因处于 Exhaust 而丢弃 Carry Token。

## 18. 验收场景

### 18.1 阶段与血量

- 1200 HP 入场，P1 额度为 216；
- 一击 288 只实际扣除 216，剩余 72 不进入 P2；
- 达到 P1 额度时只设置 PendingPhase，旧 P1 行动仍完整执行；
- P1 迁移后 HP 为 984，P2 额度为 432；
- P1 Cap 关闭与 P2 Cap 安装发生在同一个原子迁移；
- P2 结束时 HP 为 552；
- P2 → P3 时关闭 P2 Cap，P3 不再安装阶段 Cap；
- 影灯没有直接回复 HP 的牌；
- 216 净 DPT 在第 6 玩家回合击杀；
- 288 在第 5 回合击杀；
- 200 在第 8 回合击杀；
- 180 在第 9 回合击杀。

### 18.2 迁移与 Carry

分别把 Carry Token 放在 Draw、Current、Retained、Discard、Exhaust 后触发迁移：

- 每个实例均保留对象身份、牌区、顺序和 Replay；
- 同样位置的非 Carry 来源全部移除；
- 迁移移除不触发 HeartBeat；
- Available 和 Consumed 收藏品均保留；
- Power、力量、敏捷、心之壁均保留；
- `LastMetric` 重置；
- 新模板使用全新实例并由战斗 RNG 洗牌；
- UI 和重连不会观察到半迁移状态。

### 18.3 P2 收藏品

- P2 第一行动准备周期冻结一张随机收藏品；
- 每个后续 P2/P3 行动准备周期只冻结一张；
- 同一周期三个候选看见同一个实例增量，拒绝候选不会重抽或累计收藏品；
- 只有最终行动提交才把这一个增量写入真实 Available；
- 权重符合配置；
- `StarStone` 权重为 10%；
- 重连不重复生成；
- 收藏品仅进入库存，不打开玩家 UI；
- P3 继续保留状态与全部库存。

### 18.4 P3 随机性

- P3 迁移只加入两张 `NamelessPaper`，不保证第一手抽到；
- 不存在固定 P3 第一招；
- 不存在第 7、8 回合固定强化；
- 两次 4 槽行动后，第三次需要 4 槽时验证按既有规则回洗；
- 两次 3 槽行动后，第三次只需 3 槽时不得强制提前回洗；两种路径都应进入紧凑小循环；
- 长线力量、敏捷、心之壁和 Replay 能使更多候选自然 ForcedOverLock。

### 18.5 X 与 Replay

- `N=2`、翻倍、无 Replay：SenzaihyoumeiToken 命中 8 次；
- 同条件 `ReplayCount=1`：本体 8 次、Replay 8 次，总 16 次，N 只增加一次；
- `ReplayCount=2`：三次执行均使用同一个 8 次；
- Replay 不增加 N；
- Immediate 子牌使用自己的实例计数；
- Immediate 子牌完成后先使 N 增加一次，父来源后续 Replay 仍使用父来源原 FrozenX；父来源全部完成后再增加一次；
- `N>=6` 时命中 0 次，但卡牌成功完成后 N 仍只增加一次；
- 一次都未成功的卡不增加 N；
- 翻倍条件在首个执行单元前冻结，Replay 中不重新判定；
- 重连后沿用原 FrozenX，不重新计算。

### 18.6 评分

- 修正 `atField` 自赋值 bug 后，5 心之壁不再得到 0 静态分；
- 完整投影包含 Retained、Replay、Immediate、能力和收藏品；
- 多人攻击锁按单名玩家最大伤害；
- ProjectedDamage 不被重复乘力量或易伤；
- Pride 5×10 无力量无 Replay 为 50；
- Pride +2 力量 +1 Replay 为 140；
- SenzaihyoumeiToken 的 Replay 完整重复 FrozenX 伤害；
- `Adayume` 对本体和每次 Replay 各触发一次，但同一实际卡的这些 Replay 合计只让 N 增加一次；
- 静态层通过但完整层超锁时，候选被第二层拒绝；
- 前两次拒绝只推进 RNG，不提交真实牌区；
- 第三次完整但超锁可 ForcedOverLock；
- 投影不完整不得 ForcedOverLock；
- 缺少 FrozenX/FrozenN 投影元数据视为投影不完整；
- 不存在固定保底行动。

### 18.7 牌池与定义不变量

- P1 恰好 12 个普通来源实例且没有 Compose；
- P2 恰好 11 个普通来源实例，每个候选最多 1 个 Compose 来源；
- P3 恰好 9 个普通来源实例，其中 `NamelessPaper` 为 2 个；
- P3 每候选最多 2 个 Compose 来源，且最多 1 个生成 Immediate 攻击 Token；
- `Utakotoba / 诗超绊` 及其 Token 从正式目录、预加载和时间线全部排除；
- 所有普通阶段来源均为非 Carry，七种明确 Token 均为 Carry；
- Available 与 Consumed 收藏品不因阶段迁移丢失。

## 19. 实现前必须解决的基础设施差距

当前基础设施尚不直接提供以下正式能力：

- 原地原子阶段迁移 API；
- `CarryAcrossPhase` 定义字段与全五区迁移过滤；
- 新阶段模板运行时实例注册与正式同步目录；
- `ResetLastMetric()`；
- 正式影灯卡牌与收藏品目录，替代测试目录硬编码；
- `ShadowTomoriFormPower` 的确定性收藏品生成；
- 行动准备级 `FrozenPreparationCollection` / `PreparedPreActionInventoryDelta`；
- 阶段行为指标和权重；
- 现有 Tag 与 DefinitionId 效果谓词的正式映射；
- 阶段伤害额度 Power 的原子替换与多人同源缩放；
- 完整评分所需的 projected end-state、牌区与库存摘要；
- 逐 `ExecutingCardInstanceKey` 的 FrozenN/FrozenX 数据；
- 支持 Compose/Immediate 与本体效果显式排序的定义程序；
- Ability Hook 纯模拟适配器；
- 完整投影软锁插入点与诊断 DTO；
- 正式 Shadow Tomorin Monster、Encounter、Stage 路线替换与本地化；
- 对应 ModelDb、Intent Timeline、预加载、多人和断线恢复测试。

这些差距属于后续实现计划范围，不改变本文已经确认的玩法契约。
