using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class UIArrowNavButtonRelay : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler
{
    public UIArrowNavigator navigator;
    public Button button;

    void Awake()
    {
        if (!button) button = GetComponent<Button>();
        if (!navigator) navigator = GetComponentInParent<UIArrowNavigator>();
    }
    public void OnPointerEnter(PointerEventData e)
    {
        if (!navigator || !button) return;
        if (!button.interactable) return;
        if (navigator.IsLocked) return;

        // 호버 시 포커스 이동 (EventSystem까지 바꾸지 않도록 false 권장)
        navigator.SetExternalFocus(button, false);
    }
    public void OnPointerClick(PointerEventData e)
    {
        if (!navigator || !button) return;
        if (e.button != PointerEventData.InputButton.Left) return;
        if (!button.interactable) return;          // 비활성 버튼 클릭 무시
        if (navigator.IsLocked) return;            // 타겟팅/잠금 상태면 클릭 무시)

        button.onClick?.Invoke();
    }
}
