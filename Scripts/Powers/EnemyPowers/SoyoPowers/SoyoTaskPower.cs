using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2_Tomorin_Mod.Enemy;

namespace STS2_Tomorin_Mod.Powers;

public abstract class SoyoTaskPower : BasePowerModel
{
    private bool _settled;

    protected abstract int BaseRequired { get; }
    protected int Progress { get; private set; }
    protected int Required => BaseRequired * PlayerCount;
    protected int Missing => Math.Max(0, Required - Progress);
    private int PlayerCount => Math.Max(1, CombatState?.Players.Count ?? 1);

    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override int DisplayAmount
    {
        get
        {
            SyncVars();
            return Missing;
        }
    }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Progress", 0m),
        new IntVar("Required", 0m),
        new IntVar("Missing", 0m),
        new IntVar("PlayerCount", 1m)
    ];

    public static async Task ApplyRandomTask(PlayerChoiceContext choiceContext, Creature owner)
    {
        await RemoveCurrentTask(owner);

        int taskId = owner.Monster?.Rng.NextInt(0, 3) ?? 0;
        switch (taskId)
        {
            case 0:
                await PowerCmd.Apply<SoyoAttackTaskPower>(choiceContext, owner, 1, owner, null);
                break;
            case 1:
                await PowerCmd.Apply<SoyoSkillTaskPower>(choiceContext, owner, 1, owner, null);
                break;
            case 2:
                await PowerCmd.Apply<SoyoPlayCardsTaskPower>(choiceContext, owner, 1, owner, null);
                break;
            default:
                await PowerCmd.Apply<SoyoKeepHandTaskPower>(choiceContext, owner, 1, owner, null);
                break;
        }
    }

    public static async Task RemoveCurrentTask(Creature owner)
    {
        foreach (var task in owner.Powers.OfType<SoyoTaskPower>().ToList())
        {
            await PowerCmd.Remove(task);
        }
    }

    protected void AddProgress(int amount)
    {
        if (_settled || amount <= 0) return;
        Progress += amount;
        SyncVars();
    }

    public override async Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (_settled || side != CombatSide.Player) return;

        _settled = true;
        RefreshProgressBeforeSettle();
        SyncVars();

        if (Missing == 0)
        {
            Flash();
            await ApplyReward(choiceContext);
        }
        else
        {
            Flash();
            await ApplyPenalty(choiceContext);
            await SoyoEstrangementPower.Modify(choiceContext, Owner, Missing, this);
        }

        await PowerCmd.Remove(this);

        if (Owner.Monster is Soyo { Phase: Soyo.SoyoPhase.Mask })
        {
            await ApplyRandomTask(choiceContext, Owner);
        }
    }

    protected virtual void RefreshProgressBeforeSettle()
    {
    }

    protected abstract Task ApplyReward(PlayerChoiceContext choiceContext);
    protected abstract Task ApplyPenalty(PlayerChoiceContext choiceContext);

    protected IReadOnlyList<Creature> LivingPlayers() =>
        CombatState.Players.Select(player => player.Creature).Where(creature => creature.IsAlive).ToList();

    private void SyncVars()
    {
        if (DynamicVars.ContainsKey("Progress")) DynamicVars["Progress"].BaseValue = Progress;
        if (DynamicVars.ContainsKey("Required")) DynamicVars["Required"].BaseValue = Required;
        if (DynamicVars.ContainsKey("Missing")) DynamicVars["Missing"].BaseValue = Missing;
        if (DynamicVars.ContainsKey("PlayerCount")) DynamicVars["PlayerCount"].BaseValue = PlayerCount;
    }
}

public class SoyoAttackTaskPower : SoyoTaskPower
{
    protected override int BaseRequired => 3;

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner?.Creature.Side == CombatSide.Player && cardPlay.Card.Type == CardType.Attack)
        {
            AddProgress(1);
        }

        return Task.CompletedTask;
    }

    protected override async Task ApplyReward(PlayerChoiceContext choiceContext)
    {
        foreach (var player in LivingPlayers())
        {
            await PowerCmd.Apply<SoyoNextTurnTemporaryStrengthPower>(choiceContext, player, 3, Owner, null);
        }
    }

    protected override Task ApplyPenalty(PlayerChoiceContext choiceContext) =>
        PowerCmd.Apply<WeakPower>(choiceContext, LivingPlayers(), 1, Owner, null);
}

