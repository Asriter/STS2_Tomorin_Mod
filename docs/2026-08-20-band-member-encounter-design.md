# BandMemberEncounter 精英房设计 Spec

- 日期：2026-08-20
- 状态：实现完成，自动化验证通过，等待游戏内验收
- 设计范围：房间、敌人变体、成员选择、夹击布局、存档与测试
- 不包含：出现条件、Act 遭遇池注入、专属背景与专属 BGM

## 1. 需求目标

新增一个名为 `BandMemberEncounter` 的精英战 Encounter：

- 房间类型为 `RoomType.Elite`。
- 从 `Anon`、`Taki`、`Soyo`、`Raana` 派生四个代码身份独立的 Elite 敌人。
- 根据当前局已经遇到的原始 Boss，从固定候选顺序中选择两个未遇到成员。
- 第一名位于玩家左侧，第二名位于玩家右侧，玩家队伍居中。
- 完整复用 Kaiser Crab 的原生夹击语义，而不是只复制视觉站位。
- 房间保留原生标准精英奖励，但不追加任何角色 Boss/自定义奖励。
- 暂不处理正式出现条件。

成功标准：成员选择确定且可恢复；四个 Elite 的生命、基础攻击和 `Taki` 阶段容量符合倍率规则；原 Boss 行为不发生回归；单人和多人均能正确处理左右背击、死亡、逃跑和存档恢复。

## 2. 已确认的设计决策

1. “遇到过”只检查当前局 `IRunState.MapPointHistory`，不检查往期局、图鉴或全局存档。
2. 一层可能遇到 `Anon` 或 `Taki`，二层可能遇到 `Raana` 或 `Soyo`；选择器不根据层数推断，只读取实际历史。
3. 固定候选顺序为 `Anon → Taki → Soyo → Raana`。
4. 第一轮按固定顺序选择未遇到成员；不足两个时，第二轮从尚未入选的成员中补足。禁止生成重复成员。
5. Elite 身份不计入已遇到集合。设计上该房间单局不会重复进入；异常重复进入时仍按原始 Boss 历史执行正常流程。
6. 四个 Elite 保留原 Boss 的招式、阶段、Power、状态牌、复活、逃跑、视觉、动画、音效和台词。
7. 移除 `AnonElite`、`TakiElite` 的 Boss 专属追加遗物奖励；`TakiElite` 不得由自身直接结束整个房间。
8. 房间仍启用游戏原生的标准精英奖励。
9. 生命和基础攻击在基础属性层缩放；力量、虚弱、易伤、夹击等原生战斗修正在其后生效。
10. `TakiElite` 的正常生命和各阶段伤害容量一起缩放；`AnonElite` 的无限生命哨兵不缩放。
11. 第一名固定放左侧，第二名固定放右侧；玩家初始朝向右侧。
12. 只复刻 Kaiser Crab 的站位与夹击机制，不引用其场景、背景、BGM 或专属特效。
13. 当前暂用所在 Act 的常规战斗背景与标准精英音乐，并记录后续替换任务。
14. 四个 Elite 只在代码类型与 `ModelId` 层面区分，界面名称继续显示原 Boss 名称。
15. Encounter 代码类型为 `BandMemberEncounter`，中文标题为“乐队”，英文标题为“Band”。
16. Encounter 通过 BaseLib 注册到 `ModelDb`，但暂不进入任何 Act 的精英遭遇池，`IsValidForAct` 对所有 Act 返回 `false`。

## 3. 当前框架事实与约束

### 3.1 现有 Boss

项目已有以下原始敌人与 Encounter：

- `Anon` / `AnonBoss`
- `Taki` / `TakiBoss`
- `Soyo` / `SoyoBoss`
- `Raana` / `RaanaBoss`

四个 Boss 当前都是独立的 `CustomMonsterModel` 与单敌人 `CustomEncounterModel`。现有攻击属性主要是私有计算属性，攻击意图与 `DamageCmd.Attack` 共用这些值。

### 3.2 历史记录

`IRunState.MapPointHistory` 的每个 `MapPointHistoryEntry` 通过 `Rooms` 暴露房间记录；其中 `MapPointRoomHistoryEntry` 同时包含 Encounter `ModelId` 和 `MonsterIds`。选择器应遍历每个地图点的全部房间记录，并同时识别四个原始 Boss Encounter ID 与原始 Monster ID，以兼容正常流程和存档表达差异。

