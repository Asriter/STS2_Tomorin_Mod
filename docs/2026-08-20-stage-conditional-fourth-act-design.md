# Stage（舞台）条件式第四层设计规格

> 状态：代码实现与自动化验证已完成，游戏内集成验收待执行
>
> 设计日期：2026-08-20
>
> 关键词标题：**Stage 舞台｜FPO 隐藏解锁｜指定遗物同持｜条件 Boss 奖励｜固定单路线第四层**

## 1. 文档目的与范围

本规格为 `STS2_Tomorin_Mod` 设计一个条件式第四层 `Stage`，中文名为“舞台”。本文定义章节注册、进入条件、固定地图、内容绑定、`FullPowerOblivionis` 击败进度、第三层 Boss 奖励恢复、多人同步、存读档、错误处理、测试用例与 TODO。

本文最初用于约束实现边界；2026-08-20 已按确认方案完成代码、静态检查与本地单元测试，并保留需游戏内验证和后续资源替换的事项。

本次设计未读取或写入 Basic Memory；最终规格仅保存在项目 `docs` 目录。

## 2. 已确认目标

- 新增独立章节模型 `Stage`，英文显示名为 `Stage`，简体中文显示名为“舞台”。
- Stage 是 Glory 结束后的条件式最终层，不是普通随机章节。
- Stage 只有一条固定可见路线：

  ```text
  GiraffeAncient
  → FeedTheCat
  → MechaKnightElite
  → Shop
  → FeedTheCat
  → RestSite
  → CrychicPhatomBoss
  ```

- Stage 只会为“非每日挑战且玩家列表中至少包含一名 Tomorin”的新 Run 注册隐藏候选层。
- 真正进入 Stage 还要求本 Run 中真实击败过 `FullPowerOblivionis`，并且同一名玩家当前独自持有全部指定遗物。
- 当前 Boss 战真实击败 FPO 并赢得战斗时，先通过原版管线发放标准 Boss 奖励；奖励完成后才判断 Stage。
- Stage 的临时章节资源全部复用 Glory 对应资源，并逐项标记为 TODO。
- 不为旧存档增加迁移、补注册或强制地图重建。

## 3. 非目标

- 不修改 `FeedTheCat` 的页面、选项、投票、奖励或事件内部状态机。
- 不为第二次 `FeedTheCat` 设计差异化内容；它只是固定事件占位。
- 不修改 `MechaKnightElite`、Shop、RestSite 或 `CrychicPhatomBoss` 的内部玩法。
- 不让 `LoadModBoss` 控制 Stage Boss。
- 不手工复制原版 Boss 奖励内容，也不配置固定奖励数值。
- 不对旧存档执行数据迁移或补偿。
- 本地 .NET 测试项目不纳入 Git。
- 当前阶段不制作 Stage 专属美术、音频、场景或转场资源。

## 4. 当前项目事实与约束

### 4.1 相关现有文件

- `Scripts/Events/GiraffeAncient.cs`：当前仍将 Giraffe 限制在 Glory，且要求全员 Tomorin；实现时需要按本规格调整。
- `Scripts/Events/FeedTheCat.cs`：现有 `IsAllowed` 受固定选择上下文和旧章节索引限制。
- `Scripts/Patch/FixedFirstEventPatch.cs`：现有逻辑只负责旧章节的首次固定事件，不能直接承担 Stage 的两个事件节点。
- `Scripts/Enemy/FullPowerOblivionis.cs`：FPO 敌人模型。
- `Scripts/Encounters/OblivionisBoss.cs`：现有隐藏路线 Boss 遭遇，FPO 可在其战斗中动态出现。
- `Scripts/Encounters/CrychicPhatomBoss.cs`：Stage 固定 Boss 遭遇。
- `Scripts/Relics/AnonGuitar.cs`、`RaanaGuitar.cs`、`SoyoBase.cs`、`TakiDrum.cs`：进入 Stage 所需遗物。
- `Scripts/Patch/ActModelBossPatch.cs` 与 `ActModelBossOrderPatch.cs`：现有 `LoadModBoss` 只继续管理旧章节 Boss，不得覆盖 Stage 的固定 Boss。

### 4.2 已核对的框架行为

