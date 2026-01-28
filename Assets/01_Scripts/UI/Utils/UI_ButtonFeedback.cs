using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class UI_ButtonFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField] private Text targetText; // 색깔 바꿀 텍스트
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f); // 회색

    // 외부에서(초기화 시) 텍스트 색상이 바뀔 수 있으므로(초록색 등), 기준 색상을 업데이트하는 함수
    public void SetNormalColor(Color color)
    {
        normalColor = color;
        if (targetText) targetText.color = normalColor;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (targetText) targetText.color = pressedColor;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (targetText) targetText.color = normalColor;
    }

    // 누른 상태로 밖으로 나가면 다시 원래 색으로
    public void OnPointerExit(PointerEventData eventData)
    {
        if (targetText) targetText.color = normalColor;
    }

    private void OnValidate()
    {
        if (targetText == null) targetText = GetComponentInChildren<Text>();
    }
}