### 3.3 Kaiser Crab 夹击

原生 Kaiser Crab 机制包含：

- `FullyCenterPlayers = true`；
- 左右敌人场景槽位；
- 玩家持有 `SurroundedPower`；
- 左右敌人分别持有 `BackAttackLeftPower` 与 `BackAttackRightPower`；
- 玩家根据卡牌或药水目标改变朝向；
- 一侧死亡后 `SurroundedPower` 根据剩余敌人更新方向。

`CreatureCmd.Escape` 不触发 `SurroundedPower.AfterDeath`，且直接执行方向刷新的 `UpdateDirection(Creature)` 是原生私有方法。因此 `AnonElite` 和 `TakiElite` 的逃跑必须在确认仍有可攻击敌人后，显式调用公开的 `SurroundedPower.AfterDeath(...)` 入口复用原生方向刷新逻辑。

### 3.4 奖励和房间结束风险

- `Anon` 在第二次死亡时会追加稀有遗物奖励。
- `Taki` 死亡时会追加稀有遗物奖励。
- `Taki` 的逃跑回调会直接调用 `room.OnCombatEnded()`。

上述行为适用于原 Boss 房，但会在双敌人精英房中造成重复奖励或提前跳过仍存活敌人，必须通过受控扩展点只对 Elite 关闭。

## 4. 架构方案评估

### 4.1 采用方案：定向扩展基础 Boss，Elite 继承复用

将需要缩放的伤害属性和 `Taki` 阶段容量改为 `protected virtual`，为 Boss 专属奖励、强制结束房间及逃跑后通知增加最小化扩展点。四个 Elite 继承原类，只覆写属性和行为边界。

优点：攻击意图和实际结算继续共享同一属性；基础 Boss 的修复能自动惠及 Elite；重复代码最少；可以精确保证向下取整发生在基础属性层。

风险：会修改四个原 Boss 源文件，因此必须通过默认扩展点和回归测试证明原 Boss 行为不变。

### 4.2 未采用方案：完整复制四个 Boss 类

虽然可以避免修改原 Boss，但会复制大型状态机、死亡回调和阶段逻辑。后续修复容易在 Boss 与 Elite 之间产生漂移，不满足可维护性和扩展性要求。

### 4.3 未采用方案：隐藏 Power 或 Harmony 动态缩放

运行时倍率 Power 容易连同力量、夹击等外部修正一起放大，并可能使攻击意图与结算不一致；私有奖励和结束房间回调仍需额外 Patch。该方案与已确认的基础伤害口径冲突。

## 5. 组件设计

### 5.1 `BandMemberEncounter`

职责：

- 以 `RoomType.Elite` 和启用标准奖励的配置构造。
- 暴露 `LeftMember`、`RightMember` 两个槽位常量。
- 从 Encounter 自定义状态恢复成员，或从当前 `RunState` 收集历史后调用选择器。
- 生成两个对应 Elite 的 mutable model，并绑定左右槽位。
- `AllPossibleMonsters` 返回全部四个 Elite，确保资源预加载完整。
- `FullyCenterPlayers` 返回 `true`。
- 使用独立双槽位场景。
- 在战斗开始时调用夹击协调器。
- 使用 `SaveCustomState` / `LoadCustomState` 保存稳定的左右成员标识。
- `IsValidForAct` 始终返回 `false`；不修改任何 Act 的遭遇列表。

### 5.2 `BandMemberSelector`

纯逻辑模块，不读取全局单例。在 `BandMemberSelector.cs` 中一并定义稳定的 `BandMemberKind` 身份和描述映射。

输入：已遇到的原始 Boss 身份集合。

输出：有顺序的左右成员对。

算法：

1. 按 `Anon → Taki → Soyo → Raana` 扫描。
2. 跳过已遇到成员，将未遇到成员加入结果。
3. 获得完整左右成员对后立即停止。
4. 如果第一轮不足，再按相同顺序加入尚未入选成员。
5. 如果内部候选表错误而无法形成两个不同成员，抛出带上下文的开发错误，不生成重复敌人。

该模块不识别 Elite 身份，也不保存历史。

### 5.3 Elite 敌人

- `AnonElite : Anon`
- `TakiElite : Taki`
- `SoyoElite : Soyo`
- `RaanaElite : Raana`

职责仅限：

