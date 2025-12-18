using UnityEngine;
using UnityEngine.UI;

public class DescriptionDialogUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] CanvasGroup group;
    [SerializeField] Text bodyText;

    bool isOpen;
    float hideLockUntil = 0f;
    [SerializeField] float defaultMovementLock = 0.5f; // 0이면 잠금 안 함

    void Awake()
    {
        HideImmediate();
        Shared.descriptionDialogUI = this;
    }
    // 잠금형 표시
    public void ShowTemporarily(string text, float seconds)
    {
        Show(text);
        hideLockUntil = Mathf.Max(hideLockUntil, Time.time + Mathf.Max(0f, seconds));
        Shared.PlayerMovement?.LockMovementFor(seconds);
    }

    public void Show(string text)
    {
        if (!group) return;
        if (bodyText) bodyText.text = text;

        isOpen = true;

        group.alpha = 1f;
        group.blocksRaycasts = false;
        group.interactable = false;

        if (defaultMovementLock > 0f)
            Shared.PlayerMovement?.LockMovementFor(defaultMovementLock); // 기본 잠금
    }

    public void Hide()
    {

        if (Time.time < hideLockUntil) return;  // 잠금 중이면 무시
        if (!group) return;

        isOpen = false;
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
    }

    // 강제 닫기(잠금 무시)
    public void ForceHide()
    {
        hideLockUntil = 0f;
        Hide();
    }

    public void HideImmediate() => Hide();

    public void Toggle(string text)
    {
        if (isOpen) Hide();
        else Show(text);
    }

    public bool IsOpen => isOpen;
}