- BaseLib 的 `CustomActModel` 可使用自然层编号参数 `-1`，使自定义层不参与普通层编号匹配。
- `RunState.CreateForNewRun` 与旧存档恢复路径相互独立，允许只在新 Run 注册 Stage。
- `RunState.Acts` 在运行期间不适合临时改写，因此 Stage 应在创建新 Run 时作为隐藏候选层注册。
- 原版 Boss 奖励由现有奖励管线生成；奖励内容生成本身不要求下一层存在。
- 战斗胜利 UI 会根据 Encounter 的奖励资格决定是否进入原版奖励流程。
- 当前静态代码与“第三层 Boss 实际不掉奖励”的运行观察不一致，因此实现前必须验证当前部署版本的真实阻断点。

## 5. 总体设计

### 5.1 核心组件职责

| 组件 | 职责 |
|---|---|
| `Stage` | 独立章节模型、名称、临时资源代理、固定内容入口 |
| `StageRouteDefinition` | 固定路线的唯一语义来源 |
| `StageActMap` | 根据路线定义创建确定性单路线地图 |
| `StageRegistrationPolicy` | 判断新 Run 是否注册隐藏 Stage 候选层并负责插入顺序 |
| `StageUnlockProgress` | 持久记录本 Run 是否真实击败过 FPO |
| `StageEligibility` | 在 Glory 奖励完成后计算是否真正进入 Stage |
| FPO 战斗奖励状态 | 将“历史击败进度”与“当前 Boss 战奖励资格”分离 |
| 奖励适配器 | 只恢复原版 Boss 奖励资格，不自行生成奖励 |
| Stage 转层适配器 | 在多人统一同步点选择进入 Stage 或正常结束 Run |

### 5.2 主流程

```text
创建新 Run
  └─ 非 Daily 且任意玩家为 Tomorin？
       ├─ 否：不注册 Stage
       └─ 是：在 Glory 后注册隐藏 Stage 候选层

任意位置发生 FPO 死亡
  └─ 是否为真实且未被阻止的死亡？
       ├─ 否：不记录
       └─ 是：持久化 StageUnlockProgress，并首次输出中文日志
              └─ 当前是否为 Boss 房间战斗？
                   ├─ 否：只保留解锁进度
                   └─ 是：标记当前战斗可获得标准 Boss 奖励

当前 Boss 战胜利
  └─ 本战是否真实击败 FPO？
       ├─ 是：走原版标准 Boss 奖励流程
       └─ 否：不因历史进度补发奖励

所有玩家完成奖励并到达统一转层同步点
  └─ 计算 StageEligibility
       ├─ 满足：进入 Stage
       └─ 不满足：正常结束 Run
```

## 6. Stage 章节模型与资源

### 6.1 身份与层级

- 模型名：`Stage`。
- 英文本地化：`Stage`。
- 简体中文本地化：`舞台`。
- `CustomActModel` 自然层编号参数使用 `-1`，仅用于阻止自然层级生成。
- 实际第四层身份由 `RunState.Acts` 中紧邻 Glory 的顺序确定。
- Stage 不参与普通随机章节选择，也不会在 Glory 结束前生成可进入地图。

### 6.2 临时资源代理

Stage 应通过一个集中式临时资源代理取得 Glory 的规范资源，避免在多个属性中复制资源路径。以下每项引用都必须包含可搜索的 Stage 资源 TODO：

- 章节场景背景与环境视觉。
- 地图背景、地图装饰和图例。
- 环境音、背景音乐及音频切换。
- 进入和离开章节的转场资源。
- 商店场景与商人展示资源。
- 篝火场景与角色休息展示资源。
- 宝箱或公共房间资源引用，即使固定路线当前不直接使用。
- Boss 前后场景表现及章节级房间资源。

`GiraffeAncient`、`FeedTheCat`、`MechaKnightElite`、Shop、RestSite 和 `CrychicPhatomBoss` 继续使用各自内容模型的资源；这些不属于 Stage 章节资源代理。

## 7. 固定地图设计

### 7.1 路线定义

`StageRouteDefinition` 是地图顺序、房间解析和测试期望的唯一来源。禁止在地图生成器、房间选择器和测试中分别维护重复的节点数组。

