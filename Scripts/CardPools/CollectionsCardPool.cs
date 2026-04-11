using BaseLib.Abstracts;
using Godot;
using MegaCrit.Sts2.Core.Models;
using STS2_Tomorin_Mod.Cards.Collections;

namespace STS2_Tomorin_Mod.CardPools;

public class CollectionsCardPool : CustomCardPoolModel
{
    public override string Title => "collections";

    public override string EnergyColorName => "colorless";

    public override string CardFrameMaterialPath => "card_frame_colorless";
    
    public override Color DeckEntryCardColor => Colors.White;
    
    public override bool IsColorless => false;

    public override bool IsShared => true;

    protected override CardModel[] GenerateAllCards()
	{
		return
		[
		];
	}
	
	
}