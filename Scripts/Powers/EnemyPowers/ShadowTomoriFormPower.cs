using MegaCrit.Sts2.Core.Entities.Powers;
using STS2_Tomorin_Mod.Enemy.CardIntents;

namespace STS2_Tomorin_Mod.Powers;

/// <summary>P2/P3 每次准备周期冻结一件加权收藏品的状态标记。</summary>
public sealed class ShadowTomoriFormPower : BasePowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override string CustomPackedIconPath =>
        "res://STS2_Tomorin_Mod/images/powers/TomoriFormPower.png";
    public override string? CustomBigIconPath =>
        "res://STS2_Tomorin_Mod/images/powers/big/TomoriFormPower.png";

    public EnemyPreparationCycle CreatePreparationCycle(
        EnemyCardCombatState state,
        IEnemyCardRandomSource randomSource,
        IReadOnlyList<(EnemyCollectionDefinition Definition, int Weight)> weightedCollections)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(randomSource);
        ArgumentNullException.ThrowIfNull(weightedCollections);
        if (weightedCollections.Count == 0 ||
            weightedCollections.Any(item => item.Definition is null || item.Weight <= 0))
        {
            throw new ArgumentException("影灯准备收藏品权重必须全部为正且至少包含一项。", nameof(weightedCollections));
        }

        int totalWeight = checked(weightedCollections.Sum(item => item.Weight));
        int roll = randomSource.NextIndex(totalWeight);
        EnemyCollectionDefinition selected = weightedCollections.First(item =>
        {
            if (roll < item.Weight)
            {
                return true;
            }

            roll -= item.Weight;
            return false;
        }).Definition;
        EnemyCollectionInstance reserved = new(selected, state.CollectionInventory.NextSequence);
        return new EnemyPreparationCycle(
            reserved,
            new EnemyPreparedPreActionInventoryDelta([reserved]));
    }
}
