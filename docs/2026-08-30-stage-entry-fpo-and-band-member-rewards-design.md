# 第四层双遗物门槛、FPO 原版结算与乐队双 Boss 遗物奖励设计

## 1. 文档状态

- 状态：设计已由使用者逐段确认，并已在当前工作区完成实现、自动化验证与独立代码审查。
- 范围：只设计第四层进入资格、FullPowerOblivionis 击败进度与战后结算边界、`BandMemberEncounter` 战胜后的专属遗物奖励。
- 非目标：不修改遗物本身的效果，不调整 Boss/Elite 战斗行为，不修改第四层路线，不新增 Godot 资源，不处理旧版进行中存档兼容。
- 实施说明：使用者随后明确要求在当前环境立即实现，因此本文件同时记录最终落地结构与剩余实机验收项。

## 2. 已确认需求

### 2.1 第四层进入条件

进入第四层必须同时满足现有前置条件和以下核心条件：

1. 当前局真实击败过 `FullPowerOblivionis`；该条件保持不变。
2. 同一名玩家独自持有四件候选遗物中的至少两件：
   - `AnonGuitar`
   - `TakiDrum`
   - `SoyoBase`
   - `RaanaGuitar`
3. 不允许把不同玩家持有的遗物合并计数。
4. 多人局中，只要任意一名玩家独自满足遗物条件即可；遗物持有者不额外要求必须是 Tomorin，但队伍中存在 Tomorin 的既有条件继续保留。
5. 持有超过最低要求仍然满足条件；重复的模型 ID 不得重复计数。

### 2.2 FPO 战后结算

1. 击败 FPO 后不执行 Mod 自定义奖励逻辑。
2. 不新增“禁止奖励”或“空奖励”补丁。
3. 是否显示奖励、显示何种奖励、如何继续，完全交给游戏原版流程。
4. Mod 只记录 FPO 已被真实击败的本局进度。
5. 已确认不兼容带有旧 FPO 奖励状态字段的进行中存档；相关字段、方法、枚举和补丁全部删除。

### 2.3 BandMemberEncounter 专属奖励

1. `BandMemberEncounter` 继续保留原版标准精英奖励。
2. 整场 Encounter 获胜后，根据本场两名乐队成员各自是否达到其本体的专属遗物奖励触发条件，最多额外提供两件对应遗物：
   - Anon 对应 `AnonGuitar`
   - Taki 对应 `TakiDrum`
   - Soyo 对应 `SoyoBase`
   - Raana 对应 `RaanaGuitar`
3. 出场 Boss 身份只决定遗物类型；该 Boss 本体的生命周期结果决定是否取得奖励资格。不得以“曾经出场”或“Encounter 最终获胜”替代本体条件。
4. Anon 只有在二阶段再次真实死亡时取得资格；按状态机逃跑不取得 `AnonGuitar` 资格。
5. Taki 只有自身真实死亡并进入现有 `Creature.Died` 奖励回调时取得资格；第三阶段把玩家锁血后自行逃跑不取得 `TakiDrum` 资格。
6. Soyo 与 Raana 继续在自身真实死亡并进入现有本体奖励回调时取得对应资格。
7. 对所有已取得资格的成员，所有玩家面对相同的候选遗物集合；每名玩家按自己的持有状态过滤。
8. 玩家已持有某件同 ID 遗物时，不再为该玩家提供该件；其他已取得资格的遗物仍可正常提供。
9. 若没有成员取得资格，或玩家已持有全部已取得资格的遗物，则该玩家不新增专属遗物奖励，但标准精英奖励不受影响。

## 3. 现状与问题

### 3.1 StageEligibility

`Scripts/Stage/StageEligibility.cs` 当前持有四件候选遗物 ID，并以 `IsSubsetOf` 表达“同一玩家持有全部四件”。当前工作区还临时把核心资格判断注释掉并直接返回 `true`；实施时必须恢复完整资格链，而不能只修改辅助方法。

### 3.2 FPO 奖励状态机

当前实现包含以下额外奖励适配：