| 路线语义 | 地图点类型 | 房间或内容 |
|---|---|---|
| Ancient | `MapPointType.Ancient` | `GiraffeAncient` |
| FirstEvent | `MapPointType.Unknown` | 固定 `FeedTheCat` |
| Elite | `MapPointType.Elite` | 原版 `MechaKnightElite` |
| Shop | `MapPointType.Shop` | 原版 Shop |
| SecondEvent | `MapPointType.Unknown` | 固定 `FeedTheCat` |
| RestSite | `MapPointType.RestSite` | 原版 RestSite |
| Boss | `MapPointType.Boss` | `CrychicPhatomBoss` |

两个事件节点使用 `MapPointType.Unknown`，Stage 房间解析器必须将其确定为 `RoomType.Event`，不得进入随机事件池。

### 7.2 连线与技术锚点

- 每个可见节点只连接到路线定义中的下一个节点。
- 不存在分支、旁路、随机替换或附加可进入节点。
- 如果引擎要求起始锚点，可创建隐藏技术锚点。
- 技术锚点必须不可见、不可选择、不可进入，不计入层数，也不写入房间历史。
- 非预期房间请求应记录错误并中止非法解析，不得随机回退到其他房间。

### 7.3 确定性布局

- 可见节点位于 Glory 临时地图可用区域的水平中心线。
- 起始节点区域与 Boss 节点区域沿用 Glory 地图的安全边界。
- 中间节点按路线定义中的顺序等距布局。
- 技术锚点使用框架默认入口偏移。
- 不维护与 Glory 临时资源尺寸绑定的逐节点硬编码坐标。

## 8. 固定内容规则

### 8.1 GiraffeAncient

- `IsValidForAct` 只接受 `Stage`，不再允许 Glory。
- `IsAllowed` 在玩家列表非空且至少存在一名 Tomorin 时成立。
- 不要求全员都是 Tomorin。
- 不修改 Giraffe 内部选项和遗物逻辑。

### 8.2 两个 FeedTheCat

- 两个事件节点都由 Stage 专用固定事件解析器直接选择规范 `FeedTheCat` 模型。
- Stage 解析器绕过随机事件池、访问历史去重以及用于旧章节随机资格判断的 `IsAllowed` 选择流程。
- 不复用或扩张 `FixedFirstEventPatch` 的“首次事件”语义。
- 不修改 `FeedTheCat` 的内部事件逻辑。
- 第二次事件允许重复执行现有事件内容；由此产生的重复遗物、选项或投票表现保持现状，不在本需求中修正。

### 8.3 精英、公共房间与 Boss

- 精英固定为原版稳定模型 `MegaCrit.Sts2.Core.Models.Encounters.MechaKnightElite`。
- Shop 与 RestSite 使用原版房间行为。
- Stage Boss 固定为 `CrychicPhatomBoss`。
- `LoadModBoss` 的开启或关闭均不得改变 Stage Boss。
- 击败 Stage 的 `CrychicPhatomBoss` 后按最终层正常胜利流程结束 Run，不继续进入排在 Stage 后方的其他自定义层。

## 9. Stage 候选层注册

### 9.1 基础注册条件

只在创建新 Run 时检查：

- 游戏模式不是每日挑战。
- `RunState.Players` 中至少存在一名 Tomorin。

标准模式和自定义模式均可注册。注册阶段不要求指定遗物，也不要求 FPO 击败进度。

### 9.2 插入与去重

- Stage 作为内部隐藏候选层插入 Glory 后方。
- 注册必须幂等；已有相同稳定模型标识时不得重复插入。
- 其他自定义层彼此之间的相对顺序保持不变并整体顺延到 Stage 后方。
- Stage 模型注册失败时，不启用 Stage 转层拦截。
- 候选层存在不代表最终一定进入；条件不足时 Glory 结束后直接正常结束 Run。

## 10. FPO 击败进度

### 10.1 记录条件

`StageUnlockProgress` 是 Run 级持久状态，语义为“本 Run 是否真实击败过 `FullPowerOblivionis`”。记录条件只有：

- 死亡对象的稳定模型标识对应 FPO。
- 死亡没有被复活、替死或其他机制阻止。

该进度不绑定 Glory、第三层、`OblivionisBoss` 或任何特定遭遇。FPO 在本 Run 任意位置真实死亡都能记录。

