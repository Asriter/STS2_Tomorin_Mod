using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Audio;
using STS2_Tomorin_Mod.Audio;

namespace STS2_Tomorin_Mod.Patch;

[HarmonyPatch(typeof(NRunMusicController), "PlayCustomMusic")]
public class MusicControllerPatch
{
    [HarmonyPrefix]
    private static bool Prefix(NRunMusicController __instance, string customMusic)
    {
        CustomAudioController.StopMusic();
        
        //判断是event还是guid
        if (customMusic.Contains("event"))
            return true;
        
        //走自己的逻辑播放guid，然后跳过原方法
        if (!NonInteractiveMode.IsActive)
        {
            var traverse = Traverse.Create(__instance);
            
            var node = traverse.Field<Node>("_proxy").Value;
            node.Call(traverse.Field<StringName>("_stopMusic").Value);

            //播放我自己的bgm
            CustomAudioController.PlayMusic(customMusic);
        }

        return false;
    }
}