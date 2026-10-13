using System.Collections;
using System.Collections.Generic;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BattleUI : MonoBehaviour
{
    [Header("玩家")]
    public Player player;

    [Header("敌人")]
    public Player enemy;

    [Header("回合管理器")]
    public TurnManager turnManager;

    [Header("血量文本")]
    public TMP_Text playerHpText;
    public TMP_Text enemyHpText;

    [Header("手牌文本")]
    public TMP_Text playerHandText;
    public TMP_Text enemyHandText;

    [Header("阶段文本")]
    public TMP_Text phaseText;

    [Header("技能提示")]
    public TMP_Text skillPopupText;
    public CanvasGroup skillPopupCanvasGroup;

    [Header("技能提示动画设置")]
    public float skillPopupFadeInTime = 0.15f;
    public float skillPopupStayTime = 0.8f;
    public float skillPopupFadeOutTime = 0.25f;
    public float skillPopupStartScale = 0.7f;
    public float skillPopupEndScale = 1.15f;

    [Header("出牌提示")]
    public TMP_Text cardActionText;
    public CanvasGroup cardActionCanvasGroup;

    [Header("出牌提示动画设置")]
    public float cardActionFadeInTime = 0.12f;
    public float cardActionStayTime = 1.0f;
    public float cardActionFadeOutTime = 0.3f;

    [Header("操作按钮")]
    public Button endTurnButton;
    public TMP_Text endTurnButtonText;

    [Header("主动技能按钮（运行时创建）")]
    private Button activeSkillButton;
    private TMP_Text activeSkillButtonText;

    [Header("手牌按钮区域")]
    public Transform playerHandButtonRoot;

    [Header("手牌按钮预制体")]
    public GameObject cardButtonPrefab;

    [Header("兜底刷新间隔（秒）")]
    public float refreshInterval = 0.1f;

    private CardData selectedCard;
    private int selectedCardIndex = -1;
    private Player selectedTarget;
    private List<CardButton> cardButtons = new List<CardButton>();
    private readonly Queue<DiscardRevealRequest> discardRevealQueue = new Queue<DiscardRevealRequest>();
    private bool isPlayingDiscardReveal;
    private GameObject pendingAttackOverlay;
    private TargetArrowGraphic pendingAttackArrow;
    private Player pendingResponseTarget;
    private GameObject centerEventRoot;
    private RectTransform centerEventCardsRoot;
    private TMP_Text centerEventHeading;
    private TMP_Text centerEventDetails;
    private CanvasGroup centerEventCanvasGroup;
    private Coroutine centerEventRoutine;
    private bool centerEventPinned;
    private bool centerEventHasCards;
    private CardData lastPlayedCard;
    private Player lastCardUser;
    private Player lastCardTarget;
    private Player lastPendingResponseTarget;
    private int centerEventGeneration;
    private readonly List<CardData> displayedCenterCards = new List<CardData>();

    private float refreshTimer = 0f;
    private RectTransform handViewportRect;
    private RectTransform handContentRect;
    private ScrollRect handScrollRect;
    private BattleGuideAndSummary gameFlowOverlay;
    private int lastRenderedHandCount = -1;
    private Vector2 lastCanvasSize;
    private GameObject cardChoiceOverlay;
    private Action<CardData> cardChoiceCallback;

    private const float PreferredHandCardWidth = 142f;
    private const float MinimumHandCardWidth = 104f;
    private const float HandCardSpacing = 12f;
    private const float HandViewportHeight = 226f;

    private class DiscardRevealRequest
    {
        public Player owner;
        public CardData card;
        public bool wasEquipment;
        public string reason;
    }

    void Start()
    {
        ApplyResponsiveBattleLayout(true);

        // This is an animated notification, not permanent HUD text.
        if (cardActionText != null)
        {
            cardActionText.gameObject.SetActive(false);
        }
        if (cardActionCanvasGroup != null)
        {
            cardActionCanvasGroup.alpha = 0f;
            cardActionCanvasGroup.blocksRaycasts = false;
            cardActionCanvasGroup.interactable = false;
        }

        RefreshUI();

        gameFlowOverlay = GetComponent<BattleGuideAndSummary>();
        if (gameFlowOverlay == null)
        {
            gameFlowOverlay = gameObject.AddComponent<BattleGuideAndSummary>();
        }
        gameFlowOverlay.Initialize(this);
    }

    void Update()
    {
        if (cardChoiceOverlay != null && Input.GetKeyDown(KeyCode.Escape))
        {
            CompleteCardChoice(null);
        }

        RefreshPendingAttackCue();

        // 状态变化时 TurnManager 和本类都会显式调用 RefreshUI()，
        // 这里只做低频兜底同步，避免每帧拼接字符串产生大量 GC。
        refreshTimer += Time.unscaledDeltaTime;

        if (refreshTimer < refreshInterval)
        {
            return;
        }

        refreshTimer = 0f;

        if (ApplyResponsiveBattleLayout(false))
        {
            RefreshHandButtons();
        }

        RefreshHpText();
        RefreshHandText();
        RefreshPhaseText();
        RefreshActionButton();
        RefreshCharacterActiveSkillButton();
    }

    public void RefreshUI()
    {
        ClearInvalidSelectedCard();

        RefreshHpText();
        RefreshHandText();
        RefreshPhaseText();
        RefreshActionButton();
        RefreshCharacterActiveSkillButton();
        RefreshHandButtons();
    }

    public void ShowMatchSummary(string result, int rounds, List<Player> participants)
    {
        if (gameFlowOverlay == null)
        {
            gameFlowOverlay = GetComponent<BattleGuideAndSummary>();
            if (gameFlowOverlay == null)
            {
                gameFlowOverlay = gameObject.AddComponent<BattleGuideAndSummary>();
            }
            gameFlowOverlay.Initialize(this);
        }

        gameFlowOverlay.ShowMatchSummary(result, rounds, participants);
    }

    private bool ApplyResponsiveBattleLayout(bool force)
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            return false;
        }

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        Canvas.ForceUpdateCanvases();
        RectTransform canvasRect = canvas.transform as RectTransform;
        Vector2 canvasSize = canvasRect != null ? canvasRect.rect.size : Vector2.zero;
        if (canvasSize.x <= 0f || canvasSize.y <= 0f)
        {
            canvasSize = new Vector2(Screen.width, Screen.height);
        }

        if (!force && (canvasSize - lastCanvasSize).sqrMagnitude < 1f)
        {
            return false;
        }

        lastCanvasSize = canvasSize;
        LayoutCharacterPanels(canvasSize);
        LayoutBattleControls(canvasSize);
        ConfigureHandViewport(canvasSize);
        LayoutCenterEvent(canvasSize);
        if (centerEventHasCards)
        {
            RefreshCenterEventCards(canvasSize);
        }
        return true;
    }

    private void LayoutCharacterPanels(Vector2 canvasSize)
    {
        BattleCharacterPanel[] panels = FindObjectsOfType<BattleCharacterPanel>(true);
        BattleCharacterPanel playerPanel = null;
        List<BattleCharacterPanel> enemyPanels = new List<BattleCharacterPanel>();

        foreach (BattleCharacterPanel panel in panels)
        {
            if (panel == null || panel.player == null)
            {
                continue;
            }

            if (panel.player == player)
            {
                playerPanel = panel;
            }
            else
            {
                enemyPanels.Add(panel);
            }
        }

        enemyPanels.Sort((left, right) =>
            string.CompareOrdinal(left.gameObject.name, right.gameObject.name)
        );

        float panelWidth = Mathf.Min(320f, canvasSize.x * 0.18f);
        float panelHeight = Mathf.Min(420f, canvasSize.y * 0.39f);
        if (playerPanel != null)
        {
            playerPanel.ApplyLayout(
                new Vector2(-canvasSize.x * 0.39f, -canvasSize.y * 0.26f),
                new Vector2(panelWidth, panelHeight)
            );
        }

        if (enemyPanels.Count == 0)
        {
            return;
        }

        float enemyY;
        float leftExtent;
        float rightExtent;

        switch (GameSelectionData.gameMode)
        {
            case GameMode.FivePlayer:
                enemyY = canvasSize.y * 0.15f;
                leftExtent = -canvasSize.x * 0.35f;
                rightExtent = canvasSize.x * 0.35f;
                break;

            case GameMode.ThreePlayer:
                enemyY = canvasSize.y * 0.18f;
                leftExtent = -canvasSize.x * 0.28f;
                rightExtent = canvasSize.x * 0.28f;
                break;

            default:
                enemyY = canvasSize.y * 0.22f;
                leftExtent = canvasSize.x * 0.30f;
                rightExtent = leftExtent;
                break;
        }

        for (int i = 0; i < enemyPanels.Count; i++)
        {
            float x = enemyPanels.Count == 1
                ? leftExtent
                : Mathf.Lerp(leftExtent, rightExtent, i / (float)(enemyPanels.Count - 1));

            enemyPanels[i].ApplyLayout(
                new Vector2(x, enemyY),
                new Vector2(panelWidth, panelHeight)
            );
        }

        // In two-player mode the enemy hand label is a legacy canvas-level
        // object. Attach it to the enemy panel so it sits under the portrait,
        // matching the per-panel labels used in three-player mode.
        if (enemyHandText != null && enemyPanels.Count == 1)
        {
            LayoutEnemyHandCount(enemyPanels[0]);
        }
    }

    private void LayoutEnemyHandCount(BattleCharacterPanel enemyPanel)
    {
        if (enemyPanel == null || enemyHandText == null)
        {
            return;
        }

        RectTransform panelRect = enemyPanel.transform as RectTransform;
        RectTransform handCountRect = enemyHandText.rectTransform;
        if (panelRect == null || handCountRect == null)
        {
            return;
        }

        handCountRect.SetParent(panelRect, false);
        handCountRect.anchorMin = new Vector2(0.5f, 0.5f);
        handCountRect.anchorMax = new Vector2(0.5f, 0.5f);
        handCountRect.pivot = new Vector2(0.5f, 0.5f);
        handCountRect.anchoredPosition = new Vector2(0f, -236f);
        handCountRect.sizeDelta = new Vector2(
            Mathf.Min(200f, Mathf.Max(120f, panelRect.rect.width - 20f)),
            50f
        );

        enemyHandText.alignment = TextAlignmentOptions.Center;
        ConfigureHudText(enemyHandText, 18f, 15f);
        enemyPanel.handCountText = enemyHandText;
        enemyHandText.text = "手牌：" + enemyPanel.player.handCards.Count;
    }

    private void LayoutBattleControls(Vector2 canvasSize)
    {
        if (phaseText != null)
        {
            RectTransform phaseRect = phaseText.rectTransform;
            phaseRect.anchorMin = new Vector2(0.5f, 0.5f);
            phaseRect.anchorMax = new Vector2(0.5f, 0.5f);
            phaseRect.anchoredPosition = new Vector2(0f, -canvasSize.y * 0.13f);
            phaseRect.sizeDelta = new Vector2(
                Mathf.Min(660f, canvasSize.x * 0.36f),
                140f
            );
            ConfigureHudText(phaseText, 22f, 18f);
        }

        if (playerHandText != null)
        {
            RectTransform handLabelRect = playerHandText.rectTransform;
            handLabelRect.anchorMin = new Vector2(0.5f, 0.5f);
            handLabelRect.anchorMax = new Vector2(0.5f, 0.5f);
            handLabelRect.anchoredPosition = new Vector2(
                canvasSize.x * 0.03f,
                -canvasSize.y * 0.285f
            );
            handLabelRect.sizeDelta = new Vector2(
                Mathf.Min(600f, canvasSize.x * 0.48f),
                38f
            );
            ConfigureHudText(playerHandText, 24f, 18f);
        }

        // Character panels already show both sides' HP and every enemy's hand count.
        if (GameSelectionData.gameMode == GameMode.FivePlayer)
        {
            if (playerHpText != null)
            {
                playerHpText.gameObject.SetActive(false);
            }
            if (enemyHpText != null)
            {
                enemyHpText.gameObject.SetActive(false);
            }
            if (enemyHandText != null)
            {
                enemyHandText.gameObject.SetActive(false);
            }
        }

        float enemyPanelCenterY;
        switch (GameSelectionData.gameMode)
        {
            case GameMode.FivePlayer:
                enemyPanelCenterY = canvasSize.y * 0.15f;
                break;
            case GameMode.ThreePlayer:
                enemyPanelCenterY = canvasSize.y * 0.18f;
                break;
            default:
                enemyPanelCenterY = canvasSize.y * 0.22f;
                break;
        }

        GameLogger gameLogger = FindObjectOfType<GameLogger>(true);
        if (gameLogger != null)
        {
            gameLogger.ApplyResponsiveLayout(
                canvasSize,
                enemyPanelCenterY,
                Mathf.Min(420f, canvasSize.y * 0.39f)
            );
        }

        SetButtonLayout(
            "BackToMainMenuButton",
            new Vector2(-canvasSize.x * 0.44f, canvasSize.y * 0.44f),
            new Vector2(180f, 50f)
        );
        SetButtonLayout(
            "RestartButton",
            new Vector2(canvasSize.x * 0.44f, canvasSize.y * 0.44f),
            new Vector2(160f, 50f)
        );

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            Button settingsButton = GameSettingsPanel.CreateAccessButton(
                canvas,
                "BattleSettingsButton",
                "设置",
                GameUIButtonTone.Navigation
            );
            RectTransform settingsRect = settingsButton.transform as RectTransform;
            if (settingsRect != null)
            {
                settingsRect.anchorMin = new Vector2(0.5f, 0.5f);
                settingsRect.anchorMax = new Vector2(0.5f, 0.5f);
                settingsRect.anchoredPosition = new Vector2(0f, canvasSize.y * 0.44f);
                settingsRect.sizeDelta = new Vector2(150f, 50f);
            }
        }

        EnsureCharacterActiveSkillButton(canvas);
        LayoutCharacterActiveSkillButton(canvasSize);

        if (endTurnButton != null)
        {
            RectTransform buttonRect = endTurnButton.transform as RectTransform;
            if (buttonRect != null)
            {
                buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
                buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
                buttonRect.anchoredPosition = new Vector2(
                    canvasSize.x * 0.44f,
                    -canvasSize.y * 0.44f
                );
                buttonRect.sizeDelta = new Vector2(184f, 82f);
            }

            StyleActionButton();
        }
    }

    private void EnsureCharacterActiveSkillButton(Canvas canvas)
    {
        if (canvas == null || activeSkillButton != null)
        {
            return;
        }

        GameObject buttonObject = new GameObject(
            "CharacterActiveSkillButton",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button)
        );
        buttonObject.layer = canvas.gameObject.layer;
        buttonObject.transform.SetParent(canvas.transform, false);
        buttonObject.transform.SetAsLastSibling();

        Image background = buttonObject.GetComponent<Image>();
        background.raycastTarget = true;

        activeSkillButton = buttonObject.GetComponent<Button>();
        activeSkillButton.targetGraphic = background;
        activeSkillButton.onClick.AddListener(OnClickCharacterActiveSkillButton);

        GameObject labelObject = new GameObject(
            "Label",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI)
        );
        labelObject.layer = buttonObject.layer;
        labelObject.transform.SetParent(buttonObject.transform, false);

        RectTransform labelRect = labelObject.transform as RectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(10f, 6f);
        labelRect.offsetMax = new Vector2(-10f, -6f);

        activeSkillButtonText = labelObject.GetComponent<TextMeshProUGUI>();
        if (endTurnButtonText != null)
        {
            activeSkillButtonText.font = endTurnButtonText.font;
        }
        else if (phaseText != null)
        {
            activeSkillButtonText.font = phaseText.font;
        }
        activeSkillButtonText.alignment = TextAlignmentOptions.Center;
        activeSkillButtonText.enableAutoSizing = true;
        activeSkillButtonText.fontSize = 22f;
        activeSkillButtonText.fontSizeMin = 15f;
        activeSkillButtonText.fontSizeMax = 22f;
        activeSkillButtonText.raycastTarget = false;

        GameUIButtonStyler.Apply(activeSkillButton, GameUIButtonTone.Destructive);
    }

    private void LayoutCharacterActiveSkillButton(Vector2 canvasSize)
    {
        if (activeSkillButton == null)
        {
            return;
        }

        RectTransform buttonRect = activeSkillButton.transform as RectTransform;
        if (buttonRect == null)
        {
            return;
        }

        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(
            canvasSize.x * 0.36f,
            -canvasSize.y * 0.215f
        );
        buttonRect.sizeDelta = new Vector2(
            Mathf.Min(160f, canvasSize.x * 0.12f),
            Mathf.Min(82f, canvasSize.y * 0.1f)
        );
    }

    private void RefreshCharacterActiveSkillButton()
    {
        if (activeSkillButton == null)
        {
            return;
        }

        bool hasSkill = player != null
            && player.characterData != null
            && player.characterData.activeSkillType == CharacterActiveSkillType.LoseOneHpDrawTwo;
        activeSkillButton.gameObject.SetActive(hasSkill);
        if (!hasSkill)
        {
            return;
        }

        if (activeSkillButtonText != null)
        {
            string skillName = player.characterData.skillName;
            activeSkillButtonText.text = player.usedActiveSkillThisTurn
                ? skillName + "\n本回合已发动"
                : skillName + "\n失去1体力，摸2张";
        }

        activeSkillButton.interactable =
            turnManager != null && turnManager.CanUseCharacterActiveSkill(player);
    }

    private void OnClickCharacterActiveSkillButton()
    {
        if (turnManager != null && turnManager.TryUseCharacterActiveSkill(player))
        {
            RefreshCharacterActiveSkillButton();
            RefreshUI();
        }
    }

    private void SetButtonLayout(string objectName, Vector2 position, Vector2 size)
    {
        GameObject buttonObject = GameObject.Find(objectName);
        RectTransform buttonRect = buttonObject != null
            ? buttonObject.transform as RectTransform
            : null;
        if (buttonRect == null)
        {
            return;
        }

        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = position;
        buttonRect.sizeDelta = size;

        Button button = buttonObject.GetComponent<Button>();
        if (button != null)
        {
            GameUIButtonStyler.Apply(
                button,
                objectName == "RestartButton"
                    ? GameUIButtonTone.PrimaryAction
                    : GameUIButtonTone.Navigation
            );
        }
    }

    private void ConfigureHudText(TMP_Text text, float maxSize, float minSize)
    {
        if (text == null)
        {
            return;
        }

        text.color = Color.white;
        text.enableWordWrapping = true;
        text.enableAutoSizing = true;
        text.fontSize = maxSize;
        text.fontSizeMax = maxSize;
        text.fontSizeMin = minSize;
        text.raycastTarget = false;

        Outline outline = text.GetComponent<Outline>();
        if (outline == null)
        {
            outline = text.gameObject.AddComponent<Outline>();
        }
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        outline.useGraphicAlpha = true;
    }

    private void ConfigureHandViewport(Vector2 canvasSize)
    {
        if (playerHandButtonRoot == null)
        {
            return;
        }

        handViewportRect = playerHandButtonRoot as RectTransform;
        if (handViewportRect == null)
        {
            return;
        }

        handViewportRect.anchorMin = new Vector2(0.5f, 0.5f);
        handViewportRect.anchorMax = new Vector2(0.5f, 0.5f);
        handViewportRect.pivot = new Vector2(0.5f, 0.5f);
        handViewportRect.anchoredPosition = new Vector2(
            canvasSize.x * 0.03f,
            -canvasSize.y * 0.37f
        );
        handViewportRect.sizeDelta = new Vector2(
            Mathf.Min(1500f, canvasSize.x * 0.64f),
            HandViewportHeight
        );

        EnsureHandScrollView();
    }

    private void EnsureHandScrollView()
    {
        if (handViewportRect == null)
        {
            return;
        }

        HorizontalLayoutGroup layoutGroup = handViewportRect.GetComponent<HorizontalLayoutGroup>();
        if (layoutGroup != null)
        {
            layoutGroup.enabled = false;
        }

        ContentSizeFitter sizeFitter = handViewportRect.GetComponent<ContentSizeFitter>();
        if (sizeFitter != null)
        {
            sizeFitter.enabled = false;
        }

        Image viewportImage = handViewportRect.GetComponent<Image>();
        if (viewportImage == null)
        {
            viewportImage = handViewportRect.gameObject.AddComponent<Image>();
        }
        viewportImage.color = new Color(0.035f, 0.055f, 0.085f, 0.76f);
        viewportImage.type = Image.Type.Sliced;
        viewportImage.raycastTarget = true;

        Outline viewportOutline = viewportImage.GetComponent<Outline>();
        if (viewportOutline == null)
        {
            viewportOutline = viewportImage.gameObject.AddComponent<Outline>();
        }
        viewportOutline.effectColor = new Color(0.69f, 0.49f, 0.2f, 0.65f);
        viewportOutline.effectDistance = new Vector2(1.5f, -1.5f);
        viewportOutline.useGraphicAlpha = true;

        if (handViewportRect.GetComponent<RectMask2D>() == null)
        {
            handViewportRect.gameObject.AddComponent<RectMask2D>();
        }

        handScrollRect = handViewportRect.GetComponent<ScrollRect>();
        if (handScrollRect == null)
        {
            handScrollRect = handViewportRect.gameObject.AddComponent<ScrollRect>();
        }

        if (handContentRect == null)
        {
            Transform existingContent = handViewportRect.Find("HandCardsContent");
            if (existingContent != null)
            {
                handContentRect = existingContent as RectTransform;
            }
            else
            {
                GameObject contentObject = new GameObject("HandCardsContent", typeof(RectTransform));
                contentObject.layer = handViewportRect.gameObject.layer;
                handContentRect = contentObject.GetComponent<RectTransform>();
                handContentRect.SetParent(handViewportRect, false);
            }
        }

        handContentRect.anchorMin = new Vector2(0f, 0.5f);
        handContentRect.anchorMax = new Vector2(0f, 0.5f);
        handContentRect.pivot = new Vector2(0f, 0.5f);
        handContentRect.anchoredPosition = Vector2.zero;

        handScrollRect.viewport = handViewportRect;
        handScrollRect.content = handContentRect;
        handScrollRect.horizontal = true;
        handScrollRect.vertical = false;
        handScrollRect.movementType = ScrollRect.MovementType.Clamped;
        handScrollRect.scrollSensitivity = 45f;
        handScrollRect.inertia = true;
    }

    public void ShowSkillPopup(string message)
    {
        if (skillPopupText == null || skillPopupCanvasGroup == null)
        {
            return;
        }

        StopCoroutine(nameof(ShowSkillPopupCoroutine));
        StartCoroutine(ShowSkillPopupCoroutine(message));
    }

    private IEnumerator ShowSkillPopupCoroutine(string message)
    {
        skillPopupText.gameObject.SetActive(true);
        skillPopupText.text = message;

        skillPopupCanvasGroup.alpha = 0f;
        skillPopupText.transform.localScale = Vector3.one * skillPopupStartScale;

        float timer = 0f;

        while (timer < skillPopupFadeInTime)
        {
            timer += Time.deltaTime;

            float t = timer / skillPopupFadeInTime;
            skillPopupCanvasGroup.alpha = Mathf.Lerp(0f, 1f, t);
            skillPopupText.transform.localScale = Vector3.Lerp(
                Vector3.one * skillPopupStartScale,
                Vector3.one * skillPopupEndScale,
                t
            );

            yield return null;
        }

        skillPopupCanvasGroup.alpha = 1f;
        skillPopupText.transform.localScale = Vector3.one * skillPopupEndScale;

        yield return new WaitForSeconds(skillPopupStayTime);

        timer = 0f;

        while (timer < skillPopupFadeOutTime)
        {
            timer += Time.deltaTime;

            float t = timer / skillPopupFadeOutTime;
            skillPopupCanvasGroup.alpha = Mathf.Lerp(1f, 0f, t);

            yield return null;
        }

        skillPopupCanvasGroup.alpha = 0f;
        skillPopupText.transform.localScale = Vector3.one;
        skillPopupText.gameObject.SetActive(false);
    }

    // 出牌提示动画：显示"谁 对 谁 使用了 什么牌"
    public void ShowCardAction(string message)
    {
        ShowCenterTableEvent(null, "对局动态", message, 1.15f, false);
    }

    public void ShowCardAction(Player source, Player target, CardData card)
    {
        if (source == null || card == null)
        {
            return;
        }

        string message = target != null && target != source
            ? source.playerName + " 对 " + target.playerName + " 使用【" + card.cardName + "】"
            : source.playerName + " 使用【" + card.cardName + "】";
        lastPlayedCard = card;
        lastCardUser = source;
        lastCardTarget = target;
        lastPendingResponseTarget = null;
        ShowCenterTableEvent(new[] { card }, "使用牌", message, 1.3f, false);
        GameSfxPlayer.PlayCardAction(card);
        if (!GameSfxPlayer.PlayCardVoice(source, card))
        {
            PlayCharacterVoice(source);
        }

        if (target != null && target != source)
        {
            PlayCardFlight(source, target, card);
        }
    }

    public void ShowEquipmentAction(Player owner, CardData equipment)
    {
        if (owner == null || equipment == null)
        {
            return;
        }

        ShowCardAction(owner, owner, equipment);
        PlayEquipmentFlight(owner, equipment);
        StartCoroutine(PulseEquipmentSlotAfterArrival(owner, equipment));
    }

    public void ShowDamageEffect(Player target, int damage)
    {
        if (target == null || damage <= 0)
        {
            return;
        }

        BattleCharacterPanel[] panels = FindObjectsOfType<BattleCharacterPanel>(true);
        foreach (BattleCharacterPanel panel in panels)
        {
            if (panel != null && panel.player == target)
            {
                panel.PlayDamageEffect(damage);
                return;
            }
        }
    }

    public void ShowHealEffect(Player target, int amount)
    {
        if (target == null || amount <= 0)
        {
            return;
        }

        BattleCharacterPanel[] panels = FindObjectsOfType<BattleCharacterPanel>(true);
        foreach (BattleCharacterPanel panel in panels)
        {
            if (panel != null && panel.player == target)
            {
                panel.PlayHealEffect(amount);
                return;
            }
        }
    }

    public void ShowPendingAttack(Player attacker, Player defender, string responseMessage)
    {
        ClearPendingAttack();

        Canvas canvas = GetComponentInParent<Canvas>();
        RectTransform canvasRect = canvas != null ? canvas.transform as RectTransform : null;
        Vector2 start;
        Vector2 center;
        Vector2 end;
        if (canvas == null
            || canvasRect == null
            || attacker == null
            || defender == null
            || !TryGetSourcePoint(attacker, canvas, canvasRect, out start)
            || !TryGetPanelPoint(defender, canvas, canvasRect, out center))
        {
            return;
        }

        Vector2 direction = (center - start).normalized;
        start += direction * 44f;
        if (!TryGetPanelEdgePoint(defender, start, canvas, canvasRect, out end))
        {
            end = center - direction * 76f;
        }

        pendingResponseTarget = defender;
        lastPendingResponseTarget = defender;
        SetPanelPendingResponse(defender, true);

        pendingAttackOverlay = CreateFullCanvasOverlay(canvas, "PendingAttackResponse");
        CanvasGroup group = pendingAttackOverlay.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        GameObject arrowObject = CreateArrowGraphic(pendingAttackOverlay.transform, start, end);
        pendingAttackArrow = arrowObject.GetComponent<TargetArrowGraphic>();
        pendingAttackArrow.color = new Color(0.9f, 0.43f, 0.23f, 0.78f);
        pendingAttackArrow.lineWidth = 4.5f;
        pendingAttackArrow.arrowLength = 20f;
        pendingAttackArrow.arrowWidth = 20f;

        KeepBattleHudAboveEffect(canvas);

        // The target arrow communicates who is responding; keep the actual card and
        // response prompt in the table center, where the player's attention already is.
        CardData pendingCard = lastCardUser == attacker ? lastPlayedCard : null;
        ShowCenterTableEvent(
            pendingCard != null ? new[] { pendingCard } : null,
            "待响应",
            string.IsNullOrEmpty(responseMessage) ? "请使用对应的响应牌" : responseMessage,
            0f,
            true
        );
    }

    public void ShowCardResponse(Player responder, CardData responseCard, string message)
    {
        if (responseCard == null)
        {
            ShowCardAction(message);
            return;
        }

        List<CardData> responseCards = new List<CardData>(2);
        if (lastPlayedCard != null
            && (lastCardTarget == null
                || lastCardTarget == responder
                || lastCardTarget == lastPendingResponseTarget))
        {
            responseCards.Add(lastPlayedCard);
        }
        responseCards.Add(responseCard);

        ShowCenterTableEvent(
            responseCards,
            "响应牌",
            string.IsNullOrEmpty(message)
                ? (responder != null ? responder.playerName + " 打出响应牌" : "打出响应牌")
                : message,
            1.45f,
            false
        );
    }

    public void ClearPendingAttack()
    {
        bool wasPinned = centerEventPinned;

        if (pendingResponseTarget != null)
        {
            SetPanelPendingResponse(pendingResponseTarget, false);
        }

        pendingResponseTarget = null;
        pendingAttackArrow = null;

        if (pendingAttackOverlay != null)
        {
            Destroy(pendingAttackOverlay);
            pendingAttackOverlay = null;
        }

        if (wasPinned)
        {
            centerEventPinned = false;
            ScheduleCenterEventHide(0.85f);
        }
    }

    public void ShowDiscardedCard(Player owner, CardData card, bool wasEquipment, string reason)
    {
        if (card == null)
        {
            return;
        }

        discardRevealQueue.Enqueue(
            new DiscardRevealRequest
            {
                owner = owner,
                card = card,
                wasEquipment = wasEquipment,
                reason = reason,
            }
        );

        if (!isPlayingDiscardReveal)
        {
            StartCoroutine(PlayDiscardRevealQueue());
        }
    }

    public void RequestTargetZoneCardChoice(Player target, string title, Action<CardData> onChoice)
    {
        RequestCardChoice(target, true, true, true, title, "取消", onChoice);
    }

    public void RequestOwnHandCardChoice(Player owner, string title, Action<CardData> onChoice)
    {
        RequestCardChoice(owner, true, false, false, title, "自动弃置", onChoice);
    }

    public void RequestRevealedCardChoice(
        List<CardData> choices,
        string title,
        Action<CardData> onChoice,
        string choiceLabel = "亮出的牌"
    )
    {
        RequestCardChoice(null, false, false, false, title, "取消", onChoice, choices, choiceLabel);
    }

    private void RequestCardChoice(
        Player owner,
        bool includeHand,
        bool includeEquipment,
        bool hideHandCards,
        string title,
        string cancelLabel,
        Action<CardData> onChoice,
        List<CardData> fixedChoices = null,
        string fixedChoiceLabel = "亮出的牌"
    )
    {
        if (onChoice == null || (owner == null && fixedChoices == null))
        {
            onChoice?.Invoke(null);
            return;
        }

        List<CardData> choices = fixedChoices != null
            ? new List<CardData>(fixedChoices)
            : new List<CardData>();
        if (fixedChoices == null && includeHand)
        {
            choices.AddRange(owner.handCards);
        }
        if (fixedChoices == null && includeEquipment)
        {
            if (owner.weapon != null) choices.Add(owner.weapon);
            if (owner.armor != null) choices.Add(owner.armor);
            if (owner.treasure != null) choices.Add(owner.treasure);
            if (owner.offensiveHorse != null) choices.Add(owner.offensiveHorse);
            if (owner.defensiveHorse != null) choices.Add(owner.defensiveHorse);
        }

        if (choices.Count == 0)
        {
            onChoice(null);
            return;
        }

        CompleteCardChoice(null);
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            onChoice(null);
            return;
        }

        cardChoiceCallback = onChoice;
        cardChoiceOverlay = CreateFullCanvasOverlay(canvas, "CardZoneChoiceOverlay");
        Image backdrop = cardChoiceOverlay.AddComponent<Image>();
        backdrop.color = new Color(0.008f, 0.014f, 0.026f, 0.84f);
        backdrop.raycastTarget = true;
        CanvasGroup overlayGroup = cardChoiceOverlay.AddComponent<CanvasGroup>();
        overlayGroup.blocksRaycasts = true;
        overlayGroup.interactable = true;

        GameObject panel = new GameObject(
            "CardChoicePanel",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        panel.transform.SetParent(cardChoiceOverlay.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(900f, 540f);
        panel.GetComponent<Image>().color = new Color(0.04f, 0.055f, 0.075f, 0.99f);
        Outline panelOutline = panel.AddComponent<Outline>();
        panelOutline.effectColor = new Color(0.8f, 0.62f, 0.32f, 0.92f);
        panelOutline.effectDistance = new Vector2(2f, -2f);

        TMP_FontAsset font = phaseText != null
            ? phaseText.font
            : cardActionText != null ? cardActionText.font : null;
        CreateCardChoiceText(
            panel.transform,
            "Title",
            title,
            font,
            27f,
            new Vector2(0f, 226f),
            new Vector2(800f, 46f),
            FontStyles.Bold
        );
        CreateCardChoiceText(
            panel.transform,
            "Hint",
            hideHandCards
                ? "手牌以暗牌显示；装备区的牌面公开"
                : owner == null ? "从亮出的牌中选择一张" : "选择一张手牌",
            font,
            16f,
            new Vector2(0f, 188f),
            new Vector2(800f, 30f),
            FontStyles.Normal
        );

        GameObject viewportObject = new GameObject(
            "ChoiceViewport",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(RectMask2D),
            typeof(ScrollRect)
        );
        viewportObject.transform.SetParent(panel.transform, false);
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        viewportRect.anchorMin = new Vector2(0.5f, 0.5f);
        viewportRect.anchorMax = new Vector2(0.5f, 0.5f);
        viewportRect.pivot = new Vector2(0.5f, 0.5f);
        viewportRect.anchoredPosition = new Vector2(0f, -12f);
        viewportRect.sizeDelta = new Vector2(840f, 350f);
        viewportObject.GetComponent<Image>().color = new Color(0.018f, 0.028f, 0.042f, 0.75f);
        viewportObject.GetComponent<Image>().raycastTarget = true;

        GameObject contentObject = new GameObject("ChoiceContent", typeof(RectTransform));
        contentObject.transform.SetParent(viewportObject.transform, false);
        RectTransform contentRect = contentObject.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        int columns = 4;
        int rows = Mathf.CeilToInt(choices.Count / (float)columns);
        contentRect.sizeDelta = new Vector2(0f, Mathf.Max(350f, rows * 84f + 16f));
        ScrollRect scrollRect = viewportObject.GetComponent<ScrollRect>();
        scrollRect.content = contentRect;
        scrollRect.viewport = viewportRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 42f;

        for (int i = 0; i < choices.Count; i++)
        {
            CardData choice = choices[i];
            bool isHandCard = owner != null && owner.handCards.Contains(choice);
            string optionLabel;
            GameUIButtonTone tone;
            if (isHandCard && hideHandCards)
            {
                optionLabel = "暗牌 " + (owner.handCards.IndexOf(choice) + 1) + "\n手牌内容隐藏";
                tone = GameUIButtonTone.Navigation;
            }
            else
            {
                string zone = owner == null ? fixedChoiceLabel : isHandCard ? "手牌" : "装备区";
                optionLabel = zone + " · " + choice.cardName + "\n" + choice.GetSuitRankRichText();
                tone = choice.category == CardCategory.Equip
                    ? GameUIButtonTone.Navigation
                    : GameUIButtonTone.Main;
            }

            CreateCardChoiceOption(
                contentRect,
                optionLabel,
                new Vector2(-306f + (i % columns) * 204f, -12f - (i / columns) * 84f),
                choice,
                font,
                tone
            );
        }

        CreateCardChoiceButton(
            panel.transform,
            "CancelChoice",
            cancelLabel,
            new Vector2(0f, -235f),
            font,
            GameUIButtonTone.Navigation,
            () => CompleteCardChoice(null)
        );
        cardChoiceOverlay.transform.SetAsLastSibling();
    }

    private void CreateCardChoiceOption(
        RectTransform parent,
        string label,
        Vector2 position,
        CardData card,
        TMP_FontAsset font,
        GameUIButtonTone tone
    )
    {
        GameObject buttonObject = new GameObject(
            "CardChoiceOption",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button)
        );
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(188f, 70f);

        Button button = buttonObject.GetComponent<Button>();
        GameUIButtonStyler.Apply(button, tone);
        GameSfxPlayer.RegisterButton(button);
        button.onClick.AddListener(() => CompleteCardChoice(card));

        TMP_Text labelText = CreateCardChoiceText(
            button.transform,
            "Label",
            label,
            font,
            17f,
            Vector2.zero,
            new Vector2(174f, 60f),
            FontStyles.Bold
        );
        labelText.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        labelText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        labelText.raycastTarget = false;
    }

    private void CreateCardChoiceButton(
        Transform parent,
        string objectName,
        string label,
        Vector2 position,
        TMP_FontAsset font,
        GameUIButtonTone tone,
        UnityEngine.Events.UnityAction action
    )
    {
        GameObject buttonObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button)
        );
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(190f, 54f);

        Button button = buttonObject.GetComponent<Button>();
        GameUIButtonStyler.Apply(button, tone);
        GameSfxPlayer.RegisterButton(button);
        button.onClick.AddListener(action);

        TMP_Text labelText = CreateCardChoiceText(
            button.transform,
            "Label",
            label,
            font,
            19f,
            Vector2.zero,
            new Vector2(180f, 48f),
            FontStyles.Bold
        );
        labelText.rectTransform.anchorMin = Vector2.zero;
        labelText.rectTransform.anchorMax = Vector2.one;
        labelText.raycastTarget = false;
    }

    private TMP_Text CreateCardChoiceText(
        Transform parent,
        string objectName,
        string value,
        TMP_FontAsset font,
        float size,
        Vector2 position,
        Vector2 rectSize,
        FontStyles style
    )
    {
        GameObject textObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI)
        );
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = rectSize;

        TMP_Text label = textObject.GetComponent<TMP_Text>();
        label.font = font;
        label.text = value;
        label.fontSize = size;
        label.fontSizeMin = Mathf.Max(11f, size - 5f);
        label.fontSizeMax = size;
        label.enableAutoSizing = true;
        label.enableWordWrapping = true;
        label.alignment = TextAlignmentOptions.Center;
        label.fontStyle = style;
        label.color = new Color(0.95f, 0.91f, 0.81f, 1f);
        label.raycastTarget = false;
        Outline outline = textObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.82f);
        outline.effectDistance = new Vector2(1f, -1f);
        return label;
    }

    private void CompleteCardChoice(CardData choice)
    {
        Action<CardData> callback = cardChoiceCallback;
        cardChoiceCallback = null;
        if (cardChoiceOverlay != null)
        {
            Destroy(cardChoiceOverlay);
            cardChoiceOverlay = null;
        }

        callback?.Invoke(choice);
    }

    private void PlayCharacterVoice(Player source)
    {
        GameSfxPlayer.PlayCharacterCardVoice(source);
    }

    private void PlayCardFlight(Player source, Player target, CardData card)
    {
        if (card != null && card.effectType == CardEffectType.Attack)
        {
            PlayAttackFlight(source, target);
            return;
        }

        Canvas canvas = GetComponentInParent<Canvas>();
        RectTransform canvasRect = canvas != null ? canvas.transform as RectTransform : null;
        Vector2 start;
        Vector2 end;
        if (canvasRect == null
            || !TryGetSourcePoint(source, canvas, canvasRect, out start)
            || !TryGetPanelPoint(target, canvas, canvasRect, out end))
        {
            return;
        }

        Vector2 direction = (end - start).normalized;
        start += direction * 52f;
        end -= direction * 58f;

        CreateCardFlightVisual(card, start, end, true, new Vector2(78f, 104f));
    }

    private void PlayAttackFlight(Player source, Player target)
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        RectTransform canvasRect = canvas != null ? canvas.transform as RectTransform : null;
        Vector2 start;
        Vector2 targetCenter;
        Vector2 end;
        if (canvas == null
            || canvasRect == null
            || !TryGetSourcePoint(source, canvas, canvasRect, out start)
            || !TryGetPanelPoint(target, canvas, canvasRect, out targetCenter))
        {
            return;
        }

        Vector2 direction = (targetCenter - start).normalized;
        start += direction * 48f;
        if (!TryGetPanelEdgePoint(target, start, canvas, canvasRect, out end))
        {
            end = targetCenter - direction * 82f;
        }

        GameObject overlayObject = CreateFullCanvasOverlay(canvas, "AttackStrikeOverlay");
        CanvasGroup overlayGroup = overlayObject.AddComponent<CanvasGroup>();
        overlayGroup.blocksRaycasts = false;
        overlayGroup.interactable = false;

        GameObject arrowObject = CreateArrowGraphic(overlayObject.transform, start, end);
        TargetArrowGraphic arrow = arrowObject.GetComponent<TargetArrowGraphic>();
        arrow.color = new Color(0.9f, 0.36f, 0.22f, 0.78f);
        arrow.lineWidth = 4.5f;
        arrow.arrowLength = 20f;
        arrow.arrowWidth = 20f;

        GameObject slashObject = new GameObject(
            "SlashTrail",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(AttackSlashGraphic)
        );
        slashObject.transform.SetParent(overlayObject.transform, false);
        RectTransform slashRect = slashObject.GetComponent<RectTransform>();
        slashRect.anchorMin = new Vector2(0.5f, 0.5f);
        slashRect.anchorMax = new Vector2(0.5f, 0.5f);
        slashRect.pivot = new Vector2(0.5f, 0.5f);
        slashRect.sizeDelta = new Vector2(104f, 66f);
        slashObject.GetComponent<AttackSlashGraphic>().raycastTarget = false;

        KeepBattleHudAboveEffect(canvas);
        StartCoroutine(AnimateAttackFlight(overlayObject, slashRect, overlayGroup, start, end));
    }

    private void PlayEquipmentFlight(Player owner, CardData equipment)
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        RectTransform canvasRect = canvas != null ? canvas.transform as RectTransform : null;
        Vector2 start;
        Vector2 end;
        if (canvasRect == null
            || !TryGetSourcePoint(owner, canvas, canvasRect, out start)
            || !TryGetEquipmentPoint(owner, equipment.effectType, canvas, canvasRect, out end))
        {
            return;
        }

        CreateCardFlightVisual(equipment, start, end, false, new Vector2(96f, 128f));
    }

    private bool TryGetEquipmentPoint(
        Player owner,
        CardEffectType equipmentType,
        Canvas canvas,
        RectTransform canvasRect,
        out Vector2 localPoint
    )
    {
        BattleCharacterPanel[] panels = FindObjectsOfType<BattleCharacterPanel>(true);
        foreach (BattleCharacterPanel panel in panels)
        {
            if (panel == null || panel.player != owner)
            {
                continue;
            }

            RectTransform slotRect = panel.GetEquipmentSlotRect(equipmentType);
            if (slotRect != null)
            {
                return TryConvertRectCenter(slotRect, canvas, canvasRect, out localPoint);
            }
        }

        return TryGetPanelPoint(owner, canvas, canvasRect, out localPoint);
    }

    private IEnumerator PulseEquipmentSlotAfterArrival(Player owner, CardData equipment)
    {
        yield return new WaitForSecondsRealtime(0.48f);
        BattleCharacterPanel[] panels = FindObjectsOfType<BattleCharacterPanel>(true);
        foreach (BattleCharacterPanel panel in panels)
        {
            if (panel != null && panel.player == owner)
            {
                panel.PulseEquipmentSlot(equipment.effectType);
                break;
            }
        }
    }

    private void CreateCardFlightVisual(
        CardData card,
        Vector2 start,
        Vector2 end,
        bool showTargetArrow,
        Vector2 cardSize
    )
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null || card == null)
        {
            return;
        }

        GameObject overlayObject = new GameObject("CardActionOverlay", typeof(RectTransform));
        overlayObject.transform.SetParent(canvas.transform, false);
        overlayObject.transform.SetAsLastSibling();
        RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.pivot = new Vector2(0.5f, 0.5f);
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        KeepBattleHudAboveEffect(canvas);

        CanvasGroup arrowGroup = overlayObject.AddComponent<CanvasGroup>();
        arrowGroup.blocksRaycasts = false;
        arrowGroup.interactable = false;
        if (showTargetArrow)
        {
            GameObject arrowObject = new GameObject(
                "TargetArrow",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TargetArrowGraphic)
            );
            arrowObject.transform.SetParent(overlayObject.transform, false);
            RectTransform arrowRect = arrowObject.GetComponent<RectTransform>();
            arrowRect.anchorMin = Vector2.zero;
            arrowRect.anchorMax = Vector2.one;
            arrowRect.pivot = new Vector2(0.5f, 0.5f);
            arrowRect.offsetMin = Vector2.zero;
            arrowRect.offsetMax = Vector2.zero;
            TargetArrowGraphic arrow = arrowObject.GetComponent<TargetArrowGraphic>();
            arrow.color = new Color(0.88f, 0.69f, 0.38f, 0.84f);
            arrow.lineWidth = 3.5f;
            arrow.arrowLength = 17f;
            arrow.arrowWidth = 18f;
            arrow.raycastTarget = false;
            arrow.SetPath(start, end);
        }

        GameObject projectileObject = new GameObject(
            "FlyingCard",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(CanvasGroup)
        );
        projectileObject.transform.SetParent(overlayObject.transform, false);
        RectTransform projectileRect = projectileObject.GetComponent<RectTransform>();
        projectileRect.anchorMin = new Vector2(0.5f, 0.5f);
        projectileRect.anchorMax = new Vector2(0.5f, 0.5f);
        projectileRect.pivot = new Vector2(0.5f, 0.5f);
        projectileRect.sizeDelta = cardSize;

        Image projectileImage = projectileObject.GetComponent<Image>();
        projectileImage.sprite = CardArtLibrary.GetArtwork(card);
        projectileImage.preserveAspect = true;
        projectileImage.color = Color.white;
        projectileImage.raycastTarget = false;
        Outline projectileOutline = projectileObject.AddComponent<Outline>();
        projectileOutline.effectColor = new Color(0.94f, 0.76f, 0.43f, 0.72f);
        projectileOutline.effectDistance = new Vector2(1f, -1f);
        projectileOutline.useGraphicAlpha = true;

        CreateProjectileLabel(projectileObject.transform, card.cardName);
        StartCoroutine(AnimateCardFlight(overlayObject, projectileRect, arrowGroup, start, end));
    }

    private bool TryGetPanelPoint(Player target, Canvas canvas, RectTransform canvasRect, out Vector2 localPoint)
    {
        BattleCharacterPanel[] panels = FindObjectsOfType<BattleCharacterPanel>(true);
        for (int i = 0; i < panels.Length; i++)
        {
            if (panels[i] == null || panels[i].player != target)
            {
                continue;
            }

            RectTransform panelRect = panels[i].transform as RectTransform;
            if (panelRect == null)
            {
                break;
            }

            Vector3[] corners = new Vector3[4];
            panelRect.GetWorldCorners(corners);
            Vector3 center = (corners[0] + corners[2]) * 0.5f;
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, center);
            Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPoint,
                eventCamera,
                out localPoint
            );
        }

        localPoint = Vector2.zero;
        return false;
    }

    private bool TryGetPanelEdgePoint(
        Player target,
        Vector2 pointTowardSource,
        Canvas canvas,
        RectTransform canvasRect,
        out Vector2 edgePoint
    )
    {
        BattleCharacterPanel[] panels = FindObjectsOfType<BattleCharacterPanel>(true);
        Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        foreach (BattleCharacterPanel panel in panels)
        {
            if (panel == null || panel.player != target)
            {
                continue;
            }

            RectTransform panelRect = panel.transform as RectTransform;
            if (panelRect == null)
            {
                break;
            }

            Vector3[] corners = new Vector3[4];
            panelRect.GetWorldCorners(corners);
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i < corners.Length; i++)
            {
                Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, corners[i]);
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect,
                    screenPoint,
                    eventCamera,
                    out Vector2 localCorner
                ))
                {
                    continue;
                }

                min = Vector2.Min(min, localCorner);
                max = Vector2.Max(max, localCorner);
            }

            Vector2 center = (min + max) * 0.5f;
            Vector2 towardSource = pointTowardSource - center;
            if (towardSource.sqrMagnitude < 1f)
            {
                towardSource = Vector2.up;
            }

            towardSource.Normalize();
            Vector2 extents = (max - min) * 0.5f;
            float xDistance = Mathf.Abs(towardSource.x) < 0.001f
                ? float.PositiveInfinity
                : extents.x / Mathf.Abs(towardSource.x);
            float yDistance = Mathf.Abs(towardSource.y) < 0.001f
                ? float.PositiveInfinity
                : extents.y / Mathf.Abs(towardSource.y);
            edgePoint = center + towardSource * Mathf.Min(xDistance, yDistance);
            return true;
        }

        edgePoint = Vector2.zero;
        return false;
    }

    private GameObject CreateFullCanvasOverlay(Canvas canvas, string objectName)
    {
        GameObject overlayObject = new GameObject(objectName, typeof(RectTransform));
        overlayObject.transform.SetParent(canvas.transform, false);
        overlayObject.transform.SetAsLastSibling();
        RectTransform rect = overlayObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return overlayObject;
    }

    private GameObject CreateArrowGraphic(Transform parent, Vector2 start, Vector2 end)
    {
        GameObject arrowObject = new GameObject(
            "TargetArrow",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TargetArrowGraphic)
        );
        arrowObject.transform.SetParent(parent, false);
        RectTransform arrowRect = arrowObject.GetComponent<RectTransform>();
        arrowRect.anchorMin = Vector2.zero;
        arrowRect.anchorMax = Vector2.one;
        arrowRect.pivot = new Vector2(0.5f, 0.5f);
        arrowRect.offsetMin = Vector2.zero;
        arrowRect.offsetMax = Vector2.zero;
        TargetArrowGraphic arrow = arrowObject.GetComponent<TargetArrowGraphic>();
        arrow.raycastTarget = false;
        arrow.SetPath(start, end);
        return arrowObject;
    }

    private void RefreshPendingAttackCue()
    {
        if (pendingAttackArrow != null)
        {
            float pulse = (Mathf.Sin(Time.unscaledTime * 6f) + 1f) * 0.5f;
            pendingAttackArrow.color = new Color(0.9f, Mathf.Lerp(0.38f, 0.52f, pulse), 0.25f, 0.66f + pulse * 0.14f);
        }

    }

    private void SetPanelPendingResponse(Player target, bool pending)
    {
        BattleCharacterPanel[] panels = FindObjectsOfType<BattleCharacterPanel>(true);
        foreach (BattleCharacterPanel panel in panels)
        {
            if (panel != null && panel.player == target)
            {
                panel.SetPendingResponse(pending);
                break;
            }
        }
    }

    private void KeepBattleHudAboveEffect(Canvas canvas)
    {
        if (canvas == null)
        {
            return;
        }

        PromoteCanvasBranch(cardActionText != null ? cardActionText.transform : null, canvas.transform);
        PromoteCanvasBranch(phaseText != null ? phaseText.transform : null, canvas.transform);
        PromoteCanvasBranch(playerHandText != null ? playerHandText.transform : null, canvas.transform);
        PromoteCanvasBranch(endTurnButton != null ? endTurnButton.transform : null, canvas.transform);

        GameLogger gameLogger = FindObjectOfType<GameLogger>(true);
        if (gameLogger != null)
        {
            PromoteCanvasBranch(gameLogger.transform, canvas.transform);
        }
    }

    private void PromoteCanvasBranch(Transform element, Transform canvasRoot)
    {
        if (element == null || canvasRoot == null)
        {
            return;
        }

        Transform branch = element;
        while (branch.parent != null && branch.parent != canvasRoot)
        {
            branch = branch.parent;
        }

        if (branch.parent == canvasRoot)
        {
            branch.SetAsLastSibling();
        }
    }

    private bool TryGetSourcePoint(Player source, Canvas canvas, RectTransform canvasRect, out Vector2 localPoint)
    {
        if (source == player)
        {
            RectTransform selectedCardRect = selectedCardIndex >= 0 && selectedCardIndex < cardButtons.Count
                && cardButtons[selectedCardIndex] != null
                    ? cardButtons[selectedCardIndex].transform as RectTransform
                    : null;

            if (selectedCardRect != null)
            {
                return TryConvertRectCenter(selectedCardRect, canvas, canvasRect, out localPoint);
            }

            if (handViewportRect != null)
            {
                return TryConvertRectCenter(handViewportRect, canvas, canvasRect, out localPoint);
            }
        }

        return TryGetPanelPoint(source, canvas, canvasRect, out localPoint);
    }

    private bool TryConvertRectCenter(
        RectTransform rectTransform,
        Canvas canvas,
        RectTransform canvasRect,
        out Vector2 localPoint
    )
    {
        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);
        Vector3 center = (corners[0] + corners[2]) * 0.5f;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, center);
        Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPoint,
            eventCamera,
            out localPoint
        );
    }

    private void CreateProjectileLabel(Transform parent, string text)
    {
        GameObject labelObject = new GameObject(
            "CardName",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        labelObject.transform.SetParent(parent, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 0.25f);
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        Image labelPlate = labelObject.GetComponent<Image>();
        labelPlate.color = new Color(0.035f, 0.05f, 0.07f, 0.86f);
        labelPlate.raycastTarget = false;

        GameObject textObject = new GameObject("NameText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(labelObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(2f, 0f);
        textRect.offsetMax = new Vector2(-2f, 0f);
        TMP_Text label = textObject.GetComponent<TMP_Text>();
        if (cardActionText != null)
        {
            label.font = cardActionText.font;
        }
        label.text = text;
        label.fontSize = 18f;
        label.color = new Color(1f, 0.94f, 0.76f, 1f);
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true;
        label.fontSizeMin = 12f;
        label.fontSizeMax = 18f;
        label.raycastTarget = false;
    }

    private IEnumerator PlayDiscardRevealQueue()
    {
        isPlayingDiscardReveal = true;
        while (discardRevealQueue.Count > 0)
        {
            DiscardRevealRequest request = discardRevealQueue.Dequeue();
            yield return StartCoroutine(AnimateDiscardReveal(request));
        }
        isPlayingDiscardReveal = false;
    }

    private IEnumerator AnimateDiscardReveal(DiscardRevealRequest request)
    {
        if (request == null || request.card == null)
        {
            yield break;
        }

        string ownerName = request.owner != null ? request.owner.playerName : "弃置牌";
        string reason = string.IsNullOrEmpty(request.reason) ? "进入弃牌堆" : request.reason;
        string location = request.wasEquipment
            ? "装备区"
            : !string.IsNullOrEmpty(request.reason) && request.reason.Contains("判定")
                ? "判定区"
                : "手牌";
        string categoryName = request.card.category == CardCategory.Equip
            ? "装备牌"
            : request.card.category == CardCategory.Basic ? "基本牌" : "锦囊牌";
        ShowCenterTableEvent(
            new[] { request.card },
            "弃置展示",
            ownerName + " · " + reason + " · " + location + " · " + categoryName
                + " · " + request.card.GetSuitRankRichText(),
            1.2f,
            false
        );
        GameSfxPlayer.PlayCardDiscard();
        yield return new WaitForSecondsRealtime(1.2f);
    }

    private void CreateRevealText(
        Transform parent,
        string objectName,
        string text,
        Vector2 anchorMin,
        Vector2 anchorMax,
        float fontSize,
        Color color,
        TextAlignmentOptions alignment,
        Vector2? size = null,
        Vector2? anchoredPosition = null
    )
    {
        GameObject textObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI)
        );
        textObject.transform.SetParent(parent, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = anchorMin;
        textRect.anchorMax = anchorMax;
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        if (size.HasValue)
        {
            textRect.sizeDelta = size.Value;
        }
        if (anchoredPosition.HasValue)
        {
            textRect.anchoredPosition = anchoredPosition.Value;
        }

        TMP_Text label = textObject.GetComponent<TMP_Text>();
        if (cardActionText != null)
        {
            label.font = cardActionText.font;
        }
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = alignment;
        label.enableAutoSizing = true;
        label.fontSizeMin = Mathf.Max(11f, fontSize * 0.68f);
        label.fontSizeMax = fontSize;
        label.enableWordWrapping = objectName == "DiscardDescription";
        label.raycastTarget = false;
    }

    private void ShowCenterTableEvent(
        IList<CardData> cards,
        string heading,
        string details,
        float visibleSeconds,
        bool pinned
    )
    {
        if (!EnsureCenterEventUI())
        {
            return;
        }

        centerEventGeneration++;
        if (centerEventRoutine != null)
        {
            StopCoroutine(centerEventRoutine);
            centerEventRoutine = null;
        }

        centerEventPinned = pinned;
        centerEventHeading.text = heading ?? string.Empty;
        centerEventDetails.text = details ?? string.Empty;
        displayedCenterCards.Clear();
        if (cards != null)
        {
            for (int i = 0; i < cards.Count && displayedCenterCards.Count < 2; i++)
            {
                if (cards[i] != null)
                {
                    displayedCenterCards.Add(cards[i]);
                }
            }
        }

        centerEventHasCards = displayedCenterCards.Count > 0;
        Canvas canvas = GetComponentInParent<Canvas>();
        RectTransform canvasRect = canvas != null ? canvas.transform as RectTransform : null;
        Vector2 canvasSize = canvasRect != null
            ? canvasRect.rect.size
            : new Vector2(Screen.width, Screen.height);
        LayoutCenterEvent(canvasSize);
        RefreshCenterEventCards(canvasSize);

        centerEventRoot.SetActive(true);
        centerEventRoot.transform.SetAsLastSibling();
        int generation = centerEventGeneration;
        centerEventRoutine = StartCoroutine(
            AnimateCenterEvent(generation, Mathf.Max(0f, visibleSeconds), pinned)
        );
    }

    private bool EnsureCenterEventUI()
    {
        if (centerEventRoot != null)
        {
            return true;
        }

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            return false;
        }

        centerEventRoot = new GameObject(
            "CenterTableEvent",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(CanvasGroup)
        );
        centerEventRoot.layer = canvas.gameObject.layer;
        centerEventRoot.transform.SetParent(canvas.transform, false);

        RectTransform rootRect = centerEventRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);

        Image backing = centerEventRoot.GetComponent<Image>();
        backing.color = new Color(0.025f, 0.04f, 0.055f, 0.91f);
        backing.raycastTarget = false;
        Outline outline = centerEventRoot.AddComponent<Outline>();
        outline.effectColor = new Color(0.77f, 0.59f, 0.31f, 0.9f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        outline.useGraphicAlpha = true;

        centerEventCanvasGroup = centerEventRoot.GetComponent<CanvasGroup>();
        centerEventCanvasGroup.blocksRaycasts = false;
        centerEventCanvasGroup.interactable = false;
        centerEventCanvasGroup.alpha = 0f;

        centerEventHeading = CreateCenterEventLabel(
            centerEventRoot.transform,
            "Heading",
            new Vector2(0.04f, 0.84f),
            new Vector2(0.96f, 0.97f),
            18f,
            new Color(1f, 0.87f, 0.58f, 1f),
            TextAlignmentOptions.Center
        );
        centerEventCardsRoot = new GameObject("Cards", typeof(RectTransform))
            .GetComponent<RectTransform>();
        centerEventCardsRoot.SetParent(centerEventRoot.transform, false);
        centerEventCardsRoot.anchorMin = new Vector2(0.04f, 0.17f);
        centerEventCardsRoot.anchorMax = new Vector2(0.96f, 0.83f);
        centerEventCardsRoot.pivot = new Vector2(0.5f, 0.5f);
        centerEventCardsRoot.offsetMin = Vector2.zero;
        centerEventCardsRoot.offsetMax = Vector2.zero;

        centerEventDetails = CreateCenterEventLabel(
            centerEventRoot.transform,
            "Details",
            new Vector2(0.04f, 0.025f),
            new Vector2(0.96f, 0.16f),
            15f,
            new Color(0.96f, 0.93f, 0.85f, 1f),
            TextAlignmentOptions.Center
        );

        centerEventRoot.SetActive(false);
        return true;
    }

    private TMP_Text CreateCenterEventLabel(
        Transform parent,
        string objectName,
        Vector2 anchorMin,
        Vector2 anchorMax,
        float fontSize,
        Color color,
        TextAlignmentOptions alignment
    )
    {
        GameObject labelObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI)
        );
        labelObject.layer = parent.gameObject.layer;
        labelObject.transform.SetParent(parent, false);
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TMP_Text label = labelObject.GetComponent<TMP_Text>();
        label.font = phaseText != null
            ? phaseText.font
            : cardActionText != null ? cardActionText.font : null;
        label.fontSize = fontSize;
        label.fontSizeMin = Mathf.Max(11f, fontSize * 0.72f);
        label.fontSizeMax = fontSize;
        label.enableAutoSizing = true;
        label.enableWordWrapping = true;
        label.alignment = alignment;
        label.color = color;
        label.fontStyle = FontStyles.Bold;
        label.raycastTarget = false;
        return label;
    }

    private void LayoutCenterEvent(Vector2 canvasSize)
    {
        if (centerEventRoot == null)
        {
            return;
        }

        float scale = Mathf.Clamp(canvasSize.y / 1080f, 0.68f, 1.12f);
        RectTransform rootRect = centerEventRoot.transform as RectTransform;
        if (rootRect == null)
        {
            return;
        }

        float width = Mathf.Min(520f * scale, canvasSize.x * 0.48f);
        float height = centerEventHasCards ? 310f * scale : 104f * scale;
        rootRect.anchoredPosition = new Vector2(0f, canvasSize.y * 0.105f);
        rootRect.sizeDelta = new Vector2(width, height);
        if (centerEventHeading != null)
        {
            RectTransform headingRect = centerEventHeading.rectTransform;
            headingRect.anchorMin = centerEventHasCards
                ? new Vector2(0.04f, 0.86f)
                : new Vector2(0.04f, 0.58f);
            headingRect.anchorMax = new Vector2(0.96f, 0.97f);
        }
        if (centerEventDetails != null)
        {
            RectTransform detailsRect = centerEventDetails.rectTransform;
            detailsRect.anchorMin = centerEventHasCards
                ? new Vector2(0.04f, 0.025f)
                : new Vector2(0.04f, 0.08f);
            detailsRect.anchorMax = centerEventHasCards
                ? new Vector2(0.96f, 0.12f)
                : new Vector2(0.96f, 0.54f);
        }
        if (centerEventCardsRoot != null)
        {
            centerEventCardsRoot.gameObject.SetActive(centerEventHasCards);
        }

        if (centerEventHeading != null)
        {
            centerEventHeading.fontSize = centerEventHasCards ? 18f * scale : 17f * scale;
            centerEventHeading.fontSizeMax = centerEventHeading.fontSize;
        }
        if (centerEventDetails != null)
        {
            centerEventDetails.fontSize = centerEventHasCards ? 15f * scale : 17f * scale;
            centerEventDetails.fontSizeMax = centerEventDetails.fontSize;
        }
    }

    private void RefreshCenterEventCards(Vector2 canvasSize)
    {
        if (centerEventCardsRoot == null)
        {
            return;
        }

        for (int i = centerEventCardsRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(centerEventCardsRoot.GetChild(i).gameObject);
        }

        if (displayedCenterCards.Count == 0)
        {
            return;
        }

        float scale = Mathf.Clamp(canvasSize.y / 1080f, 0.68f, 1.12f);
        bool multiple = displayedCenterCards.Count > 1;
        float cardWidth = (multiple ? 126f : 142f) * scale;
        float cardHeight = (multiple ? 174f : 196f) * scale;
        float spacing = 18f * scale;
        float rowWidth = displayedCenterCards.Count * cardWidth
            + Mathf.Max(0, displayedCenterCards.Count - 1) * spacing;
        float firstX = -rowWidth * 0.5f + cardWidth * 0.5f;

        for (int i = 0; i < displayedCenterCards.Count; i++)
        {
            CreateCenterEventCard(
                displayedCenterCards[i],
                new Vector2(firstX + i * (cardWidth + spacing), 0f),
                new Vector2(cardWidth, cardHeight),
                scale
            );
        }
    }

    private void CreateCenterEventCard(
        CardData card,
        Vector2 position,
        Vector2 size,
        float scale
    )
    {
        GameObject cardObject = new GameObject(
            "TableCard_" + card.cardName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        cardObject.layer = centerEventCardsRoot.gameObject.layer;
        cardObject.transform.SetParent(centerEventCardsRoot, false);
        RectTransform cardRect = cardObject.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = position;
        cardRect.sizeDelta = size;

        Image backing = cardObject.GetComponent<Image>();
        backing.color = new Color(0.035f, 0.048f, 0.06f, 1f);
        backing.raycastTarget = false;
        Outline outline = cardObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.84f, 0.68f, 0.4f, 0.95f);
        outline.effectDistance = new Vector2(1f * scale, -1f * scale);
        outline.useGraphicAlpha = true;

        GameObject artObject = new GameObject(
            "Artwork",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        artObject.layer = cardObject.layer;
        artObject.transform.SetParent(cardObject.transform, false);
        RectTransform artRect = artObject.GetComponent<RectTransform>();
        artRect.anchorMin = new Vector2(0.07f, 0.22f);
        artRect.anchorMax = new Vector2(0.93f, 0.77f);
        artRect.offsetMin = Vector2.zero;
        artRect.offsetMax = Vector2.zero;
        Image art = artObject.GetComponent<Image>();
        art.sprite = CardArtLibrary.GetArtwork(card);
        art.preserveAspect = true;
        art.color = Color.white;
        art.raycastTarget = false;

        GameObject titlePlate = new GameObject(
            "TitlePlate",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        titlePlate.layer = cardObject.layer;
        titlePlate.transform.SetParent(cardObject.transform, false);
        RectTransform titleRect = titlePlate.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.04f, 0.78f);
        titleRect.anchorMax = new Vector2(0.96f, 0.97f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;
        Image titleBackground = titlePlate.GetComponent<Image>();
        titleBackground.color = GetCenterCardAccent(card);
        titleBackground.raycastTarget = false;
        CreateCenterCardLabel(
            titlePlate.transform,
            "CardName",
            card.cardName,
            14f * scale,
            new Color(0.99f, 0.96f, 0.87f, 1f)
        );

        string categoryName = card.category == CardCategory.Equip
            ? "装备"
            : card.category == CardCategory.Basic ? "基本" : "锦囊";
        CreateCenterCardLabel(
            cardObject.transform,
            "CardMetadata",
            categoryName + " · " + card.GetSuitRankRichText(),
            10f * scale,
            new Color(0.9f, 0.83f, 0.68f, 1f),
            new Vector2(0.05f, 0.035f),
            new Vector2(0.95f, 0.2f)
        );
    }

    private void CreateCenterCardLabel(
        Transform parent,
        string objectName,
        string value,
        float fontSize,
        Color color,
        Vector2? anchorMin = null,
        Vector2? anchorMax = null
    )
    {
        GameObject labelObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI)
        );
        labelObject.layer = parent.gameObject.layer;
        labelObject.transform.SetParent(parent, false);
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin ?? Vector2.zero;
        rect.anchorMax = anchorMax ?? Vector2.one;
        rect.offsetMin = new Vector2(3f, 1f);
        rect.offsetMax = new Vector2(-3f, -1f);

        TMP_Text label = labelObject.GetComponent<TMP_Text>();
        label.font = phaseText != null
            ? phaseText.font
            : cardActionText != null ? cardActionText.font : null;
        label.text = value;
        label.fontSize = fontSize;
        label.fontSizeMin = Mathf.Max(8f, fontSize * 0.72f);
        label.fontSizeMax = fontSize;
        label.enableAutoSizing = true;
        label.alignment = TextAlignmentOptions.Center;
        label.color = color;
        label.fontStyle = FontStyles.Bold;
        label.raycastTarget = false;
    }

    private Color GetCenterCardAccent(CardData card)
    {
        if (card == null)
        {
            return new Color(0.18f, 0.2f, 0.22f, 1f);
        }

        switch (card.category)
        {
            case CardCategory.Basic:
                return new Color(0.45f, 0.18f, 0.14f, 1f);
            case CardCategory.Trick:
                return new Color(0.25f, 0.19f, 0.39f, 1f);
            default:
                return new Color(0.42f, 0.31f, 0.13f, 1f);
        }
    }

    private IEnumerator AnimateCenterEvent(int generation, float visibleSeconds, bool pinned)
    {
        centerEventCanvasGroup.alpha = 0f;
        centerEventRoot.transform.localScale = Vector3.one * 0.96f;
        const float fadeInSeconds = 0.12f;
        float timer = 0f;
        while (timer < fadeInSeconds)
        {
            if (generation != centerEventGeneration)
            {
                yield break;
            }
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / fadeInSeconds);
            centerEventCanvasGroup.alpha = t;
            centerEventRoot.transform.localScale = Vector3.Lerp(Vector3.one * 0.96f, Vector3.one, t);
            yield return null;
        }

        centerEventCanvasGroup.alpha = 1f;
        centerEventRoot.transform.localScale = Vector3.one;
        if (pinned)
        {
            centerEventRoutine = null;
            yield break;
        }

        if (visibleSeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(visibleSeconds);
        }

        yield return StartCoroutine(FadeOutCenterEvent(generation));
    }

    private void ScheduleCenterEventHide(float visibleSeconds)
    {
        if (centerEventRoot == null || !centerEventRoot.activeSelf)
        {
            return;
        }
        if (centerEventRoutine != null)
        {
            StopCoroutine(centerEventRoutine);
        }
        centerEventGeneration++;
        centerEventRoutine = StartCoroutine(
            HideCenterEventAfter(centerEventGeneration, visibleSeconds)
        );
    }

    private IEnumerator HideCenterEventAfter(int generation, float visibleSeconds)
    {
        yield return new WaitForSecondsRealtime(visibleSeconds);
        yield return StartCoroutine(FadeOutCenterEvent(generation));
    }

    private IEnumerator FadeOutCenterEvent(int generation)
    {
        const float fadeOutSeconds = 0.18f;
        float timer = 0f;
        float initialAlpha = centerEventCanvasGroup != null ? centerEventCanvasGroup.alpha : 1f;
        while (timer < fadeOutSeconds)
        {
            if (generation != centerEventGeneration)
            {
                yield break;
            }
            timer += Time.unscaledDeltaTime;
            centerEventCanvasGroup.alpha = Mathf.Lerp(
                initialAlpha,
                0f,
                Mathf.Clamp01(timer / fadeOutSeconds)
            );
            yield return null;
        }

        if (generation == centerEventGeneration && centerEventRoot != null)
        {
            centerEventRoot.SetActive(false);
            centerEventPinned = false;
            centerEventHasCards = false;
            displayedCenterCards.Clear();
            lastPlayedCard = null;
            lastCardUser = null;
            lastCardTarget = null;
            lastPendingResponseTarget = null;
        }
        centerEventRoutine = null;
    }

    private void SetRevealAnchors(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private IEnumerator AnimateCardFlight(
        GameObject overlayObject,
        RectTransform projectileRect,
        CanvasGroup arrowGroup,
        Vector2 start,
        Vector2 end
    )
    {
        const float duration = 0.48f;
        Vector2 control = (start + end) * 0.5f
            + Vector2.up * Mathf.Clamp(Vector2.Distance(start, end) * 0.075f, 18f, 62f);
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / duration);
            float eased = t * t * (3f - 2f * t);
            projectileRect.anchoredPosition = (1f - eased) * (1f - eased) * start
                + 2f * (1f - eased) * eased * control
                + eased * eased * end;
            float angle = Mathf.Lerp(6f, -4f, t);
            projectileRect.localRotation = Quaternion.Euler(0f, 0f, angle);
            float scale = Mathf.Lerp(0.9f, 1.05f, Mathf.Sin(t * Mathf.PI));
            projectileRect.localScale = Vector3.one * scale;
            float fade = t < 0.78f ? 1f : 1f - (t - 0.78f) / 0.22f;
            arrowGroup.alpha = Mathf.Min(0.92f, fade);
            yield return null;
        }

        Destroy(overlayObject);
    }

    private IEnumerator AnimateAttackFlight(
        GameObject overlayObject,
        RectTransform slashRect,
        CanvasGroup overlayGroup,
        Vector2 start,
        Vector2 end
    )
    {
        const float duration = 0.34f;
        Vector2 direction = (end - start).normalized;
        Vector2 control = (start + end) * 0.5f
            + Vector2.up * Mathf.Clamp(Vector2.Distance(start, end) * 0.055f, 14f, 54f);
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 45f;
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            slashRect.anchoredPosition = (1f - eased) * (1f - eased) * start
                + 2f * (1f - eased) * eased * control
                + eased * eased * end;
            slashRect.localRotation = Quaternion.Euler(0f, 0f, angle);
            slashRect.localScale = Vector3.one * Mathf.Lerp(0.8f, 1.06f, Mathf.Sin(t * Mathf.PI * 0.72f));
            overlayGroup.alpha = t < 0.68f ? 0.82f : Mathf.Lerp(0.82f, 0f, (t - 0.68f) / 0.32f);
            yield return null;
        }

        Destroy(overlayObject);
    }

    private void StyleActionButton()
    {
        if (endTurnButton == null)
        {
            return;
        }

        GameUIButtonStyler.Apply(endTurnButton, GameUIButtonTone.PrimaryAction);

        if (endTurnButtonText != null)
        {
            ConfigureHudText(endTurnButtonText, 28f, 20f);
            endTurnButtonText.color = new Color(1f, 0.88f, 0.62f, 1f);
        }
    }

    private IEnumerator ShowCardActionCoroutine(string message)
    {
        cardActionText.gameObject.SetActive(true);
        cardActionText.text = message;

        cardActionCanvasGroup.alpha = 0f;
        cardActionText.transform.localScale = Vector3.one * 0.85f;

        float timer = 0f;

        while (timer < cardActionFadeInTime)
        {
            timer += Time.deltaTime;
            float t = timer / cardActionFadeInTime;
            cardActionCanvasGroup.alpha = Mathf.Lerp(0f, 1f, t);
            cardActionText.transform.localScale = Vector3.Lerp(Vector3.one * 0.85f, Vector3.one, t);
            yield return null;
        }

        cardActionCanvasGroup.alpha = 1f;
        cardActionText.transform.localScale = Vector3.one;

        yield return new WaitForSeconds(cardActionStayTime);

        timer = 0f;

        while (timer < cardActionFadeOutTime)
        {
            timer += Time.deltaTime;
            float t = timer / cardActionFadeOutTime;
            cardActionCanvasGroup.alpha = Mathf.Lerp(1f, 0f, t);
            yield return null;
        }

        cardActionCanvasGroup.alpha = 0f;
        cardActionText.gameObject.SetActive(false);
    }

    public void SelectCard(CardData card, int index)
    {
        if (card == null)
        {
            selectedCard = null;
            selectedCardIndex = -1;
            selectedTarget = null;
        }
        else if (selectedCardIndex == index)
        {
            selectedCard = null;
            selectedCardIndex = -1;
            selectedTarget = null;
        }
        else
        {
            selectedCard = card;
            selectedCardIndex = index;

            if (!selectedCard.needTarget)
            {
                selectedTarget = null;
            }
        }

        RefreshActionButton();
        RefreshCardSelectionView();
        RefreshPhaseText();
    }

    public void SelectTarget(Player target)
    {
        if (selectedCard == null)
        {
            Debug.Log("请先选择一张需要目标的牌");
            selectedTarget = null;
            RefreshActionButton();
            RefreshPhaseText();
            return;
        }

        if (!selectedCard.needTarget)
        {
            Debug.Log("当前选择的牌不需要目标");
            selectedTarget = null;
            RefreshActionButton();
            RefreshPhaseText();
            return;
        }

        if (target == null)
        {
            selectedTarget = null;
        }
        else if (
            target == player
            && selectedCard.effectType != CardEffectType.Protect
            && selectedCard.effectType != CardEffectType.ReduceSuspicion
            && selectedCard.effectType != CardEffectType.AidDraw
        )
        {
            Debug.Log("不能选择自己作为目标");
            selectedTarget = null;
        }
        else if (turnManager != null && !turnManager.CanUseCardOnTarget(player, target, selectedCard))
        {
            selectedTarget = null;
            string message = GetInvalidTargetMessage(target, selectedCard);
            Debug.Log(message);
            ShowCardAction(message);
        }
        else if (selectedTarget == target)
        {
            selectedTarget = null;
        }
        else
        {
            selectedTarget = target;
        }

        if (selectedTarget != null)
        {
            GameSfxPlayer.PlayTargetSelect();
        }

        RefreshActionButton();
        RefreshPhaseText();
    }

    private string GetInvalidTargetMessage(Player target, CardData card)
    {
        if (card != null && target != null)
        {
            if (card.effectType == CardEffectType.ForceDiscard && target.handCards.Count == 0)
            {
                return target.playerName + "没有手牌，不能对其使用这张牌";
            }

            if (card.effectType == CardEffectType.DiscardEquip && !target.HasEquipment())
            {
                return target.playerName + "没有装备，不能对其使用这张牌";
            }

            if ((card.effectType == CardEffectType.DiscardTarget
                    || card.effectType == CardEffectType.StealTarget)
                && target.handCards.Count == 0
                && !target.HasEquipment())
            {
                return target.playerName + "没有手牌或装备，不能对其使用这张牌";
            }
        }

        return "该角色不满足这张牌的使用条件";
    }

    public bool IsTargetSelected(Player target)
    {
        return selectedTarget == target;
    }

    public void OnClickEndTurnButton()
    {
        if (turnManager == null)
        {
            return;
        }

        if (selectedCard != null)
        {
            turnManager.PlayerPlayCard(selectedCard, selectedTarget);
            selectedCard = null;
            selectedCardIndex = -1;
            selectedTarget = null;
            RefreshUI();
            return;
        }

        turnManager.EndPlayerTurn();
        RefreshUI();
    }

    private void ClearInvalidSelectedCard()
    {
        if (player == null)
        {
            selectedCard = null;
            selectedCardIndex = -1;
            selectedTarget = null;
            return;
        }

        if (selectedCardIndex < 0 || selectedCardIndex >= player.handCards.Count)
        {
            selectedCard = null;
            selectedCardIndex = -1;
            selectedTarget = null;
            return;
        }

        if (selectedCard != player.handCards[selectedCardIndex])
        {
            selectedCard = null;
            selectedCardIndex = -1;
            selectedTarget = null;
        }

        if (selectedTarget != null && selectedTarget.currentHp <= 0)
        {
            selectedTarget = null;
        }
    }

    private void RefreshHpText()
    {
        if (player != null && playerHpText != null)
        {
            playerHpText.text = $"玩家血量：{player.currentHp}/{player.maxHp}";
        }

        if (enemy != null && enemyHpText != null)
        {
            enemyHpText.text = $"敌人血量：{enemy.currentHp}/{enemy.maxHp}";
        }
    }

    private void RefreshHandText()
    {
        if (player != null && playerHandText != null)
        {
            int visibleCardLimit = handViewportRect != null
                ? Mathf.FloorToInt(handViewportRect.rect.width / (MinimumHandCardWidth + HandCardSpacing))
                : 0;
            string scrollHint = player.handCards.Count > visibleCardLimit
                ? "（左右滚动查看更多）"
                : "";
            playerHandText.text = "玩家手牌：" + player.handCards.Count + scrollHint;
        }

        if (enemy != null && enemyHandText != null)
        {
            enemyHandText.text = $"手牌：{enemy.handCards.Count}";
        }
    }

    private void RefreshPhaseText()
    {
        if (turnManager == null || phaseText == null)
        {
            return;
        }

        string currentPlayerName = "无";

        if (turnManager.currentPlayer != null)
        {
            currentPlayerName = turnManager.currentPlayer.playerName;
        }

        string extraMessage = "";

        if (turnManager.currentPlayer == player && turnManager.currentPhase == TurnPhase.Play)
        {
            if (selectedCard != null)
            {
                extraMessage = "\n已选择：" + selectedCard.cardName;

                if (selectedCard.needTarget)
                {
                    if (selectedTarget != null)
                    {
                        extraMessage += "\n目标：" + selectedTarget.playerName + "\n点击按钮出牌";
                    }
                    else
                    {
                        extraMessage += "\n请选择目标";
                    }
                }
                else
                {
                    extraMessage += "\n点击按钮确认";
                }
            }
            else
            {
                extraMessage = "\n点击手牌选择，或直接结束回合";
            }
        }

        if (turnManager.currentPlayer == player && turnManager.currentPhase == TurnPhase.Discard)
        {
            int needDiscardCount = player.handCards.Count - player.currentHp;

            if (needDiscardCount > 0)
            {
                if (selectedCard != null)
                {
                    extraMessage = "\n已选择：" + selectedCard.cardName + "，点击按钮弃牌";
                }
                else
                {
                    extraMessage = "\n请弃掉 " + needDiscardCount + " 张牌";
                }
            }
            else
            {
                extraMessage = "\n无需弃牌";
            }
        }

        if (turnManager.currentPhase == TurnPhase.Response)
        {
            if (selectedCard != null)
            {
                extraMessage = "\n已选择：" + selectedCard.cardName + "，点击按钮确认";
            }
            else
            {
                if (turnManager.pendingDefender != null && turnManager.pendingDefender != player
                    && turnManager.pendingResponseCardType == CardEffectType.Defense)
                {
                    extraMessage =
                        "\n队友被杀：选择牌帮队友出闪（轻羽可任意牌当闪），或点击按钮不出";
                }
                else
                {
                    extraMessage = "\n需要打出【" + turnManager.PendingResponseCardName + "】响应，或点击按钮不出";
                }
            }
        }

        if (turnManager.currentPhase == TurnPhase.Dying)
        {
            string dyingName = "角色";

            if (turnManager.dyingPlayer != null)
            {
                dyingName = turnManager.dyingPlayer.playerName;
            }

            if (selectedCard != null)
            {
                extraMessage = "\n已选择：" + selectedCard.cardName + "，点击按钮确认";
            }
            else
            {
                extraMessage = "\n" + dyingName + " 濒死：选择桃，或点击按钮不使用桃";
            }
        }

        string attackInfo = "";

        if (turnManager.currentPlayer != null)
        {
            string attackLimit = turnManager.currentPlayer.weapon != null
                    && turnManager.currentPlayer.weapon.cardName == "诸葛连弩"
                ? "∞"
                : turnManager.currentPlayer.GetAttackLimit().ToString();
            attackInfo =
                "\n本回合已使用杀："
                + turnManager.currentPlayer.usedAttackCountThisTurn
                + "/"
                + attackLimit;
        }

        phaseText.text =
            $"当前角色：{currentPlayerName}\n"
            + $"当前阶段：{GetPhaseName(turnManager.currentPhase)}"
            + attackInfo
            + extraMessage;
    }

    private void RefreshActionButton()
    {
        if (turnManager == null || endTurnButton == null || endTurnButtonText == null)
        {
            return;
        }

        endTurnButton.interactable = true;

        if (selectedCard != null)
        {
            switch (turnManager.currentPhase)
            {
                case TurnPhase.Play:
                    endTurnButtonText.text = "出牌";

                    if (selectedCard.needTarget)
                    {
                        endTurnButton.interactable =
                            turnManager.currentPlayer == player
                            && selectedTarget != null
                            && turnManager.CanUseCardOnTarget(player, selectedTarget, selectedCard);
                    }
                    else
                    {
                        endTurnButton.interactable = turnManager.currentPlayer == player;
                    }

                    return;

                case TurnPhase.Response:
                    endTurnButtonText.text = "出" + turnManager.PendingResponseCardName;
                    endTurnButton.interactable = true;
                    return;

                case TurnPhase.Dying:
                    endTurnButtonText.text = selectedCard.effectType == CardEffectType.Wine
                        ? "使用酒自救"
                        : "使用桃";
                    endTurnButton.interactable = true;
                    return;

                case TurnPhase.Discard:
                    endTurnButtonText.text = "弃牌";
                    endTurnButton.interactable = turnManager.currentPlayer == player;
                    return;
            }
        }

        switch (turnManager.currentPhase)
        {
            case TurnPhase.Play:
                if (turnManager.currentPlayer == player)
                {
                    endTurnButtonText.text = "结束回合";
                    endTurnButton.interactable = true;
                }
                else
                {
                    endTurnButtonText.text = "敌人行动中";
                    endTurnButton.interactable = false;
                }
                break;

            case TurnPhase.Response:
                endTurnButtonText.text = "不出" + turnManager.PendingResponseCardName;
                endTurnButton.interactable = true;
                break;

            case TurnPhase.Dying:
                endTurnButtonText.text = turnManager.dyingPlayer == player
                    ? "不使用桃/酒"
                    : "不使用桃";
                endTurnButton.interactable = true;
                break;

            case TurnPhase.Discard:
                if (turnManager.currentPlayer == player)
                {
                    endTurnButtonText.text = "请选择要弃的牌";
                }
                else
                {
                    // 显示具体角色名，三人场中能区分是哪个AI
                    string aiName =
                        turnManager.currentPlayer != null
                            ? turnManager.currentPlayer.playerName
                            : "敌人";
                    endTurnButtonText.text = aiName + "弃牌中";
                }

                endTurnButton.interactable = false;
                break;

            case TurnPhase.Draw:
                endTurnButtonText.text = "摸牌中";
                endTurnButton.interactable = false;
                break;

            case TurnPhase.End:
                endTurnButtonText.text = "回合结束";
                endTurnButton.interactable = false;
                break;

            default:
                endTurnButtonText.text = "等待中";
                endTurnButton.interactable = false;
                break;
        }
    }

    private void RefreshHandButtons()
    {
        if (playerHandButtonRoot == null || cardButtonPrefab == null || player == null)
        {
            return;
        }

        EnsureHandScrollView();
        if (handContentRect == null || handScrollRect == null || handViewportRect == null)
        {
            return;
        }

        int handCount = player.handCards.Count;
        float previousScrollPosition = handScrollRect.horizontalNormalizedPosition;

        for (int i = handContentRect.childCount - 1; i >= 0; i--)
        {
            Destroy(handContentRect.GetChild(i).gameObject);
        }

        cardButtons.Clear();

        float viewportWidth = Mathf.Max(1f, handViewportRect.rect.width);
        float viewportHeight = Mathf.Max(1f, handViewportRect.rect.height);
        float cardHeight = Mathf.Min(196f, viewportHeight - 20f);
        float spacing = handCount > 1 ? HandCardSpacing : 0f;
        float fitCardWidth = handCount > 0
            ? (viewportWidth - 32f - spacing * (handCount - 1)) / handCount
            : PreferredHandCardWidth;
        float cardWidth = Mathf.Max(
            MinimumHandCardWidth,
            Mathf.Min(PreferredHandCardWidth, cardHeight * (130f / 180f), fitCardWidth)
        );
        float rowWidth = handCount == 0
            ? 0f
            : handCount * cardWidth + spacing * (handCount - 1);
        float contentWidth = Mathf.Max(viewportWidth, rowWidth + 32f);
        float leadingSpace = rowWidth <= viewportWidth
            ? (viewportWidth - rowWidth) * 0.5f
            : 16f;

        handContentRect.sizeDelta = new Vector2(contentWidth, viewportHeight);

        for (int i = 0; i < handCount; i++)
        {
            CardData card = player.handCards[i];

            GameObject buttonObject = Instantiate(cardButtonPrefab, handContentRect);
            RectTransform cardRect = buttonObject.transform as RectTransform;
            if (cardRect != null)
            {
                cardRect.anchorMin = new Vector2(0f, 0.5f);
                cardRect.anchorMax = new Vector2(0f, 0.5f);
                cardRect.pivot = new Vector2(0.5f, 0.5f);
                cardRect.sizeDelta = new Vector2(cardWidth, cardHeight);
                cardRect.anchoredPosition = new Vector2(
                    leadingSpace + cardWidth * 0.5f + i * (cardWidth + spacing),
                    0f
                );
            }

            CardButton cardButton = buttonObject.GetComponent<CardButton>();

            if (cardButton != null)
            {
                cardButton.SetLayoutSize(cardWidth, cardHeight);
                cardButton.Setup(card, i, this);
                cardButton.SetSelected(i == selectedCardIndex);
                cardButtons.Add(cardButton);
            }
        }

        Canvas.ForceUpdateCanvases();
        handScrollRect.StopMovement();
        if (handCount == 0 || lastRenderedHandCount < 0)
        {
            handScrollRect.horizontalNormalizedPosition = 0f;
        }
        else if (handCount > lastRenderedHandCount)
        {
            // Keep newly drawn cards in view when the hand becomes scrollable.
            handScrollRect.horizontalNormalizedPosition = 1f;
        }
        else
        {
            handScrollRect.horizontalNormalizedPosition = previousScrollPosition;
        }

        lastRenderedHandCount = handCount;
    }

    private void RefreshCardSelectionView()
    {
        foreach (CardButton cardButton in cardButtons)
        {
            if (cardButton == null)
            {
                continue;
            }

            // 因为 CardButton 内部没有暴露 CardData，
            // 所以这里直接重刷按钮最稳。
        }

        RefreshHandButtons();
    }

    private string GetPhaseName(TurnPhase phase)
    {
        switch (phase)
        {
            case TurnPhase.Draw:
                return "摸牌阶段";

            case TurnPhase.Play:
                return "出牌阶段";

            case TurnPhase.Response:
                return "响应阶段";

            case TurnPhase.Dying:
                return "濒死阶段";

            case TurnPhase.Discard:
                return "弃牌阶段";

            case TurnPhase.End:
                return "结束阶段";

            default:
                return "未知阶段";
        }
    }
}
