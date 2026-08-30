# FateGuidance 多人共享事件设计

**状态：** 已完成对话设计确认，等待书面 Spec 审阅与后续 Agent 实现  
**设计日期：** 2026-08-20  
**节点/类名：** `FateGuidance`  
**英文标题：** `Fate's Guidance`  
**中文标题：** `命运所指`  
**关联待办：** [TODO.md](./TODO.md)

## 1. 目标

在隐藏章节 Stage 的固定路线中，将第二个事件节点替换为多人共享事件 `FateGuidance`。每名玩家从三个具名 Boss 中提交一个选择，最终结果完全使用游戏原生共享事件规则裁决并同步。结果确定后，修改当前章节的第一 Boss、立即刷新地图上的 Boss 图标，并保证实际遭遇、多人状态和存档恢复一致。

三个选项按以下顺序展示：

1. `CrychicPhatomBoss`
2. `OblivionisBoss`
3. `TakiBoss`

三个 Boss 当前均为占位内容；本设计只引用其稳定 Encounter 类型与 `ModelId`，不修改 Boss 行为。

## 2. 已确认的产品规则

### 2.1 事件位置

`FateGuidance` 固定放在 Stage 的第二事件位置。路线保持：

```text
Ancient → FeedTheCat → Elite → Shop → FateGuidance → RestSite → Boss
```

事件不进入普通随机事件池，不改变 Stage 地图拓扑或节点数量。

### 2.2 多人裁决

事件声明 `IsShared => true`，使用原生 `EventSynchronizer`：

- 每名玩家提交一个选项。
- 全部选择到齐后，主机从所有玩家提交的选择中等权随机抽取最终结果。
- 最终选项通过原生 `SharedEventOptionChosenMessage` 同步。
- 不实现多数票、房主指定、自定义 VoteCoordinator、额外 Choice ID 或专用同步消息。

### 2.3 Boss 修改

- Stage 进入时的默认第一 Boss 继续为 `CrychicPhatomBoss`。
- 若目标 Boss 已经位于第一 Boss 或第二 Boss 槽位，返回“不变”，不写入状态、不刷新 UI。
- 若目标 Boss 未被选中，只替换第一 Boss。
- 第二 Boss 的身份和顺序始终保持不变。
- 不允许因替换产生重复 Boss。
- 无论状态是否实际改变，事件都进入最终选中 Boss 对应的独立结算页。

### 2.4 视觉资源

本次不新增事件图片。`FateGuidance` 暂时复用：

```text
res://STS2_Tomorin_Mod/images/events/Giraffe.png
```

专属事件立绘作为明确的后续任务登记在项目 TODO 中。

## 3. 方案选择

### 3.1 采用方案

采用“原生共享事件 + 通用第一 Boss 路线服务 + 通用地图 Boss 视觉同步器”。

该方案让游戏原生系统负责投票和网络同步，让事件只负责页面与选项映射，并将后续其他事件、遗物或命令也会需要的“修改第一 Boss 并刷新地图图标”封装为 Mod 内部通用接口。

### 3.2 未采用方案

- **事件内联 Boss 修改：** UI、多人流程和 Run 状态规则耦合，难以复用和独立验证。
- **自定义投票协调器：** 与原生共享事件重复，会增加 Choice ID、断线恢复和会话清理风险。
- **专用 Modifier 保存选择：** 原版 `SerializableRoomSet.BossId` 已能持久化第一 Boss，重复保存会产生双重权威状态。
- **现在支持第二 Boss 写入：** 当前没有第二 Boss 创建、删除或换位需求；提前定义其规则违反 YAGNI。未来出现明确需求时新增独立接口。

## 4. 架构与文件边界

### 4.1 新建 `Scripts/Events/FateGuidance.cs`

职责仅限事件表现和目标映射：