- `StageFpoProgressPatch` 在 FPO 死亡时既记录击败进度，又把当前 Boss 战标记为奖励合格。
- `StageRunProgressModifier` 保存 Encounter ID、章节索引、地图坐标和奖励状态。
- `StageBossRewardLifecyclePatch` Patch `CombatRoom.OfferRoomEndRewards` 与 `RewardsSet.WithRewardsFromRoom`，对原版奖励生成做额外放行、空奖励和重入控制。

新需求明确要求回归原版逻辑，因此这一状态机不应被改造成“禁止奖励”状态机，而应彻底移除。

### 3.3 BandMemberEncounter

`BandMemberEncounter` 已确定性选择两名不同成员，并通过 `SaveCustomState`/`LoadCustomState` 保存左右成员稳定名称。四个 Elite 当前均将 `ShouldGrantBossReward` 设为 `false`，防止复用原始 Boss 的 Boss 房即时奖励逻辑；该行为应继续保留，但不能再被误解为 Elite 永远不产生 Encounter 级奖励资格。

四个本体的现有奖励触发点并不等价于“出场”：Anon 只在二阶段再次死亡时触发，Taki 只在自身真实死亡回调触发，Soyo 与 Raana 也只在自身死亡回调触发。Anon 逃跑，以及 Taki 第三阶段把玩家锁血后逃跑，都不会进入本体奖励触发点。

仓库已有 `AfterCombatEnd(CombatRoom)` 添加额外奖励的框架用法，因此奖励应放在 Encounter 的胜利结算钩子，而不是四个 Elite 的单体死亡回调或全局 Harmony Patch。

## 4. 方案比较与决策

### 4.1 已选方案：Encounter AfterCombatEnd + 既有 Hook 适配层

在 `BandMemberEncounter.AfterCombatEnd(CombatRoom)` 中调用集中式奖励辅助类。经实现期程序集调用链复核，原生 `Hook.AfterCombatEnd` 的监听器枚举不包含 Encounter；因此由仓库既有的 `HookAfterCombatEndPatch` 在等待原生 Hook 完成后，显式且仅针对当前 `BandMemberEncounter` 派发该方法。

优点：

- 与“战胜整个 Encounter 后发奖”的业务语义一致。
- Encounter 只在胜利结算时统一展示奖励，但资格由 Anon、Taki、Soyo、Raana 各自现有本体触发点记录，因此不会绕过复活、逃跑或锁血阶段语义。
- 影响范围局限在目标 Encounter。
- 不新增 Harmony Patch；只在仓库既有战斗结束适配层增加目标 Encounter 派发。
- 可以复用房间额外奖励与原版领取流程。

### 4.2 未选方案：全局奖励 Patch

可在 `Hook.AfterCombatEnd`、`CombatRoom.OnCombatEnded` 或 `OfferRoomEndRewards` 识别 Encounter 后发奖，但会扩大补丁影响面，并与仓库现有 `HookAfterCombatEndPatch` 或其他 Mod 产生调用顺序风险，故不采用。

### 4.3 未选方案：Elite 单体死亡发奖

可让四个 Elite 各自在死亡回调直接添加 `RelicReward`，但奖励会早于整场胜利且难以统一处理玩家持有过滤。最终方案只在本体触发点记录资格，实际 `RelicReward` 仍由 Encounter 胜利结算统一创建。

## 5. 代码级设计

### 5.1 StageEligibility：从全集覆盖改为最低交集门槛

修改文件：`Scripts/Stage/StageEligibility.cs`

保留：

```csharp
public static IReadOnlySet<ModelId> RequiredStageRelics { get; }
```

新增具名门槛常量，业务值为两件：

```csharp
public const int MinimumRequiredStageRelicCount = 2;
```

将旧方法：

```csharp
public static bool HasAllRequiredRelics(Player player)
public static bool CoversRequiredRelics<T>(IEnumerable<T> held, IReadOnlySet<T> required)
```

替换为语义准确的方法：

```csharp
public static bool HasMinimumRequiredRelics(Player player)

public static bool CoversMinimumRequiredRelics<T>(
    IEnumerable<T> held,
    IReadOnlySet<T> required,
    int minimumRequiredCount)
    where T : notnull
```

`CoversMinimumRequiredRelics` 的规则：

