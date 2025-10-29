using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class UIArrowNavButtonRelay : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler
{
    public UIArrowNavigator navigator;
    public Button button;
    void Reset() { if (!button) button = GetComponent<Button>(); }

    // 클릭: 포커스 확정(텍스트 주황 + EventSystem 선택)
    public void OnPointerDown(PointerEventData e)
    {
        if (navigator && button && button.interactable)
            navigator.SetExternalFocus(button, true); // ← 하이라이트 즉시 이동
    }
    // 호버: 선택은 훔치지 않고 텍스트만 주황
    public void OnPointerEnter(PointerEventData e)
    {
        if (navigator && button && button.interactable)
            navigator.SetExternalFocus(button, true);  // ← true 로 변경
    }
}
