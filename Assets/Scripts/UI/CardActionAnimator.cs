using System.Collections;
using TMPro;
using UnityEngine;

public class CardActionAnimator : MonoBehaviour
{
    [Header("提示根对象")]
    public RectTransform popupRoot;

    [Header("提示文本")]
    public TMP_Text actionText;

    [Header("透明度控制")]
    public CanvasGroup canvasGroup;

    [Header("动画参数")]
    public float fadeInTime = 0.15f;
    public float stayTime = 0.85f;
    public float fadeOutTime = 0.25f;
    public float startScale = 0.85f;
    public float endScale = 1.08f;

    private Coroutine currentCoroutine;

    void Awake()
    {
        if (popupRoot != null)
        {
            popupRoot.gameObject.SetActive(false);
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }
    }

    public void ShowCardAction(Player user, Player target, CardData card)
    {
        if (user == null || card == null)
        {
            return;
        }

        string message;

        if (target != null && target != user)
        {
            message = user.playerName + " 对 " + target.playerName + " 使用【" + card.cardName + "】";
        }
        else
        {
            message = user.playerName + " 使用【" + card.cardName + "】";
        }

        ShowMessage(message);
    }

    public void ShowMessage(string message)
    {
        if (popupRoot == null || actionText == null || canvasGroup == null)
        {
            return;
        }

        if (currentCoroutine != null)
        {
            StopCoroutine(currentCoroutine);
        }

        currentCoroutine = StartCoroutine(ShowMessageCoroutine(message));
    }

    private IEnumerator ShowMessageCoroutine(string message)
    {
        popupRoot.gameObject.SetActive(true);
        actionText.text = message;

        canvasGroup.alpha = 0f;
        popupRoot.localScale = Vector3.one * startScale;

        float timer = 0f;

        while (timer < fadeInTime)
        {
            timer += Time.deltaTime;

            float t = timer / fadeInTime;

            canvasGroup.alpha = Mathf.Lerp(0f, 1f, t);
            popupRoot.localScale = Vector3.Lerp(
                Vector3.one * startScale,
                Vector3.one * endScale,
                t
            );

            yield return null;
        }

        canvasGroup.alpha = 1f;
        popupRoot.localScale = Vector3.one * endScale;

        yield return new WaitForSeconds(stayTime);

        timer = 0f;

        while (timer < fadeOutTime)
        {
            timer += Time.deltaTime;

            float t = timer / fadeOutTime;

            canvasGroup.alpha = Mathf.Lerp(1f, 0f, t);

            yield return null;
        }

        canvasGroup.alpha = 0f;
        popupRoot.localScale = Vector3.one;
        popupRoot.gameObject.SetActive(false);

        currentCoroutine = null;
    }
}