using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using STS2_Tomorin_Mod.CardPools;
using STS2_Tomorin_Mod.Cards.Base;
using STS2_Tomorin_Mod.Powers;

namespace STS2_Tomorin_Mod.Cards;

/// <summary>
/// 高松积分
/// 蓝卡 技能 1费 获得1点格挡，对一名敌人造成1点伤害，获得1层心之壁 消耗
/// 这张卡每打出一次，在这场游戏中数值+1
/// </summary>
[Pool(typeof(TomorinCardPool))]
public class TakamasuCalculus : BaseCardModel
{
    private const string _increaseKey = "Increase";
    private int _currentBlock = 1;
    private int _currentDamage = 1;
    private int _currentAtField = 1;
    private int _increasedAmount;

    [SavedProperty]
    public int CurrentBlock
    {
        get => _currentBlock;
        set
        {
            AssertMutable();
            _currentBlock = value;
            base.DynamicVars.Block.BaseValue = _currentBlock;
        }
    }

    [SavedProperty]
    public int CurrentDamage
    {
        get => _currentDamage;
        set
        {
            AssertMutable();
            _currentDamage = value;
            base.DynamicVars.Damage.BaseValue = _currentDamage;
        }
    }

    [SavedProperty]
    public int CurrentAtField
    {
        get => _currentAtField;
        set
        {
            AssertMutable();
            _currentAtField = value;
            base.DynamicVars["AtFieldPower"].BaseValue = _currentAtField;
        }
    }

    [SavedProperty]
    public int IncreasedAmount
    {
        get => _increasedAmount;
        set
        {
            AssertMutable();
            _increasedAmount = value;
        }
    }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(CurrentBlock, ValueProp.Move),
        new DamageVar(CurrentDamage, ValueProp.Move),
        new PowerVar<AtFieldPower>(CurrentAtField),
        new IntVar(_increaseKey, 1m)
    ];

    public TakamasuCalculus() :
        base(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
    }

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            var list = base.ExtraHoverTips.ToList();
            list.Add(HoverTipFactory.FromPower<AtFieldPower>());
            return list;
        }
    }

    public override IEnumerable<CardKeyword> CanonicalKeywords
    {
        get
        {
            var list = base.CanonicalKeywords.ToList();
            list.Add(CardKeyword.Exhaust);
            return list;
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        // 获得格挡
        await CreatureCmd.GainBlock(base.Owner.Creature, base.DynamicVars.Block, cardPlay);

        // 造成伤害
        await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue)
            .FromCard(this)
            .Targeting(cardPlay.Target)
            .Execute(choiceContext);

        // 获得心之壁
        await PowerCmd.Apply<AtFieldPower>(base.Owner.Creature, base.DynamicVars["AtFieldPower"].BaseValue, base.Owner.Creature, this);

        // 增加数值
        int increase = base.DynamicVars[_increaseKey].IntValue;
        BuffFromPlay(increase);
        (base.DeckVersion as TakamasuCalculus)?.BuffFromPlay(increase);
    }

    protected override void OnUpgrade()
    {
        base.DynamicVars[_increaseKey].UpgradeValueBy(1m);
    }

    protected override void AfterDowngraded()
    {
        UpdateValues();
    }

    private void BuffFromPlay(int extraAmount)
    {
        IncreasedAmount += extraAmount;
        UpdateValues();
    }

    private void UpdateValues()
    {
        CurrentBlock = 1 + IncreasedAmount;
        CurrentDamage = 1 + IncreasedAmount;
        CurrentAtField = 1 + IncreasedAmount;
    }
}
