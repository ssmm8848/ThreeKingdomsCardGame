using UnityEngine;

public enum CardCategory
{
    Basic, // 基本牌：杀、闪、桃
    Trick, // 锦囊牌
    Equip, // 装备牌
}

public enum CardSuit
{
    None,
    Spade,
    Heart,
    Club,
    Diamond,
}

public enum CardEffectType
{
    // 基本牌
    Attack, // 杀
    Defense, // 闪
    Heal, // 桃

    // 锦囊牌
    DrawCards, // 摸牌
    DirectDamage, // 直接伤害
    ForceDiscard, // 令目标弃牌
    DiscardTarget, // 弃置目标区域的牌
    DiscardEquip, // 弃置目标装备牌
    Protect, // 防护 / 减伤

    // 装备牌
    Weapon,
    Armor,
    Treasure,

    // Special cards added for the public-suspicion ruleset.
    InvestigateIdentity,
    PlantSuspicion,
    ReduceSuspicion,
    StealTarget, // 选择获得目标区域的一张牌
    AreaDamage, // 对所有其他角色造成伤害
    AidDraw, // 令友方摸牌
    DrawAndDiscard, // 摸牌后弃牌
    SuitJudgement, // 按牌堆顶判定牌结算

    // Official standard / military package card families. Appended to preserve
    // the serialized numeric values already stored in existing ScriptableObjects.
    Wine,
    Duel,
    BorrowWeapon,
    Nullification,
    RecoverAll,
    Harvest,
    DelayedSkipPlay,
    DelayedSkipDraw,
    Lightning,
    Chain,
    FireAttack,
    MountPlus,
    MountMinus,
}

public enum CardDamageElement
{
    Normal,
    Fire,
    Thunder,
}

[CreateAssetMenu(fileName = "NewCard", menuName = "Card Game/Card Data")]
public class CardData : ScriptableObject
{
    [Header("基础信息")]
    public string cardName;

    [TextArea]
    public string description;

    [Header("卡牌分类")]
    public CardCategory category;

    [Header("效果类型")]
    public CardEffectType effectType;

    [Header("卡牌图片")]
    public Sprite artwork;

    [Header("卡牌语音")]
    public AudioClip voiceClip;

    [Header("实体牌面信息（运行时每张牌会有自己的点数和花色）")]
    public CardSuit suit = CardSuit.None;
    [Range(0, 13)]
    public int rank;

    [Header("军争牌面规则信息")]
    public CardDamageElement damageElement = CardDamageElement.Normal;
    [Min(0)]
    public int weaponRange = 1;
    public bool isOfficialPackageCard;
    public string cardPackage;

    [System.NonSerialized]
    public CardData runtimeTemplate;

    [Header("数值")]
    public int value = 1;

    [Header("是否需要目标")]
    public bool needTarget = false;

    [Header("牌堆配置")]
    [Tooltip("勾选后只加入五人身份局牌堆。")]
    public bool fivePlayerOnly = false;

    [Min(0)]
    [Tooltip("补充牌堆中加入的数量。")]
    public int supplementalCopies = 1;

    public CardData GetTemplate()
    {
        return runtimeTemplate != null ? runtimeTemplate : this;
    }

    public string GetSuitName()
    {
        switch (suit)
        {
            case CardSuit.Spade: return "黑桃";
            case CardSuit.Heart: return "红桃";
            case CardSuit.Club: return "梅花";
            case CardSuit.Diamond: return "方块";
            default: return "无花色";
        }
    }

    public string GetRankName()
    {
        switch (rank)
        {
            case 1: return "A";
            case 11: return "J";
            case 12: return "Q";
            case 13: return "K";
            default: return rank >= 2 && rank <= 10 ? rank.ToString() : "?";
        }
    }

    public string GetSuitRankLabel()
    {
        return GetSuitName() + " " + GetRankName();
    }

    public string GetSuitRankRichText()
    {
        string tint = IsRedSuit() ? "#E27761" : "#D9DED8";
        return "<color=" + tint + ">" + GetSuitRankLabel() + "</color>";
    }

    public bool IsRedSuit()
    {
        return suit == CardSuit.Heart || suit == CardSuit.Diamond;
    }
}
