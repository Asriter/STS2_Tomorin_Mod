using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Audio;
using STS2_Tomorin_Mod.Audio;

namespace STS2_Tomorin_Mod.Patch;

[HarmonyPatch(typeof(NRunMusicController), "UpdateTrack", [typeof(string), typeof(float)])]
public class MusicControllerUpdateTrackPatch
{
    [HarmonyPostfix]
    private static void Postfix(string label, float trackIndex)
    {
        if (Math.Abs(trackIndex - 7) < 0.001f)
            CustomAudioController.StopMusic();
    }
}