- 定义 `FateGuidance : CustomEventModel`。
- 声明 `IsShared => true`。
- `IsAllowed(IRunState)` 只允许当前 Act 为 `STS2_Tomorin_Mod.Acts.Stage`。
- `CustomInitialPortraitPath` 返回已确认的 Giraffe 占位图片。
- `GenerateInitialOptions()` 按已确认顺序创建三个选项。
- 每个选项处理器取得 `Owner.RunState` 与对应的 `ModelDb.Encounter<T>()`，调用通用服务。
- 处理器在首次异步等待之前完成同步状态修改，随后调用 `SetEventFinished(...)` 进入对应结果页。
- 不直接调用 `SetBossEncounter`、`SetSecondBossEncounter`，也不访问地图 UI。

页面常量固定为：

```text
INITIAL
CRYCHIC
OBLIVIONIS
TAKI
```

选项处理器名称固定为：

```text
ChooseCrychic
ChooseOblivionis
ChooseTaki
```

BaseLib 的 `CustomEventModel` 自动注册模型；不需要将事件加入 `Stage.AllEvents`。

### 4.2 新建 `Scripts/Services/BossMapRouteService.cs`

该文件提供本 Mod 内部稳定门面：

```csharp
internal enum PrimaryBossChangeResult
{
    AlreadySelected,
    PrimaryBossChanged,
}

internal static class BossMapRouteService
{
    internal static PrimaryBossChangeResult ChangePrimaryBoss(
        IRunState runState,
        EncounterModel targetBoss);
}
```

方法契约：

1. 校验 `runState`、`targetBoss`、当前 Act 和当前第一 Boss。
2. 校验 `targetBoss.RoomType == RoomType.Boss`。
3. 使用 `ModelId` 与 `Act.BossEncounter`、`Act.SecondBossEncounter` 比较；禁止使用对象引用相等。
4. 目标已经存在时返回 `AlreadySelected`，不得产生任何写入或视觉刷新。
5. 目标不存在时，将 `targetBoss.CanonicalInstance.ToMutable()` 传给 `Act.SetBossEncounter(...)`。
6. 成功写入后调用 `BossMapVisualSynchronizer.RefreshCurrentBossVisuals(runState)`。
7. 返回 `PrimaryBossChanged`。

服务保持 `internal`，供本 Mod 后续功能复用，不形成面向其他 Mod 的公开兼容承诺。

### 4.3 新建 `Scripts/Services/BossMapVisualSynchronizer.cs`

该文件只把当前 Act 的权威 Boss 状态投影到已创建的地图 UI，不修改 Run 状态。

内部入口固定为：

```csharp
internal static void RefreshCurrentBossVisuals(IRunState runState);
```

实现约束：

- 取得当前 `NRun` 的 `NMapScreen`，确认其 Run、Act 和 Map 与传入状态一致。
- 集中封装对 `NMapScreen._bossPointNode`、`NMapScreen._secondBossPointNode` 以及 `NBossMapPoint` 显示字段的反射访问。
- 第一节点绑定 `Act.BossEncounter`；存在第二节点和第二 Boss 时绑定 `Act.SecondBossEncounter`。
- 每次刷新都读取 Act，不维护第二份 Boss 身份缓存。
- 地图 UI 或 Boss 节点尚未创建时安全返回；节点以后首次 `_Ready()` 时会读取最新 Act 状态。
- 普通贴图分支使用 `BossNodePath + ".png"` 和 `BossNodePath + "_outline.png"` 重新绑定正文与描边，隐藏 Spine 节点。
- Spine 分支使用 `BossNodeSpineResource` 重新设置骨骼和原版默认动画，隐藏普通贴图节点。
- PNG 与 Spine 互相切换时，必须清理旧表现的可见状态，禁止重叠。
- 重新绑定后复用原版 Boss 节点的颜色刷新流程。
- 私有字段或资源加载失败时记录一次包含字段名、Boss ID 和游戏阶段的错误，不回滚真实 Boss，也不中断共享事件。
- 不新增全局 Harmony Patch；只有显式调用 `BossMapRouteService` 的功能才触发状态改变与即时刷新。

