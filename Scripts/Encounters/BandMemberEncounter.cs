using BaseLib.Abstracts;
using Godot;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using STS2_Tomorin_Mod.Enemy;
using STS2_Tomorin_Mod.Enemy.Elite;
using STS2_Tomorin_Mod.Enemy.Ememies;

namespace STS2_Tomorin_Mod.Encounters;

/// <summary>
/// 从当前局未遇到的原始乐队 Boss 中确定两名成员，并以原生夹击语义生成精英战。
/// </summary>
public sealed class BandMemberEncounter : CustomEncounterModel
{
    public const string LeftMember = "LeftMember";
    public const string RightMember = "RightMember";

    private const string LeftMemberStateKey = "leftMember";
    private const string RightMemberStateKey = "rightMember";

    private BandMemberKind? _leftMember;
    private BandMemberKind? _rightMember;
    private bool _warnedAboutMissingRunState;

    /// <summary>
    /// 创建保留原生标准精英奖励的乐队 Encounter。
    /// </summary>
    public BandMemberEncounter() : base(RoomType.Elite, true)
    {
    }

    /// <summary>
    /// 指示玩家队伍使用原生居中布局。
    /// </summary>
    public override bool FullyCenterPlayers => true;

    /// <summary>
    /// 允许 Encounter 接收战斗开始钩子以初始化夹击机制。
    /// </summary>
    public override bool ShouldReceiveCombatHooks => true;

    /// <summary>
    /// 指示 Encounter 使用独立的左右敌人槽位场景。
    /// </summary>
    public override bool HasScene => true;

    /// <summary>
    /// 获取乐队精英房的左右槽位场景路径。
    /// </summary>
    public override string? CustomScenePath =>
        "res://STS2_Tomorin_Mod/scenes/encounters/band_member_encounter.tscn";

    /// <summary>
    /// 获取场景内全部合法敌人槽位。
    /// </summary>
    public override IReadOnlyList<string> Slots => [LeftMember, RightMember];

    /// <summary>
    /// 获取适配夹击布局的相机缩放。
    /// </summary>
    public override float GetCameraScaling() => 0.75f;

    /// <summary>
    /// 获取适配夹击布局的相机偏移。
    /// </summary>
    public override Vector2 GetCameraOffset() => new(0f, 35f);

