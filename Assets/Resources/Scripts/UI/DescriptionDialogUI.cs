using UnityEngine;
using UnityEngine.UI;

public class DescriptionDialogUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] CanvasGroup group;
    [SerializeField] Text bodyText;

    bool isOpen;

    void Awake()
    {
        HideImmediate();
        Shared.descriptionDialogUI = this;
    }

    public void Show(string text)
    {
        if (!group) return;
        if (bodyText) bodyText.text = text;
        isOpen = true;
        group.alpha = 1f;
        group.blocksRaycasts = false;
        group.interactable = false;
    }

    public void Hide()
    {
        if (!group) return;
        isOpen = false;
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
    }

    public void HideImmediate() => Hide();

    public void Toggle(string text)
    {
        if (isOpen) Hide();
        else Show(text);
    }

    public bool IsOpen => isOpen;
}
