using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shared modal confirmation used before leaving or resetting a match.</summary>
public sealed class GameConfirmationDialog : MonoBehaviour
{
    private Canvas canvas;
    private TMP_FontAsset font;
    private TMP_Text titleText;
    private TMP_Text messageText;
    private TMP_Text confirmText;
    private Button confirmButton;
    private Button cancelButton;
    private Action confirmAction;
    private float previousTimeScale = 1f;
    private bool built;

    public static void Show(
        Canvas ownerCanvas,
        string title,
        string message,
        string confirmLabel,
        string cancelLabel,
        Action onConfirm,
        GameUIButtonTone confirmTone
    )
    {
        if (ownerCanvas == null)
        {
            ownerCanvas = FindObjectOfType<Canvas>();
        }

        if (ownerCanvas == null)
        {
            Debug.LogWarning("无法显示确认弹窗：当前场景没有 Canvas。");
            return;
        }

        GameConfirmationDialog dialog = ownerCanvas.GetComponentInChildren<GameConfirmationDialog>(true);
        if (dialog == null)
        {
            GameObject dialogObject = new GameObject(
                "GameConfirmationDialog",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(GameConfirmationDialog)
            );
            dialogObject.transform.SetParent(ownerCanvas.transform, false);
            dialog = dialogObject.GetComponent<GameConfirmationDialog>();
        }

        dialog.canvas = ownerCanvas;
        dialog.BuildIfNeeded();
        dialog.titleText.text = title;
        dialog.messageText.text = message;
        dialog.confirmText.text = confirmLabel;
        dialog.cancelButton.GetComponentInChildren<TMP_Text>(true).text = cancelLabel;
        GameUIButtonStyler.Apply(dialog.confirmButton, confirmTone);
        dialog.confirmAction = onConfirm;
        dialog.previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        dialog.transform.SetAsLastSibling();
        dialog.gameObject.SetActive(true);
    }

    private void Update()
    {
        if (gameObject.activeSelf && Input.GetKeyDown(KeyCode.Escape))
        {
            Cancel();
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
        backdrop.color = new Color(0.012f, 0.018f, 0.03f, 0.82f);
        backdrop.raycastTarget = true;

        GameObject card = CreateUiObject("ConfirmationCard", transform);
        Image cardImage = card.AddComponent<Image>();
        cardImage.color = new Color(0.055f, 0.075f, 0.10f, 0.99f);
        cardImage.raycastTarget = true;
        RectTransform cardRect = card.transform as RectTransform;
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = Vector2.zero;
        cardRect.sizeDelta = new Vector2(590f, 310f);
        Outline outline = card.AddComponent<Outline>();
        outline.effectColor = new Color(0.72f, 0.55f, 0.29f, 0.92f);
        outline.effectDistance = new Vector2(2f, -2f);
        outline.useGraphicAlpha = true;

        titleText = CreateText(
            card.transform,
            "Title",
            "确认操作",
            32f,
            new Color(0.95f, 0.82f, 0.57f, 1f),
            TextAlignmentOptions.Center,
            new Vector2(0f, 91f),
            new Vector2(500f, 48f),
            FontStyles.Bold
        );
        messageText = CreateText(
            card.transform,
            "Message",
            "确定执行此操作吗？",
            21f,
            Color.white,
            TextAlignmentOptions.Center,
            new Vector2(0f, 20f),
            new Vector2(490f, 82f)
        );

        confirmButton = CreateButton(
            card.transform,
            "ConfirmButton",
            "确定",
            new Vector2(-112f, -91f),
            GameUIButtonTone.PrimaryAction,
            Confirm
        );
        confirmText = confirmButton.GetComponentInChildren<TMP_Text>(true);
        cancelButton = CreateButton(
            card.transform,
            "CancelButton",
            "取消",
            new Vector2(112f, -91f),
            GameUIButtonTone.Navigation,
            Cancel
        );
    }

    private void Confirm()
    {
        Action action = confirmAction;
        confirmAction = null;
        RestoreAndHide();
        if (action != null)
        {
            action.Invoke();
        }
    }

    private void Cancel()
    {
        confirmAction = null;
        RestoreAndHide();
    }

    private void RestoreAndHide()
    {
        Time.timeScale = previousTimeScale;
        PlayerPrefs.Save();
        gameObject.SetActive(false);
    }

    private Button CreateButton(
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
        rect.sizeDelta = new Vector2(190f, 58f);

        TMP_Text text = CreateText(
            button.transform,
            "Label",
            label,
            20f,
            Color.white,
            TextAlignmentOptions.Center,
            Vector2.zero,
            Vector2.zero,
            FontStyles.Bold
        );
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = new Vector2(8f, 4f);
        text.rectTransform.offsetMax = new Vector2(-8f, -4f);

        GameUIButtonStyler.Apply(button, tone);
        GameSfxPlayer.RegisterButton(button);
        button.onClick.AddListener(action);
        return button;
    }

    private TMP_Text CreateText(
        Transform parent,
        string objectName,
        string value,
        float fontSize,
        Color color,
        TextAlignmentOptions alignment,
        Vector2 position,
        Vector2 size,
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
        text.fontSizeMin = Mathf.Max(14f, fontSize - 4f);
        text.fontSizeMax = fontSize;
        text.enableWordWrapping = true;
        text.raycastTarget = false;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
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
}