### 4.4 修改 `Scripts/Stage/StageRouteDefinition.cs`

- 将 `StageRouteNodeKind.SecondEvent` 改名为 `StageRouteNodeKind.FateGuidance`。
- 对应节点仍为 `MapPointType.Unknown` 与 `RoomType.Event`。
- 更新注释，使代码层节点语义与需求名称一致。

### 4.5 修改 `Scripts/Stage/StageRoomResolver.cs`

- `FirstEvent` 继续解析为 `ModelDb.Event<FeedTheCat>().ToMutable()`。
- `FateGuidance` 解析为 `ModelDb.Event<FateGuidance>().ToMutable()`。
- Boss 节点改为读取 `runState.Act.BossEncounter` 并创建其可变实例。
- Boss 缺失时抛出包含章节、地图节点和楼层上下文的 `InvalidOperationException`，不静默回退到 Crychic。

### 4.6 修改 `Scripts/Acts/Stage.cs`

将“合法 Boss 集合”与“默认 Boss”分离：

- 覆盖 `BossDiscoveryOrder`，只返回 `CrychicPhatomBoss`，保证默认第一 Boss 不变。
- `GenerateAllEncounters()` 继续包含固定精英，并包含三个目标 Boss，使替换后的 Encounter 能通过模型校验和存档恢复。
- `AllEvents` 继续为空；固定路线解析器直接创建 FateGuidance。

### 4.7 修改本地化

修改：

```text
STS2_Tomorin_Mod/localization/eng/events.json
STS2_Tomorin_Mod/localization/zhs/events.json
```

统一使用 `STS2_TOMORIN_MOD-FATE_GUIDANCE` 键空间。两个语言文件必须具有相同键集合，所有值非空。

### 4.8 测试文件

- 新建 `tests/FateGuidance.Tests.ps1`。
- 修改 `tests/Stage.Tests.ps1` 中第二事件节点与 Boss 解析的旧断言。
- 不新增测试框架或测试工程，保持当前仓库的 PowerShell 聚焦测试方式。
- PowerShell 测试验证静态结构契约；单机、多人、图标和存档行为通过运行时验收矩阵验证。

## 5. 数据流

```text
Stage 初始化
  └─ BossDiscoveryOrder 默认选择 CrychicPhatomBoss
       ↓
玩家进入 FateGuidance
       ↓
每名玩家通过原生共享事件提交一个选项
       ↓
主机从已提交选择中等权随机抽取最终索引
       ↓
SharedEventOptionChosenMessage 同步最终索引
       ↓
每个联机端执行相同的选项处理器
       ↓
BossMapRouteService.ChangePrimaryBoss
  ├─ 目标已在第一或第二槽位 → AlreadySelected
  └─ 目标不存在
       ├─ SetBossEncounter(canonical.ToMutable())
       ├─ 刷新第一、第二 Boss 地图图标
       └─ PrimaryBossChanged
       ↓
每个玩家事件实例进入同一个具名 Boss 结果页
       ↓
StageRoomResolver 在 Boss 节点读取 Act.BossEncounter
       ↓
原版 ToSave/FromSave 通过 SerializableRoomSet.BossId 持久化
```

原生共享事件会在每个联机端为所有玩家事件实例执行最终处理器。同一端首次调用完成实际写入，后续调用因稳定 ID 已匹配而返回 `AlreadySelected`。方法在首次异步等待之前同步执行，因此不需要额外锁或静态协调状态。

## 6. 异常处理

### 6.1 快速失败

- `runState` 或 `targetBoss` 缺失：`ArgumentNullException`。
- 目标不是 Boss Encounter：`ArgumentException`。
- 当前 Act 或第一 Boss 缺失：包含 Act ID 的 `InvalidOperationException`。
- 到达 Stage Boss 节点却无法取得第一 Boss：包含 Act、地图节点和楼层上下文的 `InvalidOperationException`。

