using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using STS2_Tomorin_Mod.Encounters;

namespace STS2_Tomorin_Mod.Patch;

/// <summary>
/// 批量patch所有场景并设置boss
/// </summary>
[HarmonyPatch]
public class ActModelBossOrderPatch
{
    static IEnumerable<MethodInfo?> TargetMethods()
    {
        var parentType = typeof(ActModel); 

        var classList = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => assembly.FullName != null && assembly.FullName.Contains("sts2"))
            .SelectMany(s => s.GetTypes())
            .Where(p => parentType.IsAssignableFrom(p) && p.IsClass && !p.IsAbstract);
        // 获取所有已加载程序集中，继承自 ActModel 且不是抽象类的子类
        var list = classList.Select(t =>
            t.GetProperty("BossDiscoveryOrder", BindingFlags.Public | BindingFlags.Instance)
                ?.GetGetMethod(nonPublic: false));

        return list;
    }

    [HarmonyPriority(Priority.Last)]
    [HarmonyPostfix]
    public static void Postfix(object __instance, ref IEnumerable<EncounterModel> __result)
    {
        if (TomorinModConfig.LoadModBoss)
        {
            //根据房间类型判断要给的boss
            if (__instance is Underdocks)
                __result = [ModelDb.Encounter<AnonBoss>()];
        }
    }
}