using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Face-by-face manifest for the 108-card Standard + EX set and the 52-card
/// Military set. The Standard/EX list follows the classic printed suit/rank
/// table; the Military set has exactly one card at every suit/rank pair.
/// </summary>
public static class OfficialDeckCatalog
{
    public const int StandardAndExCardCount = 108;
    public const int MilitaryCardCount = 52;

    public static List<CardData> CreateStandardMilitaryDeck()
    {
        List<CardData> cards = new List<CardData>(StandardAndExCardCount + MilitaryCardCount);
        AddStandardAndExCards(cards);
        AddMilitaryCards(cards);

        if (cards.Count != StandardAndExCardCount + MilitaryCardCount)
        {
            Debug.LogError("官方标准军争牌表数量错误：" + cards.Count + "，预期160张");
        }

        ValidatePackageCounts(cards, "标准/EX", 53, 36, 19);
        ValidatePackageCounts(cards, "军争篇", 32, 14, 6);

        return cards;
    }

    private static void ValidatePackageCounts(
        List<CardData> cards,
        string packageName,
        int expectedBasic,
        int expectedTrick,
        int expectedEquip
    )
    {
        int basic = 0;
        int trick = 0;
        int equip = 0;
        foreach (CardData card in cards)
        {
            if (card == null || card.cardPackage != packageName)
            {
                continue;
            }

            switch (card.category)
            {
                case CardCategory.Basic: basic++; break;
                case CardCategory.Trick: trick++; break;
                case CardCategory.Equip: equip++; break;
            }
        }

        if (basic != expectedBasic || trick != expectedTrick || equip != expectedEquip)
        {
            Debug.LogError(
                packageName + "分类数量不符：基本 " + basic + " / 锦囊 " + trick + " / 装备 " + equip
            );
        }
    }

    public static bool IsOfficialCardName(string cardName)
    {
        return cardName == "杀"
            || cardName == "火杀"
            || cardName == "雷杀"
            || cardName == "闪"
            || cardName == "桃"
            || cardName == "酒"
            || cardName == "闪电"
            || cardName == "八卦阵"
            || cardName == "雌雄双股剑"
            || cardName == "寒冰剑"
            || cardName == "过河拆桥"
            || cardName == "拆桥"
            || cardName == "顺手牵羊"
            || cardName == "青龙偃月刀"
            || cardName == "绝影"
            || cardName == "乐不思蜀"
            || cardName == "南蛮入侵"
            || cardName == "无懈可击"
            || cardName == "丈八蛇矛"
            || cardName == "大宛"
            || cardName == "桃园结义"
            || cardName == "万箭齐发"
            || cardName == "五谷丰登"
            || cardName == "麒麟弓"
            || cardName == "赤兔"
            || cardName == "无中生有"
            || cardName == "爪黄飞电"
            || cardName == "诸葛连弩"
            || cardName == "仁王盾"
            || cardName == "的卢"
            || cardName == "借刀杀人"
            || cardName == "贯石斧"
            || cardName == "方天画戟"
            || cardName == "紫骍"
            || cardName == "古锭刀"
            || cardName == "藤甲"
            || cardName == "兵粮寸断"
            || cardName == "铁索连环"
            || cardName == "白银狮子"
            || cardName == "朱雀羽扇"
            || cardName == "火攻"
            || cardName == "骅骝";
    }