1. `held` 先转为 `HashSet<T>`，消除重复 ID。
2. 统计该集合与 `required` 的交集大小。
3. 交集达到 `minimumRequiredCount` 时返回 `true`。
4. `minimumRequiredCount` 非正数或大于 `required.Count` 属于开发配置错误，抛出 `ArgumentOutOfRangeException`，不静默产生永真或永假资格。

`HasMinimumRequiredRelics` 只负责从 `Player.Relics` 提取稳定 ID，并使用上述纯集合函数。

`IsEligible(IRunState)` 恢复并保持完整短路顺序：

1. Daily 或当前 Act 不是 Glory：拒绝。
2. 队伍中没有 Tomorin：拒绝。
3. Stage 候选不唯一或不紧邻当前 Glory：拒绝。
4. `StageRunProgressModifier` 不存在或尚未记录 FPO 真实击败：拒绝。
5. `runState.Players.Any(HasMinimumRequiredRelics)`：任意单一玩家满足时通过。

不得把玩家遗物先在队伍级别合并后再计算。

### 5.2 StageRunProgressModifier：只保存击败进度

修改文件：`Scripts/Stage/StageRunProgressModifier.cs`

最终仅保留：

- `[SavedProperty] bool HasDefeatedFullPowerOblivionis`
- `bool MarkFullPowerOblivionisDefeated()`
- `static StageRunProgressModifier? Find(IRunState runState)`

删除：

- `EligibleBossEncounterId`
- `EligibleBossActIndex`
- `EligibleBossMapCoord`
- `BossRewardState`
- `MarkBossRewardEligible`
- `MarkBossRewardsGenerated`
- `ClearStaleBossRewardEligibility`
- `MatchesBossRewardBattle`
- `ToSavedMapCoord`
- `StageBossRewardState` 枚举
- 已无用途的 `MegaCrit.Sts2.Core.Map` 与其他 using

类注释同步修改为“只保存第四层解锁所需的 FPO 击败进度”，不得继续描述奖励资格。

### 5.3 StageFpoProgressPatch：只记录真实死亡

修改文件：`Scripts/Patch/StageFpoProgressPatch.cs`

保留 Harmony 目标：

```csharp
[HarmonyPatch(typeof(Hook), nameof(Hook.AfterDeath))]
```

Postfix 只执行以下判断与动作：

1. `wasRemovalPrevented` 为真：返回。
2. `creature.ModelId` 不是规范 `ModelDb.Monster<FullPowerOblivionis>().Id`：返回。
3. 当前 Run 没有 `StageRunProgressModifier`：返回。
4. 调用 `MarkFullPowerOblivionisDefeated()`；仅首次变化时写日志。

Postfix 明确精简为只接收实际使用的原方法参数：

```csharp
private static void Postfix(
    IRunState runState,
    Creature creature,
    bool wasRemovalPrevented)
```

Harmony Postfix 允许只声明需要注入的原方法参数，因此删除 `ICombatState combatState` 及其 using。同步删除对 `RoomType`、Encounter、ActIndex、MapCoord 和奖励资格方法的全部引用。

### 5.4 删除 FPO 奖励补丁

删除文件：`Scripts/Patch/StageBossRewardLifecyclePatch.cs`

不得以其他文件或新 Patch 替代它。删除后应确保项目中不存在下列 Stage 专用行为：

- Patch `CombatRoom.OfferRoomEndRewards`
- Patch `RewardsSet.WithRewardsFromRoom`
- 为 FPO 手工创建奖励
- 为 FPO 强制创建空奖励集
- 为 FPO 维护奖励生成状态

### 5.5 BandMemberRelicRewardLifecycle：复用本体触发点记录资格

新增文件：`Scripts/Enemy/BandMemberRelicRewardLifecycle.cs`

类型与入口：

```csharp
internal static class BandMemberRelicRewardLifecycle
{
    internal static void RecordEarnedAndGrantBossReward<TRelic>(
        Creature creature,
        BandMemberKind member,
        bool shouldGrantBossReward)
        where TRelic : RelicModel
    {
        ArgumentNullException.ThrowIfNull(creature);
        if (creature.CombatState.RunState.CurrentRoom is not CombatRoom room)
        {
            throw new InvalidOperationException(
                $"成员 {member} 取得遗物资格时不在 CombatRoom 中。");
        }

        if (room.CombatState.Encounter is BandMemberEncounter encounter)
        {
            encounter.MarkRelicRewardEarned(member, creature.SlotName);
        }

        if (shouldGrantBossReward)
        {
            BandBossRelicReward.Add<TRelic>(room);
        }
    }
}
```

