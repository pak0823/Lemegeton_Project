using UnityEngine;
using UnityEngine.UI;

public class CraftResultPopup : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image resultIcon;
    [SerializeField] private Text itemNameText;
    [SerializeField] private Text itemTypeText; // 도구 or 장비
    [SerializeField] private Button closeButton; // 배경 전체 혹은 닫기 버튼

    private void Start()
    {
        // 닫기 버튼(또는 배경) 누르면 창 꺼지게 설정
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(ClosePopup);
        }

        // 시작할 땐 꺼둠
        gameObject.SetActive(false);
    }

    public void Show(ItemData item)
    {
        if (item == null) return;

        // 데이터 세팅
        resultIcon.sprite = item.itemIcon;
        itemNameText.text = $"[{item.itemName}]";

        // 타입 텍스트 (enum을 한글로 변환)
        string typeString = "";
        switch (item.itemType)
        {
            case ItemType.Material: typeString = "재료"; break;
            case ItemType.Consumable: typeString = "도구"; break;
            case ItemType.Equipment: typeString = "장비"; break;
        }
        itemTypeText.text = typeString;

        // 팝업 켜기
        gameObject.SetActive(true);
    }

    public void ClosePopup()
    {
        gameObject.SetActive(false);
    }
}