    private static void AddStandardAndExCards(List<CardData> cards)
    {
        AddFace(cards, CardSuit.Spade, 1, "闪电", "决斗");
        AddFace(cards, CardSuit.Spade, 2, "八卦阵", "雌雄双股剑", "寒冰剑");
        AddFace(cards, CardSuit.Spade, 3, "过河拆桥", "顺手牵羊");
        AddFace(cards, CardSuit.Spade, 4, "过河拆桥", "顺手牵羊");
        AddFace(cards, CardSuit.Spade, 5, "青龙偃月刀", "绝影");
        AddFace(cards, CardSuit.Spade, 6, "乐不思蜀", "青釭剑");
        AddFace(cards, CardSuit.Spade, 7, "南蛮入侵", "杀");
        AddFace(cards, CardSuit.Spade, 8, "杀", "杀");
        AddFace(cards, CardSuit.Spade, 9, "杀", "杀");
        AddFace(cards, CardSuit.Spade, 10, "杀", "杀");
        AddFace(cards, CardSuit.Spade, 11, "无懈可击", "顺手牵羊");
        AddFace(cards, CardSuit.Spade, 12, "丈八蛇矛", "过河拆桥");
        AddFace(cards, CardSuit.Spade, 13, "大宛", "南蛮入侵");

        AddFace(cards, CardSuit.Heart, 1, "桃园结义", "万箭齐发");
        AddFace(cards, CardSuit.Heart, 2, "闪", "闪");
        AddFace(cards, CardSuit.Heart, 3, "桃", "五谷丰登");
        AddFace(cards, CardSuit.Heart, 4, "桃", "五谷丰登");
        AddFace(cards, CardSuit.Heart, 5, "麒麟弓", "赤兔");
        AddFace(cards, CardSuit.Heart, 6, "桃", "乐不思蜀");
        AddFace(cards, CardSuit.Heart, 7, "桃", "无中生有");
        AddFace(cards, CardSuit.Heart, 8, "桃", "无中生有");
        AddFace(cards, CardSuit.Heart, 9, "桃", "无中生有");
        AddFace(cards, CardSuit.Heart, 10, "杀", "杀");
        AddFace(cards, CardSuit.Heart, 11, "杀", "无中生有");
        AddFace(cards, CardSuit.Heart, 12, "桃", "过河拆桥", "闪电");
        AddFace(cards, CardSuit.Heart, 13, "爪黄飞电", "闪");

        AddFace(cards, CardSuit.Club, 1, "诸葛连弩", "决斗");
        AddFace(cards, CardSuit.Club, 2, "八卦阵", "杀", "仁王盾");
        AddFace(cards, CardSuit.Club, 3, "过河拆桥", "杀");
        AddFace(cards, CardSuit.Club, 4, "过河拆桥", "杀");
        AddFace(cards, CardSuit.Club, 5, "的卢", "杀");
        AddFace(cards, CardSuit.Club, 6, "乐不思蜀", "杀");
        AddFace(cards, CardSuit.Club, 7, "南蛮入侵", "杀");
        AddFace(cards, CardSuit.Club, 8, "杀", "杀");
        AddFace(cards, CardSuit.Club, 9, "杀", "杀");
        AddFace(cards, CardSuit.Club, 10, "杀", "杀");
        AddFace(cards, CardSuit.Club, 11, "杀", "杀");
        AddFace(cards, CardSuit.Club, 12, "借刀杀人", "无懈可击");
        AddFace(cards, CardSuit.Club, 13, "借刀杀人", "无懈可击");

        AddFace(cards, CardSuit.Diamond, 1, "诸葛连弩", "决斗");
        AddFace(cards, CardSuit.Diamond, 2, "闪", "闪");
        AddFace(cards, CardSuit.Diamond, 3, "闪", "顺手牵羊");
        AddFace(cards, CardSuit.Diamond, 4, "闪", "顺手牵羊");
        AddFace(cards, CardSuit.Diamond, 5, "闪", "贯石斧");
        AddFace(cards, CardSuit.Diamond, 6, "闪", "杀");
        AddFace(cards, CardSuit.Diamond, 7, "闪", "杀");
        AddFace(cards, CardSuit.Diamond, 8, "杀", "闪");
        AddFace(cards, CardSuit.Diamond, 9, "杀", "闪");
        AddFace(cards, CardSuit.Diamond, 10, "杀", "闪");
        AddFace(cards, CardSuit.Diamond, 11, "闪", "闪");
        AddFace(cards, CardSuit.Diamond, 12, "桃", "方天画戟", "无懈可击");
        AddFace(cards, CardSuit.Diamond, 13, "杀", "紫骍");
    }