这些情况表示编程错误或 Run 状态损坏，不使用默认 Boss 掩盖问题。

### 6.2 正常幂等

目标已位于任一 Boss 槽位属于正常结果：

- 返回 `AlreadySelected`。
- 不写日志错误。
- 不调用任何 setter。
- 不刷新图标。
- 事件仍进入目标 Boss 的结算页。

### 6.3 UI 降级

Run 中的 `BossEncounter` 是权威状态；实际房间解析和存档均以它为准。地图图标只是派生显示。

如果因游戏版本变化或资源缺失导致图标即时刷新失败：

- 记录明确错误。
- 保留真实 Boss 修改。
- 不回滚。
- 不向共享事件抛出 UI 异常，避免部分联机端停在事件页。

## 7. 已确认的中英文文案

| 页面 | English | 中文 |
|---|---|---|
| 标题 | Fate's Guidance | 命运所指 |
| 初始描述 | Three paths unfold beneath the stage lights. Choose which fate will be waiting at the end. | 舞台灯光之下，三条道路在你们面前展开。选择将在终点等待你们的命运。 |
| Crychic 选项标题 | Choose the Phantom of Crychic. | 选择Crychic的幻影。 |
| Crychic 选项描述 | Vote for the Phantom of Crychic to await at the end of the Stage. | 投票让Crychic的幻影在舞台终点等待你们。 |
| Oblivionis 选项标题 | Choose Oblivionis. | 选择Oblivionis。 |
| Oblivionis 选项描述 | Vote for Oblivionis to await at the end of the Stage. | 投票让Oblivionis在舞台终点等待你们。 |
| Taki 选项标题 | Choose Taki Shiina. | 选择椎名立希。 |
| Taki 选项描述 | Vote for Taki Shiina to await at the end of the Stage. | 投票让椎名立希在舞台终点等待你们。 |
| Crychic 结算 | The lights dim. The Phantom of Crychic takes its place at the end of the chosen path. | 灯光渐暗。Crychic的幻影已在命运所指的终点就位。 |
| Oblivionis 结算 | The song fades into silence. Oblivionis awaits at the end of the chosen path. | 歌声归于寂静。Oblivionis已在命运所指的终点等待。 |
| Taki 结算 | A rapid rhythm answers your choice. Taki Shiina awaits at the end of the chosen path. | 急促的节奏回应了你们的选择。椎名立希已在命运所指的终点等待。 |

本地化键固定为：

```text
STS2_TOMORIN_MOD-FATE_GUIDANCE.title
STS2_TOMORIN_MOD-FATE_GUIDANCE.pages.INITIAL.description
STS2_TOMORIN_MOD-FATE_GUIDANCE.pages.INITIAL.options.ChooseCrychic.title
STS2_TOMORIN_MOD-FATE_GUIDANCE.pages.INITIAL.options.ChooseCrychic.description
STS2_TOMORIN_MOD-FATE_GUIDANCE.pages.INITIAL.options.ChooseOblivionis.title
STS2_TOMORIN_MOD-FATE_GUIDANCE.pages.INITIAL.options.ChooseOblivionis.description
STS2_TOMORIN_MOD-FATE_GUIDANCE.pages.INITIAL.options.ChooseTaki.title
STS2_TOMORIN_MOD-FATE_GUIDANCE.pages.INITIAL.options.ChooseTaki.description
STS2_TOMORIN_MOD-FATE_GUIDANCE.pages.CRYCHIC.description
STS2_TOMORIN_MOD-FATE_GUIDANCE.pages.OBLIVIONIS.description
STS2_TOMORIN_MOD-FATE_GUIDANCE.pages.TAKI.description
```

## 8. 测试方案

