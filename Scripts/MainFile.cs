using BaseLib.Config;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using FileAccess = Godot.FileAccess;

namespace STS2_Tomorin_Mod;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Godot.Node
{
    public const string ModId = "STS2_Tomorin_Mod"; //At the moment, this is used only for the Logger and harmony names.
    
    public const string BigEnergyIconPath =  "res://STS2_Tomorin_Mod/images/atlases/ui_atlas.sprites/card/energy_tomorin.tres";
    public const string TextEnergyIconPath = "res://STS2_Tomorin_Mod/images/packed/sprite_fonts/tomorin_energy_icon.png";

    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } = new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    public static void Initialize()
    {
        Harmony harmony = new(ModId);

        harmony.PatchAll();
        
        //加载设置
        ModConfigRegistry.Register(ModId, new TomorinModConfig());
    }
}