    private static void AddMilitaryCards(List<CardData> cards)
    {
        AddFace(cards, CardSuit.Heart, 1, "无懈可击", true);
        AddFace(cards, CardSuit.Heart, 2, "火攻", true);
        AddFace(cards, CardSuit.Heart, 3, "火攻", true);
        AddFace(cards, CardSuit.Heart, 4, "火杀", true);
        AddFace(cards, CardSuit.Heart, 5, "桃", true);
        AddFace(cards, CardSuit.Heart, 6, "桃", true);
        AddFace(cards, CardSuit.Heart, 7, "火杀", true);
        AddFace(cards, CardSuit.Heart, 8, "闪", true);
        AddFace(cards, CardSuit.Heart, 9, "闪", true);
        AddFace(cards, CardSuit.Heart, 10, "火杀", true);
        AddFace(cards, CardSuit.Heart, 11, "闪", true);
        AddFace(cards, CardSuit.Heart, 12, "闪", true);
        AddFace(cards, CardSuit.Heart, 13, "无懈可击", true);

        AddFace(cards, CardSuit.Club, 1, "白银狮子", true);
        AddFace(cards, CardSuit.Club, 2, "藤甲", true);
        AddFace(cards, CardSuit.Club, 3, "酒", true);
        AddFace(cards, CardSuit.Club, 4, "兵粮寸断", true);
        AddFace(cards, CardSuit.Club, 5, "雷杀", true);
        AddFace(cards, CardSuit.Club, 6, "雷杀", true);
        AddFace(cards, CardSuit.Club, 7, "雷杀", true);
        AddFace(cards, CardSuit.Club, 8, "雷杀", true);
        AddFace(cards, CardSuit.Club, 9, "酒", true);
        AddFace(cards, CardSuit.Club, 10, "铁索连环", true);
        AddFace(cards, CardSuit.Club, 11, "铁索连环", true);
        AddFace(cards, CardSuit.Club, 12, "铁索连环", true);
        AddFace(cards, CardSuit.Club, 13, "铁索连环", true);

        AddFace(cards, CardSuit.Spade, 1, "古锭刀", true);
        AddFace(cards, CardSuit.Spade, 2, "藤甲", true);
        AddFace(cards, CardSuit.Spade, 3, "酒", true);
        AddFace(cards, CardSuit.Spade, 4, "雷杀", true);
        AddFace(cards, CardSuit.Spade, 5, "雷杀", true);
        AddFace(cards, CardSuit.Spade, 6, "雷杀", true);
        AddFace(cards, CardSuit.Spade, 7, "雷杀", true);
        AddFace(cards, CardSuit.Spade, 8, "雷杀", true);
        AddFace(cards, CardSuit.Spade, 9, "酒", true);
        AddFace(cards, CardSuit.Spade, 10, "兵粮寸断", true);
        AddFace(cards, CardSuit.Spade, 11, "铁索连环", true);
        AddFace(cards, CardSuit.Spade, 12, "铁索连环", true);
        AddFace(cards, CardSuit.Spade, 13, "无懈可击", true);

        AddFace(cards, CardSuit.Diamond, 1, "朱雀羽扇", true);
        AddFace(cards, CardSuit.Diamond, 2, "桃", true);
        AddFace(cards, CardSuit.Diamond, 3, "桃", true);
        AddFace(cards, CardSuit.Diamond, 4, "火杀", true);
        AddFace(cards, CardSuit.Diamond, 5, "火杀", true);
        AddFace(cards, CardSuit.Diamond, 6, "闪", true);
        AddFace(cards, CardSuit.Diamond, 7, "闪", true);
        AddFace(cards, CardSuit.Diamond, 8, "闪", true);
        AddFace(cards, CardSuit.Diamond, 9, "酒", true);
        AddFace(cards, CardSuit.Diamond, 10, "闪", true);
        AddFace(cards, CardSuit.Diamond, 11, "闪", true);
        AddFace(cards, CardSuit.Diamond, 12, "火攻", true);
        AddFace(cards, CardSuit.Diamond, 13, "骅骝", true);
    }

