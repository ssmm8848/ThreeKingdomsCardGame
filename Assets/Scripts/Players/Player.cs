using System.Collections.Generic;
using UnityEngine;

public enum TeamType
{
    None,
    Overlord, // 霸主
    Alliance, // 协战
}

// 灵契之争身份类型
public enum IdentityType
{
    None,
    Commander, // 执令者（公开）
    Protector, // 护令者（隐藏）
    Rebel, // 逆契者（隐藏）
    Wanderer, // 游离者（隐藏）
}

public class Player : MonoBehaviour
{
    public const int CommanderSuspicionRevealThreshold = 2;

    [Header("基础信息")]
    public string playerName;

    [Header("控制与阵营")]
    public bool isAI = false;
    public TeamType teamType = TeamType.None;

    [Header("灵契之争身份")]
    public IdentityType identityType = IdentityType.None;
    public bool isIdentityRevealed = false;

    [Header("嫌疑标记（五人场）")]
    public bool hasAttackedCommander = false; // 是否攻击过执令者
    public int commanderSuspicion = 0;

    [Header("临时护盾")]
    public int protectionValue = 0;
    // Counts this character's turn-end cleanups, not global turns.
    public int protectionTurnsRemaining = 0;

    [Header("血量")]
    public int maxHp = 4;
    public int currentHp = 4;
    public bool isDead;

    [Header("出牌限制")]
    public int usedAttackCountThisTurn = 0;

    [Header("装备触发状态")]
    public bool armorUsedThisTurn = false;
    public bool usedActiveSkillThisTurn = false;
    public bool usedZhaoChenThisTurn = false;
    public bool usedZhenCeThisTurn = false;
    public bool usedQiaoZhuThisTurn = false;
    public bool usedJieFengThisTurn = false;
    public bool usedGuanXiangThisTurn = false;

    [Header("手牌")]
    public List<CardData> handCards = new List<CardData>();

    [Header("装备区")]
    public CardData weapon;
    public CardData armor;
    public CardData treasure;
    public CardData offensiveHorse;
    public CardData defensiveHorse;

    [Header("军争牌状态")]
    public bool chained;
    public bool hasWineBoost;
    public bool usedWineThisTurn;
    public bool skipDrawThisTurn;
    public bool skipPlayThisTurn;
    public List<CardData> delayedTricks = new List<CardData>();
    [System.NonSerialized]
    public Player qingLongFollowUpTarget;

    [Header("角色数据")]
    public CharacterData characterData;

    public void InitializePlayer()
    {
        if (characterData != null)
        {
            playerName = characterData.characterName;
            maxHp = characterData.maxHp;
        }

        currentHp = maxHp;
        isDead = false;
        handCards.Clear();

        weapon = null;
        armor = null;
        treasure = null;
        offensiveHorse = null;
        defensiveHorse = null;
        chained = false;
        delayedTricks.Clear();

        isIdentityRevealed = false;
        hasAttackedCommander = false;
        commanderSuspicion = 0;
        protectionValue = 0;
        protectionTurnsRemaining = 0;

        ResetTurnState();

        Debug.Log(playerName + " 初始化完成，血量：" + currentHp + "/" + maxHp);
    }

    public CardData GetEquipment(CardEffectType equipType)
    {
        switch (equipType)
        {
            case CardEffectType.Weapon:
                return weapon;

            case CardEffectType.Armor:
                return armor;

            case CardEffectType.Treasure:
                return treasure;

            case CardEffectType.MountPlus:
                return defensiveHorse;

            case CardEffectType.MountMinus:
                return offensiveHorse;

            default:
                return null;
        }
    }

    public void DrawCard(CardData card)
    {
        if (card == null)
        {
            Debug.LogWarning($"{playerName} 抽牌失败：卡牌为空");
            return;
        }

        handCards.Add(card);
        Debug.Log(isAI
            ? $"{playerName} 摸了一张牌（手牌 {handCards.Count} 张）"
            : $"{playerName} 抽到了：{card.cardName} · {card.GetSuitRankLabel()}");
        if (!isAI)
        {
            GameSfxPlayer.PlayCardDraw();
        }
    }