### 10.2 状态与中文日志

- 状态只能从未达成转为已达成，之后保持稳定。
- 重复死亡通知不得产生重复副作用。
- 状态写入可同步的 Run 数据，不使用进程级静态字段。
- FPO 真实死亡记录入口首次成功完成状态转换时输出中文信息日志。
- 日志必须包含可稳定搜索的文本：`已记录 FPO 击败进度`。
- 完整日志文本使用：`[Stage] 已记录 FPO 击败进度：FullPowerOblivionis 已在本局中真实死亡。`
- 同一进程只在状态首次转换时输出；重复钩子回调不重复输出。
- 存档反序列化、已有进度恢复和重复网络状态同步不输出该成功日志。

## 11. 当前 Boss 战奖励

### 11.1 与解锁进度分离

历史 `StageUnlockProgress` 只用于 Stage 资格，不会让之后无关的 Boss 战获得奖励。当前战斗另有奖励状态，其生命周期绑定当前 Boss 房间战斗。

只有同时满足以下语义，才启用本场奖励：

- 当前房间是 Boss 房间。
- FPO 在当前战斗内真实且未被阻止地死亡。
- 玩家最终赢得同一场战斗。

FPO 在普通战斗中死亡只更新历史进度。战斗失败、放弃或切换战斗后，当前战斗奖励资格不得泄漏到下一场战斗。

### 11.2 奖励状态生命周期

当前战斗奖励状态至少区分：未获得资格、已获得资格、奖励已生成。状态与当前战斗的稳定身份绑定，并覆盖游戏合法支持的“FPO 死亡后到奖励完成前”存读档窗口。

- FPO 真实死亡时，从未获得资格进入已获得资格。
- 原版奖励集合开始生成时，进入奖励已生成。
- 重复进入生成入口不得再次创建奖励集合。
- UI 重绘或多人重连应继续使用已经生成的原版奖励状态。
- 战斗失败或离开不匹配的战斗时清除未消费资格。

实现前应使用当前游戏版本提供的稳定战斗或房间身份；不得用不受控的全局布尔值跨战斗保存资格。

### 11.3 原版奖励管线

- 奖励适配器只把原版 Boss 奖励资格打开。
- 奖励内容继续由原版 `RewardsSet` 流程按玩家生成和展示。
- 不手工添加固定遗物、卡牌、药水、金币或其他数值奖励。
- 每名玩家不得重复获得同一场战斗的奖励集合。
- 奖励资格不检查 Tomorin、指定遗物、每日挑战或 Stage 是否存在。
- 即使完整 Stage 条件失败，也要先完成已获得的标准 Boss 奖励，再正常结束 Run。

### 11.4 实现前运行时核查

静态检查显示奖励生成不依赖下一层，且当前相关 Encounter 源码未明确解释实际不掉奖励的现象。实现 Agent 必须先在当前部署版本记录：

- 实际 Encounter 类型。
- 当前 RoomType。
- 奖励资格属性的运行时结果。
- 战斗胜利 UI 到奖励展示的实际调用链。

随后只修改被证实的最窄阻断节点。如果当前奖励资格本已为真，则不得叠加无意义补丁；应继续定位实际抑制点。此处的补丁位置需要运行证据确定，但奖励行为与边界已经固定，不属于未决产品决策。

## 12. 最终 Stage 资格与转层

### 12.1 判断时机

- 所有玩家完成当前 Boss 奖励处理后再判断。
- 在原版多人统一转层同步点判断，不在单个玩家的奖励按钮事件中判断。
- 遗物使用奖励完成后的当前状态。

### 12.2 完整资格

```text
StageEligible =
    当前层为 Glory
    且 Stage 候选层存在并紧邻 Glory
    且游戏模式不是每日挑战
    且 RunState.Players 中至少存在一名 Tomorin
    且 StageUnlockProgress 已达成
    且至少一名玩家独自持有 RequiredStageRelics 完整集合
```

`RequiredStageRelics` 由以下稳定模型标识组成：

- `AnonGuitar`
- `RaanaGuitar`
- `SoyoBase`
- `TakiDrum`

多人语义：

