using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class GameUIButtonHoverFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
{
    private GameUIButtonChrome chrome;
    private Button button;
    private TMP_Text[] labels;
    private Color labelColor;
    private Vector3 originalScale;
    private bool isConfigured;
    private bool isHovered;
    private bool lastInteractable;

    public void Configure(GameUIButtonChrome buttonChrome, Color styledLabelColor)
    {
        if (!isConfigured)
        {
            originalScale = transform.localScale;
            isConfigured = true;
        }

        chrome = buttonChrome;
        button = GetComponent<Button>();
        labels = GetComponentsInChildren<TMP_Text>(true);
        labelColor = styledLabelColor;
        isHovered = false;
        chrome?.SetHovered(false);
        lastInteractable = button == null || button.interactable;
        ApplyInteractableState(lastInteractable);
    }

    private void Update()
    {
        bool interactable = button == null || button.interactable;
        if (interactable != lastInteractable)
        {
            lastInteractable = interactable;
            ApplyInteractableState(interactable);
        }
    }

    private void ApplyInteractableState(bool interactable)
    {
        chrome?.SetInteractable(interactable);
        if (labels == null)
        {
            return;
        }

        Color disabledColor = Color.Lerp(labelColor, new Color(0.48f, 0.50f, 0.51f, 1f), 0.62f);
        foreach (TMP_Text label in labels)
        {
            if (label != null)
            {
                label.color = interactable ? labelColor : disabledColor;
            }
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        transform.localScale = originalScale * 1.025f;
        chrome?.SetHovered(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        transform.localScale = originalScale;
        chrome?.SetHovered(false);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        transform.localScale = originalScale * 0.985f;
        chrome?.SetPressed(true);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        transform.localScale = originalScale * (isHovered ? 1.025f : 1f);
        chrome?.SetHovered(isHovered);
    }

    public void OnSelect(BaseEventData eventData)
    {
        isHovered = true;
        transform.localScale = originalScale * 1.025f;
        chrome?.SetHovered(true);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        isHovered = false;
        transform.localScale = originalScale;
        chrome?.SetHovered(false);
    }
}