该方法是本体奖励触发点的单一出口：

1. 校验 `creature`，并取得其当前 `CombatRoom`；缺少战斗房间时抛出带成员上下文的 `InvalidOperationException`。
2. 若当前 Encounter 是 `BandMemberEncounter`，调用其 `MarkRelicRewardEarned(member, creature.SlotName)`，只记录 Encounter 级资格，不立即创建奖励。
3. 若 `shouldGrantBossReward` 为真，继续调用现有 `BandBossRelicReward.Add<TRelic>(room)`，保持原始 Boss 房奖励。
4. 若 `shouldGrantBossReward` 为假，不调用 Boss 房奖励；因此 Elite 只记录资格，实际奖励等待 Encounter 胜利结算。

修改四个本体文件，使“记录 Elite 资格”和“原始 Boss 发奖”共用完全相同的触发分支：

- `Scripts/Enemy/Anon.cs`：在 `_isSecondPhase` 为真的再次死亡分支调用 `RecordEarnedAndGrantBossReward<AnonGuitar>`；删除只服务旧直接发奖的 `_isAddReward` 与 `AddReward`。第一次死亡进入复活流程和之后的状态机逃跑均不调用。
- `Scripts/Enemy/Taki.cs`：在 `PhaseThreeClearCallBack` 的自身 `Creature.Died` 回调中调用 `RecordEarnedAndGrantBossReward<TakiDrum>`。第三阶段玩家被 `TakiLockHpPower` 锁血后触发 `RunCallBack` 的逃跑路径不调用。
- `Scripts/Enemy/Soyo.cs`：在现有 `creature == Creature` 的真实死亡分支调用 `RecordEarnedAndGrantBossReward<SoyoBase>`。
- `Scripts/Enemy/Raana.cs`：在现有 `creature == Creature` 的真实死亡分支调用 `RecordEarnedAndGrantBossReward<RaanaGuitar>`。

四个位置不得分别复制 Encounter 类型判断或直接创建 Elite 遗物奖励，避免本体条件与 Elite 条件以后发生漂移。

### 5.6 BandMemberEncounter：保存资格并在胜利后结算

修改文件：`Scripts/Encounters/BandMemberEncounter.cs`

Encounter 不覆写 `ShouldReceiveCombatHooks`，因为该属性本身不会把 Encounter 加入原生监听器集合。夹击初始化继续由四个 Elite 的 `AfterAddedToRoom` 和 `BandSurroundedCoordinator.InitializeFor` 承担，不恢复旧的 Encounter `BeforeCombatStart` 夹击逻辑。

新增 Encounter 状态：

```csharp
private const string LeftRewardEarnedStateKey = "leftRewardEarned";
private const string RightRewardEarnedStateKey = "rightRewardEarned";

private bool _leftRewardEarned;
private bool _rightRewardEarned;
```

`SaveCustomState` 在保存左右成员稳定名称的同时保存两侧资格布尔值。`LoadCustomState` 的规则：

- 合法布尔文本恢复对应资格。
- 新字段缺失时安全默认为 `false`，避免凭空发奖。
- 字段存在但无法解析时抛出 `InvalidOperationException`，不得把损坏状态解释为已取得资格。
- 资格为真但对应左右成员状态非法时同样抛出，不重新选择成员。

新增资格记录入口：

```csharp
internal void MarkRelicRewardEarned(
    BandMemberKind member,
    string? slotName)
```

规则：

1. 先验证左右成员选择已经存在且互不相同。
2. `slotName == LeftMember` 时，`member` 必须等于 `_leftMember`；匹配后把 `_leftRewardEarned` 设为真。
3. `slotName == RightMember` 时，`member` 必须等于 `_rightMember`；匹配后把 `_rightRewardEarned` 设为真。
4. 未知槽位或成员—槽位不匹配时抛出 `InvalidOperationException`，避免给错误成员取得资格。
5. 重复记录同一侧资格幂等。

新增私有选择读取方法：