- 覆写初始/最大生命。
- 覆写所有基础攻击伤害属性。
- `TakiElite` 额外覆写阶段伤害容量。
- 关闭 Boss 专属追加奖励。
- `TakiElite` 关闭逃跑后强制结束房间。
- `AnonElite`、`TakiElite` 在逃跑完成后通知夹击协调器。
- `Title` 返回对应原 Boss 的 `Title`，保证表现名称完全一致。

不复制状态机、不新增招式、不改变命中次数、格挡、治疗、状态牌、Power 数值或阶段门槛。

### 5.4 基础 Boss 扩展点

只开放 Elite 所需的最小边界：

- 攻击计算属性改为 `protected virtual`。
- `Taki` 阶段容量改为 `protected virtual`。
- “是否发放 Boss 专属奖励”保护级虚属性，基础默认开启。
- `Taki` 的“逃跑后是否强制结束房间”保护级虚属性，基础默认开启。
- `Anon`、`Taki` 的“逃跑完成后”保护级异步虚方法，基础默认空操作。

基础类原有默认值不得改变。

### 5.5 `BandSurroundedCoordinator`

职责：

- 战斗开始时，为每名玩家幂等应用 `SurroundedPower`。
- 为左侧敌人幂等应用 `BackAttackLeftPower`。
- 为右侧敌人幂等应用 `BackAttackRightPower`。
- 将玩家初始方向设为右侧。
- 在 Elite 逃跑后，从 `HittableEnemies` 中确认存在剩余敌人，并对每名玩家的 `SurroundedPower` 调用公开的 `AfterDeath(...)` 入口；原生 Power 随后使用剩余敌人执行私有方向刷新。
- 没有剩余敌人时不刷新方向，由原生战斗结束流程接管。

敌人正常死亡继续交由原生 `SurroundedPower.AfterDeath()` 处理。

### 5.6 属性缩放工具

新增一个仅供 Elite 使用的纯函数工具，放在 `Scripts/Enemy/Elite/EliteStatScaler.cs`：

`ScaleDown(baseValue, multiplier) = floor(baseValue × multiplier)`

该工具不读取战斗状态、不执行多人缩放，也不处理力量等战斗修正。

## 6. 成员选择与存档数据流

### 6.1 首次生成

1. `BandMemberEncounter.GenerateMonsters()` 检查是否已有合法的内部左右成员。
2. 没有合法状态时，通过公开的 `RunManager.Instance.DebugOnlyGetState()` 获取当前 Run，并从 `MapPointHistory[*].Rooms` 扁平收集历史。
3. 仅将四个原始 Boss Encounter/Monster ID 映射为已遇到身份。
4. 调用 `BandMemberSelector` 获得左右成员。
5. 通过成员描述映射调用相应 `ModelDb.Monster<TElite>().ToMutable()`。
6. 第一名绑定 `LeftMember`，第二名绑定 `RightMember`。
7. 保存成员稳定字符串，例如 `leftMember=Anon`、`rightMember=Taki`。

### 6.2 保存与恢复

保存键使用稳定字符串，不使用枚举整数或程序集限定类型名。恢复时必须同时满足：

- 左右键均存在；
- 两个值均为已知身份；
- 左右身份不同。

合法状态始终优先于重新读取历史。缺键、未知值或重复值会使整组状态失效，随后重新执行选择流程。Elite 选择状态只用于恢复，不加入已遇到历史。

### 6.3 无活动 RunState

如果在图鉴预览、调试实例化或特殊上下文中没有活动 `RunState`，记录一次警告并将历史视为空集合，随后执行正常固定顺序选择，避免空引用。

## 7. 房间布局与夹击数据流

### 7.1 场景

新增 `STS2_Tomorin_Mod/scenes/encounters/band_member_encounter.tscn`：

- 根节点：全屏 `Control`。
- `LeftMember`：`Marker2D`，位置 `(420, 720)`。
- `RightMember`：`Marker2D`，位置 `(1500, 720)`。
- 相机缩放：`0.75`。
- 相机偏移：`(0, 35)`。
- 玩家队伍通过 `FullyCenterPlayers` 居中。

相机参数沿用 Kaiser Crab 的布局参数；槽位坐标基于 1920×1080 战斗画布和现有四名角色视觉宽度确定。实现阶段必须进行单人和多人视觉检查，但自动测试只验证相对布局，不断言固定坐标。

