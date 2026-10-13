using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BattleCharacterPanel : MonoBehaviour, IPointerClickHandler
{
    [Header("角色")]
    public Player player;

    [Header("战斗UI")]
    public BattleUI battleUI;

    [Header("是否可以作为目标")]
    public bool canBeSelectedAsTarget = true;

    [Header("UI")]
    public TMP_Text nameText;
    public TMP_Text hpText;
    public TMP_Text skillText;
    public TMP_Text portraitPlaceholderText;
    public Image portraitImage;
    public Image backgroundImage;

    [Header("选中边框")]
    public Image selectedBorderImage;

    [Header("立绘设置")]
    public bool usePortraitImage = false;

    [Header("装备文本")]
    public TMP_Text weaponText;
    public TMP_Text armorText;
    public TMP_Text treasureText;

    [Header("身份文本")]
    public TMP_Text identityText;

    [Header("手牌数量文本")]
    public TMP_Text handCountText;

    [Header("颜色设置")]
    public Color normalColor = Color.white;
    public Color selectedColor = new Color(1f, 0.92f, 0.45f);
    public Color deadColor = new Color(0.22f, 0.075f, 0.085f, 1f);

    [Header("选中缩放")]
    public float selectedScale = 1.06f;

    [Header("兜底刷新间隔（秒）")]
    public float refreshInterval = 0.1f;

    private Vector3 originalScale;
    private float refreshTimer = 0f;
    private GameObject publicTeamBadge;
    private RectTransform publicTeamBadgeRect;
    private Image publicTeamBadgeBackground;
    private Image publicTeamBadgeAccent;
    private TMP_Text publicTeamBadgeText;
    private Outline publicTeamBadgeOutline;
    private GameObject deadPanelOverlay;
    private float damageFlashRemaining;
    private float pendingResponsePulseTime;
    private Coroutine damageShakeCoroutine;
    private Vector2 layoutAnchoredPosition;
    private bool pendingResponse;
    private RectTransform selectionFrameRoot;
    private Image[] selectionFrameEdges;

    private const float DamageFlashDuration = 0.38f;
    private const float SelectionFrameThickness = 4f;

    void Awake()
    {
        originalScale = transform.localScale;
        RectTransform panelRect = transform as RectTransform;
        if (panelRect != null)
        {
            layoutAnchoredPosition = panelRect.anchoredPosition;
        }

        CreateSelectionFrame();
        ApplyReadableTextStyle();
    }

    void Start()
    {
        Refresh();
    }

    void Update()
    {
        damageFlashRemaining = Mathf.Max(0f, damageFlashRemaining - Time.unscaledDeltaTime);
        pendingResponsePulseTime += Time.unscaledDeltaTime;

        // 低频兜底同步：面板要跟随自己绑定的角色变化，但不必每帧重算文本与立绘
        refreshTimer += Time.unscaledDeltaTime;

        if (refreshTimer < refreshInterval)
        {
            return;
        }

        refreshTimer = 0f;

        Refresh();
    }

    public void Refresh()
    {
        if (player == null)
        {
            return;
        }

        RefreshName();
        RefreshHp();
        RefreshSkill();
        RefreshIdentity();
        RefreshHandCount();
        RefreshPortrait();
        RefreshEquipment();
        RefreshSelectedView();
    }

    public void ApplyLayout(Vector2 anchoredPosition, Vector2 size)
    {
        RectTransform panelRect = transform as RectTransform;
        if (panelRect == null)
        {
            return;
        }

        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = anchoredPosition;
        panelRect.sizeDelta = size;
        layoutAnchoredPosition = anchoredPosition;

        // The scene used a 0.5 local scale for every character panel.
        // Reset it here so the new responsive dimensions are the actual UI size.
        originalScale = Vector3.one;
        transform.localScale = originalScale;
        ResizePanelContent(size.x);
    }

    private void ApplyReadableTextStyle()
    {
        TMP_Text[] labels = GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text label in labels)
        {
            if (label == null)
            {
                continue;
            }

            label.color = Color.white;
            label.enableWordWrapping = true;
            label.raycastTarget = false;

            Outline outline = label.GetComponent<Outline>();
            if (outline == null)
            {
                outline = label.gameObject.AddComponent<Outline>();
            }

            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            outline.useGraphicAlpha = true;
        }

        ConfigureText(nameText, 28f, 22f);
        ConfigureText(hpText, 22f, 18f);
        ConfigureText(skillText, 18f, 15f);
        ConfigureText(identityText, 20f, 17f);
        ConfigureText(weaponText, 17f, 15f);
        ConfigureText(armorText, 17f, 15f);
        ConfigureText(treasureText, 17f, 15f);
        ConfigureText(handCountText, 18f, 16f);
        ConfigureText(portraitPlaceholderText, 16f, 12f);
    }

    private void ConfigureText(TMP_Text label, float maxSize, float minSize)
    {
        if (label == null)
        {
            return;
        }

        label.fontSize = maxSize;
        label.fontSizeMin = minSize;
        label.fontSizeMax = maxSize;
        label.enableAutoSizing = true;
    }

    private void ResizePanelContent(float panelWidth)
    {
        float contentWidth = Mathf.Max(160f, panelWidth - 36f);
        SetTextWidth(nameText, contentWidth);
        SetTextWidth(hpText, contentWidth);
        SetTextWidth(skillText, contentWidth);
        SetTextWidth(identityText, contentWidth);
        SetTextWidth(weaponText, contentWidth);
        SetTextWidth(armorText, contentWidth);
        SetTextWidth(treasureText, contentWidth);
        SetTextWidth(handCountText, contentWidth);
        SetTextWidth(portraitPlaceholderText, contentWidth);

        if (portraitImage != null)
        {
            SetTextWidth(portraitImage.rectTransform, contentWidth);
        }

        if (selectedBorderImage != null)
        {
            selectedBorderImage.rectTransform.sizeDelta = new Vector2(panelWidth + 20f, 440f);
        }

        if (publicTeamBadgeRect != null)
        {
            publicTeamBadgeRect.sizeDelta = new Vector2(
                Mathf.Min(148f, Mathf.Max(112f, panelWidth - 48f)),
                30f
            );
        }
    }

    private void SetTextWidth(TMP_Text label, float width)
    {
        if (label != null)
        {
            SetTextWidth(label.rectTransform, width);
        }
    }

    private void SetTextWidth(RectTransform rectTransform, float width)
    {
        if (rectTransform == null)
        {
            return;
        }

        Vector2 size = rectTransform.sizeDelta;
        size.x = width;
        rectTransform.sizeDelta = size;
    }

    private void RefreshEquipment()
    {
        if (weaponText != null)
        {
            weaponText.text = "武器：" + GetEquipmentName(player.weapon);
        }

        if (armorText != null)
        {
            armorText.text = "防具：" + GetEquipmentName(player.armor);
        }

        if (treasureText != null)
        {
            string mounts = string.Empty;
            if (player.offensiveHorse != null || player.defensiveHorse != null)
            {
                mounts = "\n坐骑：进攻 "
                    + GetEquipmentName(player.offensiveHorse)
                    + " · 防御 "
                    + GetEquipmentName(player.defensiveHorse);
            }
            string delayed = string.Empty;
            if (player.delayedTricks != null && player.delayedTricks.Count > 0)
            {
                List<string> names = new List<string>();
                foreach (CardData delayedCard in player.delayedTricks)
                {
                    if (delayedCard != null)
                    {
                        names.Add(delayedCard.cardName);
                    }
                }
                delayed = "\n判定区：" + string.Join("、", names);
            }
            treasureText.text = "宝物：" + GetEquipmentName(player.treasure) + mounts + delayed;
        }
    }

    private string GetEquipmentName(CardData equipment)
    {
        if (equipment == null)
        {
            return "无";
        }

        return equipment.cardName + " · " + equipment.GetSuitRankRichText();
    }

    public RectTransform GetEquipmentSlotRect(CardEffectType equipmentType)
    {
        TMP_Text slot = equipmentType == CardEffectType.Weapon
            ? weaponText
            : equipmentType == CardEffectType.Armor ? armorText : treasureText;
        return slot != null ? slot.rectTransform : transform as RectTransform;
    }

    public void PulseEquipmentSlot(CardEffectType equipmentType)
    {
        TMP_Text slot = equipmentType == CardEffectType.Weapon
            ? weaponText
            : equipmentType == CardEffectType.Armor ? armorText : treasureText;
        if (slot != null)
        {
            StartCoroutine(PulseEquipmentSlotCoroutine(slot));
        }
    }

    private IEnumerator PulseEquipmentSlotCoroutine(TMP_Text slot)
    {
        Color baseColor = slot.color;
        Vector3 baseScale = slot.transform.localScale;
        Color highlight = new Color(1f, 0.78f, 0.35f, baseColor.a);
        const float duration = 0.55f;
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / duration);
            float pulse = Mathf.Sin(t * Mathf.PI * 2f);
            slot.color = Color.Lerp(baseColor, highlight, Mathf.Max(0f, pulse));
            slot.transform.localScale = baseScale * (1f + Mathf.Max(0f, pulse) * 0.14f);
            yield return null;
        }

        slot.color = baseColor;
        slot.transform.localScale = baseScale;
    }

    private void RefreshName()
    {
        if (nameText != null)
        {
            nameText.text = player.chained ? "横置 · " + player.playerName : player.playerName;
        }
    }

    private void RefreshHp()
    {
        if (hpText != null)
        {
            hpText.text = player.isDead
                ? "阵亡"
                : "体力：" + player.currentHp + "/" + player.maxHp;
        }
    }

    private void RefreshSkill()
    {
        if (skillText == null)
        {
            return;
        }

        if (player.characterData == null)
        {
            skillText.text = "技能：无";
        }
        else
        {
            skillText.text =
                player.characterData.skillName + "：\n" + player.characterData.skillDescription;
        }

        if (player.protectionValue > 0)
        {
            skillText.text += "\n护盾：下次伤害 -" + player.protectionValue;
        }
    }

    private void RefreshIdentity()
    {
        if (GameSelectionData.gameMode == GameMode.ThreePlayer)
        {
            if (identityText != null)
            {
                identityText.gameObject.SetActive(true);
                identityText.text = "阵营：" + GetTeamName(player.teamType);
                identityText.color = GetTeamColor(player.teamType);
            }
            else
            {
                RefreshPublicTeamBadge();
            }
            return;
        }

        if (publicTeamBadge != null)
        {
            publicTeamBadge.SetActive(false);
        }

        if (identityText == null)
        {
            return;
        }

        // 二人场是直接对战；五人场仍遵守隐藏身份规则。
        if (GameSelectionData.gameMode != GameMode.FivePlayer)
        {
            identityText.gameObject.SetActive(false);
            return;
        }

        identityText.gameObject.SetActive(true);

        // 自己始终能看到自己的身份；其他身份只在公开后显示。
        string identityLabel;
        if (player == battleUI?.player || player.isIdentityRevealed)
        {
            identityLabel = "身份：" + GetIdentityName(player.identityType);
        }
        else
        {
            identityLabel = "身份：未知";
        }

        if (player.commanderSuspicion > 0)
        {
            identityLabel +=
                " · 嫌疑 "
                + Mathf.Min(player.commanderSuspicion, Player.CommanderSuspicionRevealThreshold)
                + "/"
                + Player.CommanderSuspicionRevealThreshold;

            identityLabel += player.hasAttackedCommander ? "（攻主实证）" : "（未证实）";
        }

        identityText.text = identityLabel;
    }

    private void RefreshPublicTeamBadge()
    {
        EnsurePublicTeamBadge();
        if (publicTeamBadge == null)
        {
            return;
        }

        publicTeamBadge.SetActive(true);
        Color teamColor = GetTeamColor(player.teamType);
        publicTeamBadgeBackground.color = new Color(0.025f, 0.035f, 0.05f, 0.94f);
        publicTeamBadgeAccent.color = teamColor;
        publicTeamBadgeText.color = new Color(0.96f, 0.92f, 0.82f, 1f);
        publicTeamBadgeText.text = GetTeamName(player.teamType) + "阵营";
        publicTeamBadgeOutline.effectColor = new Color(teamColor.r, teamColor.g, teamColor.b, 0.82f);
    }

    private void EnsurePublicTeamBadge()
    {
        if (publicTeamBadge != null)
        {
            return;
        }

        publicTeamBadge = new GameObject(
            "PublicTeamBadge",
            typeof(RectTransform),
            typeof(Image)
        );
        publicTeamBadgeRect = publicTeamBadge.GetComponent<RectTransform>();
        publicTeamBadgeRect.SetParent(transform, false);
        publicTeamBadgeRect.anchorMin = new Vector2(0.5f, 0.5f);
        publicTeamBadgeRect.anchorMax = new Vector2(0.5f, 0.5f);
        publicTeamBadgeRect.pivot = new Vector2(0.5f, 0.5f);
        publicTeamBadgeRect.anchoredPosition = new Vector2(0f, 86f);
        publicTeamBadgeRect.sizeDelta = new Vector2(142f, 30f);

        publicTeamBadgeBackground = publicTeamBadge.GetComponent<Image>();
        publicTeamBadgeBackground.color = new Color(0.025f, 0.035f, 0.05f, 0.94f);
        publicTeamBadgeBackground.raycastTarget = false;
        publicTeamBadgeOutline = publicTeamBadge.AddComponent<Outline>();
        publicTeamBadgeOutline.effectDistance = new Vector2(1.2f, -1.2f);
        publicTeamBadgeOutline.useGraphicAlpha = true;

        GameObject accentObject = new GameObject(
            "TeamAccent",
            typeof(RectTransform),
            typeof(Image)
        );
        accentObject.transform.SetParent(publicTeamBadgeRect, false);
        RectTransform accentRect = accentObject.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 0.5f);
        accentRect.anchorMax = new Vector2(0f, 0.5f);
        accentRect.pivot = new Vector2(0f, 0.5f);
        accentRect.anchoredPosition = new Vector2(8f, 0f);
        accentRect.sizeDelta = new Vector2(3f, 16f);
        publicTeamBadgeAccent = accentObject.GetComponent<Image>();
        publicTeamBadgeAccent.raycastTarget = false;

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(publicTeamBadgeRect, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(16f, 2f);
        labelRect.offsetMax = new Vector2(-8f, -2f);

        publicTeamBadgeText = labelObject.GetComponent<TextMeshProUGUI>();
        publicTeamBadgeText.font = nameText != null
            ? nameText.font
            : (skillText != null ? skillText.font : null);
        publicTeamBadgeText.fontSize = 16f;
        publicTeamBadgeText.fontSizeMin = 14f;
        publicTeamBadgeText.fontSizeMax = 16f;
        publicTeamBadgeText.enableAutoSizing = true;
        publicTeamBadgeText.alignment = TextAlignmentOptions.Center;
        publicTeamBadgeText.fontStyle = FontStyles.Bold;
        publicTeamBadgeText.raycastTarget = false;

        Outline textOutline = publicTeamBadgeText.gameObject.AddComponent<Outline>();
        textOutline.effectColor = new Color(0f, 0f, 0f, 0.92f);
        textOutline.effectDistance = new Vector2(1f, -1f);
        textOutline.useGraphicAlpha = true;

        publicTeamBadge.transform.SetAsLastSibling();
    }

    private string GetTeamName(TeamType team)
    {
        switch (team)
        {
            case TeamType.Overlord:
                return "霸主";
            case TeamType.Alliance:
                return "协战";
            default:
                return "待定";
        }
    }

    private Color GetTeamColor(TeamType team)
    {
        return team == TeamType.Overlord
            ? new Color(1f, 0.79f, 0.37f, 1f)
            : new Color(0.55f, 0.84f, 0.84f, 1f);
    }

    private string GetIdentityName(IdentityType identity)
    {
        switch (identity)
        {
            case IdentityType.Commander:
                return "执令者";
            case IdentityType.Protector:
                return "护令者";
            case IdentityType.Rebel:
                return "逆契者";
            case IdentityType.Wanderer:
                return "游离者";
            default:
                return "";
        }
    }

    private void RefreshHandCount()
    {
        if (handCountText == null)
            return;
        handCountText.text = "手牌：" + player.handCards.Count;
    }

    private void RefreshPortrait()
    {
        if (player.characterData == null)
        {
            return;
        }

        if (portraitImage != null)
        {
            portraitImage.sprite = player.characterData.portrait;
            portraitImage.enabled = usePortraitImage && player.characterData.portrait != null;
        }

        if (portraitPlaceholderText != null)
        {
            if (usePortraitImage && player.characterData.portrait != null)
            {
                portraitPlaceholderText.gameObject.SetActive(false);
            }
            else
            {
                portraitPlaceholderText.gameObject.SetActive(true);
                portraitPlaceholderText.text =
                    "【立绘区域】\n" + player.characterData.portraitPrompt;
            }
        }
    }

    private void RefreshSelectedView()
    {
        bool isDead = player.isDead;
        bool isSelected = battleUI != null && battleUI.IsTargetSelected(player);

        if (backgroundImage != null)
        {
            backgroundImage.color = isDead ? deadColor : normalColor;

            if (!isDead && damageFlashRemaining > 0f)
            {
                float flashStrength = Mathf.Clamp01(damageFlashRemaining / DamageFlashDuration);
                backgroundImage.color = Color.Lerp(
                    backgroundImage.color,
                    new Color(1f, 0.28f, 0.2f, 1f),
                    flashStrength * 0.82f
                );
            }
        }

        ApplyDeadCardAppearance(isDead);

        // Scene files contain a legacy full-size Image named SelectedBorder.
        // Tinting that white quad paints over the portrait, so keep it disabled
        // and draw a true four-sided frame instead.
        if (selectedBorderImage != null && selectedBorderImage.gameObject.activeSelf)
        {
            selectedBorderImage.gameObject.SetActive(false);
        }

        bool showSelectionFrame = (isSelected || pendingResponse) && !isDead;
        if (selectionFrameRoot != null)
        {
            selectionFrameRoot.gameObject.SetActive(showSelectionFrame);
            if (showSelectionFrame)
            {
                selectionFrameRoot.SetAsLastSibling();
                float pulse = pendingResponse
                    ? (Mathf.Sin(pendingResponsePulseTime * 7f) + 1f) * 0.5f
                    : 0f;
                Color frameColor = pendingResponse
                    ? Color.Lerp(new Color(1f, 0.24f, 0.12f, 0.92f), new Color(1f, 0.78f, 0.34f, 1f), pulse)
                    : selectedColor;
                for (int i = 0; i < selectionFrameEdges.Length; i++)
                {
                    selectionFrameEdges[i].color = frameColor;
                }
            }
        }

        if (isSelected && !isDead)
        {
            transform.localScale = originalScale * selectedScale;
        }
        else if (pendingResponse && !isDead)
        {
            float pulse = (Mathf.Sin(pendingResponsePulseTime * 7f) + 1f) * 0.5f;
            transform.localScale = originalScale * (1.006f + pulse * 0.014f);
        }
        else
        {
            transform.localScale = originalScale;
        }
    }

    private void CreateSelectionFrame()
    {
        if (selectedBorderImage != null)
        {
            selectedBorderImage.raycastTarget = false;
            selectedBorderImage.gameObject.SetActive(false);
        }

        GameObject frameObject = new GameObject("SelectionFrame", typeof(RectTransform));
        selectionFrameRoot = frameObject.GetComponent<RectTransform>();
        selectionFrameRoot.SetParent(transform, false);
        selectionFrameRoot.anchorMin = Vector2.zero;
        selectionFrameRoot.anchorMax = Vector2.one;
        selectionFrameRoot.offsetMin = Vector2.zero;
        selectionFrameRoot.offsetMax = Vector2.zero;
        selectionFrameRoot.SetAsLastSibling();

        selectionFrameEdges = new Image[4];
        selectionFrameEdges[0] = CreateSelectionFrameEdge(
            "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, SelectionFrameThickness));
        selectionFrameEdges[1] = CreateSelectionFrameEdge(
            "Bottom", Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, SelectionFrameThickness));
        selectionFrameEdges[2] = CreateSelectionFrameEdge(
            "Left", Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f),
            new Vector2(SelectionFrameThickness, 0f));
        selectionFrameEdges[3] = CreateSelectionFrameEdge(
            "Right", new Vector2(1f, 0f), Vector2.one, new Vector2(1f, 0.5f),
            new Vector2(SelectionFrameThickness, 0f));

        frameObject.SetActive(false);
    }

    private Image CreateSelectionFrameEdge(
        string edgeName,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 sizeDelta)
    {
        GameObject edgeObject = new GameObject(edgeName, typeof(RectTransform), typeof(Image));
        RectTransform edgeRect = edgeObject.GetComponent<RectTransform>();
        edgeRect.SetParent(selectionFrameRoot, false);
        edgeRect.anchorMin = anchorMin;
        edgeRect.anchorMax = anchorMax;
        edgeRect.pivot = pivot;
        edgeRect.sizeDelta = sizeDelta;
        edgeRect.anchoredPosition = Vector2.zero;

        Image edgeImage = edgeObject.GetComponent<Image>();
        edgeImage.raycastTarget = false;
        edgeImage.color = selectedColor;
        return edgeImage;
    }

    public void SetPendingResponse(bool value)
    {
        pendingResponse = value;
        RefreshSelectedView();
    }

    public void PlayDamageEffect(int damage)
    {
        if (player == null || damage <= 0)
        {
            return;
        }

        damageFlashRemaining = player.currentHp > 0 ? DamageFlashDuration : 0f;
        RefreshSelectedView();
        ShowFloatingDamageNumber(damage, false);

        RectTransform panelRect = transform as RectTransform;
        if (panelRect != null)
        {
            if (damageShakeCoroutine != null)
            {
                StopCoroutine(damageShakeCoroutine);
            }

            panelRect.anchoredPosition = layoutAnchoredPosition;
            damageShakeCoroutine = StartCoroutine(ShakeOnDamage(panelRect));
        }
    }

    public void PlayHealEffect(int amount)
    {
        if (player == null || amount <= 0)
        {
            return;
        }

        RefreshSelectedView();
        ShowFloatingDamageNumber(amount, true);
    }

    private void ApplyDeadCardAppearance(bool isDead)
    {
        if (isDead)
        {
            EnsureDeadPanelOverlay();
        }

        if (deadPanelOverlay != null)
        {
            deadPanelOverlay.SetActive(isDead);
        }

        if (portraitImage != null)
        {
            if (!isDead && damageFlashRemaining > 0f)
            {
                float flashStrength = Mathf.Clamp01(damageFlashRemaining / DamageFlashDuration);
                portraitImage.color = Color.Lerp(
                    Color.white,
                    new Color(1f, 0.44f, 0.4f, 1f),
                    flashStrength * 0.68f
                );
            }
            else
            {
                portraitImage.color = Color.white;
            }
        }

        Color livingText = Color.white;
        Color deadText = new Color(0.88f, 0.75f, 0.64f, 1f);
        SetDeadTextColor(nameText, isDead ? deadText : livingText);
        SetDeadTextColor(hpText, isDead ? deadText : livingText);
        SetDeadTextColor(skillText, isDead ? deadText : livingText);
        SetDeadTextColor(identityText, isDead ? deadText : livingText);
        SetDeadTextColor(weaponText, isDead ? deadText : livingText);
        SetDeadTextColor(armorText, isDead ? deadText : livingText);
        SetDeadTextColor(treasureText, isDead ? deadText : livingText);
        SetDeadTextColor(handCountText, isDead ? deadText : livingText);
        SetDeadTextColor(portraitPlaceholderText, isDead ? deadText : livingText);
    }

    private void EnsureDeadPanelOverlay()
    {
        if (deadPanelOverlay != null)
        {
            return;
        }

        GameObject overlayObject = new GameObject(
            "DeadStateOverlay",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
        overlayRect.SetParent(transform, false);
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlayImage = overlayObject.GetComponent<Image>();
        overlayImage.color = new Color(0.15f, 0.025f, 0.035f, 0.34f);
        overlayImage.raycastTarget = false;

        GameObject ribbon = new GameObject(
            "DeadStateRibbon",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        ribbon.transform.SetParent(overlayObject.transform, false);
        RectTransform ribbonRect = ribbon.GetComponent<RectTransform>();
        ribbonRect.anchorMin = new Vector2(0.5f, 0.5f);
        ribbonRect.anchorMax = new Vector2(0.5f, 0.5f);
        ribbonRect.pivot = new Vector2(0.5f, 0.5f);
        RectTransform panelRect = transform as RectTransform;
        float ribbonWidth = panelRect != null ? Mathf.Clamp(panelRect.rect.width * 0.76f, 178f, 258f) : 210f;
        ribbonRect.sizeDelta = new Vector2(ribbonWidth, 68f);
        ribbonRect.anchoredPosition = new Vector2(0f, 2f);
        ribbonRect.localRotation = Quaternion.Euler(0f, 0f, -7f);

        Image ribbonImage = ribbon.GetComponent<Image>();
        ribbonImage.color = new Color(0.29f, 0.035f, 0.045f, 0.97f);
        ribbonImage.raycastTarget = false;
        Outline ribbonOutline = ribbon.AddComponent<Outline>();
        ribbonOutline.effectColor = new Color(0.92f, 0.61f, 0.29f, 0.96f);
        ribbonOutline.effectDistance = new Vector2(2f, -2f);

        TMP_Text title = CreateDeadStateLabel(
            ribbon.transform,
            "阵亡",
            27f,
            new Vector2(0f, 9f),
            new Vector2(ribbonWidth - 18f, 36f)
        );
        title.fontStyle = FontStyles.Bold;
        CreateDeadStateLabel(
            ribbon.transform,
            "已出局",
            13f,
            new Vector2(0f, -19f),
            new Vector2(ribbonWidth - 18f, 20f)
        );

        deadPanelOverlay = overlayObject;
        overlayObject.transform.SetAsLastSibling();
    }

    private TMP_Text CreateDeadStateLabel(
        Transform parent,
        string value,
        float fontSize,
        Vector2 position,
        Vector2 size
    )
    {
        GameObject labelObject = new GameObject(
            "DeadStateText",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI)
        );
        labelObject.transform.SetParent(parent, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.anchoredPosition = position;
        labelRect.sizeDelta = size;

        TMP_Text label = labelObject.GetComponent<TMP_Text>();
        label.font = nameText != null ? nameText.font : null;
        label.text = value;
        label.fontSize = fontSize;
        label.fontSizeMin = Mathf.Max(11f, fontSize - 4f);
        label.fontSizeMax = fontSize;
        label.enableAutoSizing = true;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(1f, 0.84f, 0.65f, 1f);
        label.raycastTarget = false;
        Outline textOutline = labelObject.AddComponent<Outline>();
        textOutline.effectColor = new Color(0.1f, 0.012f, 0.016f, 0.96f);
        textOutline.effectDistance = new Vector2(1.2f, -1.2f);
        return label;
    }

    private void SetDeadTextColor(TMP_Text label, Color color)
    {
        if (label != null)
        {
            label.color = color;
        }
    }

    private void ShowFloatingDamageNumber(int amount, bool isHealing)
    {
        GameObject numberObject = new GameObject(
            "DamageNumber",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI)
        );
        numberObject.transform.SetParent(transform, false);
        numberObject.transform.SetAsLastSibling();

        RectTransform numberRect = numberObject.GetComponent<RectTransform>();
        numberRect.anchorMin = new Vector2(0.5f, 0.5f);
        numberRect.anchorMax = new Vector2(0.5f, 0.5f);
        numberRect.pivot = new Vector2(0.5f, 0.5f);
        numberRect.anchoredPosition = new Vector2(0f, 36f);
        numberRect.sizeDelta = new Vector2(160f, 72f);

        TMP_Text numberText = numberObject.GetComponent<TMP_Text>();
        if (hpText != null)
        {
            numberText.font = hpText.font;
        }
        numberText.text = (isHealing ? "+" : "-") + amount;
        numberText.fontSize = 48f;
        numberText.fontStyle = FontStyles.Bold;
        numberText.alignment = TextAlignmentOptions.Center;
        numberText.color = isHealing
            ? new Color(0.38f, 1f, 0.61f, 1f)
            : new Color(1f, 0.28f, 0.2f, 1f);
        numberText.raycastTarget = false;

        Outline outline = numberObject.AddComponent<Outline>();
        outline.effectColor = isHealing
            ? new Color(0.015f, 0.12f, 0.045f, 0.95f)
            : new Color(0.12f, 0.015f, 0.01f, 0.95f);
        outline.effectDistance = new Vector2(2f, -2f);
        outline.useGraphicAlpha = true;

        StartCoroutine(AnimateDamageNumber(numberText, numberRect));
    }

    private IEnumerator AnimateDamageNumber(TMP_Text label, RectTransform labelRect)
    {
        const float duration = 0.7f;
        Vector2 startPosition = labelRect.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < duration && label != null && labelRect != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            labelRect.anchoredPosition = startPosition + Vector2.up * (82f * progress);
            Color color = label.color;
            color.a = 1f - progress;
            label.color = color;
            yield return null;
        }

        if (label != null)
        {
            Destroy(label.gameObject);
        }
    }

    private IEnumerator ShakeOnDamage(RectTransform panelRect)
    {
        const float duration = 0.24f;
        float elapsed = 0f;

        while (elapsed < duration && panelRect != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float strength = 1f - Mathf.Clamp01(elapsed / duration);
            Vector2 jitter = Random.insideUnitCircle * (7f * strength);
            panelRect.anchoredPosition = layoutAnchoredPosition + jitter;
            yield return null;
        }

        if (panelRect != null)
        {
            panelRect.anchoredPosition = layoutAnchoredPosition;
        }
        damageShakeCoroutine = null;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!canBeSelectedAsTarget)
        {
            return;
        }

        if (battleUI == null || player == null)
        {
            return;
        }

        if (player.currentHp <= 0)
        {
            Debug.Log("不能选择已经阵亡的角色");
            return;
        }

        battleUI.SelectTarget(player);
    }
}