```csharp
private BandMemberSelection GetSelectedMembersForReward()
```

规则：

- `_leftMember`、`_rightMember` 都有值且不同，返回新的 `BandMemberSelection`。
- 任一缺失或重复时，抛出 `InvalidOperationException`，消息包含 Encounter 名称与左右状态。
- 不调用 `EnsureMemberSelection()`，避免奖励阶段按变化后的历史重新选择。

新增钩子：

```csharp
public override Task AfterCombatEnd(CombatRoom room)
```

处理顺序：

1. 若 `room.CombatState.HittableEnemies` 仍存在成员，说明不是已取得胜利的结算状态，直接返回 `Task.CompletedTask`，不登记专属奖励。
2. 取得 `GetSelectedMembersForReward()`。
3. 调用 `BandMemberEncounterRewardPolicy.AddEarnedRewards(room, selection, _leftRewardEarned, _rightRewardEarned)`。
4. 返回 `Task.CompletedTask`。

单个 Elite 的本体触发点只记录资格，不调用实际 `RelicReward` 添加辅助类。

### 5.7 HookAfterCombatEndPatch：显式派发 Encounter 结算

修改文件：`Scripts/Patch/HookAfterCombatEndPatch.cs`

既有 `AsyncWrapper` 等待原生 `Hook.AfterCombatEnd` 完成后调用：

```csharp
await DispatchBandMemberEncounterAfterCombatEnd(combatState, room);
```

适配器只在 `combatState?.Encounter is BandMemberEncounter` 时调用 `encounter.AfterCombatEnd(room)`，其他 Encounter 原样返回完成任务。该适配器由 Harness 实际调用验证，不能只依赖源码存在性断言。

### 5.8 BandMemberEncounterRewardPolicy：映射、过滤与幂等

新增文件：`Scripts/Encounters/BandMemberEncounterRewardPolicy.cs`

类型：

```csharp
internal static class BandMemberEncounterRewardPolicy
```

入口：

```csharp
internal static void AddEarnedRewards(
    CombatRoom room,
    BandMemberSelection selection,
    bool leftEarned,
    bool rightEarned)
```

内部职责拆分：

```csharp
private static RelicModel ResolveCanonicalRelic(BandMemberKind member)

private static bool HasOwnedOrPendingReward(
    CombatRoom room,
    Player player,
    ModelId relicId)
```

`ResolveCanonicalRelic` 使用完整 switch：

```csharp
BandMemberKind.Anon  => ModelDb.Relic<AnonGuitar>()
BandMemberKind.Taki  => ModelDb.Relic<TakiDrum>()
BandMemberKind.Soyo  => ModelDb.Relic<SoyoBase>()
BandMemberKind.Raana => ModelDb.Relic<RaanaGuitar>()
```

未知枚举值抛出 `ArgumentOutOfRangeException`，不得回退到随机遗物或默认成员。

`AddEarnedRewards`：

1. 对 `room` 和 `selection` 做非空校验。
2. 验证 `room.CombatState.Encounter` 的运行时类型是 `BandMemberEncounter`；误用于其他房间时抛出 `InvalidOperationException`，不得污染其他 Encounter 的奖励。
3. 仅选择资格布尔值为真的一侧；两侧均未取得资格时直接返回，不创建专属奖励。
4. 按左、右稳定顺序解析已取得资格成员的规范遗物模型。`BandMemberSelection` 已保证成员不同；映射错误应在添加任何玩家奖励前暴露，避免部分发放。
5. 遍历 `room.CombatState.Players`，每名玩家使用相同的已取得资格遗物集合。
6. 若玩家 `Relics` 已含目标 `ModelId`，跳过。
7. 若 `room.ExtraRewards` 已为该玩家登记相同 `ModelId` 的 `RelicReward`，跳过。
8. 其他情况使用规范模型的 `ToMutable()` 创建奖励实例，并调用：

```csharp
room.AddExtraReward(player, new RelicReward(relic, player));
```

幂等判断必须同时覆盖“已经拥有”和“本房间已待领取”两种状态。只检查 `Player.Relics` 无法阻止奖励钩子在领取前重入时重复登记。

### 5.9 保持现有 Boss 与 Elite 边界

以下代码保持原语义：