    /// <summary>
    /// 返回需要预加载资源的全部乐队成员精英模型。
    /// </summary>
    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<AnonElite>(),
        ModelDb.Monster<TakiElite>(),
        ModelDb.Monster<SoyoElite>(),
        ModelDb.Monster<RaanaElite>()
    ];

    /// <summary>
    /// 明确禁止本 Encounter 自然进入任何 Act 的精英遭遇池。
    /// </summary>
    /// <param name="act">待检查的 Act。</param>
    /// <returns>始终返回 false。</returns>
    public override bool IsValidForAct(ActModel act)
    {
        return false;
    }

    /// <summary>
    /// 按已保存或确定性选择的成员生成左右两个精英敌人。
    /// </summary>
    /// <returns>带稳定左右槽位的敌人模型列表。</returns>
    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        EnsureMemberSelection();

        var leftKind = _leftMember!.Value;
        var rightKind = _rightMember!.Value;
        return
        [
            (CreateElite(leftKind, LeftMember), LeftMember),
            (CreateElite(rightKind, RightMember), RightMember)
        ];
    }

    /// <summary>
    /// 在战斗开始时幂等应用原生夹击 Power。
    /// </summary>
    public override Task BeforeCombatStart()
    {
        if (SpawnedEnemies.Count != 2)
        {
            throw new InvalidOperationException(
                $"{nameof(BandMemberEncounter)} 需要恰好两个已生成敌人，实际数量为 {SpawnedEnemies.Count}。");
        }

        return BandSurroundedCoordinator.Initialize(SpawnedEnemies[0].Creature, SpawnedEnemies[1].Creature);
    }

    /// <summary>
    /// 保存确定后的左右成员稳定名称。
    /// </summary>
    /// <returns>包含合法左右成员时的 Encounter 自定义状态。</returns>
    public override Dictionary<string, string> SaveCustomState()
    {
        var state = base.SaveCustomState();
        if (HasValidMemberSelection())
        {
            state[LeftMemberStateKey] = BandMemberSelector.GetStableName(_leftMember!.Value);
            state[RightMemberStateKey] = BandMemberSelector.GetStableName(_rightMember!.Value);
        }

        return state;
    }

    /// <summary>
    /// 从稳定字符串恢复左右成员；任一字段非法时整体拒绝该组状态。
    /// </summary>
    /// <param name="state">Encounter 自定义状态。</param>
    public override void LoadCustomState(Dictionary<string, string> state)
    {
        base.LoadCustomState(state);
        if (state.TryGetValue(LeftMemberStateKey, out var leftName) &&
            state.TryGetValue(RightMemberStateKey, out var rightName) &&
            BandMemberSelector.TryParseStableName(leftName, out var left) &&
            BandMemberSelector.TryParseStableName(rightName, out var right) &&
            left != right)
        {
            _leftMember = left;
            _rightMember = right;
            return;
        }

        _leftMember = null;
        _rightMember = null;
    }

    /// <summary>
    /// 在没有合法恢复状态时从当前 Run 历史确定左右成员。
    /// </summary>
    private void EnsureMemberSelection()
    {
        if (HasValidMemberSelection())
        {
            return;
        }

        IRunState? runState = RunManager.Instance?.DebugOnlyGetState();
        HashSet<BandMemberKind> encountered;
        if (runState == null)
        {
            if (!_warnedAboutMissingRunState)
            {
                Log.Warn($"[{nameof(BandMemberEncounter)}] 当前没有活动 RunState，将按空历史选择成员。");
                _warnedAboutMissingRunState = true;
            }

            encountered = [];
        }
        else
        {
            encountered = CollectEncounteredOriginalMembers(runState);
        }

        var selection = BandMemberSelector.Select(encountered);
        _leftMember = selection.Left;
        _rightMember = selection.Right;
    }

    /// <summary>
    /// 检查内部左右成员是否均有效且互不相同。
    /// </summary>
    /// <returns>成员状态是否合法。</returns>
    private bool HasValidMemberSelection()
    {
        return _leftMember.HasValue && _rightMember.HasValue && _leftMember.Value != _rightMember.Value;
    }

    /// <summary>
    /// 从当前局地图历史收集四个原始 Boss 的 Encounter 与 Monster 身份。
    /// </summary>
    /// <param name="runState">当前 Run 状态。</param>
    /// <returns>已经遇到的原始成员集合。</returns>
    private static HashSet<BandMemberKind> CollectEncounteredOriginalMembers(IRunState runState)
    {
        HashSet<BandMemberKind> encountered = [];
        foreach (IReadOnlyList<MapPointHistoryEntry>? actHistory in runState.MapPointHistory)
        {
            if (actHistory == null)
            {
                continue;
            }

            foreach (var historyEntry in actHistory)
            {
                if (historyEntry?.Rooms == null)
                {
                    continue;
                }

                foreach (MapPointRoomHistoryEntry? roomHistory in historyEntry.Rooms)
                {
                    if (roomHistory == null)
                    {
                        continue;
                    }

                    AddOriginalMember(roomHistory.ModelId, encountered);
                    if (roomHistory.MonsterIds == null)
                    {
                        continue;
                    }

                    foreach (var monsterId in roomHistory.MonsterIds)
                    {
                        AddOriginalMember(monsterId, encountered);
                    }
                }
            }
        }

        return encountered;
    }

    /// <summary>
    /// 将原始 Boss Encounter 或 Monster 的模型 ID 映射为成员身份。
    /// </summary>
    /// <param name="modelId">历史中的模型 ID。</param>
    /// <param name="encountered">需要更新的已遇到集合。</param>
    private static void AddOriginalMember(ModelId? modelId, ISet<BandMemberKind> encountered)
    {
        if (modelId == null)
        {
            return;
        }

        if (modelId.Equals(ModelDb.Encounter<AnonBoss>().Id) || modelId.Equals(ModelDb.Monster<Anon>().Id))
        {
            encountered.Add(BandMemberKind.Anon);
        }
        else if (modelId.Equals(ModelDb.Encounter<TakiBoss>().Id) || modelId.Equals(ModelDb.Monster<Taki>().Id))
        {
            encountered.Add(BandMemberKind.Taki);
        }
        else if (modelId.Equals(ModelDb.Encounter<SoyoBoss>().Id) || modelId.Equals(ModelDb.Monster<Soyo>().Id))
        {
            encountered.Add(BandMemberKind.Soyo);
        }
        else if (modelId.Equals(ModelDb.Encounter<RaanaBoss>().Id) || modelId.Equals(ModelDb.Monster<Raana>().Id))
        {
            encountered.Add(BandMemberKind.Raana);
        }
    }

    /// <summary>
    /// 创建指定成员的 mutable Elite 模型，并在失败时补充 Encounter 与槽位上下文。
    /// </summary>
    /// <param name="member">需要创建的成员身份。</param>
    /// <param name="slot">目标场景槽位。</param>
    /// <returns>对应成员的 mutable Elite 模型。</returns>
    private static MonsterModel CreateElite(BandMemberKind member, string slot)
    {
        try
        {
            return member switch
            {
                BandMemberKind.Anon => ModelDb.Monster<AnonElite>().ToMutable(),
                BandMemberKind.Taki => ModelDb.Monster<TakiElite>().ToMutable(),
                BandMemberKind.Soyo => ModelDb.Monster<SoyoElite>().ToMutable(),
                BandMemberKind.Raana => ModelDb.Monster<RaanaElite>().ToMutable(),
                _ => throw new ArgumentOutOfRangeException(nameof(member), member, "未知乐队成员身份。")
            };
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"{nameof(BandMemberEncounter)} 无法为成员 {member} 创建槽位 {slot} 的 Elite 模型。", exception);
        }
    }
}
