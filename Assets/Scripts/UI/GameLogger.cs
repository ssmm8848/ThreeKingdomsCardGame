using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameLogger : MonoBehaviour
{
    [Header("日志文本")]
    public TMP_Text logText;

    [Header("滚动区域")]
    public ScrollRect scrollRect;

    [Header("最大显示行数")]
    public int maxLineCount = 30;

    private Queue<string> logLines = new Queue<string>();

    // 一帧内可能产生几十条日志（抽牌、弃牌、伤害都会打日志）。
    // 这里只打脏标记，由 LateUpdate 统一刷新一次，
    // 避免每条日志都执行 Canvas.ForceUpdateCanvases() 强制重建整个 Canvas。
    private bool isDirty = false;

    public void ApplyResponsiveLayout(Vector2 canvasSize, float enemyPanelCenterY, float enemyPanelHeight)
    {
        if (scrollRect == null)
        {
            return;
        }

        RectTransform logRect = scrollRect.transform as RectTransform;
        if (logRect == null)
        {
            return;
        }

        float logWidth = Mathf.Min(293f, canvasSize.x * 0.18f);
        float logHeight = Mathf.Min(300f, canvasSize.y * 0.28f);
        float rightInset = 12f;

        logRect.anchorMin = new Vector2(0.5f, 0.5f);
        logRect.anchorMax = new Vector2(0.5f, 0.5f);
        logRect.pivot = new Vector2(0.5f, 0.5f);
        logRect.sizeDelta = new Vector2(logWidth, logHeight);
        logRect.anchoredPosition = new Vector2(
            canvasSize.x * 0.43f,
            Mathf.Min(
                -canvasSize.y * 0.22f,
                enemyPanelCenterY
                    - enemyPanelHeight * 0.5f
                    - 60f // Reserve room for the hand-count label below the card.
                    - 16f
                    - logHeight * 0.5f
            )
        );

        // Keep the panel fully inside the canvas on narrower aspect ratios.
        Vector2 position = logRect.anchoredPosition;
        position.x = Mathf.Min(position.x, canvasSize.x * 0.5f - logWidth * 0.5f - rightInset);
        logRect.anchoredPosition = position;
    }

    private void OnEnable()
    {
        Application.logMessageReceived += HandleLogMessage;
    }

    private void OnDisable()
    {
        Application.logMessageReceived -= HandleLogMessage;
    }

    private void HandleLogMessage(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Exception)
        {
            AddLog("[错误] " + condition);
            return;
        }

        if (type == LogType.Error)
        {
            AddLog("[错误] " + condition);
            return;
        }

        if (type == LogType.Warning)
        {
            AddLog("[警告] " + condition);
            return;
        }

        AddLog(condition);
    }

    private void AddLog(string message)
    {
        logLines.Enqueue(message);

        while (logLines.Count > maxLineCount)
        {
            logLines.Dequeue();
        }

        isDirty = true;
    }

    private void LateUpdate()
    {
        if (!isDirty)
        {
            return;
        }

        isDirty = false;
        RefreshLogText();
    }

    private void RefreshLogText()
    {
        if (logText == null)
        {
            return;
        }

        logText.text = string.Join("\n", logLines);

        Canvas.ForceUpdateCanvases();

        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }

    public void ClearLog()
    {
        logLines.Clear();
        isDirty = false;
        RefreshLogText();
    }
}