- `Scripts/Enemy/BandBossRelicReward.cs` 继续只接受 `RoomType.Boss`。
- `Anon`、`Taki`、`Soyo`、`Raana` 原始 Boss 在各自原有触发点通过 `BandMemberRelicRewardLifecycle` 继续发放专属遗物。
- `AnonElite`、`TakiElite`、`SoyoElite`、`RaanaElite` 继续覆写 `ShouldGrantBossReward => false`。

Encounter 奖励辅助类只服务 `BandMemberEncounter`，不得把现有 `BandBossRelicReward` 放宽为可从任意 Elite 房直接调用。

## 6. 完整数据流

### 6.1 第四层资格

```text
Glory 奖励完成后的统一同步点
  -> StageEligibility.IsEligible(runState)
  -> 校验模式、Act、Tomorin、Stage 邻接唯一性
  -> StageRunProgressModifier.HasDefeatedFullPowerOblivionis
  -> 逐玩家计算“持有 ID 与四件候选 ID 的交集”
  -> 任意单一玩家达到最低门槛
  -> 允许进入 Stage
```

### 6.2 BandMemberEncounter 奖励

```text
GenerateMonsters 确定并保存左右成员
  -> 战斗进行
  -> 各本体仅在原有奖励触发点调用 BandMemberRelicRewardLifecycle
  -> 原始 Boss：立即走 Boss 房奖励；BandMember Elite：按槽位记录 Encounter 资格
  -> Anon/Taki 逃跑路径不记录资格
  -> 整场 Encounter 获胜
  -> 既有 HookAfterCombatEndPatch 在原生 Hook 完成后显式派发
  -> BandMemberEncounter.AfterCombatEnd
  -> 验证已保存成员状态和左右资格状态
  -> 只解析已取得资格成员的规范遗物
  -> 逐玩家过滤已拥有/已待领取遗物
  -> CombatRoom.AddExtraReward
  -> 原版奖励界面合并标准精英奖励和额外遗物奖励
```

## 7. 异常与边界策略

1. 候选遗物最低门槛配置越界：抛出 `ArgumentOutOfRangeException`。
2. 未知 `BandMemberKind`：抛出 `ArgumentOutOfRangeException`。
3. 奖励阶段左右成员缺失或重复：抛出 `InvalidOperationException`，不重新选择，不静默少发或错发。
4. FPO 死亡被阻止：不记录进度。
5. FPO 重复真实死亡回调：幂等，无额外副作用。
6. 玩家已持有或已存在待领取同 ID 奖励：跳过该项，其余奖励继续处理。
7. Anon 或 Taki 通过本体逃跑路径离场：对应资格保持为假，即使 Encounter 最终获胜也不发该成员遗物。
8. 旧进行中存档：不提供兼容保证；删除的 SavedProperty 不保留遗留占位字段。
9. `AfterCombatEnd` 调用时仍有可攻击敌人：视为非胜利结算，不登记专属奖励。
10. 奖励辅助类被误用于其他 Encounter：抛出 `InvalidOperationException`，不污染无关房间奖励。
11. 本体报告的成员身份与 Encounter 槽位选择不一致：抛出 `InvalidOperationException`，不得为错误成员记录资格。
12. 资格状态文本损坏：恢复失败并显式报错，不按真值解释，也不重新选择成员。

## 8. 测试设计

测试不得断言敌人、遗物、金币、卡牌或其他配置对象的固定属性数值。集合门槛按业务语义测试为“任意候选遗物对”“低于门槛”“达到门槛”和“门槛的超集”，不把配置属性值写成测试断言。

### 8.1 StageHarness

新增：

- `tests/StageHarness/StageHarness.csproj`
- `tests/StageHarness/Directory.Build.props`
- `tests/StageHarness/StageEligibilityTests.testcs`

用例：

1. 空集合、无关遗物集合和低于门槛的候选集合均被拒绝。
2. 枚举四件候选遗物形成的所有无序遗物对，每一对均满足资格。
3. 任意有效组合的超集继续满足资格。
4. 重复 ID 不增加有效持有数量。
5. 不同玩家各自低于门槛时不能合并通过。
6. 任一玩家独自持有有效组合时通过玩家级聚合判断。
7. 其他条件成立但 FPO 进度未记录时拒绝，真实记录后通过。
8. Daily、非 Glory、无 Tomorin、Stage 非唯一或不相邻继续拒绝。
9. `MarkFullPowerOblivionisDefeated` 首次改变状态，重复调用保持状态且不产生奖励状态。
10. FPO 死亡被阻止和非 FPO 死亡均不记录；FPO 真实死亡记录。

