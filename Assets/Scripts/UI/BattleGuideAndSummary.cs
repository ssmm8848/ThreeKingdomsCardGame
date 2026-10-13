using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class BattleGuideAndSummary : MonoBehaviour
{
    private BattleUI battleUI;
    private Canvas canvas;
    private TMP_FontAsset font;
    private GameObject overlayRoot;
    private List<string> guidePages = new List<string>();
    private int guidePageIndex;

    public void Initialize(BattleUI owner)
    {
        battleUI = owner;
        canvas = owner != null ? owner.GetComponentInParent<Canvas>() : null;
        font = owner != null && owner.phaseText != null ? owner.phaseText.font : null;

        if (canvas == null || owner == null)
        {
            return;
        }

        string preferenceKey = "BattleGuideSeen_" + GameSelectionData.gameMode;
        if (PlayerPrefs.GetInt(preferenceKey, 0) == 0)
        {
            ShowGuide(preferenceKey);
        }
    }

    private void ShowGuide(string preferenceKey)
    {
        guidePages.Clear();
        guidePages.Add(
            "出牌阶段：先点选手牌，再选择目标，最后按右下角的出牌按钮。\n"
                + "没有选牌时，按钮会结束回合；被杀时可以打出闪，濒死时可以用桃。"
        );

        if (GameSelectionData.gameMode == GameMode.ThreePlayer)
        {
            guidePages.Add(
                "三人场阵营公开。霸主独自对抗两名协战角色；霸主体力上限与出杀次数较高，"
                    + "协战角色需要互相配合保护队友。"
            );
        }
        else if (GameSelectionData.gameMode == GameMode.FivePlayer)
        {
            guidePages.Add(
                "五人场执令者开局公开，其余身份隐藏。正常行动的疑迹记在出牌者身上：对执令者出杀是实证，敌对锦囊是未证实；两点公开该角色身份。"
                    + "‘伪迹’可把未证实疑迹栽到目标身上；‘洗迹’可清除未证实疑迹；‘察微’可查验有疑迹的角色。"
            );
        }
        else
        {
            guidePages.Add("二人场是直接对决。留意手牌数量与体力：回合结束时，手牌上限等于当前体力。");
        }

        guidePages.Add(
            "装备会留在角色区域，‘护心’能抵消下一次伤害，‘搜痕’会随机弃掉目标的一张手牌或装备。"
                + "每种牌的详细效果都写在牌面下方。"
        );

        guidePageIndex = 0;
        ShowGuidePage(preferenceKey);
    }

    private void ShowGuidePage(string preferenceKey)
    {
        string title = "战场手册  ·  " + (guidePageIndex + 1) + "/" + guidePages.Count;
        string nextLabel = guidePageIndex + 1 < guidePages.Count ? "继续" : "开始对局";
        BuildOverlay(
            "BattleGuideOverlay",
            title,
            guidePages[guidePageIndex],
            nextLabel,
            "跳过",
            () =>
            {
                guidePageIndex++;
                if (guidePageIndex >= guidePages.Count)
                {
                    FinishGuide(preferenceKey);
                }
                else
                {
                    ShowGuidePage(preferenceKey);
                }
            },
            () => FinishGuide(preferenceKey),
            GameUIButtonTone.PrimaryAction
        );
    }

    private void FinishGuide(string preferenceKey)
    {
        PlayerPrefs.SetInt(preferenceKey, 1);
        PlayerPrefs.Save();
        ClearOverlay();
    }

    public void ShowMatchSummary(string result, int rounds, IList<Player> players)
    {
        if (canvas == null)
        {
            canvas = GetComponentInParent<Canvas>();
        }
        if (canvas == null)
        {
            return;
        }

        StringBuilder summary = new StringBuilder();
        summary.AppendLine(result);
        summary.AppendLine("进行了 " + Mathf.Max(1, rounds) + " 轮");
        summary.AppendLine();
        TurnManager manager = FindObjectOfType<TurnManager>();
        List<string> highlights = manager != null ? manager.GetMatchHighlights() : null;
        if (highlights != null && highlights.Count > 0)
        {
            summary.AppendLine("关键行动");
            foreach (string highlight in highlights)
            {
                summary.Append("• ").AppendLine(highlight);
            }
            summary.AppendLine();
        }
        summary.AppendLine("战局记录");

        if (players != null)
        {
            foreach (Player participant in players)
            {
                if (participant == null)
                {
                    continue;
                }

                summary.Append(participant.playerName).Append("  ·  ");
                if (GameSelectionData.gameMode == GameMode.FivePlayer)
                {
                    summary.Append(GetIdentityName(participant.identityType));
                }
                else if (GameSelectionData.gameMode == GameMode.ThreePlayer)
                {
                    summary.Append(participant.teamType == TeamType.Overlord ? "霸主" : "协战");
                }
                else
                {
                    summary.Append("对决");
                }

                summary.Append("  ·  ");
                if (participant.currentHp > 0)
                {
                    summary.Append("存活  ").Append(participant.currentHp).Append("/").Append(participant.maxHp);
                }
                else
                {
                    summary.Append("阵亡");
                }

                if (GameSelectionData.gameMode == GameMode.FivePlayer && participant.commanderSuspicion > 0)
                {
                    summary.Append("  ·  疑迹 ").Append(participant.commanderSuspicion).Append("/2");
                }
                summary.AppendLine();
            }
        }

        BuildOverlay(
            "MatchSummaryOverlay",
            "战局结算",
            summary.ToString(),
            "再战一局",
            "返回主界面",
            () => SceneManager.LoadScene(SceneManager.GetActiveScene().name),
            () => SceneManager.LoadScene("MainMenu"),
            GameUIButtonTone.PrimaryAction
        );
    }

    private void BuildOverlay(
        string objectName,
        string title,
        string body,
        string primaryLabel,
        string secondaryLabel,
        UnityEngine.Events.UnityAction primaryAction,
        UnityEngine.Events.UnityAction secondaryAction,
        GameUIButtonTone primaryTone
    )
    {
        ClearOverlay();

        overlayRoot = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasGroup),
            typeof(Image)
        );
        RectTransform rootRect = overlayRoot.GetComponent<RectTransform>();
        rootRect.SetParent(canvas.transform, false);
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        CanvasGroup group = overlayRoot.GetComponent<CanvasGroup>();
        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;
        Image backdrop = overlayRoot.GetComponent<Image>();
        backdrop.color = new Color(0.012f, 0.02f, 0.035f, 0.82f);
        backdrop.raycastTarget = true;

        GameObject panel = new GameObject("DialogPanel", typeof(RectTransform), typeof(Image));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.SetParent(rootRect, false);
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        RectTransform canvasRect = canvas.transform as RectTransform;
        Vector2 canvasSize = canvasRect != null ? canvasRect.rect.size : new Vector2(1920f, 1080f);
        panelRect.sizeDelta = new Vector2(
            Mathf.Min(820f, canvasSize.x * 0.84f),
            Mathf.Min(570f, canvasSize.y * 0.82f)
        );
        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.045f, 0.065f, 0.09f, 0.98f);
        panelImage.raycastTarget = true;
        GameUIButtonStyler.AddFrame(panelRect, "KenneyFantasyFrame", new Color(0.82f, 0.64f, 0.29f, 1f), new Vector2(6f, 6f));

        CreateText(
            panelRect,
            "DialogTitle",
            title,
            new Vector2(0.08f, 0.84f),
            new Vector2(0.92f, 0.96f),
            34f,
            TextAlignmentOptions.Center,
            new Color(0.96f, 0.83f, 0.57f, 1f)
        );
        CreateText(
            panelRect,
            "DialogBody",
            body,
            new Vector2(0.1f, 0.22f),
            new Vector2(0.9f, 0.8f),
            24f,
            TextAlignmentOptions.TopLeft,
            new Color(0.91f, 0.91f, 0.87f, 1f)
        );

        CreateButton(
            panelRect,
            "PrimaryButton",
            primaryLabel,
            new Vector2(-132f, -panelRect.sizeDelta.y * 0.39f),
            new Vector2(220f, 64f),
            primaryTone,
            primaryAction
        );
        CreateButton(
            panelRect,
            "SecondaryButton",
            secondaryLabel,
            new Vector2(132f, -panelRect.sizeDelta.y * 0.39f),
            new Vector2(220f, 64f),
            GameUIButtonTone.Navigation,
            secondaryAction
        );

        rootRect.SetAsLastSibling();
    }

    private void CreateText(
        RectTransform parent,
        string objectName,
        string value,
        Vector2 anchorMin,
        Vector2 anchorMax,
        float fontSize,
        TextAlignmentOptions alignment,
        Color color
    )
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TMP_Text label = textObject.GetComponent<TMP_Text>();
        label.font = font;
        label.text = value;
        label.fontSize = fontSize;
        label.fontSizeMin = Mathf.Max(15f, fontSize - 7f);
        label.fontSizeMax = fontSize;
        label.enableAutoSizing = true;
        label.enableWordWrapping = true;
        label.alignment = alignment;
        label.color = color;
        label.raycastTarget = false;
        label.margin = new Vector4(8f, 5f, 8f, 5f);

        Outline outline = textObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.88f);
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = true;
    }

    private void CreateButton(
        RectTransform parent,
        string objectName,
        string label,
        Vector2 position,
        Vector2 size,
        GameUIButtonTone tone,
        UnityEngine.Events.UnityAction action
    )
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = buttonObject.GetComponent<Image>();
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.SetParent(rect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 4f);
        textRect.offsetMax = new Vector2(-8f, -4f);
        TMP_Text buttonLabel = textObject.GetComponent<TMP_Text>();
        buttonLabel.font = font;
        buttonLabel.text = label;
        buttonLabel.fontSize = 23f;
        buttonLabel.fontSizeMin = 16f;
        buttonLabel.fontSizeMax = 23f;
        buttonLabel.enableAutoSizing = true;
        buttonLabel.alignment = TextAlignmentOptions.Center;
        buttonLabel.raycastTarget = false;

        button.onClick.AddListener(action);
        GameUIButtonStyler.Apply(button, tone);
    }

    private void ClearOverlay()
    {
        if (overlayRoot != null)
        {
            Destroy(overlayRoot);
            overlayRoot = null;
        }
    }

    private string GetIdentityName(IdentityType identity)
    {
        switch (identity)
        {
            case IdentityType.Commander: return "执令者";
            case IdentityType.Protector: return "护令者";
            case IdentityType.Rebel: return "逆契者";
            case IdentityType.Wanderer: return "游离者";
            default: return "未知身份";
        }
    }
}
