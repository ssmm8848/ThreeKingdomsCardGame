using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    private void Start()
    {
        StyleMainMenu();
        CreateSettingsButton();
    }

    private void StyleMainMenu()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        CanvasScaler scaler = canvas != null ? canvas.GetComponent<CanvasScaler>() : null;
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        Button[] buttons = FindObjectsOfType<Button>(true);
        foreach (Button button in buttons)
        {
            GameUIButtonTone tone = GameUIButtonTone.Main;
            RectTransform rect = button.transform as RectTransform;
            if (rect != null)
            {
                Vector2 position = rect.anchoredPosition;
                switch (button.gameObject.name)
                {
                    case "Battle2PButton":
                        position.y = 106f;
                        rect.sizeDelta = new Vector2(300f, 74f);
                        break;
                    case "Battle3PButton":
                        position.y = 21f;
                        rect.sizeDelta = new Vector2(300f, 74f);
                        break;
                    case "Battle5PButton":
                        position.y = -64f;
                        rect.sizeDelta = new Vector2(300f, 74f);
                        tone = GameUIButtonTone.PrimaryAction;
                        TMP_Text fivePlayerLabel = button.GetComponentInChildren<TMP_Text>(true);
                        if (fivePlayerLabel != null)
                        {
                            fivePlayerLabel.text = "五人场";
                        }
                        break;
                    case "QuitButton":
                        position.y = -234f;
                        rect.sizeDelta = new Vector2(300f, 74f);
                        tone = GameUIButtonTone.Destructive;
                        break;
                }
                rect.anchoredPosition = position;
            }

            GameUIButtonStyler.Apply(button, tone);
        }

        GameObject titleObject = GameObject.Find("TitleText");
        TMP_Text title = titleObject != null ? titleObject.GetComponent<TMP_Text>() : null;
        if (title != null)
        {
            title.text = "灵契之争";
            title.color = new Color(0.94f, 0.82f, 0.57f, 1f);
            title.fontStyle = FontStyles.Bold;
            title.raycastTarget = false;

            Outline outline = title.GetComponent<Outline>();
            if (outline == null)
            {
                outline = title.gameObject.AddComponent<Outline>();
            }
            outline.effectColor = new Color(0.015f, 0.025f, 0.04f, 0.95f);
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;
        }
    }

    private void CreateSettingsButton()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            return;
        }

        Button button = GameSettingsPanel.CreateAccessButton(
            canvas,
            "MainMenuSettingsButton",
            "设置",
            GameUIButtonTone.Navigation
        );
        RectTransform rect = button.transform as RectTransform;
        if (rect != null)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, -149f);
            rect.sizeDelta = new Vector2(300f, 74f);
        }

    }

    public void StartBattle2P()
    {
        SceneManager.LoadScene("CharacterSelect2P");
    }

    public void QuitGame()
    {
        GameConfirmationDialog.Show(
            FindObjectOfType<Canvas>(),
            "退出游戏",
            "确定退出游戏吗？",
            "退出游戏",
            "继续游玩",
            QuitAfterConfirmation,
            GameUIButtonTone.Destructive
        );
    }

    private void QuitAfterConfirmation()
    {
        Debug.Log("退出游戏");
        Application.Quit();
    }

    public void StartBattle3P()
    {
        SceneManager.LoadScene("CharacterSelect3P");
    }

    public void StartBattle5P()
    {
        SceneManager.LoadScene("CharacterSelect5P");
    }
}
