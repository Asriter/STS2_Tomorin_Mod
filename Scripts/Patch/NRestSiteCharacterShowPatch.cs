using BaseLib.Abstracts;
using BaseLib.Utils.NodeFactories;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.RestSite;

namespace STS2_Tomorin_Mod.Patch;

[HarmonyPatch(typeof(NRestSiteCharacter), "ShowSelectedRestSiteOption")]
internal class NRestSiteCharacterShowPatch
{
    [HarmonyPrefix]
    private static bool Custom(ref RestSiteOption option)
    {
       //临时处理，直接跳过

        return false;
    }
}