### 7.2 开战初始化

两名敌人先执行各自继承的 `AfterAddedToRoom()`，建立原 Boss 状态机、Power 和事件回调。Encounter 随后在战斗开始钩子中：

1. 为每名玩家应用唯一的 `SurroundedPower`。
2. 为左敌人应用 `BackAttackLeftPower`。
3. 为右敌人应用 `BackAttackRightPower`。
4. 令玩家初始面向右侧。

所有 Power 应用都必须检查已有实例，以支持恢复和重复钩子的幂等性。

### 7.3 一侧离场

- 死亡：原生 `SurroundedPower.AfterDeath()` 根据剩余敌人更新方向。
- 逃跑：`AnonElite`、`TakiElite` 在 `CreatureCmd.Escape` 完成后调用协调器。协调器确认存在剩余可攻击敌人后，以逃跑敌人显式通知每名玩家的 `SurroundedPower.AfterDeath(...)`，复用原生方向刷新逻辑。
- 没有剩余敌人：不进行额外刷新。
- `TakiElite` 逃跑而另一敌人仍存活：战斗继续，不能调用 `room.OnCombatEnded()`。

## 8. 属性设计

所有表格均为 Elite 最终基础属性。左列“普通”表示未达到对应敌人进阶阈值；右列表示达到原属性使用的 `ToughEnemies` 或 `DeadlyEnemies` 阈值。多人生命缩放在这些值之后由游戏原生系统处理。

### 8.1 AnonElite

| 属性 | 普通 | 对应进阶阈值 |
|---|---:|---:|
| 初始/最大生命 | 420 | 442 |
| 单段攻击 | 36 | 42 |
| 多段攻击每击 | 10 | 12 |
| 多段次数 | 3 | 3 |

不缩放格挡、状态牌数量、逃跑计数或无限生命哨兵。

### 8.2 TakiElite

| 属性 | 普通 | 对应进阶阈值 |
|---|---:|---:|
| 初始/最大生命 | 1000 | 1120 |
| 第一阶段伤害容量 | 420 | 470 |
| 第二阶段伤害容量 | 400 | 450 |
| 第一阶段单段攻击 | 32 | 36 |
| 第一阶段普通多段每击 | 6 | 8 |
| 第一阶段普通多段次数 | 5 | 5 |
| 第一阶段重击每击 | 24 | 26 |
| 第一阶段重击次数 | 2 | 2 |
| 第二阶段攻击每击 | 10 | 12 |
| 第二阶段攻击次数 | 5 | 5 |
| 第三阶段攻击每击 | 20 | 20 |
| 第三阶段攻击次数 | 5 | 5 |

不缩放格挡、卡牌数量、Power 数值或阶段顺序。

### 8.3 SoyoElite

| 属性 | 普通 | 对应进阶阈值 |
|---|---:|---:|
| 初始/最大生命 | 600 | 645 |
| 假面多段攻击每击 | 13 | 15 |
| 假面多段次数 | 2 | 2 |
| 真实单段攻击 | 36 | 40 |
| 真实多段攻击每击 | 4 | 6 |
| 真实多段次数 | 继续由疏远值决定 | 继续由疏远值决定 |

不缩放格挡、治疗、任务门槛、疏远值、伤口数量或状态切换门槛。

### 8.4 RaanaElite

| 属性 | 普通 | 对应进阶阈值 |
|---|---:|---:|
| 初始/最大生命 | 592 | 637 |
| 第一招单段攻击 | 27 | 31 |
| 第二招多段攻击每击 | 7 | 9 |
| 第二招多段次数 | 4 | 4 |
| 第四招低/中兴趣攻击 | 42 | 48 |
| 第四招高兴趣攻击每击 | 15 | 16 |
| 第四招高兴趣攻击次数 | 3 | 3 |

不缩放格挡、治疗、兴趣阈值、剩饭数量、弱化/易伤或力量增益。

## 9. 奖励与原 Boss 行为边界

### 9.1 BandMemberEncounter

- 房间类型仍为 Elite。
- `ShouldGiveRewards` 保持开启。
- 胜利后走游戏原生标准精英奖励流程。
- Encounter 本身不追加任何自定义奖励。

### 9.2 Elite 敌人

