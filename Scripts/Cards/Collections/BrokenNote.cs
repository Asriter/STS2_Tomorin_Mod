using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2_Tomorin_Mod.CardPools;
using STS2_Tomorin_Mod.Cards.Base;
using STS2_Tomorin_Mod.Powers;

namespace STS2_Tomorin_Mod.Cards.Collections;

/// <summary>
/// 满是划痕的笔记本
/// 搜集品 tomorin
/// 获得5点防御，本回合[心之壁]不会因为敌人的攻击而减少
/// </summary>


[Pool(typeof(CollectionsCardPool))]
public class BrokenNote() : 
    BaseCardModel(-1, CardType.Status, CardRarity.Status, TargetType.None, true)
{
    public override int MaxUpgradeLevel => 0;
    
    public override IEnumerable<CardKeyword> CanonicalKeywords
    {
        get
        {
            var list = base.CanonicalKeywords.ToList();
            list.Add(CardKeyword.Unplayable);
            list.Add(CardKeyword.Retain);
            return list;
        }
    }

    protected override IEnumerable<DynamicVar> CanonicalVars
    {
        get
        {
            var list = base.CanonicalVars.ToList();
            list.Add(new BlockVar(5m, ValueProp.Move));
            return list;
        }
    }

    protected override bool IsPlayable => false;

    public override bool IsInspiration => true;

    //打出效果，抽二
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(base.Owner.Creature, base.DynamicVars.Block, cardPlay);
		await PowerCmd.Apply<BrokenNotePower>(base.Owner.Creature, 1m, base.Owner.Creature, this);
    }
}