using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models.Relics;

namespace STS2_Tomorin_Mod.Patch;

[HarmonyPatch(typeof(CardRarityExtensions), "GetNextHighestRarity")]
internal class TestPatch
{
    [HarmonyPrefix]
    private static bool Custom(ref CardRarity cardRarity, ref CardRarity __result)
    {
        if (cardRarity == CardRarity.Rare)
        {
            Log.Info("卡牌稀有度为none，太抽象了：" + cardRarity);
            __result = CardRarity.Common;
            return false;
        }

        return true;
    }
    
}