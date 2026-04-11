using BaseLib.Extensions;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace STS2_Tomorin_Mod.Localization.DynamicVars;

public class ComposeVar : DynamicVar
{
	public const string DefaultName = "Compose";
   
    private readonly Dictionary<CardType, int>? _costCards;
    
    public Dictionary<CardType, int>? CostCards => _costCards;
    
    public string Title;
    
    //各种判断相关
    public bool isCostAttack = false;
    public int atkCost = 0;
    public bool isCostSkill = false;
    public int skillCost = 0;
    public bool isCostState = false;
    public int stateCost = 0;
    
    //各参数名称
    public const string isCostAttackName = "isCostAttack";
    public const string isCostSkillName = "isCostSkill";
    public const string isCostStateName = "isCostState";
    public const string atkCostName = "atkCost";
    public const string skillCostName = "skillCost";
    public const string stateCostName = "stateCost";
    public const string titleName = "cardName";

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="costCards">消耗的卡的类型和数量</param>
    /// <param name="targetCard">要生成的卡</param>
    public ComposeVar(Dictionary<CardType, int> costCards, CardModel targetCard) : base(DefaultName, 0m)
    {
        _costCards = costCards;
        Title = targetCard.TitleLocString.GetFormattedText();
        
        //根据costCards进行判断
        foreach (CardType type in costCards.Keys)
        {
            switch (type)
            {
                case CardType.Attack:
                    isCostAttack = true;
                    atkCost = costCards[type];
                    break;
                case CardType.Skill:
                    isCostSkill = true;
                    skillCost = costCards[type];
                    break;
                case CardType.Status:
                    isCostState = true;
                    stateCost = costCards[type];
                    break;
            }
        }
    }
}