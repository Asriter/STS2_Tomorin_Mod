using BaseLib.Extensions;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.PotionPools;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using STS2_Tomorin_Mod.CardPools;
using STS2_Tomorin_Mod.Cards;
using STS2_Tomorin_Mod.Enchantments;
using STS2_Tomorin_Mod.Localization.CustomEnums;

namespace STS2_Tomorin_Mod.Relics;

/// <summary>
/// 再生产的舞台装置：立即变化一张永久牌，并强化此后永久获得的牌。
/// </summary>
[Pool(typeof(EventRelicPool))]
public sealed class ReproductionStageDevice : GiraffeStageDeviceRelic
{
    /// <summary>
    /// 创建遗物并登记“牌组非空”的先古选项出现条件。
    /// </summary>
    public ReproductionStageDevice()
    {
        this.AddCustomAncientSpawnCondition(ancient => ancient.Owner?.Deck.Cards.Count > 0);
    }

    /// <summary>
    /// 取得遗物时随机变化一张永久牌，并在变化结束后保存下一张牌的强化资格。
    /// </summary>
    public override async Task AfterObtained()
    {
        if (Owner.Deck.Cards.Count > 0)
        {
            CardModel? original = Owner.PlayerRng.Transformations.NextItem(Owner.Deck.Cards);
            if (original == null)
            {
                return;
            }

            CardModel[] options = GetTransformationOptions(original);
            if (options.Length > 0)
            {
                CardModel replacement = CardFactory.CreateRandomCardForTransform(
                    original, options, false, Owner.PlayerRng.Transformations);
                PreserveUpgradeState(original, replacement);
                await CardCmd.Transform(original, replacement, CardPreviewStyle.EventLayout);
            }
        }
    }

    /// <summary>
    /// 后续所有获得的牌复制后升级并赋予灵光乍现，临时战斗牌不会进入此钩子。
    /// </summary>
    /// <param name="card">即将加入永久牌组的牌。</param>
    /// <param name="newCard">替代原牌加入牌组的强化副本。</param>
    /// <returns>本次是否替换了加入牌组的牌。</returns>
    public override bool TryModifyCardBeingAddedToDeck(CardModel card, out CardModel newCard)
    {
        newCard = null!;
        if (card.Owner != Owner)
        {
            return false;
        }

        newCard = Owner.RunState.CloneCard(card);
        if (newCard.IsUpgradable)
        {
            CardCmd.Upgrade(newCard, CardPreviewStyle.None);
        }

        StageDeviceEnchantment.ApplyReplacingExisting<ReproductionStageDeviceEnchantment>(newCard);
        Flash();
        return true;
    }

    /// <summary>
    /// 根据原牌类别生成符合再生产规则的合法变化池。
    /// </summary>
    /// <param name="original">将被变化的永久牌。</param>
    /// <returns>经过游戏通用变化过滤器验证的候选牌。</returns>
    private CardModel[] GetTransformationOptions(CardModel original)
    {
        CardPoolModel collectionPool = ModelDb.CardPool<CollectionsCardPool>();
        IEnumerable<CardModel> candidates = ModelDb.AllCards.Where(candidate =>
            candidate.Id != original.Id &&
            candidate.Rarity != CardRarity.Token &&
            candidate.ShouldShowInCardLibrary &&
            IsTransformationCategoryAllowed(original, candidate, collectionPool));

        return candidates.ToArray();
    }

    /// <summary>
    /// 判断候选牌是否符合诅咒、状态牌或一般牌的类别限制。
    /// </summary>
    /// <param name="original">被变化的原牌。</param>
    /// <param name="candidate">待检查的候选牌。</param>
    /// <param name="collectionPool">收藏品卡池。</param>
    /// <returns>候选牌是否属于允许的变化类别。</returns>
    private bool IsTransformationCategoryAllowed(
        CardModel original,
        CardModel candidate,
        CardPoolModel collectionPool)
    {
        if (original.Type == CardType.Curse)
        {
            return candidate.Type == CardType.Curse;
        }

        if (original.Type == CardType.Status)
        {
            return candidate.Type == CardType.Status || candidate.Pool == collectionPool;
        }

        return candidate.Pool == Owner.Character.CardPool ||
               candidate.Pool.IsColorless ||
               candidate.Pool == collectionPool ||
               candidate.Type == CardType.Curse ||
               candidate.Rarity == CardRarity.Ancient;
    }

    /// <summary>
    /// 在新牌允许升级时继承原牌已有的升级层数。
    /// </summary>
    /// <param name="original">提供升级状态的原牌。</param>
    /// <param name="replacement">接收升级状态的新牌。</param>
    private static void PreserveUpgradeState(CardModel original, CardModel replacement)
    {
        if (original.IsUpgraded && replacement.IsUpgradable)
        {
            CardCmd.Upgrade(replacement, CardPreviewStyle.None);
        }
    }
}

