using BaseLib.Abstracts;
using BaseLib.Utils.NodeFactories;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.RestSite;

namespace STS2_Tomorin_Mod.Patch;

[HarmonyPatch(typeof(NRestSiteCharacter), "Create")]
internal class NRestSiteCharacterPatch
{
    [HarmonyPrefix]
    private static bool Custom(ref Player player, ref int characterIndex, ref NRestSiteCharacter? __result)
    {
        if (player.Character is CustomCharacterModel customCharacter)
        {
            __result = NodeFactory<NRestSiteCharacter>.CreateFromScene(customCharacter.RestSiteAnimPath);
            if (__result != null)
            {
                Traverse.Create(__result).Field("_characterIndex").SetValue(characterIndex);
                Traverse.Create(__result).Property("Player").SetValue(player);
                return false;
            }
        }

        return true;
    }
}

internal class Customcharacter
{
}