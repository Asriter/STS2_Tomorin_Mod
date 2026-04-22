using BaseLib.Config;
using Godot;

namespace STS2_Tomorin_Mod;

[ConfigHoverTipsByDefault]
internal class TomorinModConfig : SimpleModConfig
{
    public static bool LoadModBoss  { get; set; }  = false;
}