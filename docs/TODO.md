# TODO List

本文件记录当前项目内已经完成设计、尚待其他 Agent 实现或后续另行设计的任务。当前对话不使用 Basic Memory。

## Tomorin 测试敌人卡牌逻辑后续事项

设计依据：[2026-08-29-card-intent-tomorin-test-enemy-logic-design.md](./2026-08-29-card-intent-tomorin-test-enemy-logic-design.md)

基础框架依据：[2026-08-28-card-list-intent-design.md](./2026-08-28-card-list-intent-design.md)

UI 修订规格：[2026-08-29-card-list-intent-ui-revision-design.md](./2026-08-29-card-list-intent-ui-revision-design.md)

可执行计划：[2026-08-29-card-list-intent-ui-revision-implementation-plan.md](./2026-08-29-card-list-intent-ui-revision-implementation-plan.md)

测试敌人卡牌领域逻辑、冻结 DFS 投影和逐牌 Intent／Hover 代码已经完成自动化验证；本节只保留尚未执行的实机验收与后续独立设计事项。

### 实机验收

- [ ] 通过稳定 Encounter ID 启动显式测试房，验证单机完整回合、作词链、高重放、素材不足和死亡中止表现。
- [ ] 验证多张卡以角色头顶为中心向两侧扩展，每张卡拥有自己的效果图标，防御无格挡数字。
- [ ] 快速跨牌 Hover、移入放大预览和移出两者，确认共享预览无残留、无布局抖动且不吞战斗点击。
- [ ] 验证非空描述覆写、空覆写和池化换绑不会污染共享 `CardModel` 或泄漏旧文本。
- [ ] 改变怪物与本地玩家相关 Power，确认原版攻击标签实时刷新。
- [ ] 在多人环境验证每个客户端看到自己的受伤数值，卡牌结构和主机权威行动保持一致。
- [ ] 在安全原子步骤边界验证断线重连，确认递归计划、牌区、收藏品、ReplayCount 和游标与主机一致。
- [ ] 注入投影、描述节点和根 Holder 故障，验证分级日志与 Unknown 降级符合规格。

### 后续另行设计

- [ ] 将暂时写死的 `DescriptionOverride` 迁移到 `eng/zhs` 本地化与 DynamicVar 体系。
- [ ] UI 将素材不足、不能打出的牌显示为灰色；逻辑层只提供 `CardMarkedUnplayable` 和投影状态。
- [ ] 需要显示残页、咖啡和 LeftoverBuffet 的实际执行过程时，另行设计逐步骤动画层。
- [ ] 需要显式展示重放截断次数或状态时，设计专用标记，不复用 Unknown。
- [ ] 正式引入非标准伤害或新效果节点时增加显式 Intent 展示映射。
- [ ] 继续调整作词素材策略，使作词在更多情况下优先消耗收藏品，同时保持牌堆数量。
- [ ] 设计额外 Buff 或其他机制补充收藏品，降低无法作词的概率。
- [ ] 在完成玩法验证后重新平衡攻击锁、评分锁、卡牌数值、收藏品供给和步骤上限。
- [ ] 另行设计正式 Boss、正式牌组、阶段、强度和正式 Encounter，不直接把测试敌人加入正常内容。
- [ ] 需要敌人行动逐张消失、素材动画或回放表现时，设计独立执行动画层。
- [ ] 游戏版本更新后复核战斗 RNG、伤害/格挡修正、MoveState、Power Hook、NIntent/NCard 和联机同步接入点。

## FateGuidance 多人共享事件

设计依据：[2026-08-20-fate-guidance-event-design.md](./2026-08-20-fate-guidance-event-design.md)

### 待实现

- [ ] 新增 `Scripts/Services/BossMapRouteService.cs`，实现通用第一 Boss 去重、替换和结果返回。
- [ ] 新增 `Scripts/Services/BossMapVisualSynchronizer.cs`，实现第一、第二 Boss 地图节点的 PNG/Spine 通用重新绑定。
- [ ] 新增 `Scripts/Events/FateGuidance.cs`，使用原生共享事件规则和三个独立结算页。
- [ ] 将 Stage 固定第二事件节点语义改为 `StageRouteNodeKind.FateGuidance`。
- [ ] 让 `StageRoomResolver` 在 FateGuidance 节点创建新事件，并在 Boss 节点读取当前第一 Boss。
- [ ] 让 Stage 保持 Crychic 为默认 Boss，同时把 Crychic、Oblivionis、Taki 纳入合法 Boss Encounter 集合。
- [ ] 在中英文 `events.json` 写入已确认的 FateGuidance 本地化。
- [ ] 新增 `tests/FateGuidance.Tests.ps1` 并更新 `tests/Stage.Tests.ps1`。
- [ ] 执行 FateGuidance 与 Stage 聚焦测试。
- [ ] 执行 `dotnet build` 与 `dotnet publish`。
- [ ] 完成单机、多人分歧选择、双 Boss 去重、PNG/Spine 图标切换和存档恢复验收。
- [ ] 验证完成后记录结果，并勾选本节已完成任务。

### 后续另行设计或替换

- [ ] 后续重新设计 `CrychicPhatomBoss`、`OblivionisBoss` 与 `TakiBoss` 占位实现。
- [ ] Boss 重做后复核 FateGuidance 的选项与三个结算页占位文案。
- [ ] 为 FateGuidance 制作并替换专属事件立绘；当前复用 `Giraffe.png`。
- [ ] 出现明确的第二 Boss 修改需求时，单独设计第二 Boss 的创建、替换、去重、地图节点和存档规则。
- [ ] 游戏版本更新后复核 `NMapScreen`、`NBossMapPoint` 私有字段以及 PNG/Spine 重新绑定流程。
