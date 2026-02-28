using UnityEngine;
using UnityEngine.UI;

public class InventorySlotUI : MonoBehaviour
{
    public int slotIndex; // 에디터에서 0~11까지 직접 할당
    public InventoryDragHandler dragHandler; // 자식의 드래그 핸들러 참조
    [SerializeField] private Image itemIcon;
    [SerializeField] private Text countText;

    // 슬롯 갱신 시 자식 아이콘의 이미지도 같이 변경
    public void UpdateSlot(InventoryItem item, ItemData data)
    {
        if (item == null || data == null)
        {
            // 데이터가 없으면 드래그 핸들러(아이콘)를 무조건 비활성화
            if (dragHandler != null) dragHandler.gameObject.SetActive(false);
            if (countText != null) countText.text = "";
            return;
        }

        // 아이템이 있는 경우 (강제 초기화 로직)
        dragHandler.gameObject.SetActive(true);

        // 위치 리셋
        dragHandler.transform.SetParent(this.transform);
        dragHandler.transform.localPosition = Vector3.zero;

        // 데이터로부터 직접 수량을 가져와서 텍스트 갱신
        if (countText != null)
        {
            // 현재 전달받은 item 객체의 count를 즉시 반영
            countText.text = item.count > 1 ? item.count.ToString() : "";
        }

        dragHandler.SetIcon(data);
    }
}