- 指定遗物必须集中在同一名玩家身上，不能跨玩家合并。
- 遗物持有者可以不是 Tomorin；队伍中存在 Tomorin 与遗物同持是两个独立条件。
- 判断范围是 `RunState.Players` 中全部玩家对象。
- 不筛选遗物持有者是否存活、在线或当前连接。
- 使用稳定模型标识，不使用显示名或本地化文本。

### 12.3 转层结果

- 完整资格成立：调用原有进入下一层流程进入 Stage。
- 任一条件失败：调用原有正常胜利结束流程。
- 对新 Run 而言，即使其他自定义层被顺延到 Stage 后方，条件失败也不会跳过 Stage 去进入其他层。
- Stage 候选层缺失、重复或顺序异常时记录错误并安全结束，不随机选择其他层。
- 没有 Stage 候选层的旧存档不进入本转层拦截，保持其原有流程。

## 13. 存档与多人同步

### 13.1 新 Run 与新格式存档

- Stage 候选层随 Run 的 Acts 数据保存和恢复。
- `StageUnlockProgress` 随 Run 保存并恢复。
- 当前 Boss 战奖励状态按其合法存档窗口保存，且必须与战斗身份匹配。
- 不使用无法序列化或无法同步的进程级静态状态保存关键资格。

### 13.2 旧存档

- 不修改旧存档恢复入口来补注册 Stage。
- 不含 Stage 候选层的旧存档按原有 Acts 和地图读取。
- 缺少 `StageUnlockProgress` 字段时按未达成处理，不执行迁移补偿。
- 只有现有游戏或框架本身决定重建 Run 或层列表时，才按新游戏注册规则重新计算。

### 13.3 多人一致性

- Stage 候选层顺序、FPO 进度、当前奖励状态和最终资格必须来自同步数据或确定性同步动作。
- 所有参与者在同一同步点得到相同转层结果。
- 玩家断线、重连或角色死亡不改变已经存在于 `RunState.Players` 中的遗物持有语义。
- FPO 进度中文日志以本地状态首次从未达成转为已达成为触发条件；同一进程不因重复网络回调刷屏。

## 14. 错误处理与兼容性

- 注册失败：记录错误，不启用转层拦截。
- Stage 重复或顺序异常：记录错误并安全结束 Run。
- 固定内容模型缺失：阻止非法房间解析，不随机替代。
- 非预期地图点或 RoomType：记录足够上下文并中止该非法解析。
- 奖励资格重复回调：保持奖励已生成状态，不重复发奖。
- 死亡回调重入：进度幂等，中文成功日志不重复。
- `LoadModBoss`：只继续影响旧章节，不影响 Stage。
- 其他自定义层：相对顺序不变；Stage 是 Glory 后的条件式最终层。
- 版本差异：Harmony 或 BaseLib 接入点必须先用当前版本运行证据核对，失败时采用安全关闭而不是猜测式补丁。

## 15. 测试架构

### 15.1 本地 .NET 测试项目

- 使用本地 xUnit 测试项目，路径固定为 `local-tests/Stage.Tests/`。
- 测试项目引用主 Mod 项目，只实例化不依赖 Godot UI 的纯策略。
- 测试项目及其源码、项目文件和构建产物不得纳入 Git。
- 根 `.gitignore` 添加精确排除项 `/local-tests/Stage.Tests/`；该忽略规则本身可以纳入 Git。
- 测试框架与目标框架版本使用实现时本机 SDK 模板中与主项目兼容的版本。
- 完整测试语义保留在本规格中，以便其他 Agent 重新创建本地测试项目。

### 15.2 PowerShell 静态检查

- 现有 `tests/` 下的 PowerShell 静态检查继续作为项目资产，可纳入 Git。
- 静态检查负责模型注册、本地化键、稳定模型引用、资源 TODO、Harmony 接线和禁止项。
- 现有 Giraffe 测试中“仅 Glory”和“全员 Tomorin”的旧断言必须更新。
- 静态检查不替代纯策略行为测试和游戏内集成验收。

### 15.3 禁止的测试方式

- 不断言节点坐标、间距或地图尺寸的固定数值。
- 不断言奖励项目、奖励数量、金币或概率的固定数值。
- 不断言配置属性等于某个固定数值。
- 不通过硬编码路线长度判断地图；应与 `StageRouteDefinition` 做完整语义序列比较。
- 不通过遗物数量字面值判断；应检查玩家持有集合是否覆盖 `RequiredStageRelics`。

