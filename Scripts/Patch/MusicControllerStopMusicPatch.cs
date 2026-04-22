using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Audio;
using STS2_Tomorin_Mod.Audio;

namespace STS2_Tomorin_Mod.Patch;

[HarmonyPatch(typeof(NRunMusicController), "StopMusic")]
public class MusicControllerStopMusicPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        CustomAudioController.StopMusic();
    }
}