测试不得对配置值、概率、票数、选项数量或其他数值属性做固定值断言。测试通过具名模型、页面、状态转换和身份不变量验证需求。

### 8.1 PowerShell 聚焦测试

`tests/FateGuidance.Tests.ps1` 和更新后的 `tests/Stage.Tests.ps1` 验证：

- FateGuidance 继承 `CustomEventModel` 并使用原生共享事件。
- 三个具名选项分别映射到三个已确认 Boss。
- 事件不引用 `PlayerChoiceSynchronizer`、自定义协调器或专用 Modifier。
- 事件只调用 `BossMapRouteService`，不直接调用 Boss setter。
- 通用服务不得调用 `SetSecondBossEncounter`。
- 去重使用 `ModelId`。
- `StageRouteNodeKind.FateGuidance` 与固定房间映射存在；旧 `SecondEvent` 映射被移除。
- 第一事件仍为 FeedTheCat。
- Boss 房间读取 `runState.Act.BossEncounter`，不硬编码 Crychic。
- Stage 默认 Boss 仍为 Crychic，合法 Encounter 集合包含三个目标 Boss。
- 视觉同步器覆盖第一、第二 Boss 节点，并同时包含 PNG 与 Spine 绑定路径。
- 中英文键集合一致、内容非空、JSON 可解析。
- 事件引用的占位图片存在。

静态测试不冒充运行时行为证明；下列运行时验收为交付必需项。

### 8.2 通用服务运行时验收

- 目标已是第一 Boss：两个槽位均保持身份，结果为 `AlreadySelected`。
- 目标已是第二 Boss：两个槽位均保持身份，结果为 `AlreadySelected`。
- 目标不存在且有第二 Boss：只改变第一 Boss，第二 Boss 身份和顺序保持不变。
- 目标不存在且没有第二 Boss：改变第一 Boss，不创建第二 Boss。
- 非 Boss Encounter 被拒绝并报告参数错误。
- 当前章节缺失第一 Boss 时报告章节上下文。
- 地图 UI 未创建时状态修改成功；以后创建地图时图标正确。
- 地图 UI 已创建但隐藏时，修改后重新打开地图，图标与状态一致。
- PNG→PNG、PNG→Spine、Spine→PNG 时，新旧显示切换正确。
- 存在第二 Boss 时，第一 Boss 修改不改变第二 Boss 模型或图标。

### 8.3 单机事件验收

- 进入事件前默认第一 Boss 为 Crychic。
- 分别完成三个具名选项，结果页与最终选项一致。
- 选择已经存在的 Boss 时事件完成且 Boss 顺序不变。
- 选择未出现的 Boss 时，返回地图后第一 Boss 图标与模型一致。
- 到达 Boss 节点后，实际 Encounter 与第一 Boss 状态一致。
- 事件结束后不残留等待状态或锁定选项。

### 8.4 多人同步验收

- 所有玩家选择同一目标时，所有端得到相同 Boss、图标和结果页。
- 玩家选择不同时，最终结果必须属于某位玩家实际提交的选项；不预先断言抽中哪个 Boss。
- 所有端的第一 Boss、第二 Boss、地图图标和结果页一致。
- 不出现额外选择轮次或客户端停留。
- 同一端的多个事件实例只产生一次实际状态写入。
- 目标位于第二 Boss 时，所有端均保持原 Boss 顺序。

### 8.5 存档恢复验收

- 事件结算后保存并重新载入，第一 Boss 保持最终结果。
- 第二 Boss 存在时，读档后身份和顺序保持不变。
- 读档后的地图图标与 `BossEncounter` 一致。
- 进入 Boss 节点时，实际遭遇与读档状态一致。
- 存档中不存在 FateGuidance 专用 Modifier 或重复权威字段。

### 8.6 验证命令

```powershell
powershell -ExecutionPolicy Bypass -File tests/FateGuidance.Tests.ps1
powershell -ExecutionPolicy Bypass -File tests/Stage.Tests.ps1
dotnet build
dotnet publish
```

