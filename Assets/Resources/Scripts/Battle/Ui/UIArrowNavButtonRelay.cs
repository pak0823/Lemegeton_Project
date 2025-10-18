using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class UIArrowNavButtonRelay : MonoBehaviour, IPointerDownHandler
{
    public UIArrowNavigator navigator;
    public Button button;
    void Reset() { if (!button) button = GetComponent<Button>(); }
    public void OnPointerDown(PointerEventData e)
    {
        if (navigator && button && button.interactable)
            navigator.SetExternalFocus(button, true); // ← 하이라이트 즉시 이동
    }
}
