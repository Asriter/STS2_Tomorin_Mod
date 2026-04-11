using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2_Tomorin_Mod.CardPools;
using STS2_Tomorin_Mod.Cards.Base;

namespace STS2_Tomorin_Mod.Cards;

[Pool(typeof(TomorinCardPool))]
public class SenzaihyoumeiToken() : BaseCardModel(0, CardType.Attack, CardRarity.Token, TargetType.AnyEnemy)
{
	protected override bool HasEnergyCostX => true;
    
    private List<Type> types = new List<Type>();
    
    protected override IEnumerable<DynamicVar> CanonicalVars
    {
        get
        {
            var list = new List<DynamicVar>().ToList();
            list.Add( new DamageVar(8m, ValueProp.Move));
            list.Add(new CardsVar(5));
            return list;
        }
    }
    
    //是否闪金边
    protected override bool ShouldGlowGoldInternal
    {
        get
        {
            var exhaustPile = Owner.PlayerCombatState?.ExhaustPile;
            var cards = exhaustPile.Cards;
            types.Clear();
            foreach (var card in cards)
            {
                if (!types.Contains(card.GetType()))
                {
                    types.Add(card.GetType());
                }
            }

            return types.Count >= DynamicVars.Cards.IntValue;
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        int num = ResolveEnergyXValue();
        if (ShouldGlowGoldInternal)
        {
            num *= 2;
        }
        
        await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).WithHitCount(num).FromCard(this)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_giant_horizontal_slash", null, "slash_attack.mp3")
            .Execute(choiceContext);
    }
    
    protected override void OnUpgrade()
    {
        base.DynamicVars.Damage.UpgradeValueBy(2m);
    }
}