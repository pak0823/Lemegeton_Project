using UnityEngine;
using UnityEngine.UI;

public class MaterialSlotUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private Text amountText;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color lackColor = Color.red; // 부족할 때 색상

    public void Setup(ItemData material, int currentCount, int requiredCount)
    {
        // 아이콘 설정
        iconImage.sprite = material.itemIcon;

        // 텍스트 설정 (예: "5 / 10")
        amountText.text = $"{currentCount} / {requiredCount}";

        // 개수 부족하면 빨간색, 충분하면 흰색
        bool isEnough = currentCount >= requiredCount;
        amountText.color = isEnough ? normalColor : lackColor;
    }
}