- `AnonElite` 禁止执行原 `Anon` 的 Boss 专属追加遗物奖励。
- `TakiElite` 禁止执行原 `Taki` 的 Boss 专属追加遗物奖励。
- `SoyoElite`、`RaanaElite` 不新增任何奖励逻辑。
- 单个 Elite 不得直接结算整个房间。

### 9.3 原 Boss 回归

- 原 `Anon` 继续保留现有 Boss 奖励。
- 原 `Taki` 继续保留现有 Boss 奖励和逃跑结束房间行为。
- 原 `Soyo`、`Raana` 行为不变。

## 10. 背景、音乐与本地化

- `BandMemberEncounter` 不启用 Kaiser Crab 的自定义背景。
- 不覆写为 Kaiser Crab BGM。
- 当前使用所在 Act 的常规战斗背景和标准精英音乐。
- `eng/encounters.json` 新增标题 `Band`。
- `zhs/encounters.json` 新增标题 `乐队`。
- 四个 Elite 的 `Title` 返回原 Boss `Title`，不新增 Elite 名称文本。
- 四个 Elite 继续返回原 Boss 的 `CustomVisualPath`，不新增角色美术。

## 11. 健壮性与多人一致性

### 11.1 幂等和错误处理

- 空历史和空子列表按空集合处理。
- 未知历史 ID 被忽略。
- 非法自定义状态被整体拒绝并重新计算。
- Power 已存在时不重复应用。
- Elite 模型未注册或 `ModelDb` 创建失败时，抛出包含 Encounter、成员和槽位信息的异常，不静默退回原 Boss。
- 无剩余敌人时不调用方向刷新。

### 11.2 多人确定性

- 选择算法不使用 RNG。
- 所有客户端使用同步的 RunState 历史和 Encounter 自定义状态。
- 左右成员身份对所有玩家一致。
- 每名玩家独立持有并维护自己的 `SurroundedPower` 朝向。
- 保存、重连和恢复优先使用已序列化成员与战斗 Power 状态，不重新随机或重复叠加。

## 12. 文件变更范围

### 12.1 新增

- `Scripts/Encounters/BandMemberEncounter.cs`
- `Scripts/Encounters/BandMemberSelector.cs`
- `Scripts/Encounters/BandSurroundedCoordinator.cs`
- `Scripts/Enemy/Elite/EliteStatScaler.cs`
- `Scripts/Enemy/Elite/AnonElite.cs`
- `Scripts/Enemy/Elite/TakiElite.cs`
- `Scripts/Enemy/Elite/SoyoElite.cs`
- `Scripts/Enemy/Elite/RaanaElite.cs`
- `STS2_Tomorin_Mod/scenes/encounters/band_member_encounter.tscn`
- `tests/BandMemberEncounter.Tests.ps1`

### 12.2 修改

- `Scripts/Enemy/Anon.cs`
- `Scripts/Enemy/Taki.cs`
- `Scripts/Enemy/Soyo.cs`
- `Scripts/Enemy/Raana.cs`
- `STS2_Tomorin_Mod/localization/eng/encounters.json`
- `STS2_Tomorin_Mod/localization/zhs/encounters.json`

### 12.3 不新增或不修改

- 不新增自定义夹击 Power 或图标。
- 不新增 Elite 名称本地化。
- 不修改卡池、遗物池或药水池。
- 不新增 Act Encounter Pool Patch。
- 不修改地图生成概率。

## 13. 测试方案

测试沿用仓库现有 PowerShell 契约测试风格，并结合编译、发布和游戏内验证。属性测试只验证倍率、取整与行为关系，不断言生命、伤害或坐标的固定配置值。

### 13.1 成员选择自动测试

| 场景 | 操作 | 预期 |
|---|---|---|
| 没有原 Boss 历史 | 传入空的已遇到集合 | 按固定顺序形成完整左右成员对 |
| 部分 Boss 已遇到 | 枚举各种已遇到组合 | 优先返回固定顺序中的未遇到成员 |
| 仅剩一个未遇到成员 | 传入对应历史 | 未遇到成员必须入选，另一位置由第二轮补足 |
| 四名原 Boss 均已遇到 | 传入完整历史 | 第二轮形成互不相同的左右成员 |
| 历史只包含 Elite ID | 传入 Elite 身份 | Elite 记录被忽略，结果等同于空原 Boss 历史 |
| 混合 Encounter ID 与 Monster ID | 使用两种历史表达 | 同一原 Boss 只被识别一次 |
| 相同输入重复执行 | 多次调用选择器 | 左右顺序保持一致且不依赖 RNG |
| 包含无关 Model ID | 加入未知记录 | 无关记录不影响结果 |