## 16. 测试用例

### 16.1 纯策略单元测试

#### Stage 基础注册资格

- 非每日挑战且玩家列表包含 Tomorin 时，允许注册隐藏 Stage 候选层。
- 每日挑战即使包含 Tomorin，也不允许注册。
- 玩家列表不包含 Tomorin 时不允许注册。
- 标准模式和自定义模式在其他条件成立时均允许注册。
- 重复执行注册逻辑不会产生重复 Stage。
- Stage 紧邻 Glory，其他自定义层的相对顺序不变。

#### Stage 最终进入资格

- 所有条件成立时允许进入。
- 通过逐项移除必要条件的参数化场景验证：缺少任一必要条件都不允许进入。
- 同一玩家持有 `RequiredStageRelics` 完整集合时满足遗物条件。
- 遗物分散于不同玩家时不满足条件。
- 持有者离线、断线或角色已死亡时仍按当前玩家对象计算。
- 持有者不是 Tomorin，但队伍另有 Tomorin 时可以满足两个独立条件。
- 额外遗物不影响判断。
- 只有击败进度但没有合法 Stage 候选层时不允许进入。

#### FPO 进度与日志

- FPO 真实且未被阻止的死亡设置进度。
- 其他敌人死亡不设置进度。
- 被阻止、复活或替代的死亡不设置进度。
- FPO 在非 Glory、非第三层或非 `OblivionisBoss` 遭遇中真实死亡仍设置进度。
- 重复通知保持幂等。
- 首次记录时产生包含 `已记录 FPO 击败进度` 的中文信息日志。
- 重复通知不再次产生成功日志。
- 序列化恢复后保持相同语义；旧数据缺少字段时按未达成处理。

#### 当前战斗奖励资格

- 当前 Boss 战真实击败 FPO 并最终胜利时允许原版奖励流程。
- 普通战斗中的 FPO 死亡不获得 Boss 奖励资格。
- 被阻止的 FPO 死亡不获得奖励资格。
- 只有历史击败进度、当前战斗没有击败 FPO 时不获得奖励资格。
- 当前 Boss 战没有胜利时不生成奖励。
- 当前战斗资格不能被之后无关的 Boss 战消费。
- 奖励进入已生成状态后，重复入口不重复生成奖励。
- 奖励资格不受 Tomorin、指定遗物、每日挑战或 Stage 候选层影响。

### 16.2 PowerShell 静态检查

- Stage 有独立稳定模型标识和两种已确认本地化。
- Stage 临时资源集中引用 Glory，并逐项包含可搜索 TODO。
- 地图生成器只使用 `StageRouteDefinition`。
- 路线语义序列与本规格一致。
- Giraffe 仅适用于 Stage，且队伍中任意 Tomorin 即可。
- 两个事件节点都固定解析为规范 `FeedTheCat`，且不修改事件内部实现。
- 精英固定引用原版 `MechaKnightElite`。
- Boss 固定引用 `CrychicPhatomBoss`，且 Stage 路径不读取 `LoadModBoss`。
- FPO 进度接入 Run 序列化。
- 转层适配器只处理合法的 Glory-to-Stage 情形。
- 奖励适配器没有手工复制原版奖励内容。

### 16.3 游戏内集成验收

#### 注册、层顺序与旧存档

- 符合基础条件的新 Run 内部存在紧邻 Glory 的 Stage 候选层，但提前不可见、不可选且不生成可进入地图。
- 每日挑战不出现 Stage；无 Tomorin 队伍不出现 Stage；包含 Tomorin 的混合队伍可以注册。
- 与其他自定义层共同加载时，其彼此相对顺序不变。
- 重复初始化和重新开局不会重复注册。
- 不含 Stage 的旧存档不补注册、不重排、不重建地图。
- 已保存 Stage 候选层的新存档能恢复候选层和进度。

#### 固定地图与内容

- 按地图连通关系遍历得到的唯一可见路线与 `StageRouteDefinition` 完全一致。
- 不存在分支、旁路、随机替换或额外可进入节点。
- 技术锚点不可见、不可选、不可进入，也不写入房间历史。
- 两个事件节点都能进入 `FeedTheCat`，首次访问历史不阻止第二次固定进入。
- 第二次事件继续使用现有事件状态机。
- 非预期房间请求记录错误且不随机回退。
- Shop 与 RestSite 使用原版行为。
- `LoadModBoss` 的任意状态都不改变 Stage Boss。