    private static void AddFace(List<CardData> cards, CardSuit suit, int rank, params string[] cardNames)
    {
        for (int i = 0; i < cardNames.Length; i++)
        {
            cards.Add(CreateCard(cardNames[i], suit, rank, false));
        }
    }

    private static void AddFace(List<CardData> cards, CardSuit suit, int rank, string cardName, bool military)
    {
        cards.Add(CreateCard(cardName, suit, rank, military));
    }

    private static CardData CreateCard(string name, CardSuit suit, int rank, bool military)
    {
        CardData card = ScriptableObject.CreateInstance<CardData>();
        card.name = name + "_" + suit + "_" + rank + (military ? "_Military" : "_StandardEx");
        card.cardName = name;
        card.suit = suit;
        card.rank = rank;
        card.value = 1;
        card.isOfficialPackageCard = true;
        card.cardPackage = military ? "军争篇" : "标准/EX";
        ConfigureRules(card);
        return card;
    }

    private static void ConfigureRules(CardData card)
    {
        string name = card.cardName;
        card.needTarget = false;
        card.description = "官方牌库牌面；花色与点数按实体牌表固定。";

        if (name == "杀" || name == "火杀" || name == "雷杀")
        {
            card.category = CardCategory.Basic;
            card.effectType = CardEffectType.Attack;
            card.damageElement = name == "火杀"
                ? CardDamageElement.Fire
                : name == "雷杀" ? CardDamageElement.Thunder : CardDamageElement.Normal;
            card.description = name == "杀" ? "出牌阶段对一名其他角色使用；目标可打出闪响应。" : "火属性杀，目标可打出闪响应。";
        }
        else if (name == "闪")
        {
            card.category = CardCategory.Basic;
            card.effectType = CardEffectType.Defense;
            card.description = "响应杀时打出，抵消该杀。";
        }
        else if (name == "桃")
        {
            card.category = CardCategory.Basic;
            card.effectType = CardEffectType.Heal;
            card.description = "出牌阶段回复1点体力；濒死时可用于自救或救援。";
        }
        else if (name == "酒")
        {
            card.category = CardCategory.Basic;
            card.effectType = CardEffectType.Wine;
            card.description = "出牌阶段使用后，本回合下一张杀伤害+1；濒死时可自救。";
        }
        else if (name == "诸葛连弩" || name == "雌雄双股剑" || name == "寒冰剑"
            || name == "青龙偃月刀" || name == "青釭剑" || name == "丈八蛇矛"
            || name == "麒麟弓" || name == "贯石斧" || name == "方天画戟"
            || name == "古锭刀" || name == "朱雀羽扇")
        {
            card.category = CardCategory.Equip;
            card.effectType = CardEffectType.Weapon;
            card.weaponRange = GetWeaponRange(name);
            card.description = "武器牌。攻击距离：" + card.weaponRange + "；武器特效由对局规则结算。";
        }
        else if (name == "八卦阵" || name == "仁王盾" || name == "藤甲" || name == "白银狮子")
        {
            card.category = CardCategory.Equip;
            card.effectType = CardEffectType.Armor;
            if (name == "八卦阵")
            {
                card.description = "被杀时进行判定；红色视为打出闪。";
            }
            else if (name == "仁王盾")
            {
                card.description = "受到黑色普通杀时判定；红色抵消该杀。";
            }
            else if (name == "藤甲")
            {
                card.description = "受到南蛮入侵或万箭齐发时判定；红色抵消。受到火焰伤害时伤害+1。";
            }
            else
            {
                card.description = "受到大于1点的伤害时判定；红色将伤害降为1。失去此装备时判红回复1点体力。";
            }
        }
        else if (name == "大宛" || name == "紫骍")
        {
            card.category = CardCategory.Equip;
            card.effectType = CardEffectType.MountMinus;
            card.description = "进攻坐骑：计算与其他角色的距离时，攻击范围+1。";
        }
        else if (name == "绝影" || name == "赤兔" || name == "爪黄飞电" || name == "的卢" || name == "骅骝")
        {
            card.category = CardCategory.Equip;
            card.effectType = CardEffectType.MountPlus;
            card.description = "防御坐骑：其他角色计算与你的距离时，距离+1。";
        }
        else
        {
            card.category = CardCategory.Trick;
            ConfigureTrick(card);
        }
    }