/// <summary>
/// 渴望的舞台装置：将两张舞台番茄加入永久牌组。
/// </summary>
[Pool(typeof(EventRelicPool))]
public sealed class DesireStageDevice : GiraffeStageDeviceRelic
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[]
    {
        HoverTipFactory.FromCard<StageTomato>()
    };

    /// <summary>
    /// 取得遗物时生成并加入两张舞台番茄。
    /// </summary>
    public override async Task AfterObtained()
    {
        for (int index = 0; index < 2; index++)
        {
            CardModel tomato = Owner.RunState.CreateCard(ModelDb.Card<StageTomato>(), Owner);
            await CardPileCmd.Add(tomato, PileType.Deck, CardPilePosition.Bottom, this, false);
        }
    }
}

/// <summary>
/// 竞演的舞台装置：用诅咒交换三张高费用、可重放且带灵光乍现的牌。
/// </summary>
[Pool(typeof(EventRelicPool))]
public sealed class CompetitionStageDevice : GiraffeStageDeviceRelic
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[]
    {
        HoverTipFactory.FromKeyword(CustomKeyWord.Inspiration)
    };

    private const int SelectionCount = 3;

    /// <summary>
    /// 创建遗物并登记“至少存在足量可强化牌”的先古选项出现条件。
    /// </summary>
    public CompetitionStageDevice()
    {
        this.AddCustomAncientSpawnCondition(ancient =>
            ancient.Owner?.Deck.Cards.Count(IsEligibleCard) >= SelectionCount);
    }

    /// <summary>
    /// 取得遗物时让玩家选择三张牌进行永久强化，再加入两张被夺走的闪耀。
    /// </summary>
    public override async Task AfterObtained()
    {
        List<CardModel> selected = (await CardSelectCmd.FromDeckGeneric(
            Owner,
            new CardSelectorPrefs(SelectionScreenPrompt, SelectionCount),
            IsEligibleCard,
            null)).ToList();

        foreach (CardModel card in selected)
        {
            StageDeviceEnchantment.ApplyReplacingExisting<CompetitionStageDeviceEnchantment>(card);
        }

        for (int index = 0; index < 2; index++)
        {
            CardModel curse = Owner.RunState.CreateCard(ModelDb.Card<StolenShine>(), Owner);
            await CardPileCmd.Add(curse, PileType.Deck, CardPilePosition.Bottom, this, false);
        }
    }

    /// <summary>
    /// 判断一张永久牌是否能被竞演选择。
    /// </summary>
    /// <param name="card">待检查的永久牌。</param>
    /// <returns>牌是否可打出、非 X 费且为攻击、技能或能力。</returns>
    private static bool IsEligibleCard(CardModel card)
    {
        return !card.EnergyCost.CostsX &&
               !card.Keywords.Contains(CardKeyword.Unplayable) &&
               card.Type is CardType.Attack or CardType.Skill or CardType.Power;
    }
}

/// <summary>
/// 离别的舞台装置：移除一张可移除的永久牌。
/// </summary>
[Pool(typeof(EventRelicPool))]
public sealed class FarewellStageDevice : GiraffeStageDeviceRelic
{
    /// <summary>
    /// 创建遗物并登记“存在可移除牌”的先古选项出现条件。
    /// </summary>
    public FarewellStageDevice()
    {
        this.AddCustomAncientSpawnCondition(ancient =>
            ancient.Owner?.Deck.Cards.Any(card => card.IsRemovable) == true);
    }

    /// <summary>
    /// 取得遗物时打开标准移除界面并移除玩家选择的牌。
    /// </summary>
    public override async Task AfterObtained()
    {
        List<CardModel> selected = (await CardSelectCmd.FromDeckForRemoval(
            Owner,
            new CardSelectorPrefs(CardSelectorPrefs.RemoveSelectionPrompt, 1))).ToList();
        if (selected.Count > 0)
        {
            await CardPileCmd.RemoveFromDeck(selected);
        }
    }
}

/// <summary>
/// 骄傲的舞台装置：升级一张可升级的永久牌。
/// </summary>
[Pool(typeof(EventRelicPool))]
public sealed class PrideStageDevice : GiraffeStageDeviceRelic
{
    /// <summary>
    /// 创建遗物并登记“存在可升级牌”的先古选项出现条件。
    /// </summary>
    public PrideStageDevice()
    {
        this.AddCustomAncientSpawnCondition(ancient =>
            ancient.Owner?.Deck.Cards.Any(card => card.IsUpgradable) == true);
    }