#### FPO 奖励与转层

- 当前 Boss 战真实击败 FPO 后，先展示原版标准 Boss 奖励。
- 奖励内容由原版决定，不校验具体奖励数值。
- 完整 Stage 条件失败时，玩家仍先领取奖励，然后正常结束 Run。
- 完整 Stage 条件成立时，所有玩家完成奖励后进入 Stage。
- FPO 在其他战斗中死亡只更新进度，不为之后 Boss 补发奖励。
- FPO 死亡被阻止时，不更新进度且不启用奖励。
- 奖励界面重入、玩家重连或同步回调不重复发奖。

#### 多人同步与 Stage 结束

- 主机与客户端看到相同的候选层顺序、FPO 进度、奖励状态和转层结果。
- 遗物集中于同一玩家时可通过，分散持有时不可通过。
- 遗物持有者离线或角色死亡时仍按其玩家对象判断。
- 单个客户端完成奖励时不会提前转层。
- 客户端重连后不丢失进度、不重复奖励、不进入不同层。
- 进入 Stage 后按固定路线推进；击败 `CrychicPhatomBoss` 后正常结束 Run。
- Stage 结束后不继续进入排在后方的其他自定义层。

## 17. TODO List

本节中的 TODO 均是用户明确要求保留的未实现工作或临时资源替换，不代表行为设计未决。

### 17.1 本次功能实现记录

- 已完成：创建 `Stage` 章节模型、稳定模型标识及英文和简体中文本地化。
- 已完成：创建集中式 Stage 临时资源代理，并逐项标记 Glory 资源复用 TODO。
- 已完成：创建 `StageRouteDefinition` 与确定性 `StageActMap`。
- 已完成：接入新 Run 隐藏候选层注册、去重及 Glory 后插入逻辑。
- 已完成：以 `StageRunProgressModifier` 实现 Run 持久化、多人序列化与中文成功日志。
- 已完成：实现 FPO 真实死亡识别及当前 Boss 战奖励状态。
- 已完成：实现原版标准 Boss 奖励资格适配与防重复消费。
- 已完成：实现奖励完成后的统一 Stage 资格判断与转层。
- 已完成：将 `GiraffeAncient` 从 Glory 移至 Stage，并改为任意 Tomorin 队伍可用。
- 已完成：创建 Stage 专用固定事件解析器，允许规范 `FeedTheCat` 重复进入但不修改事件内部逻辑。
- 已完成：固定 Stage 精英、Shop、RestSite 与 Boss 解析。
- 已完成：更新受影响的 PowerShell 静态检查。
- 已完成：在 `local-tests/Stage.Tests/` 创建并运行本地 xUnit 测试项目，且由 `.gitignore` 精确排除。
- 待实机验收：标准、自定义、多人、存读档和跨 Mod 层顺序。

### 17.2 当前版本核查记录

- 待实机复现：当前部署版本第三层 Boss 不掉奖励的具体 UI 表现。
- 已完成静态核查：`OblivionisBoss` 为 Boss Encounter 且 `ShouldGiveRewards` 已为真；奖励调用链为 `CombatRoom.OfferRoomEndRewards → RewardsCmd.GenerateForRoomEnd → RewardsSet.WithRewardsFromRoom → RewardsSet.Offer`。
- 已确认：插入 Stage 后 Glory 不再是物理末章，最窄奖励资格适配入口为 `RewardsSet.WithRewardsFromRoom`；实现仅放行原版奖励或返回 `EmptyForRoom`。
- 已确认：`Hook.AfterDeath` 的 `wasRemovalPrevented` 可区分真实死亡与被阻止的死亡。
- 已确认：加入 `RunState.Modifiers` 的普通 `ModifierModel` 通过 `[SavedProperty]` 进入存档与网络序列化。
- 已确认：当前战斗身份使用 Encounter 稳定 ID、章节索引与地图坐标；奖励 UI 期间的完整存档恢复仍待实机验证。
- 待实机确认：隐藏 Stage 候选层不会被 UI 或普通地图生成器提前展示。
- 已确认：Boss 奖励结束后经 `ActChangeSynchronizer` 汇总全部玩家准备状态，再调用 `RunManager.EnterNextAct`；转层补丁位于该统一同步点之后。

