using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Runtime-built audio settings shared by the main menu and battle scenes.</summary>
public sealed class GameSettingsPanel : MonoBehaviour
{
    private const float DefaultMusicVolume = 0.22f;
    private const float DefaultSfxVolume = 0.85f;

    private Canvas canvas;
    private TMP_FontAsset font;
    private Slider musicSlider;
    private Slider sfxSlider;
    private TMP_Text musicValueText;
    private TMP_Text sfxValueText;
    private Button customExpansionButton;
    private TMP_Text customExpansionLabel;
    private bool built;

    public static Button CreateAccessButton(
        Canvas canvas,
        string objectName,
        string label,
        GameUIButtonTone tone
    )
    {
        if (canvas == null)
        {
            return null;
        }

        Transform existing = canvas.transform.Find(objectName);
        Button button = existing != null ? existing.GetComponent<Button>() : null;
        bool created = false;
        if (button == null)
        {
            GameObject buttonObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button)
            );
            buttonObject.transform.SetParent(canvas.transform, false);
            button = buttonObject.GetComponent<Button>();
            created = true;

            TMP_Text buttonLabel = CreateText(
                buttonObject.transform,
                "Label",
                label,
                ResolveFont(canvas),
                25f,
                Color.white,
                TextAlignmentOptions.Center
            );
            RectTransform labelRect = buttonLabel.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(12f, 5f);
            labelRect.offsetMax = new Vector2(-12f, -5f);
        }

        TMP_Text existingLabel = button.GetComponentInChildren<TMP_Text>(true);
        if (existingLabel != null)
        {
            existingLabel.text = label;
        }

        GameUIButtonStyler.Apply(button, tone);
        GameSfxPlayer.RegisterButton(button);
        if (created)
        {
            button.onClick.AddListener(() => Open(canvas));
        }
        return button;
    }

    public static void Open(Canvas ownerCanvas)
    {
        if (ownerCanvas == null)
        {
            return;
        }

        GameSettingsPanel panel = ownerCanvas.GetComponentInChildren<GameSettingsPanel>(true);
        if (panel == null)
        {
            GameObject panelObject = new GameObject(
                "GameSettingsPanel",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(GameSettingsPanel)
            );
            panelObject.transform.SetParent(ownerCanvas.transform, false);
            panel = panelObject.GetComponent<GameSettingsPanel>();
        }

        panel.canvas = ownerCanvas;
        panel.BuildIfNeeded();
        panel.transform.SetAsLastSibling();
        panel.gameObject.SetActive(true);
        panel.RefreshValues();
    }

    private void Update()
    {
        if (gameObject.activeSelf && Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
        }
    }

    private void BuildIfNeeded()
    {
        if (built || canvas == null)
        {
            return;
        }

        built = true;
        font = ResolveFont(canvas);

        RectTransform rootRect = transform as RectTransform;
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Image backdrop = GetComponent<Image>();
        backdrop.color = new Color(0.015f, 0.022f, 0.035f, 0.78f);
        backdrop.raycastTarget = true;

        GameObject card = CreateUiObject("SettingsCard", transform);
        Image cardImage = card.AddComponent<Image>();
        cardImage.color = new Color(0.055f, 0.075f, 0.10f, 0.98f);
        cardImage.raycastTarget = true;
        RectTransform cardRect = card.transform as RectTransform;
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = Vector2.zero;
        cardRect.sizeDelta = new Vector2(620f, 510f);
        Outline cardOutline = card.AddComponent<Outline>();
        cardOutline.effectColor = new Color(0.72f, 0.55f, 0.29f, 0.9f);
        cardOutline.effectDistance = new Vector2(2f, -2f);
        cardOutline.useGraphicAlpha = true;

        CreateText(
            card.transform,
            "Title",
            "游戏设置",
            font,
            34f,
            new Color(0.95f, 0.82f, 0.57f, 1f),
            TextAlignmentOptions.Center,
            new Vector2(0f, 185f),
            new Vector2(500f, 52f),
            FontStyles.Bold
        );
        CreateText(
            card.transform,
            "Subtitle",
            "音量与牌库内容设置",
            font,
            17f,
            new Color(0.76f, 0.79f, 0.83f, 1f),
            TextAlignmentOptions.Center,
            new Vector2(0f, 146f),
            new Vector2(500f, 30f)
        );

        CreateText(
            card.transform,
            "MusicLabel",
            "背景音乐",
            font,
            21f,
            Color.white,
            TextAlignmentOptions.Left,
            new Vector2(-170f, 91f),
            new Vector2(240f, 36f),
            FontStyles.Bold
        );
        musicValueText = CreateText(
            card.transform,
            "MusicValue",
            "",
            font,
            18f,
            new Color(0.95f, 0.82f, 0.57f, 1f),
            TextAlignmentOptions.Right,
            new Vector2(205f, 91f),
            new Vector2(100f, 36f)
        );
        musicSlider = CreateSlider(card.transform, "MusicVolumeSlider", new Vector2(0f, 48f));

        CreateText(
            card.transform,
            "SfxLabel",
            "操作音效",
            font,
            21f,
            Color.white,
            TextAlignmentOptions.Left,
            new Vector2(-170f, -6f),
            new Vector2(240f, 36f),
            FontStyles.Bold
        );
        sfxValueText = CreateText(
            card.transform,
            "SfxValue",
            "",
            font,
            18f,
            new Color(0.95f, 0.82f, 0.57f, 1f),
            TextAlignmentOptions.Right,
            new Vector2(205f, -6f),
            new Vector2(100f, 36f)
        );
        sfxSlider = CreateSlider(card.transform, "SfxVolumeSlider", new Vector2(0f, -49f));

        customExpansionButton = CreatePanelButton(
            card.transform,
            "CustomExpansionButton",
            "",
            new Vector2(0f, -115f),
            GameUIButtonTone.Navigation,
            ToggleCustomExpansion
        );
        RectTransform expansionRect = customExpansionButton.transform as RectTransform;
        expansionRect.sizeDelta = new Vector2(360f, 48f);
        customExpansionLabel = customExpansionButton.GetComponentInChildren<TMP_Text>(true);
        RefreshExpansionLabel();

        CreateText(
            card.transform,
            "SaveHint",
            "官方标准/EX/军争共160张；原创牌设置在下一局生效",
            font,
            14f,
            new Color(0.66f, 0.70f, 0.74f, 1f),
            TextAlignmentOptions.Center,
            new Vector2(0f, -157f),
            new Vector2(520f, 28f)
        );

        CreatePanelButton(
            card.transform,
            "ResetDefaultsButton",
            "恢复默认",
            new Vector2(-125f, -211f),
            GameUIButtonTone.Navigation,
            ResetDefaults
        );
        CreatePanelButton(
            card.transform,
            "CloseSettingsButton",
            "关闭",
            new Vector2(125f, -211f),
            GameUIButtonTone.PrimaryAction,
            Close
        );

        musicSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        sfxSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
        RefreshValues();
    }

    private void RefreshValues()
    {
        if (!built)
        {
            return;
        }

        float musicVolume = GameMusicPlayer.GetMusicVolume();
        float sfxVolume = GameSfxPlayer.GetSfxVolume();
        musicSlider.SetValueWithoutNotify(musicVolume);
        sfxSlider.SetValueWithoutNotify(sfxVolume);
        UpdateValueLabel(musicValueText, musicVolume);
        UpdateValueLabel(sfxValueText, sfxVolume);
        RefreshExpansionLabel();
    }

    private void OnMusicVolumeChanged(float value)
    {
        GameMusicPlayer.SetMusicVolume(value);
        UpdateValueLabel(musicValueText, value);
    }

    private void OnSfxVolumeChanged(float value)
    {
        GameSfxPlayer.SetSfxVolume(value);
        UpdateValueLabel(sfxValueText, value);
    }

    private void ResetDefaults()
    {
        musicSlider.value = DefaultMusicVolume;
        sfxSlider.value = DefaultSfxVolume;
        GameSelectionData.SetCustomExpansionEnabled(true);
        RefreshExpansionLabel();
        SaveAndClose();
    }

    private void ToggleCustomExpansion()
    {
        GameSelectionData.SetCustomExpansionEnabled(!GameSelectionData.customExpansionEnabled);
        RefreshExpansionLabel();
    }

    private void RefreshExpansionLabel()
    {
        if (customExpansionLabel != null)
        {
            customExpansionLabel.text = GameSelectionData.customExpansionEnabled
                ? "原创扩展牌：开启"
                : "原创扩展牌：关闭";
        }
    }

    private void Close()
    {
        SaveAndClose();
    }

    private void SaveAndClose()
    {
        PlayerPrefs.Save();
        gameObject.SetActive(false);
    }

    private Button CreatePanelButton(
        Transform parent,
        string objectName,
        string label,
        Vector2 position,
        GameUIButtonTone tone,
        UnityEngine.Events.UnityAction action
    )
    {
        GameObject buttonObject = CreateUiObject(objectName, parent);
        Image background = buttonObject.AddComponent<Image>();
        background.color = Color.white;
        Button button = buttonObject.AddComponent<Button>();
        RectTransform rect = button.transform as RectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(190f, 56f);

        TMP_Text buttonLabel = CreateText(
            button.transform,
            "Label",
            label,
            font,
            20f,
            Color.white,
            TextAlignmentOptions.Center,
            Vector2.zero,
            Vector2.zero,
            FontStyles.Bold
        );
        buttonLabel.rectTransform.anchorMin = Vector2.zero;
        buttonLabel.rectTransform.anchorMax = Vector2.one;
        buttonLabel.rectTransform.offsetMin = new Vector2(8f, 4f);
        buttonLabel.rectTransform.offsetMax = new Vector2(-8f, -4f);

        GameUIButtonStyler.Apply(button, tone);
        GameSfxPlayer.RegisterButton(button);
        button.onClick.AddListener(action);
        return button;
    }

    private Slider CreateSlider(Transform parent, string objectName, Vector2 position)
    {
        GameObject sliderObject = CreateUiObject(objectName, parent);
        RectTransform sliderRect = sliderObject.transform as RectTransform;
        sliderRect.anchorMin = new Vector2(0.5f, 0.5f);
        sliderRect.anchorMax = new Vector2(0.5f, 0.5f);
        sliderRect.anchoredPosition = position;
        sliderRect.sizeDelta = new Vector2(440f, 22f);

        Image trackImage = sliderObject.AddComponent<Image>();
        trackImage.color = new Color(0.12f, 0.16f, 0.21f, 1f);
        trackImage.raycastTarget = true;

        Slider slider = sliderObject.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.direction = Slider.Direction.LeftToRight;
        slider.targetGraphic = trackImage;

        GameObject fillArea = CreateUiObject("FillArea", sliderObject.transform);
        RectTransform fillAreaRect = fillArea.transform as RectTransform;
        fillAreaRect.anchorMin = new Vector2(0f, 0.25f);
        fillAreaRect.anchorMax = new Vector2(1f, 0.75f);
        fillAreaRect.offsetMin = new Vector2(8f, 0f);
        fillAreaRect.offsetMax = new Vector2(-8f, 0f);

        GameObject fillObject = CreateUiObject("Fill", fillArea.transform);
        RectTransform fillRect = fillObject.transform as RectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        Image fillImage = fillObject.AddComponent<Image>();
        fillImage.color = new Color(0.81f, 0.62f, 0.31f, 1f);
        fillImage.raycastTarget = false;

        GameObject handleArea = CreateUiObject("HandleArea", sliderObject.transform);
        RectTransform handleAreaRect = handleArea.transform as RectTransform;
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.offsetMin = new Vector2(8f, 0f);
        handleAreaRect.offsetMax = new Vector2(-8f, 0f);

        GameObject handleObject = CreateUiObject("Handle", handleArea.transform);
        RectTransform handleRect = handleObject.transform as RectTransform;
        handleRect.anchorMin = new Vector2(0f, 0.5f);
        handleRect.anchorMax = new Vector2(0f, 0.5f);
        handleRect.sizeDelta = new Vector2(24f, 24f);
        Image handleImage = handleObject.AddComponent<Image>();
        handleImage.color = new Color(0.96f, 0.86f, 0.66f, 1f);
        handleImage.raycastTarget = true;

        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.value = 1f;
        return slider;
    }

    private static TMP_Text CreateText(
        Transform parent,
        string objectName,
        string value,
        TMP_FontAsset font,
        float fontSize,
        Color color,
        TextAlignmentOptions alignment,
        Vector2? position = null,
        Vector2? size = null,
        FontStyles style = FontStyles.Normal
    )
    {
        GameObject textObject = CreateUiObject(objectName, parent);
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = font;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.fontStyle = style;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(12f, fontSize - 3f);
        text.fontSizeMax = fontSize;
        text.raycastTarget = false;
        text.enableWordWrapping = false;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position ?? Vector2.zero;
        rect.sizeDelta = size ?? new Vector2(100f, 30f);
        return text;
    }

    private static GameObject CreateUiObject(string objectName, Transform parent)
    {
        GameObject uiObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer)
        );
        uiObject.transform.SetParent(parent, false);
        return uiObject;
    }

    private static TMP_FontAsset ResolveFont(Canvas ownerCanvas)
    {
        TMP_Text sample = ownerCanvas != null
            ? ownerCanvas.GetComponentInChildren<TMP_Text>(true)
            : null;
        if (sample != null && sample.font != null)
        {
            return sample.font;
        }

        return Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
    }

    private static void UpdateValueLabel(TMP_Text label, float value)
    {
        if (label != null)
        {
            label.text = Mathf.RoundToInt(value * 100f) + "%";
        }
    }
}
