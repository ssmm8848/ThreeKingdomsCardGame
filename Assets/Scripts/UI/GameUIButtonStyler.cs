using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum GameUIButtonTone
{
    Main,
    Navigation,
    Destructive,
    PrimaryAction,
}

public static class GameUIButtonStyler
{
    private const string FrameResourcePath = "UI/KenneyFantasyUI/Frame";
    private static Sprite cachedFrameSprite;
    private static bool frameSpriteLoaded;

    public static void Apply(Button button, GameUIButtonTone tone)
    {
        if (button == null)
        {
            return;
        }

        GetPalette(tone, out Color fill, out Color hover, out Color pressed, out Color text, out Color trim);

        Image background = button.GetComponent<Image>();
        if (background != null)
        {
            // The root Image stays as the raycast target. The rounded child graphic
            // supplies the visible surface without the old square/ornate frame.
            background.color = new Color(1f, 1f, 1f, 0f);
            button.targetGraphic = background;
        }

        ColorBlock colors = button.colors;
        Color invisibleTint = new Color(1f, 1f, 1f, 0f);
        colors.normalColor = invisibleTint;
        colors.highlightedColor = invisibleTint;
        colors.pressedColor = invisibleTint;
        colors.selectedColor = invisibleTint;
        colors.disabledColor = invisibleTint;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.14f;
        button.colors = colors;
        button.transition = Selectable.Transition.None;

        GameUIButtonChrome chrome = null;
        GameUIButtonHoverFeedback feedback = null;
        RectTransform buttonRect = button.transform as RectTransform;
        if (buttonRect != null)
        {
            Transform legacyFrame = buttonRect.Find("KenneyFantasyFrame");
            if (legacyFrame != null)
            {
                Object.Destroy(legacyFrame.gameObject);
            }

            Transform chromeTransform = buttonRect.Find("GameUIButtonChrome");
            chrome = chromeTransform != null
                ? chromeTransform.GetComponent<GameUIButtonChrome>()
                : null;
            if (chrome == null)
            {
                GameObject chromeObject = new GameObject(
                    "GameUIButtonChrome",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(GameUIButtonChrome)
                );
                chromeObject.transform.SetParent(buttonRect, false);
                chrome = chromeObject.GetComponent<GameUIButtonChrome>();
            }

            RectTransform chromeRect = chrome.transform as RectTransform;
            chromeRect.anchorMin = Vector2.zero;
            chromeRect.anchorMax = Vector2.one;
            chromeRect.offsetMin = Vector2.zero;
            chromeRect.offsetMax = Vector2.zero;
            chromeRect.localScale = Vector3.one;
            chrome.transform.SetAsFirstSibling();
            chrome.Configure(fill, hover, pressed, trim);

            feedback = button.GetComponent<GameUIButtonHoverFeedback>();
            if (feedback == null)
            {
                feedback = button.gameObject.AddComponent<GameUIButtonHoverFeedback>();
            }
        }

        TMP_Text[] labels = button.GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text label in labels)
        {
            if (label == null)
            {
                continue;
            }

            label.color = text;
            label.fontStyle |= FontStyles.Bold;
            label.raycastTarget = false;

            Outline outline = label.GetComponent<Outline>();
            if (outline == null)
            {
                outline = label.gameObject.AddComponent<Outline>();
            }
            outline.effectColor = new Color(0.02f, 0.025f, 0.04f, 0.92f);
            outline.effectDistance = new Vector2(1.25f, -1.25f);
            outline.useGraphicAlpha = true;
        }

        if (feedback != null)
        {
            feedback.Configure(chrome, text);
        }
    }

    public static Image AddFrame(RectTransform parent, string childName, Color tint, Vector2 overscan)
    {
        if (parent == null)
        {
            return null;
        }

        Transform existing = parent.Find(childName);
        GameObject frameObject = existing != null
            ? existing.gameObject
            : new GameObject(childName, typeof(RectTransform), typeof(Image));
        if (existing == null)
        {
            frameObject.transform.SetParent(parent, false);
        }

        RectTransform frameRect = frameObject.transform as RectTransform;
        frameRect.anchorMin = Vector2.zero;
        frameRect.anchorMax = Vector2.one;
        frameRect.offsetMin = -overscan;
        frameRect.offsetMax = overscan;
        frameRect.localScale = Vector3.one;
        frameRect.SetAsFirstSibling();

        Image frameImage = frameObject.GetComponent<Image>();
        Sprite frameSprite = LoadFrameSprite();
        frameImage.sprite = frameSprite;
        frameImage.type = frameSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        frameImage.color = tint;
        frameImage.raycastTarget = false;

        return frameImage;
    }

    private static Sprite LoadFrameSprite()
    {
        if (frameSpriteLoaded)
        {
            return cachedFrameSprite;
        }

        frameSpriteLoaded = true;
        Texture2D frameTexture = Resources.Load<Texture2D>(FrameResourcePath);
        if (frameTexture == null)
        {
            Debug.LogWarning("Kenney Fantasy UI frame texture was not found at Resources/" + FrameResourcePath);
            return null;
        }

        Rect textureRect = new Rect(0f, 0f, frameTexture.width, frameTexture.height);
        cachedFrameSprite = Sprite.Create(
            frameTexture,
            textureRect,
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(11f, 11f, 11f, 11f),
            false
        );
        cachedFrameSprite.name = "Kenney Fantasy UI Border - CC0";
        return cachedFrameSprite;
    }

    private static void GetPalette(
        GameUIButtonTone tone,
        out Color fill,
        out Color hover,
        out Color pressed,
        out Color text,
        out Color trim
    )
    {
        switch (tone)
        {
            case GameUIButtonTone.Destructive:
                fill = Hex("24191C");
                hover = Hex("3C2528");
                pressed = Hex("523329");
                text = Hex("F0D1B1");
                trim = Hex("9D6E59");
                break;

            case GameUIButtonTone.Navigation:
                fill = Hex("131F28");
                hover = Hex("293A43");
                pressed = Hex("3B3426");
                text = Hex("EBDDBF");
                trim = Hex("A88D5D");
                break;

            case GameUIButtonTone.PrimaryAction:
                fill = Hex("182832");
                hover = Hex("304550");
                pressed = Hex("514426");
                text = Hex("FFE2A1");
                trim = Hex("D3AB62");
                break;

            default:
                fill = Hex("18232C");
                hover = Hex("2D3F48");
                pressed = Hex("463B25");
                text = Hex("F1D9A5");
                trim = Hex("C7A164");
                break;
        }
    }

    private static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color color);
        return color;
    }
}