    private static int GetWeaponRange(string name)
    {
        switch (name)
        {
            case "诸葛连弩": return 1;
            case "雌雄双股剑": return 2;
            case "寒冰剑": return 2;
            case "青龙偃月刀": return 3;
            case "青釭剑": return 2;
            case "丈八蛇矛": return 3;
            case "麒麟弓": return 5;
            case "贯石斧": return 3;
            case "方天画戟": return 4;
            case "古锭刀": return 2;
            case "朱雀羽扇": return 4;
            default: return 1;
        }
    }

    private static void ConfigureTrick(CardData card)
    {
        switch (card.cardName)
        {
            case "无中生有":
                card.effectType = CardEffectType.DrawCards;
                card.value = 2;
                card.description = "摸两张牌。";
                break;
            case "过河拆桥":
                card.effectType = CardEffectType.DiscardTarget;
                card.needTarget = true;
                card.description = "弃置一名其他角色区域里的一张牌。";
                break;
            case "顺手牵羊":
                card.effectType = CardEffectType.StealTarget;
                card.needTarget = true;
                card.description = "获得一名其他角色区域里的一张牌。";
                break;
            case "决斗":
                card.effectType = CardEffectType.Duel;
                card.needTarget = true;
                card.description = "与一名其他角色进行决斗，未能打出杀的一方受到1点伤害。";
                break;
            case "借刀杀人":
                card.effectType = CardEffectType.BorrowWeapon;
                card.needTarget = true;
                card.description = "令一名装备武器的角色对你指定的另一名角色使用杀；否则你获得其武器。";
                break;
            case "无懈可击":
                card.effectType = CardEffectType.Nullification;
                card.description = "抵消一张对角色生效的锦囊牌。";
                break;
            case "桃园结义":
                card.effectType = CardEffectType.RecoverAll;
                card.description = "所有受伤角色各回复1点体力。";
                break;
            case "五谷丰登":
                card.effectType = CardEffectType.Harvest;
                card.description = "亮出等同于存活角色数的牌，按行动顺序各选择获得一张。";
                break;
            case "南蛮入侵":
            case "万箭齐发":
                card.effectType = CardEffectType.AreaDamage;
                card.description = card.cardName == "南蛮入侵"
                    ? "其他角色需打出杀，否则受到1点伤害。"
                    : "其他角色需打出闪，否则受到1点伤害。";
                break;
            case "乐不思蜀":
                card.effectType = CardEffectType.DelayedSkipPlay;
                card.needTarget = true;
                card.description = "延时锦囊。目标判定不为红桃时跳过出牌阶段。";
                break;
            case "兵粮寸断":
                card.effectType = CardEffectType.DelayedSkipDraw;
                card.needTarget = true;
                card.description = "延时锦囊。目标判定不为梅花时跳过摸牌阶段。";
                break;
            case "闪电":
                card.effectType = CardEffectType.Lightning;
                card.description = "延时锦囊。判定为黑桃2至9时受到3点雷电伤害。";
                break;
            case "铁索连环":
                card.effectType = CardEffectType.Chain;
                card.needTarget = true;
                card.description = "重铸或令一名角色横置/重置；横置角色受到属性伤害时会传导。";
                break;
            case "火攻":
                card.effectType = CardEffectType.FireAttack;
                card.needTarget = true;
                card.damageElement = CardDamageElement.Fire;
                card.description = "令一名有手牌的角色展示一张手牌；你弃置同花色牌后对其造成1点火焰伤害。";
                break;
            default:
                card.effectType = CardEffectType.DirectDamage;
                card.description = "锦囊牌；对一名其他角色造成1点伤害。";
                break;
        }
    }
}