### 17.3 后续资源替换 TODO

- [ ] 替换 Stage 场景背景和环境视觉。
- [ ] 替换 Stage 地图背景、地图装饰和图例。
- [ ] 替换 Stage 环境音、背景音乐和音频切换。
- [ ] 替换 Stage 进入与离开转场。
- [ ] 替换 Stage 商店场景和商人展示。
- [ ] 替换 Stage 篝火场景和角色休息展示。
- [ ] 替换 Stage 宝箱或公共房间章节资源引用。
- [ ] 替换 Stage Boss 前后场景表现。
- [ ] 替换 Stage 专属地图节点美术。

## 18. 完成判定

实现只有在以下语义全部成立时才算完成：

- Stage 不在 Daily 中注册，且新 Run 的基础角色条件正确。
- Glory 结束后仅在完整资格成立时进入 Stage，否则正常结束。
- FPO 进度不绑定 Glory、第三层或特定遭遇，并在首次记录时输出中文日志。
- 标准 Boss 奖励只属于当前真实击败 FPO 且最终胜利的 Boss 战。
- 奖励先于 Stage 资格判断完成，且不重复发放。
- 指定遗物必须由同一玩家持有；不筛选其角色、在线或存活状态。
- Stage 地图只有已确认的固定可见路线，所有内容绑定准确。
- Giraffe 仅属于 Stage；两个 FeedTheCat 不修改事件内部逻辑。
- Stage Boss 不受 `LoadModBoss` 控制，击败后正常结束 Run。
- 旧存档无额外迁移和强制重建。
- 测试不使用固定数值断言；本地 .NET 测试项目未纳入 Git。
- 所有临时 Glory 资源复用均有明确 TODO。

## 19. Spec 自审记录

本节保留设计阶段的一致性自审，并补充实现后的状态说明。

### 19.1 占位项检查

- 未发现任何未确认的占位标记或临时决策文本。
- 文档中的 TODO 全部集中表达已确认但尚未实现的功能、运行时核查或后续资源替换。
- 奖励补丁位置和稳定战斗身份需要当前部署版本的运行证据决定，但目标行为、状态边界和失败策略均已确定，不构成产品行为未决。

### 19.2 一致性检查

- “新 Run 基础注册条件”与“Glory 结束后的完整进入条件”已明确分离。
- FPO 历史击败进度不绑定章节或遭遇；Boss 奖励只绑定当前实际击败 FPO 的 Boss 战，两者无混用。
- 奖励在 Stage 资格判断之前完成；条件失败时仍可领奖后正常结束。
- 多人遗物要求为同一玩家完整持有，且不筛选角色、在线或存活状态。
- Daily、旧存档、其他自定义层顺序、Giraffe 归属、FeedTheCat 重复占位和 `LoadModBoss` 边界前后一致。
- FPO 中文成功日志只在真实死亡记录入口首次改变进度时输出，不会因读取存档或重复同步产生误报。

### 19.3 范围检查

- Spec 只包含第四层、FPO 进度、条件奖励及其必要接入，不修改事件或敌人的既有内部玩法。
- 所有用户要求的未完成工作均已写入 TODO List。
- 本地 .NET 测试项目明确排除 Git；可纳入 Git 的 PowerShell 静态检查与其边界已经区分。
- 当前实现已完成代码、自动化测试与文档更新；未制作 Stage 专属资源，也未读取或修改 Basic Memory。

### 19.4 测试检查

- 测试断言采用状态、集合、稳定模型标识、流程顺序和语义路线比较。
- 未使用固定坐标、配置数值、奖励数值、概率或路线长度字面值作为测试断言。
- 单元、静态和游戏内集成三层测试覆盖注册、地图、进度、奖励、转层、存档、多人及错误路径。

### 19.5 自审结论

Spec 对应的代码与自动化验证已经完成。剩余事项仅为当前部署版本的游戏内集成验收及 17.3 列出的 Stage 专属资源替换；未完成实机验收前不得宣称多人、奖励界面重入或战斗中存读档已验证。