### 13.2 属性缩放自动测试

- 每个 Elite 的基础生命等于统一缩放函数作用于原 Boss 基础生命的结果。
- 每个 Elite 攻击属性满足相同倍率关系。
- 需要向下取整的属性不大于精确乘积，且与精确乘积的差小于一个整数单位。
- 攻击意图与实际攻击命令引用同一可覆写属性。
- `TakiElite` 阶段容量与生命遵循同一缩放口径。
- Elite 不覆写格挡、治疗、命中次数、状态牌数量或阶段门槛。
- 基础 Boss 使用默认行为开关，Elite 关闭 Boss 奖励和强制结束房间开关。

### 13.3 Encounter 与存档自动测试

- Encounter 的房间类型为 Elite，但不会被任何 Act 判定为合法自然遭遇。
- `AllPossibleMonsters` 覆盖全部四个 Elite 类型。
- 生成结果包含合法且不同的左右成员。
- 合法自定义状态恢复原左右组合。
- 缺键、未知成员或重复成员状态会触发重新计算。
- 无活动 `RunState` 时不发生空引用，并按空历史降级。
- 中英文 Encounter 标题正确解析。
- Elite 标题与对应原 Boss 标题相同。

### 13.4 夹击机制集成测试

- 战斗开始后，每名玩家持有唯一的 `SurroundedPower`。
- 左右敌人持有匹配槽位的原生背击 Power。
- 初始朝向、正面侧与背击侧符合 Kaiser Crab 原生关系。
- 玩家改变卡牌或药水目标后，朝向和背击侧同步改变。
- 一侧死亡后，玩家自动面向剩余敌人。
- `AnonElite` 或 `TakiElite` 逃跑后执行同样的方向刷新。
- 恢复存档或重复初始化不会叠加夹击 Power。
- 单人和多人模式下，每名玩家独立维护方向。

伤害测试只比较同条件下背击伤害高于正面伤害，不验证具体伤害数字。

### 13.5 奖励与战斗结束集成测试

- 击败 `AnonElite` 不追加其 Boss 专属奖励。
- 击败 `TakiElite` 不追加其 Boss 专属奖励。
- `TakiElite` 逃跑而另一敌人仍存活时，房间继续战斗。
- 所有敌人离场后由原生系统结束战斗。
- 房间胜利后仅执行标准精英奖励流程，不追加自定义奖励。
- 原始 `Anon`、`Taki` Boss 战仍保留当前奖励和结束行为。

### 13.6 场景与资源验证

- 场景包含合法的左右 `Marker2D` 槽位。
- 左槽位在玩家中心区域左侧，右槽位在右侧；不验证固定坐标。
- 单人与满员多人队伍均居中，敌人与玩家不存在不可接受的重叠或越界。
- Encounter 不引用 Kaiser Crab 场景、背景、BGM 或特效。
- 房间使用当前 Act 背景和标准精英音乐。
- 中英文 Encounter 本地化 JSON 可加载。

### 13.7 构建与回归验证

实现 Agent 必须执行：

- `tests/BandMemberEncounter.Tests.ps1`。
- 仓库全部现有 `*.Tests.ps1` 回归脚本。
- `dotnet build`，验证 C#、BaseLib 注册与本地化分析。
- `dotnet publish`，验证新增场景导出到 `.pck`。
- 游戏内验证成员组合、死亡、逃跑、保存恢复和多人同步。

## 14. 验收标准

1. `ModelDb` 可获取 `BandMemberEncounter` 和四个 Elite 模型。
2. Encounter 不能通过正常 Act 合法性检查出现，但可被显式实例化。
3. 当前局原 Boss 历史能确定性地产生正确左右成员。
4. 保存恢复后左右成员、Power 和朝向不重复、不改变。
5. 四个 Elite 的生命、基础攻击及 `Taki` 阶段容量满足已确认倍率和取整规则。
6. 命中次数、格挡、治疗、状态牌、Power 与阶段门槛保持原 Boss 规则。
7. 两侧站位、玩家居中、初始朝向、目标切换、死亡和逃跑均符合原生夹击语义。
8. `AnonElite`、`TakiElite` 不追加 Boss 奖励，且单个 Elite 不能提前结束仍有敌人的房间。
9. 房间胜利后仍提供标准精英奖励，不提供任何自定义追加奖励。
10. 原四个 Boss 的数值、奖励、状态机和结束行为无回归。
11. 自动测试、构建、发布与游戏内验证全部通过。

