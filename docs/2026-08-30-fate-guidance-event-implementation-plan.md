# FateGuidance 多人共享事件实现计划

**计划日期：** 2026-08-30  
**设计依据：** `docs/2026-08-20-fate-guidance-event-design.md`  
**确认状态：** 已完成实现、聚焦验证与发布；等待游戏内集成验收

## 1. 已确认的实现基线

- 保留当前 Stage 路线中的 `StageSupplyEvent` 与 `BandMemberEncounter`。
- 仅以 `FateGuidance` 替换当前第二事件 `FeedTheCat`。
- 路线固定为：`Ancient → StageSupplyEvent → BandMemberEncounter → Shop → FateGuidance → RestSite → Boss`。
- 共享事件完全使用游戏本体 `EventSynchronizer` 的原生投票与裁决流程。
- 当前版本只有第一选项可选择；第二、第三选项使用 BaseLib 对游戏原生 `EventOption.IsLocked` 的 `LockedOption` 门面锁定。
- 第一选项对应 `CrychicPhatomBoss`；锁定选项仍保留 `OblivionisBoss`、`TakiBoss` 的处理器和独立结果页，供未来解锁。
- Boss 权威状态只保存在当前 `ActModel` 的 Boss 房间集合中，不新增 Modifier 或重复存档字段。

## 2. 测试优先

1. 新建 `tests/FateGuidance.Tests.ps1`，验证事件、原生共享流程、原生锁定选项、Boss 映射、服务边界、反射契约、本地化和占位资源。
2. 更新 `tests/Stage.Tests.ps1`，验证新路线语义、动态 Boss 房间解析、默认 Boss 与完整 Encounter 集合。
3. 测试只验证具名模型、页面、状态转换和身份不变量，不固定断言配置值、概率、票数、平衡数值或选项总数。
4. 先运行测试确认旧实现不满足新契约，再编写生产代码使测试通过。

## 3. 生产代码

### 3.1 Boss 路线服务

新增 `Scripts/Services/BossMapRouteService.cs`：

- 定义 `PrimaryBossChangeResult`。
- 校验 Run、目标 Encounter、当前 Act、第一 Boss 与 Boss 房间类型。
- 使用 `ModelId` 比较第一、第二 Boss，目标已存在时保持完全幂等。
- 目标不存在时只调用 `SetBossEncounter` 修改第一 Boss。
- 状态修改成功后调用视觉同步器；UI 失败不得回滚权威状态。

### 3.2 地图视觉同步器

新增 `Scripts/Services/BossMapVisualSynchronizer.cs`：

- 通过 `NMapScreen.Instance` 获取当前地图 UI。
- 集中解析并缓存版本敏感的私有字段反射信息。
- 验证地图屏幕的 Run、Map 和 Boss 节点都属于传入状态。
- 每次从当前 Act 读取第一、第二 Boss，不缓存 Boss 身份。
- 支持 PNG 与 Spine 的双向切换，显式清理旧分支可见状态。
- 重新绑定后调用原生 `RefreshVisualsInstantly()`。
- 反射、节点或资源错误只记录一次明确错误并安全降级。

### 3.3 FateGuidance 事件

新增 `Scripts/Events/FateGuidance.cs`：

- 继承 `CustomEventModel`，声明 `IsShared => true`。
- 只允许当前 Act 为自定义 `Stage`。
- 复用 `Giraffe.png`。
- 初始页按 Crychic、Oblivionis、Taki 顺序生成具名选项。
- Crychic 为普通 `EventOption`；Oblivionis 与 Taki 使用相同本地化键创建原生锁定选项。
- 三个处理器均通过 `BossMapRouteService` 修改目标，并进入对应结果页。

### 3.4 Stage 接入

- 将 `StageRouteNodeKind.SecondEvent` 改名为 `FateGuidance`。
- 第一事件继续解析为 `StageSupplyEvent`，精英继续解析为 `BandMemberEncounter`。
- FateGuidance 节点直接创建新事件。
- Boss 节点读取 `runState.Act.BossEncounter`，缺失时携带 Act、节点与楼层上下文快速失败。
- `BossDiscoveryOrder` 只包含 Crychic。
- `GenerateAllEncounters()` 保留 BandMember，并包含三个 Fate Boss。

## 4. 本地化与文档

- 在中英文 `events.json` 中加入相同的 FateGuidance 键集合和非空文本。
- 更新原设计文档中的路线、原生裁决语义、锁定选项和交付状态。
- 更新 `CLAUDE.md`、根目录 `日志.txt` 与 `文档.txt`。
- 从本地 `docs/TODO.md` 移除本次实现事项；未来 Boss 重做、专属立绘和第二 Boss 写入仍作为独立后续事项保留。
- 不读取或修改 Basic Memory。

## 5. 验证顺序

1. `tests/FateGuidance.Tests.ps1`
2. `tests/Stage.Tests.ps1`
3. 仓库全部 `tests/*.Tests.ps1`
4. 所有测试 Harness
5. `dotnet build`
6. `dotnet publish`
7. 检查发布产物与 Git 工作区差异，记录无法由自动化替代的单机、多人、PNG/Spine 和存档实机验收项。

## 6. 执行结果

- FateGuidance、两个原生锁定选项、Boss 路线服务、地图视觉同步器、Stage 接入和双语本地化均已实现。
- FateGuidance、Stage、StageSupplyEvent 聚焦脚本以及相关 Stage Harness 均通过。
- `dotnet build --no-restore` 与 `dotnet publish --no-restore` 均为 0 错误；游戏 mods 目录中的 DLL 与 `.pck` 已更新。
- 当前任务已从 `docs/TODO.md` 的实施清单移除；实机验收与未来独立设计仍保留。
- 仓库全量测试仍受当前未提交 Card Intent 工作树断言、本机缺失 `pwsh`、无 Godot 运行时下测试宿主原生崩溃三个无关问题阻塞；详情见根目录 `日志.txt` 与 `文档.txt`。
