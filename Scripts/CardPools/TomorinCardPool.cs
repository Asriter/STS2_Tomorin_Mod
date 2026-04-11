using BaseLib.Abstracts;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using STS2_Tomorin_Mod.Cards;
using STS2_Tomorin_Mod.Characters;

namespace STS2_Tomorin_Mod.CardPools;

public class TomorinCardPool : CustomCardPoolModel
{
    public override string Title => "tomorin";
    // public override string EnergyColorName  => "tomorin";
    public override bool IsColorless => false;
    // public override string CardFrameMaterialPath => "card_frame_tomorin";
    public override Color DeckEntryCardColor => Tomorin.DefaultColor;
    public override Color EnergyOutlineColor => Tomorin.OutlineColor;
    
    protected override CardModel[] GenerateAllCards()
    {
        return
        [
        ];
    }
    
    public override string? BigEnergyIconPath => MainFile.BigEnergyIconPath;

    public override string? TextEnergyIconPath => MainFile.TextEnergyIconPath;

    public override float H => 0.537f;
    public override float S => 1.422f;
    public override float V => 1;
}