public class SoyoSkillTaskPower : SoyoTaskPower
{
    protected override int BaseRequired => 3;

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner?.Creature.Side == CombatSide.Player && cardPlay.Card.Type == CardType.Skill)
        {
            AddProgress(1);
        }

        return Task.CompletedTask;
    }

    protected override async Task ApplyReward(PlayerChoiceContext choiceContext)
    {
        foreach (var player in LivingPlayers())
        {
            await PowerCmd.Apply<SoyoNextTurnTemporaryDexterityPower>(choiceContext, player, 3, Owner, null);
        }
    }

    protected override Task ApplyPenalty(PlayerChoiceContext choiceContext) =>
        PowerCmd.Apply<FrailPower>(choiceContext, LivingPlayers(), 1, Owner, null);
}

public class SoyoPlayCardsTaskPower : SoyoTaskPower
{
    protected override int BaseRequired => 4;

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner?.Creature.Side == CombatSide.Player)
        {
            AddProgress(1);
        }

        return Task.CompletedTask;
    }

    protected override Task ApplyReward(PlayerChoiceContext choiceContext) =>
        PowerCmd.Apply<EnergyNextTurnPower>(choiceContext, LivingPlayers(), 1, Owner, null);

    protected override async Task ApplyPenalty(PlayerChoiceContext choiceContext)
    {
        foreach (var player in LivingPlayers())
        {
            await CreatureCmd.Damage(choiceContext, player, 3m, ValueProp.Unblockable, Owner, null);
        }
    }
}

public class SoyoKeepHandTaskPower : SoyoTaskPower
{
    protected override int BaseRequired => 4;

    protected override void RefreshProgressBeforeSettle()
    {
        AddProgress(CombatState.Players.Sum(player => player.PlayerCombatState.Hand.Cards.Count));
    }

    protected override Task ApplyReward(PlayerChoiceContext choiceContext) =>
        PowerCmd.Apply<DrawCardsNextTurnPower>(choiceContext, LivingPlayers(), 1, Owner, null);

    protected override Task ApplyPenalty(PlayerChoiceContext choiceContext) =>
        PowerCmd.Apply<VulnerablePower>(choiceContext, LivingPlayers(), 1, Owner, null);
}

public class SoyoNextTurnTemporaryStrengthPower : CustomTemporaryPowerModel
{
    protected override Func<PlayerChoiceContext, Creature, decimal, Creature?, CardModel?, bool, Task> ApplyPowerFunc =>
        (choiceContext, target, amount, applier, cardSource, silent) =>
            PowerCmd.Apply<StrengthPower>(choiceContext, target, amount, applier, cardSource, silent);

    public override PowerModel InternallyAppliedPower => ModelDb.Power<StrengthPower>();
    public override AbstractModel OriginModel => ModelDb.Card<SetupStrike>();
    protected override int LastForXExtraTurns => 1;
    public override LocString Title => ModelDb.Card<SetupStrike>().TitleLocString;
    public override LocString Description => new("powers", "TEMPORARY_STRENGTH_POWER.description");
    protected override string SmartDescriptionLocKey => "TEMPORARY_STRENGTH_POWER.smartDescription";
    public override string? CustomPackedIconPath => ModelDb.Power<SetupStrikePower>().IconPath;
    public override string? CustomBigIconPath => ModelDb.Power<SetupStrikePower>().ResolvedBigIconPath;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard(ModelDb.Card<SetupStrike>()),
        HoverTipFactory.FromPower<StrengthPower>()
    ];
}

public class SoyoNextTurnTemporaryDexterityPower : CustomTemporaryPowerModel
{
    protected override Func<PlayerChoiceContext, Creature, decimal, Creature?, CardModel?, bool, Task> ApplyPowerFunc =>
        (choiceContext, target, amount, applier, cardSource, silent) =>
            PowerCmd.Apply<DexterityPower>(choiceContext, target, amount, applier, cardSource, silent);

    public override PowerModel InternallyAppliedPower => ModelDb.Power<DexterityPower>();
    public override AbstractModel OriginModel => ModelDb.Potion<SpeedPotion>();
    protected override int LastForXExtraTurns => 1;
    public override LocString Title => ModelDb.Potion<SpeedPotion>().Title;
    public override LocString Description => new("powers", "TEMPORARY_DEXTERITY_POWER.description");
    protected override string SmartDescriptionLocKey => "TEMPORARY_DEXTERITY_POWER.smartDescription";
    public override string? CustomPackedIconPath => ModelDb.Power<SpeedPotionPower>().IconPath;
    public override string? CustomBigIconPath => ModelDb.Power<SpeedPotionPower>().ResolvedBigIconPath;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPotion(ModelDb.Potion<SpeedPotion>()),
        HoverTipFactory.FromPower<DexterityPower>()
    ];
}
