using UnityEngine;

public enum CharacterActiveSkillType
{
    None,
    LoseOneHpDrawTwo,
}


[CreateAssetMenu(fileName = "NewCharacter", menuName = "Card Game/Character Data")]
public class CharacterData : ScriptableObject
{
    [Header("角色基础信息")]
    public string characterName;

    [Header("体力")]
    public int maxHp = 4;

    [Header("技能")]
    public string skillName;

    [TextArea]
    public string skillDescription;

    [Header("技能语音")]
    public AudioClip skillVoice;

    [System.Serializable]
    public class CardVoiceEntry
    {
        public CardData card;
        public AudioClip voiceClip;
    }

    [Header("角色专属卡牌语音")]
    [Tooltip("未配置专属语音的卡牌会使用卡牌自身的通用语音。")]
    public CardVoiceEntry[] cardVoices;

    [Header("主动技能类型")]
    public CharacterActiveSkillType activeSkillType = CharacterActiveSkillType.None;

    [Header("立绘")]
    public Sprite portrait;

    [TextArea]
    public string portraitPrompt;

    [Header("语音")]
    [Tooltip("可选兜底语音，仅在卡牌未配置专属语音时播放。")]
    public AudioClip cardUseVoice;

    public AudioClip GetCardVoice(CardData card)
    {
        if (card == null || cardVoices == null)
        {
            return null;
        }

        CardData sourceCard = card.GetTemplate();

        for (int i = 0; i < cardVoices.Length; i++)
        {
            CardVoiceEntry entry = cardVoices[i];
            if (entry != null && entry.card == sourceCard && entry.voiceClip != null)
            {
                return entry.voiceClip;
            }
        }

        return null;
    }
}