## 15. 后续延期项（不是未决设计）

- 设计 `BandMemberEncounter` 的正式出现条件。
- 确定允许出现的 Act、地图阶段和前置条件。
- 将 Encounter 注入对应 Act 的精英遭遇池。
- 设计出现概率、同局唯一性约束及与其他模组遭遇池的兼容策略。
- 制作该房间的专属背景，并替换当前 Act 的临时常规背景。
- 制作该房间的专属 BGM，并替换临时标准精英音乐。
- 正式开放后收集平衡性数据；任何偏离当前倍率规则的调整必须重新提交设计确认。
- 正式开放出现条件后，补充从地图选择精英节点到战斗结算的端到端测试。

## 16. 明确排除范围

- 本次不实现出现条件、地图生成、概率或 Act 注入。
- 不实现调试快捷入口。
- 不实现专属背景、BGM、房间图标或新敌人美术。
- 不记录 Elite 遇见历史，不为异常重复进入增加专用流程。
- 不新增夹击 Power。
- 不新增 Boss 专属或 Encounter 自定义追加奖励。
- 不新增招式、阶段、状态、卡牌或其他未确认机制。

## 17. Spec 自审结论

本轮已按设计评审门禁完成自审：

- **需求完整性：通过。** 房间类型、成员缩放、当前局原 Boss 历史选择、左右夹击、保存恢复、奖励边界、临时背景与音乐均有对应设计和验收项。
- **框架一致性：通过。** 方案沿用 BaseLib Encounter 注册、原 Boss 继承关系、原生 `SurroundedPower` 与背击 Power，不复制整套 Boss 实现，也不引入新的运行时全局补丁。
- **口径一致性：通过。** 房间保留原生标准精英奖励；Encounter 与 Elite 均不追加角色 Boss 或自定义奖励。Elite 身份不参与“已遇到”判定。
- **健壮性：通过。** 空历史、未知 ID、非法自定义状态、模型注册失败、死亡、逃跑、重复初始化和多人方向维护均有明确处理方式。
- **可测试性：通过。** 数值类自动测试只验证倍率、向下取整和行为关系，不断言固定配置数值；场景测试只验证相对布局，不断言固定坐标。
- **范围控制：通过。** 出现条件、Act 注入、概率、专属背景与 BGM 已集中列入后续延期项，不混入当前实现范围。
- **歧义与占位符检查：通过。** Spec 不含占位实现或悬而未决的分支；“明确排除范围”中的“未确认机制”仅表示禁止擅自扩展需求。

自审未发现阻塞实现的未决设计。后续若改变本 Spec 已确认的选择算法、倍率、奖励口径、夹击语义或出现范围，必须重新提交设计确认。

## 18. 关键词标题

**BandMemberEncounter｜乐队精英房｜未遇 Boss 选择｜四成员 Elite 继承｜Kaiser Crab 夹击布局｜原生精英奖励｜存档确定性**

## 19. 实现与验证记录

- 已实现 `BandMemberEncounter`、确定性成员选择器、四个 Elite 派生模型、统一属性缩放器和原生夹击协调器。
- 已通过 Encounter 自定义状态保存并恢复 `leftMember`、`rightMember`；非法或重复状态会整体失效后重新选择。
- 已按当前公开 API 将历史读取落实为 `RunManager.Instance.DebugOnlyGetState()` 与 `MapPointHistory[*].Rooms`，并通过原生 `SurroundedPower.AfterDeath(...)` 刷新敌人离场后的朝向。
- 已新增独立 Encounter 场景和中英本地化；Encounter 未注入任何 Act 遭遇池。
- `tests/BandMemberEncounter.Tests.ps1` 与仓库全部 PowerShell 回归测试通过；测试只断言倍率关系、派生关系和相对布局，不固化战斗配置值或场景坐标。
- `dotnet build --no-restore` 与 `dotnet publish --no-restore` 均为零错误，包含新场景的 `.pck` 已更新。
- 游戏内单人、满员多人、死亡／逃跑、战斗中存读档、重连与标准精英奖励流程仍需人工验收。
