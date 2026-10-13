using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TargetButton : MonoBehaviour
{
    [Header("目标角色")]
    public Player targetPlayer;

    [Header("战斗UI")]
    public BattleUI battleUI;

    [Header("显示文本")]
    public TMP_Text targetNameText;

    [Header("按钮背景")]
    public Image backgroundImage;

    [Header("颜色设置")]
    public Color normalColor = Color.white;
    public Color selectedColor = Color.yellow;
    public Color deadColor = Color.gray;

    [Header("兜底刷新间隔（秒）")]
    public float refreshInterval = 0.1f;

    private float refreshTimer = 0f;

    void Start()
    {
        RefreshView();
    }

    void Update()
    {
        // 低频兜底同步，避免每帧重算文本
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
        if (targetPlayer == null)
        {
            return;
        }

        RefreshText();
        RefreshColor();
    }

    private void RefreshText()
    {
        if (targetNameText == null)
        {
            return;
        }

        targetNameText.text =
            targetPlayer.playerName + "\n" +
            "血量：" + targetPlayer.currentHp + "/" + targetPlayer.maxHp;
    }

    private void RefreshColor()
    {
        if (backgroundImage == null)
        {
            return;
        }

        if (targetPlayer.currentHp <= 0)
        {
            backgroundImage.color = deadColor;
            return;
        }

        if (battleUI != null && battleUI.IsTargetSelected(targetPlayer))
        {
            backgroundImage.color = selectedColor;
        }
        else
        {
            backgroundImage.color = normalColor;
        }
    }

    public void OnClickTarget()
    {
        if (battleUI == null || targetPlayer == null)
        {
            return;
        }

        if (targetPlayer.currentHp <= 0)
        {
            Debug.Log("不能选择已经阵亡的角色");
            return;
        }

        battleUI.SelectTarget(targetPlayer);
    }
}