using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2_Tomorin_Mod.Cards.Base;

namespace STS2_Tomorin_Mod.Cards;

[Pool(typeof(CurseCardPool))]
public class AnonReject() : BaseCardModel(-1, CardType.Curse, CardRarity.Curse, TargetType.None)
{
    public override int MaxUpgradeLevel => 0;

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Unplayable];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<WeakPower>(), HoverTipFactory.FromPower<FrailPower>()];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<WeakPower>(1m), new PowerVar<FrailPower>(1)];

    public override bool HasTurnEndInHandEffect => true;
    

    public override async Task OnTurnEndInHand(PlayerChoiceContext choiceContext)
    {
        await AddPower<WeakPower>(DynamicVars.Weak.BaseValue);
        await AddPower<FrailPower>(DynamicVars["FrailPower"].BaseValue);
    }

    private async Task AddPower<T>(decimal amount) where T : PowerModel
    {
        bool alreadyHasPower = Owner.Creature.HasPower<T>();
        PowerModel? powerModel = await PowerCmd.Apply<T>(Owner.Creature, amount, null, this);
        if (powerModel != null && !alreadyHasPower)
        {
            powerModel.SkipNextDurationTick = true;
        }
    }
}