    /// <summary>
    /// 取得遗物时打开标准升级界面并升级玩家选择的牌。
    /// </summary>
    public override async Task AfterObtained()
    {
        CardModel? selected = (await CardSelectCmd.FromDeckForUpgrade(
            Owner,
            new CardSelectorPrefs(CardSelectorPrefs.UpgradeSelectionPrompt, 1))).FirstOrDefault();
        if (selected != null)
        {
            CardCmd.Upgrade(selected, CardPreviewStyle.EventLayout);
        }
    }
}

/// <summary>
/// 幕间的舞台装置：增加药水栏位，并在规则允许时赠送一瓶稀有药水。
/// </summary>
[Pool(typeof(EventRelicPool))]
public sealed class InterludeStageDevice : GiraffeStageDeviceRelic
{
    /// <summary>
    /// 取得遗物时永久增加药水栏位，并从标准兼容药水池生成稀有药水。
    /// </summary>
    public override async Task AfterObtained()
    {
        await PlayerCmd.GainMaxPotionCount(1, Owner);

        List<PotionModel> available = PotionFactory.GetPotionOptions(Owner)
            .Where(potion => potion.Rarity == PotionRarity.Rare)
            .ToList();
        PotionModel? canonical = Owner.PlayerRng.Rewards.NextItem(available);
        if (canonical == null)
        {
            return;
        }

        await PotionCmd.TryToProcure(canonical.ToMutable(), Owner, -1);
    }
}

/// <summary>
/// 摘星的舞台装置：按自定义稀有度权重从本局合法遗物抓取袋中获得遗物。
/// </summary>
[Pool(typeof(EventRelicPool))]
public sealed class StarPickingStageDevice : GiraffeStageDeviceRelic
{
    private static readonly (RelicRarity Rarity, int Weight)[] WeightedRarities =
    [
        (RelicRarity.Common, 49),
        (RelicRarity.Uncommon, 30),
        (RelicRarity.Rare, 20),
        (RelicRarity.Ancient, 1),
    ];

    /// <summary>
    /// 创建遗物并登记“至少存在一个合法摘星候选”的先古选项出现条件。
    /// </summary>
    public StarPickingStageDevice()
    {
        this.AddCustomAncientSpawnCondition(ancient =>
            ancient.Owner != null && HasAnyAvailableRelic(ancient.Owner));
    }

    /// <summary>
    /// 取得遗物时按仅保留可用档位后的原始权重抽取稀有度，再获得对应遗物。
    /// </summary>
    public override async Task AfterObtained()
    {
        WeightedList<RelicRarity> rarities = [];
        foreach ((RelicRarity rarity, int weight) in WeightedRarities)
        {
            if (GetAvailableRelics(Owner, rarity).Count > 0)
            {
                rarities.Add(rarity, weight);
            }
        }

        if (rarities.Count == 0)
        {
            return;
        }

        RelicRarity selectedRarity = rarities.GetRandom(Owner.RunState.Rng.TreasureRoomRelics);
        RelicModel? canonical = Owner.RunState.Rng.TreasureRoomRelics.NextItem(
            GetAvailableRelics(Owner, selectedRarity));
        if (canonical != null)
        {
            await RelicCmd.Obtain(canonical.ToMutable(), Owner);
        }
    }

    /// <summary>
    /// 判断指定玩家的抓取袋中是否至少存在一个合法摘星候选。
    /// </summary>
    /// <param name="player">正在判断事件选项的玩家。</param>
    /// <returns>是否存在合法候选。</returns>
    private static bool HasAnyAvailableRelic(Player player)
    {
        return WeightedRarities.Any(entry =>
            GetAvailableRelics(player, entry.Rarity).Count > 0);
    }

    /// <summary>
    /// 获取指定玩家与稀有度下全部合法的摘星候选。
    /// </summary>
    /// <param name="player">将获得遗物的玩家。</param>
    /// <param name="rarity">待抽取的遗物稀有度。</param>
    /// <returns>合法且未持有的 canonical 遗物列表。</returns>
    private static List<RelicModel> GetAvailableRelics(Player player, RelicRarity rarity)
    {
        return ModelDb.AllRelics
            .Where(relic => relic.Rarity == rarity && IsLegalCandidate(player, relic))
            .ToList();
    }

    /// <summary>
    /// 判断玩家是否可通过摘星获得指定遗物。
    /// </summary>
    /// <param name="player">将获得遗物的玩家。</param>
    /// <param name="relic">待检查的遗物。</param>
    /// <returns>候选是否属于角色或共享卡池、未持有且不是事件舞台装置。</returns>
    private static bool IsLegalCandidate(Player player, RelicModel relic)
    {
        bool compatiblePool = relic.Pool == player.Character.RelicPool ||
                              relic.Pool == ModelDb.RelicPool<SharedRelicPool>();
        return compatiblePool &&
               relic is not GiraffeStageDeviceRelic &&
               relic.IsAllowed(player.RunState) &&
               player.Relics.All(owned => owned.Id != relic.Id);
    }
}
