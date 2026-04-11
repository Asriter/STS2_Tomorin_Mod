using System.Reflection;
using BaseLib.Abstracts;
using BaseLib.Patches.Content;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Rooms;
using STS2_Tomorin_Mod.Commands;
using STS2_Tomorin_Mod.Extensions;
using STS2_Tomorin_Mod.Localization.CustomEnums;
using STS2_Tomorin_Mod.Localization.DynamicVars;

namespace STS2_Tomorin_Mod.Cards.Base;

public abstract class BaseCardModel(
    int canonicalEnergyCost,
    CardType type,
    CardRarity rarity,
    TargetType targetType,
    bool shouldShowInCardLibrary = true,
    bool autoAdd = true)
    : CustomCardModel(canonicalEnergyCost, type, rarity, targetType, shouldShowInCardLibrary, autoAdd),
        CustomHookInterface
{
    private static MethodInfo? _taskPlayPowerCardFlyVfxMethod;

    /// <summary>
    /// 获取“创作”cmd的消耗品需求
    /// </summary>
    protected Dictionary<CardType, int>? ComposeCost
    {
        get
        {
            if (DynamicVars.ContainsKey(ComposeVar.DefaultName))
                return ((ComposeVar)DynamicVars[ComposeVar.DefaultName]).CostCards;
            return null;
        }
    }

    public bool IsCompose => ComposeCost != null;

    /// <summary>
    /// 当回合灵感是否触发
    /// </summary>
    private bool _applyInspiration = false;

    //卡图
    public override string PortraitPath => $"res://STS2_Tomorin_Mod/images/card_portraits/{this.GetType().Name}.png";

    public override string CustomPortraitPath =>
        $"res://STS2_Tomorin_Mod/images/card_portraits/big/{this.GetType().Name}.png";

    public override string BetaPortraitPath =>
        $"res://STS2_Tomorin_Mod/images/card_portraits/{this.GetType().Name}.png";

    /// <summary>
    /// 能否打出，这里默认对作词作判断
    /// </summary>
    protected override bool IsPlayable
    {
        get
        {
            if (ComposeCost != null)
                return ComposeCmd.CanUseCompose(Owner, ComposeCost, this);
            else
                return base.IsPlayable;
        }
    }


    //自动添加keyword
    public override IEnumerable<CardKeyword> CanonicalKeywords
    {
        get
        {
            var list = new List<CardKeyword>();
            if (IsInspiration)
                list.Add(CustomKeyWord.Inspiration);

            return list;
        }
    }


    public virtual bool IsInspiration => false;

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            var list = new List<IHoverTip>();
            if (ComposeCost != null)
                list.Add(GetComposeHoverTips());

            return list;
        }
    }

    /// <summary>
    /// 获取作词卡的提示词
    /// </summary>
    /// <returns></returns>
    private IHoverTip GetComposeHoverTips()
    {
        var composeVar = (ComposeVar)DynamicVars[ComposeVar.DefaultName];
        var text = $"{MainFile.ModId}-{ComposeVar.DefaultName}";
        text = text.ToUpper();
        LocString title = new LocString("static_hover_tips", text + ".title");
        LocString desc = new LocString("static_hover_tips", text + ".description");
        desc.Add(ComposeVar.titleName, composeVar.Title);
        desc.Add(ComposeVar.isCostAttackName, composeVar.isCostAttack);
        desc.Add(ComposeVar.isCostSkillName, composeVar.isCostSkill);
        desc.Add(ComposeVar.isCostStateName, composeVar.isCostState);
        desc.Add(ComposeVar.atkCostName, composeVar.atkCost);
        desc.Add(ComposeVar.skillCostName, composeVar.skillCost);
        desc.Add(ComposeVar.stateCostName, composeVar.stateCost);

        return new HoverTip(title, desc);
    }

    /// <summary>
    /// 作词完成后的回调接口
    /// </summary>
    /// <param name="choiceContext"></param>
    /// <param name="player"></param>
    /// <param name="source"></param>
    /// <returns></returns>
    public virtual Task AfterCompose(PlayerChoiceContext choiceContext, Player player, CardModel source)
    {
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// 消耗手卡
    /// </summary>
    /// <param name="choiceContext"></param>
    /// <param name="num"></param>
    protected async Task ExhaustCard(PlayerChoiceContext choiceContext, int num)
    {
        //消耗一张手牌
        var exhaustList = await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, num),
            null,
            this);
        foreach (var card in exhaustList)
        {
            await CardCmd.Exhaust(choiceContext, card);
        }
    }
}