### 8.2 BandMemberEncounterHarness

新增：

- `tests/BandMemberEncounterHarness/BandMemberRewardStateTests.testcs`

用例：

1. 遍历全部 `BandMemberKind`，每个成员解析为唯一且正确类型的遗物。
2. 未知成员枚举抛出明确异常。
3. Anon 第一次死亡进入复活流程时不记录资格；二阶段再次真实死亡时记录资格；状态机逃跑后资格仍为假。
4. Taki 自身真实死亡回调记录资格；第三阶段玩家被锁血并触发 Taki 逃跑时资格仍为假。
5. Soyo 与 Raana 自身真实死亡回调分别记录对应资格。
6. 左右均取得资格时，未持有玩家的额外遗物 ID 集合等于两侧映射集合。
7. 只有一侧取得资格时，只出现该侧对应遗物；两侧均未取得资格时不新增专属遗物。
8. 已持有某个已取得资格的遗物时只保留其他缺少项；全部已持有时不新增专属遗物。
9. 多名玩家使用同一资格映射，并按各自持有状态独立过滤。
10. 奖励入口重复调用后，额外遗物 ID 集合不变。
11. 房间已登记同 ID 待领取奖励时不重复添加。
12. 保存并恢复左右资格后，奖励结果保持一致；资格字段缺失时按未取得处理，字段损坏时显式失败。
13. 恢复的成员状态非法、左右重复，或本体报告身份与槽位不匹配时，结算失败且不重新选择成员。
14. 仍存在可攻击敌人时不登记专属奖励。
15. 辅助类被误用于非 `BandMemberEncounter` 房间时显式拒绝。
16. 现有夹击生命周期测试继续通过，证明重新启用 Encounter hooks 未恢复重复夹击初始化。

### 8.3 PowerShell 结构回归

修改：

- `tests/Stage.Tests.ps1`
- `tests/BandMemberEncounter.Tests.ps1`
- `tests/BandBossRelicRewards.Tests.ps1`

Stage 结构断言：

- 不再要求 `StageBossRewardLifecyclePatch.cs` 存在。
- 不再要求 Patch `RewardsSet.WithRewardsFromRoom` 或 `OfferRoomEndRewards`。
- `StageFpoProgressPatch` 仍使用稳定 FPO ModelId 并拒绝被阻止的死亡。
- Stage 相关源码不再包含奖励资格状态或手工 FPO 奖励生成。
- `StageEligibility` 恢复完整条件链，不允许无条件返回 `true`。

BandMemberEncounter 结构断言：

- Encounter 启用 combat hooks 并覆写 `AfterCombatEnd`。
- `AfterCombatEnd` 只把已取得资格的左右状态交给集中辅助类。
- Encounter 仍使用 `base(RoomType.Elite, true)`。
- 四个 Elite 仍关闭原始 Boss 的死亡奖励。
- 四个本体的原有奖励触发点均调用同一个 `BandMemberRelicRewardLifecycle`；Anon/Taki 逃跑路径不得调用。
- Encounter 保存并恢复左右奖励资格，且按成员—槽位匹配记录。
- 新辅助类映射四名成员，使用稳定 ModelDb 遗物并检查已拥有与待领取奖励。
- 原始 Boss 的 `BandBossRelicReward` 及 Boss 房限制保持不变。

### 8.4 验证命令

```powershell
dotnet test tests/StageHarness/StageHarness.csproj --nologo --verbosity minimal
dotnet test tests/BandMemberEncounterHarness/BandMemberEncounterHarness.csproj --nologo --verbosity minimal
pwsh -File tests/Stage.Tests.ps1
pwsh -File tests/BandMemberEncounter.Tests.ps1
pwsh -File tests/BandBossRelicRewards.Tests.ps1
dotnet build
```

本次无 Godot 资源变更，因此不运行 `dotnet publish`。

