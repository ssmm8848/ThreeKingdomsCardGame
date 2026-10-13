using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CardButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("卡牌文字")]
    public TMP_Text cardNameText;
    public TMP_Text cardTypeText;
    public TMP_Text descriptionText;

    [Header("卡牌背景")]
    public Image backgroundImage;

    [Header("悬停设置")]
    public float hoverScale = 1.05f;

    [Header("选中设置")]
    public float selectedScale = 1.08f;

    private Vector3 originalScale;

    private CardData cardData;
    private BattleUI battleUI;
    private int handIndex;

    private bool isSelected = false;
    private Image artworkImage;
    private Image titlePlateImage;
    private Image typePlateImage;
    private Image descriptionPlateImage;
    private Image artPanelImage;
    private Image badgePlateImage;
    private TMP_Text badgeText;
    private CardFaceFrameGraphic frameGraphic;

    void Awake()
    {
        originalScale = transform.localScale;
        CreateCardFaceLayers();
    }

    public void Setup(CardData data, int index, BattleUI ui)
    {
        cardData = data;
        handIndex = index;
        battleUI = ui;

        isSelected = false;

        RefreshCardView();
        RefreshSelectView();
    }

    public void SetLayoutSize(float width, float height)
    {
        RectTransform cardRect = transform as RectTransform;
        if (cardRect != null)
        {
            cardRect.sizeDelta = new Vector2(width, height);
        }

        ConfigureLabel(cardNameText, 20f, 13f);
        ConfigureLabel(cardTypeText, 13f, 10f);
        ConfigureLabel(descriptionText, 12f, 9f);
        ConfigureLabel(badgeText, 19f, 12f);
    }

    private void ConfigureLabel(TMP_Text label, float maxFontSize, float minFontSize)
    {
        if (label == null)
        {
            return;
        }

        label.enableWordWrapping = label == descriptionText;
        label.enableAutoSizing = true;
        label.fontSize = maxFontSize;
        label.fontSizeMax = maxFontSize;
        label.fontSizeMin = minFontSize;
    }

    private void RefreshCardView()
    {
        if (cardData == null)
        {
            return;
        }

        if (cardNameText != null)
        {
            cardNameText.text = cardData.cardName;
        }

        if (cardTypeText != null)
        {
            cardTypeText.text = GetCardTypeName(cardData) + " · " + cardData.GetSuitRankRichText();
        }

        if (descriptionText != null)
        {
            descriptionText.text = cardData.description;
        }

        if (backgroundImage != null)
        {
            backgroundImage.color = new Color(0.035f, 0.05f, 0.068f, 1f);
            backgroundImage.type = Image.Type.Sliced;
        }

        if (artworkImage != null)
        {
            artworkImage.sprite = CardArtLibrary.GetArtwork(cardData);
            artworkImage.enabled = artworkImage.sprite != null;
        }

        if (titlePlateImage != null)
        {
            titlePlateImage.color = new Color(0.055f, 0.075f, 0.095f, 0.98f);
        }

        if (typePlateImage != null)
        {
            Color typeTint = GetCardColor(cardData);
            typeTint.a = 0.96f;
            typePlateImage.color = typeTint;
        }

        if (artPanelImage != null)
        {
            artPanelImage.color = new Color(0.12f, 0.095f, 0.055f, 1f);
        }

        if (badgePlateImage != null)
        {
            Color badgeTint = GetCardColor(cardData);
            badgeTint.a = 1f;
            badgePlateImage.color = badgeTint;
        }

        if (badgeText != null)
        {
            badgeText.text = GetCardMark(cardData);
            badgeText.color = new Color(1f, 0.94f, 0.75f, 1f);
        }

        if (cardNameText != null)
        {
            cardNameText.color = new Color(1f, 0.92f, 0.7f, 1f);
            cardNameText.raycastTarget = false;
            cardNameText.alignment = TextAlignmentOptions.Left;
        }

        if (cardTypeText != null)
        {
            cardTypeText.color = new Color(1f, 0.95f, 0.8f, 1f);
            cardTypeText.raycastTarget = false;
            cardTypeText.alignment = TextAlignmentOptions.Center;
        }

        if (descriptionText != null)
        {
            descriptionText.color = new Color(0.13f, 0.105f, 0.07f, 1f);
            descriptionText.raycastTarget = false;
            descriptionText.alignment = TextAlignmentOptions.Center;
        }
    }

    private void CreateCardFaceLayers()
    {
        if (backgroundImage == null)
        {
            backgroundImage = GetComponent<Image>();
        }

        if (backgroundImage != null)
        {
            backgroundImage.color = new Color(0.035f, 0.05f, 0.068f, 1f);
            backgroundImage.type = Image.Type.Sliced;
        }

        artPanelImage = CreateImageLayer("CardArtWindow", 0);
        if (artPanelImage != null)
        {
            SetAnchors(artPanelImage.rectTransform, new Vector2(0.055f, 0.305f), new Vector2(0.945f, 0.72f));
            artPanelImage.color = new Color(0.12f, 0.095f, 0.055f, 1f);
            artPanelImage.gameObject.AddComponent<RectMask2D>();

            Outline artOutline = artPanelImage.gameObject.AddComponent<Outline>();
            artOutline.effectColor = new Color(0.65f, 0.47f, 0.2f, 0.9f);
            artOutline.effectDistance = new Vector2(1.2f, -1.2f);
            artOutline.useGraphicAlpha = true;

            GameObject artObject = new GameObject(
                "CardArtwork",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(AspectRatioFitter)
            );
            artObject.transform.SetParent(artPanelImage.transform, false);
            artworkImage = artObject.GetComponent<Image>();
            artworkImage.raycastTarget = false;
            artworkImage.preserveAspect = false;

            RectTransform artRect = artworkImage.rectTransform;
            artRect.anchorMin = new Vector2(0.5f, 0.5f);
            artRect.anchorMax = new Vector2(0.5f, 0.5f);
            artRect.pivot = new Vector2(0.5f, 0.5f);
            artRect.sizeDelta = Vector2.zero;

            AspectRatioFitter aspectFitter = artObject.GetComponent<AspectRatioFitter>();
            aspectFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            aspectFitter.aspectRatio = 1.24f;
        }

        titlePlateImage = CreateImageLayer("CardNameplate", 1);
        if (titlePlateImage != null)
        {
            SetAnchors(titlePlateImage.rectTransform, new Vector2(0.045f, 0.815f), new Vector2(0.955f, 0.975f));
            titlePlateImage.color = new Color(0.055f, 0.075f, 0.095f, 0.98f);
        }

        typePlateImage = CreateImageLayer("CardTypeRibbon", 2);
        if (typePlateImage != null)
        {
            SetAnchors(typePlateImage.rectTransform, new Vector2(0.075f, 0.735f), new Vector2(0.925f, 0.81f));
            typePlateImage.color = new Color(0.12f, 0.16f, 0.19f, 0.98f);
        }

        descriptionPlateImage = CreateImageLayer("CardRulesPaper", 3);
        if (descriptionPlateImage != null)
        {
            SetAnchors(descriptionPlateImage.rectTransform, new Vector2(0.055f, 0.035f), new Vector2(0.945f, 0.285f));
            descriptionPlateImage.color = new Color(0.78f, 0.69f, 0.5f, 0.98f);

            Outline rulesOutline = descriptionPlateImage.gameObject.AddComponent<Outline>();
            rulesOutline.effectColor = new Color(0.28f, 0.2f, 0.1f, 0.95f);
            rulesOutline.effectDistance = new Vector2(1f, -1f);
            rulesOutline.useGraphicAlpha = true;
        }

        badgePlateImage = CreateImageLayer("CardSeal", 4);
        if (badgePlateImage != null)
        {
            SetAnchors(badgePlateImage.rectTransform, new Vector2(0.805f, 0.82f), new Vector2(0.95f, 0.965f));
            badgePlateImage.color = new Color(0.36f, 0.25f, 0.1f, 1f);
            Outline sealOutline = badgePlateImage.gameObject.AddComponent<Outline>();
            sealOutline.effectColor = new Color(0.88f, 0.69f, 0.32f, 0.95f);
            sealOutline.effectDistance = new Vector2(1.2f, -1.2f);
            sealOutline.useGraphicAlpha = true;

            GameObject badgeObject = new GameObject(
                "CardMark",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI)
            );
            badgeObject.transform.SetParent(badgePlateImage.transform, false);
            badgeText = badgeObject.GetComponent<TMP_Text>();
            badgeText.rectTransform.anchorMin = Vector2.zero;
            badgeText.rectTransform.anchorMax = Vector2.one;
            badgeText.rectTransform.offsetMin = Vector2.zero;
            badgeText.rectTransform.offsetMax = Vector2.zero;
            if (cardNameText != null)
            {
                badgeText.font = cardNameText.font;
            }
            badgeText.fontSize = 18f;
            badgeText.fontStyle = FontStyles.Bold;
            badgeText.alignment = TextAlignmentOptions.Center;
            badgeText.raycastTarget = false;
        }

        if (cardNameText != null)
        {
            SetAnchors(cardNameText.rectTransform, new Vector2(0.09f, 0.83f), new Vector2(0.78f, 0.96f));
        }
        if (cardTypeText != null)
        {
            SetAnchors(cardTypeText.rectTransform, new Vector2(0.1f, 0.74f), new Vector2(0.9f, 0.805f));
        }
        if (descriptionText != null)
        {
            SetAnchors(descriptionText.rectTransform, new Vector2(0.1f, 0.055f), new Vector2(0.9f, 0.265f));
        }

        GameObject frameObject = new GameObject(
            "CardFrame",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(CardFaceFrameGraphic)
        );
        frameObject.transform.SetParent(transform, false);
        frameObject.transform.SetAsLastSibling();
        RectTransform frameRect = frameObject.GetComponent<RectTransform>();
        SetAnchors(frameRect, Vector2.zero, Vector2.one);
        frameGraphic = frameObject.GetComponent<CardFaceFrameGraphic>();
        frameGraphic.color = Color.white;
        frameGraphic.raycastTarget = false;

        AddTextOutline(cardNameText);
        AddTextOutline(cardTypeText);
        AddTextOutline(badgeText);
    }

    private void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private Image CreateImageLayer(string layerName, int siblingIndex)
    {
        GameObject layer = new GameObject(
            layerName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        layer.transform.SetParent(transform, false);
        layer.transform.SetSiblingIndex(Mathf.Min(siblingIndex, transform.childCount - 1));

        Image image = layer.GetComponent<Image>();
        if (backgroundImage != null)
        {
            image.sprite = backgroundImage.sprite;
            image.type = Image.Type.Sliced;
        }
        image.raycastTarget = false;
        return image;
    }

    private void AddTextOutline(TMP_Text label)
    {
        if (label == null)
        {
            return;
        }

        Outline outline = label.GetComponent<Outline>();
        if (outline == null)
        {
            outline = label.gameObject.AddComponent<Outline>();
        }
        outline.effectColor = new Color(0f, 0f, 0f, 0.88f);
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = true;
    }

    private string GetCardMark(CardData card)
    {
        switch (card.effectType)
        {
            case CardEffectType.Attack: return "刃";
            case CardEffectType.Defense: return "盾";
            case CardEffectType.Heal: return "桃";
            case CardEffectType.DrawCards: return "抽";
            case CardEffectType.DirectDamage: return "炎";
            case CardEffectType.ForceDiscard: return "弃";
            case CardEffectType.DiscardEquip: return "拆";
            case CardEffectType.Weapon: return "武";
            case CardEffectType.Armor: return "甲";
            case CardEffectType.Treasure: return "宝";
            case CardEffectType.DiscardTarget: return card.cardName == "拆桥" ? "拆" : "搜";
            case CardEffectType.Protect: return "护";
            case CardEffectType.InvestigateIdentity: return "察";
            case CardEffectType.PlantSuspicion: return "伪";
            case CardEffectType.ReduceSuspicion: return "洗";
            case CardEffectType.StealTarget: return "夺";
            case CardEffectType.AreaDamage: return "阵";
            case CardEffectType.AidDraw: return "援";
            case CardEffectType.DrawAndDiscard: return "整";
            case CardEffectType.SuitJudgement: return "卜";
            case CardEffectType.Wine: return "酒";
            case CardEffectType.Duel: return "决";
            case CardEffectType.BorrowWeapon: return "借";
            case CardEffectType.Nullification: return "无";
            case CardEffectType.RecoverAll: return "愈";
            case CardEffectType.Harvest: return "谷";
            case CardEffectType.DelayedSkipPlay: return "乐";
            case CardEffectType.DelayedSkipDraw: return "粮";
            case CardEffectType.Lightning: return "雷";
            case CardEffectType.Chain: return "链";
            case CardEffectType.FireAttack: return "火";
            case CardEffectType.MountPlus:
            case CardEffectType.MountMinus: return "马";
            default: return "策";
        }
    }

    public void OnClickCard()
    {
        if (battleUI == null || cardData == null)
        {
            return;
        }

        GameSfxPlayer.PlayCardSelect();
        battleUI.SelectCard(cardData, handIndex);
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        RefreshSelectView();
    }

    private void RefreshSelectView()
    {
        if (frameGraphic != null)
        {
            frameGraphic.color = isSelected
                ? new Color(1f, 0.89f, 0.62f, 1f)
                : Color.white;
        }

        if (titlePlateImage != null)
        {
            titlePlateImage.color = isSelected
                ? new Color(0.11f, 0.12f, 0.1f, 1f)
                : new Color(0.055f, 0.075f, 0.095f, 0.98f);
        }

        if (typePlateImage != null && cardData != null)
        {
            Color typeTint = GetCardColor(cardData);
            typeTint.a = isSelected ? 1f : 0.96f;
            typePlateImage.color = typeTint;
        }

        if (isSelected)
        {
            transform.localScale = originalScale * selectedScale;
        }
        else
        {
            transform.localScale = originalScale;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isSelected)
        {
            return;
        }

        transform.localScale = originalScale * hoverScale;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (isSelected)
        {
            return;
        }

        transform.localScale = originalScale;
    }

    private string GetCardTypeName(CardData card)
    {
        if (card == null)
        {
            return "未知";
        }

        switch (card.category)
        {
            case CardCategory.Basic:
                return GetBasicCardTypeName(card.effectType);

            case CardCategory.Trick:
                return "锦囊牌";

            case CardCategory.Equip:
                return GetEquipCardTypeName(card.effectType);

            default:
                return "未知";
        }
    }

    private string GetBasicCardTypeName(CardEffectType effectType)
    {
        switch (effectType)
        {
            case CardEffectType.Attack:
                return "攻击牌";

            case CardEffectType.Defense:
                return "防御牌";

            case CardEffectType.Heal:
                return "回复牌";

            default:
                return "基本牌";
        }
    }

    private string GetEquipCardTypeName(CardEffectType effectType)
    {
        switch (effectType)
        {
            case CardEffectType.Weapon:
                return "武器";

            case CardEffectType.Armor:
                return "防具";

            case CardEffectType.Treasure:
                return "宝物";

            case CardEffectType.MountPlus:
            case CardEffectType.MountMinus:
                return "坐骑";

            default:
                return "装备牌";
        }
    }

    private Color GetCardColor(CardData card)
    {
        if (card == null)
        {
            return Color.white;
        }

        switch (card.category)
        {
            case CardCategory.Basic:
                return GetBasicCardColor(card.effectType);

            case CardCategory.Trick:
                return new Color(0.25f, 0.18f, 0.42f);

            case CardCategory.Equip:
                return new Color(0.42f, 0.29f, 0.1f);

            default:
                return Color.white;
        }
    }

    private Color GetBasicCardColor(CardEffectType effectType)
    {
        switch (effectType)
        {
            case CardEffectType.Attack:
                return new Color(0.46f, 0.13f, 0.14f);

            case CardEffectType.Defense:
                return new Color(0.11f, 0.3f, 0.48f);

            case CardEffectType.Heal:
                return new Color(0.12f, 0.38f, 0.25f);

            default:
                return Color.white;
        }
    }
}
