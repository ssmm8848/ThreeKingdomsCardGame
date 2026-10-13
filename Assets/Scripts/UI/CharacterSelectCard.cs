using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharacterSelectCard : MonoBehaviour
{
    [Header("角色数据")]
    public CharacterData characterData;

    [Header("选择控制器")]
    public CharacterSelectController controller;
    public CharacterSelect3PController controller3P;
    public CharacterSelect5PController controller5P;

    [Header("UI")]
    public TMP_Text nameText;
    public TMP_Text hpText;
    public TMP_Text skillText;
    public TMP_Text portraitText;
    public Image portraitImage;
    public Image backgroundImage;

    [Header("颜色设置")]
    public Color normalColor = new Color(0.10f, 0.13f, 0.18f, 1f);
    public Color selectedColor = new Color(0.46f, 0.34f, 0.15f, 1f);

    [Header("兜底刷新间隔（秒）")]
    public float refreshInterval = 0.1f;

    private float refreshTimer = 0f;

    public static void AppendResourceCharacters(List<CharacterData> characters)
    {
        if (characters == null)
        {
            return;
        }

        CharacterData[] resourceCharacters = Resources.LoadAll<CharacterData>("Characters");
        foreach (CharacterData character in resourceCharacters)
        {
            if (character != null && !characters.Contains(character))
            {
                characters.Add(character);
            }
        }

        characters.Sort((left, right) =>
            string.CompareOrdinal(left.characterName, right.characterName)
        );
    }

    public static void PopulateResourceCards(CharacterSelectController controller)
    {
        PopulateResourceCards(controller != null ? controller.gameObject : null, controller, null, null);
    }

    public static void PopulateResourceCards(CharacterSelect3PController controller)
    {
        PopulateResourceCards(controller != null ? controller.gameObject : null, null, controller, null);
    }

    public static void PopulateResourceCards(CharacterSelect5PController controller)
    {
        PopulateResourceCards(controller != null ? controller.gameObject : null, null, null, controller);
    }

    private static void PopulateResourceCards(
        GameObject sceneRoot,
        CharacterSelectController controller,
        CharacterSelect3PController controller3P,
        CharacterSelect5PController controller5P
    )
    {
        if (sceneRoot == null)
        {
            return;
        }

        CharacterSelectCard[] allSceneCards = FindObjectsOfType<CharacterSelectCard>(true);
        List<CharacterSelectCard> existingCards = new List<CharacterSelectCard>();
        foreach (CharacterSelectCard candidate in allSceneCards)
        {
            if (candidate == null)
            {
                continue;
            }

            bool belongsToController = controller != null
                ? candidate.controller == controller
                : controller3P != null
                    ? candidate.controller3P == controller3P
                    : candidate.controller5P == controller5P;
            if (belongsToController)
            {
                existingCards.Add(candidate);
            }
        }

        if (existingCards.Count == 0)
        {
            return;
        }

        List<CharacterData> availableCharacters = controller != null
            ? controller.GetAllCharacters()
            : controller3P != null ? controller3P.GetAllCharacters() : controller5P.GetAllCharacters();
        HashSet<CharacterData> displayedCharacters = new HashSet<CharacterData>();
        CharacterSelectCard template = null;
        float firstRowY = float.MinValue;
        float firstRowX = float.MaxValue;

        foreach (CharacterSelectCard card in existingCards)
        {
            if (card == null || card.characterData == null)
            {
                continue;
            }

            displayedCharacters.Add(card.characterData);
            RectTransform cardRect = card.transform as RectTransform;
            if (cardRect == null || card.transform.parent == null)
            {
                continue;
            }

            if (template == null)
            {
                template = card;
            }

            if (card.transform.parent == template.transform.parent)
            {
                float y = cardRect.anchoredPosition.y;
                if (y > firstRowY + 0.5f)
                {
                    firstRowY = y;
                    firstRowX = cardRect.anchoredPosition.x;
                }
                else if (Mathf.Abs(y - firstRowY) <= 0.5f)
                {
                    firstRowX = Mathf.Min(firstRowX, cardRect.anchoredPosition.x);
                }
            }
        }

        if (template == null)
        {
            return;
        }

        Transform content = template.transform.parent;
        RectTransform contentRect = content as RectTransform;
        RectTransform templateRect = template.transform as RectTransform;
        float itemWidth = templateRect.sizeDelta.x * Mathf.Abs(templateRect.localScale.x);
        float itemHeight = templateRect.sizeDelta.y * Mathf.Abs(templateRect.localScale.y);
        float columnSpacing = Mathf.Max(12f, itemWidth * 0.055f);
        float rowSpacing = Mathf.Max(16f, itemHeight * 0.05f);
        float columnStep = itemWidth + columnSpacing;
        float rowStep = itemHeight + rowSpacing;
        int nextIndex = displayedCharacters.Count;

        foreach (CharacterData character in availableCharacters)
        {
            if (character == null || displayedCharacters.Contains(character))
            {
                continue;
            }

            GameObject cardObject = Instantiate(template.gameObject, content, false);
            cardObject.name = character.name + "Card";
            CharacterSelectCard card = cardObject.GetComponent<CharacterSelectCard>();
            card.characterData = character;
            card.controller = controller;
            card.controller3P = controller3P;
            card.controller5P = controller5P;
            Button cardButton = cardObject.GetComponent<Button>();
            if (cardButton != null)
            {
                cardButton.onClick.AddListener(card.OnClickCard);
            }

            RectTransform cardRect = cardObject.transform as RectTransform;
            int column = nextIndex % 4;
            int row = nextIndex / 4;
            cardRect.anchoredPosition = new Vector2(
                firstRowX + column * columnStep,
                firstRowY - row * rowStep
            );
            card.RefreshView();

            displayedCharacters.Add(character);
            nextIndex++;
        }

        if (contentRect != null)
        {
            int rowCount = Mathf.CeilToInt(displayedCharacters.Count / 4f);
            float neededHeight = Mathf.Max(1, rowCount) * rowStep + rowSpacing;
            contentRect.sizeDelta = new Vector2(
                contentRect.sizeDelta.x,
                Mathf.Max(contentRect.sizeDelta.y, neededHeight)
            );
        }
    }

    void Awake()
    {
        ApplyReadableTextStyle();
    }

    void Start()
    {
        RefreshView();
    }

    void Update()
    {
        // 低频兜底同步选中高亮，避免每帧重算文本
        refreshTimer += Time.unscaledDeltaTime;

        if (refreshTimer < refreshInterval)
        {
            return;
        }

        refreshTimer = 0f;

        RefreshView();
    }

    public void RefreshView()
    {
        if (characterData == null)
        {
            return;
        }

        RefreshText();
        RefreshPortrait();
        RefreshColor();
    }

    private void ApplyReadableTextStyle()
    {
        // All labels use a light face with a dark outline. The selection cards
        // use dark bronze/navy frames, while skill text sits over the artwork.
        TMP_Text[] labels = GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text label in labels)
        {
            if (label == null)
            {
                continue;
            }

            label.raycastTarget = false;

            label.color = Color.white;
            Outline outline = label.GetComponent<Outline>();
            if (outline == null)
            {
                outline = label.gameObject.AddComponent<Outline>();
            }

            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            outline.useGraphicAlpha = true;
        }
    }

    private void RefreshText()
    {
        if (nameText != null)
        {
            nameText.text = characterData.characterName;
        }

        if (hpText != null)
        {
            hpText.text = "体力：" + characterData.maxHp;
        }

        if (skillText != null)
        {
            skillText.text = characterData.skillName + "：\n" + characterData.skillDescription;
        }
    }

    private void RefreshPortrait()
    {
        if (portraitImage != null)
        {
            portraitImage.sprite = characterData.portrait;
            portraitImage.type = Image.Type.Simple;
            portraitImage.preserveAspect = true;
            portraitImage.enabled = characterData.portrait != null;
        }

        if (portraitText != null)
        {
            if (characterData.portrait != null)
            {
                portraitText.gameObject.SetActive(false);
            }
            else
            {
                portraitText.gameObject.SetActive(true);
                portraitText.text = "【立绘区域】\n" + characterData.portraitPrompt;
            }
        }
    }

    private void RefreshColor()
    {
        if (backgroundImage == null)
        {
            return;
        }

        bool selected = false;

        if (controller != null && controller.IsSelected(characterData))
        {
            selected = true;
        }

        if (controller3P != null && controller3P.IsSelected(characterData))
        {
            selected = true;
        }

        if (controller5P != null && controller5P.IsSelected(characterData))
        {
            selected = true;
        }

        backgroundImage.color = selected ? selectedColor : normalColor;
    }

    public void OnClickCard()
    {
        if (characterData == null)
        {
            return;
        }

        if (controller != null)
        {
            controller.SelectCharacter(characterData);
            return;
        }

        if (controller3P != null)
        {
            controller3P.SelectCharacter(characterData);
            return;
        }

        if (controller5P != null)
        {
            controller5P.SelectCharacter(characterData);
            return;
        }
    }
}