## 9. 文件变更清单

### 新增

- `Scripts/Enemy/BandMemberRelicRewardLifecycle.cs`
- `Scripts/Encounters/BandMemberEncounterRewardPolicy.cs`
- `tests/StageHarness/StageHarness.csproj`
- `tests/StageHarness/Directory.Build.props`
- `tests/StageHarness/StageEligibilityTests.testcs`
- `tests/BandMemberEncounterHarness/BandMemberRewardStateTests.testcs`

### 修改

- `Scripts/Stage/StageEligibility.cs`
- `Scripts/Stage/StageRunProgressModifier.cs`
- `Scripts/Patch/StageFpoProgressPatch.cs`
- `Scripts/Encounters/BandMemberEncounter.cs`
- `Scripts/Patch/HookAfterCombatEndPatch.cs`
- `Scripts/Enemy/Anon.cs`
- `Scripts/Enemy/Taki.cs`
- `Scripts/Enemy/Soyo.cs`
- `Scripts/Enemy/Raana.cs`
- `tests/Stage.Tests.ps1`
- `tests/BandMemberEncounter.Tests.ps1`
- `tests/BandBossRelicRewards.Tests.ps1`

### 删除

- `Scripts/Patch/StageBossRewardLifecyclePatch.cs`

### 明确保留不改

- `Scripts/Enemy/BandBossRelicReward.cs` 的 Boss 房限制
- 四个原始乐队 Boss 的专属遗物奖励触发条件与最终行为
- 四个 BandMember Elite 的 `ShouldGrantBossReward => false`
- Stage 路线、场景与本需求无关的战斗逻辑

## 10. 实施顺序建议

1. 先为 Stage 资格纯集合函数和 FPO 精简行为补充失败测试。
2. 修改 `StageEligibility`，恢复完整资格链并改为同一玩家最低门槛。
3. 精简 `StageRunProgressModifier` 与 `StageFpoProgressPatch`，删除奖励生命周期补丁。
4. 为四个本体同源资格触发、资格持久化、成员—遗物映射、持有过滤和幂等性补充失败测试。
5. 新增本体奖励生命周期出口，在四个现有本体奖励触发点接入资格记录，同时保持原始 Boss 发奖。
6. 新增 Encounter 资格状态和奖励辅助类，接入 `AfterCombatEnd`。
7. 更新结构回归脚本，移除旧 FPO 奖励状态机断言并加入本体同源资格断言。
8. 运行全部目标 Harness、PowerShell 回归和 `dotnet build`。

## 11. TODO List（后续执行事项）

已完成：

- [x] 建立失败测试并完成红—绿回归。
- [x] 实现第四层同一玩家最低遗物门槛。
- [x] 彻底删除 FPO 奖励状态机。
- [x] 实现本体同源资格记录、Encounter 胜利结算、逐玩家去重和存档校验。
- [x] 完成独立代码审查，并修复 Encounter 未进入原生 Hook 监听列表的问题。

后续未完成事项：

- [ ] 在真实游戏中分别覆盖 Anon 逃跑、Taki 锁血逃跑、单侧击杀、双侧击杀与不同玩家持有状态的端到端实机验收。

当前没有未确认的设计决策，也没有留待实现者自行解释的需求分支。

## 12. 验收标准

1. 满足既有前置条件、真实击败 FPO，且队伍中至少一名玩家独自持有任意候选遗物对时，可进入第四层。
2. 不允许将不同玩家的遗物合并满足条件。
3. FPO 击败只记录进度；Mod 不再介入其战后奖励生成。
4. `BandMemberEncounter` 获胜后，每名玩家只得到本场已达到各自本体奖励条件、且自己尚未持有或待领取的成员专属遗物。
5. Anon 逃跑不产生 `AnonGuitar` 资格；Taki 第三阶段锁血玩家后逃跑不产生 `TakiDrum` 资格；两者自身按本体条件真实死亡时正常产生资格。
6. 标准精英奖励和原始四个 Boss 的遗物奖励行为不回归。
7. 奖励逻辑对重复调用幂等，对非法成员、槽位和资格状态显式失败。
8. 所有目标测试与 `dotnet build` 通过，无 Godot 资源发布要求。