本次会修改 Godot 包内的中英文本地化，因此即使复用现有图片，也必须执行 `dotnet publish` 导出 `.pck`。

## 9. 实现顺序

1. 先编写失败的 FateGuidance 与 Stage 聚焦测试。
2. 实现 `BossMapRouteService` 与 `BossMapVisualSynchronizer`。
3. 实现 `FateGuidance` 事件模型。
4. 修改 Stage 路由语义、房间解析器、默认 Boss 与合法 Encounter 集合。
5. 写入中英文本地化。
6. 运行聚焦测试、`dotnet build` 与 `dotnet publish`。
7. 完成单机、多人、图标切换和存档恢复验收。
8. 验证全部通过后更新实现记录和 [TODO.md](./TODO.md) 的完成状态。

## 10. 范围外事项

- 三个占位 Boss 的行为和数值重做。
- 第二 Boss 写入接口。
- 新的多人裁决规则。
- 新事件图片、场景、动画或音效。
- Stage 地图拓扑与其他固定房间调整。
- Basic Memory 读写。

## 11. 验收标准

- FateGuidance 只出现在 Stage 固定第二事件节点。
- 原生共享事件完成投票、裁决与同步，不存在自定义协调器。
- 三个具名结果均能使所有联机端收敛到同一状态。
- 目标已存在于任一 Boss 槽位时完全不改 Boss 顺序。
- 目标不存在时只改变第一 Boss。
- 返回地图时第一 Boss 图标与权威状态一致，第二 Boss 不变。
- Boss 房间进入权威第一 Boss Encounter。
- 存档恢复后模型、图标与实际遭遇一致。
- 中英文标题、初始页、选项和独立结果页完整。
- 聚焦测试、编译和发布均通过；运行时验收结果被执行 Agent记录。

## 12. Spec 自审结论

本节按照 `superpowers:brainstorming` 的 Spec Self-Review 清单完成。

### 12.1 占位符检查

- Spec 不包含未定义占位符、缺失页面或未指定错误处理。
- [TODO.md](./TODO.md) 中的复选项是已经定义完成条件的实施与后续任务，不是方案占位符。
- 占位 Boss、占位文案和占位立绘均明确标注当前来源、使用边界和后续替换任务。

### 12.2 内部一致性检查

- 事件层只调用通用服务；只有通用服务可以写第一 Boss；视觉同步器不得写 Run 状态。
- 原生共享事件在所有端执行同一具名处理器，`ModelId` 去重使重复执行保持幂等。
- Stage 默认 Boss、事件可选 Boss、合法 Encounter 集合、Boss 房间解析和存档恢复使用同一组模型身份。
- “目标已在第二 Boss 时完全不处理”与“只修改第一 Boss、第二 Boss 保持不变”没有冲突。
- 地图图标刷新失败时的降级策略不改变 `BossEncounter`、实际房间解析或存档权威关系。

### 12.3 范围检查

- 本 Spec 只包含一个可独立交付的功能组：FateGuidance 事件及其必需的通用第一 Boss 修改/地图显示基础服务。
- 第二 Boss 写入、Boss 重做和专属美术均已排除在本次实现范围外，无需拆分当前 Spec。
- 该范围可以由一个后续实现计划覆盖，并能通过聚焦测试、编译、发布和运行时矩阵独立验收。

### 12.4 歧义检查

- 事件位置、选项顺序、多人裁决、默认 Boss、重复 Boss 规则、结果页面、文案、占位图片和 UI 失败策略均已由使用者确认。
- 通用接口只支持第一 Boss 写入；第二 Boss 只参与去重和视觉刷新。
- 所有类名、方法签名、页面常量、本地化键、文件路径和验证命令均已固定。
- 当前没有遗留的产品或架构决策需要实现 Agent自行选择。