    public void SetCharacter(CharacterData data)
    {
        characterData = data;

        if (characterData == null)
        {
            Debug.LogWarning(playerName + " 设置角色失败：角色数据为空");
            return;
        }

        playerName = characterData.characterName;
        maxHp = characterData.maxHp;
        currentHp = maxHp;

        Debug.Log("角色设置完成：" + playerName + "，体力：" + currentHp + "/" + maxHp);
    }

    public void RemoveCard(CardData card)
    {
        if (card == null)
        {
            return;
        }

        if (handCards.Contains(card))
        {
            handCards.Remove(card);
            Debug.Log($"{playerName} 失去了手牌：{card.cardName}");
        }
    }

    public void RemoveCardSilently(CardData card)
    {
        if (card != null && handCards.Contains(card))
        {
            handCards.Remove(card);
        }
    }

    public void TakeDamage(int amount)
    {
        if (amount > 0)
        {
            GameSfxPlayer.PlayDamageImpact();
        }

        currentHp -= amount;

        if (currentHp < 0)
        {
            currentHp = 0;
        }

        Debug.Log($"{playerName} 受到 {amount} 点伤害，当前血量：{currentHp}/{maxHp}");

        if (currentHp <= 0)
        {
            Debug.Log($"{playerName} 已经阵亡");
        }
    }

    public void LoseHp(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        GameSfxPlayer.PlayDamageImpact();
        currentHp = Mathf.Max(0, currentHp - amount);
        Debug.Log($"{playerName} 失去 {amount} 点体力，当前血量：{currentHp}/{maxHp}");

        if (currentHp <= 0)
        {
            Debug.Log($"{playerName} 已经阵亡");
        }
    }

    public void Heal(int amount)
    {
        currentHp += amount;

        if (currentHp > maxHp)
        {
            currentHp = maxHp;
        }

        Debug.Log($"{playerName} 回复 {amount} 点体力，当前血量：{currentHp}/{maxHp}");
    }

    public void ResetTurnState()
    {
        usedAttackCountThisTurn = 0;
        armorUsedThisTurn = false;
        usedActiveSkillThisTurn = false;
        usedZhaoChenThisTurn = false;
        usedZhenCeThisTurn = false;
        usedQiaoZhuThisTurn = false;
        usedJieFengThisTurn = false;
        usedGuanXiangThisTurn = false;
        hasWineBoost = false;
        usedWineThisTurn = false;
        skipDrawThisTurn = false;
        skipPlayThisTurn = false;
        qingLongFollowUpTarget = null;
    }

    public bool CanUseAttack()
    {
        return usedAttackCountThisTurn < GetAttackLimit();
    }

    public int GetAttackLimit()
    {
        int limit = 1;

        // 三人场霸主基础出杀上限为2
        if (GameSelectionData.gameMode == GameMode.ThreePlayer && teamType == TeamType.Overlord)
        {
            limit = 2;
        }

        // 五人场执令者出杀上限为2
        if (GameSelectionData.gameMode == GameMode.FivePlayer && identityType == IdentityType.Commander)
        {
            limit = 2;
        }

        if (weapon != null && weapon.cardName == "诸葛连弩")
        {
            return 99;
        }

        if (weapon != null && weapon.cardName == "寒星弩")
        {
            limit += 1;
        }

        return limit;
    }

    public CardData GetFirstEquipment()
    {
        if (weapon != null)
            return weapon;
        if (armor != null)
            return armor;
        if (treasure != null)
            return treasure;
        if (offensiveHorse != null)
            return offensiveHorse;
        if (defensiveHorse != null)
            return defensiveHorse;
        return null;
    }

    public void RemoveEquipment(CardData equip)
    {
        if (weapon == equip)
        {
            weapon = null;
        }
        else if (armor == equip)
        {
            armor = null;
        }
        else if (treasure == equip)
        {
            treasure = null;
        }
        else if (offensiveHorse == equip)
        {
            offensiveHorse = null;
        }
        else if (defensiveHorse == equip)
        {
            defensiveHorse = null;
        }

    }

    public bool HasEquipment()
    {
        return weapon != null
            || armor != null
            || treasure != null
            || offensiveHorse != null
            || defensiveHorse != null;
    }

    public void RecordUseAttack()
    {
        usedAttackCountThisTurn++;
    }
}
