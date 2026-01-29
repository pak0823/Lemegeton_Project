using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventoryDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
{
    private CanvasGroup canvasGroup;
    private Transform originalParent;
    private Vector3 originalPosition;
    private int fromIndex;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        fromIndex = GetComponentInParent<InventorySlotUI>().slotIndex;
    }

    private void OnDisable()
    {
        // 오브젝트가 꺼질 때(버려질 때 포함) 무조건 레이캐스트를 다시 켬
        if (canvasGroup != null) canvasGroup.blocksRaycasts = true;
    }

    public void SetIcon(Sprite icon) => GetComponent<Image>().sprite = icon;

    public int GetFromIndex() { return fromIndex; }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // 드래그 시작 시점에 현재 부모 슬롯의 인덱스를 확정함 (안전성 확보)
        InventorySlotUI parentSlot = GetComponentInParent<InventorySlotUI>();
        if (parentSlot != null) fromIndex = parentSlot.slotIndex;

        originalParent = transform.parent;
        originalPosition = transform.localPosition;

        // 드래그 중에는 다른 UI 뒤로 가거나 슬롯에 가려지지 않게 최상단 캔버스로 잠시 이동
        transform.SetParent(transform.root);
        canvasGroup.blocksRaycasts = false; // 마우스가 '도착지 슬롯'을 감지할 수 있게 함

        ItemTooltipUI.Instance.Hide();
    }

    public void OnDrag(PointerEventData eventData) => transform.position = Input.mousePosition;

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;
        transform.SetParent(originalParent); // 다시 원래 Slot으로 복귀
        transform.localPosition = originalPosition;

        // 마우스 아래에 어떤 오브젝트가 있는지 체크
        if (eventData.pointerEnter != null)
        {
            // 도착지가 Slot인지 확인 (Raycast Target이 켜져 있어야 함)
            InventorySlotUI targetSlot = eventData.pointerEnter.GetComponentInParent<InventorySlotUI>();
            if (targetSlot != null)
            {
                InventoryManager.Instance.SwapSlots(fromIndex, targetSlot.slotIndex);
                PlayerDataManager.Instance.SaveGame(); // 이동 후 저장
            }
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // 드래그 중이 아닐 때만 툴팁 표시
        if (!eventData.dragging)
        {
            var slotUI = GetComponentInParent<InventorySlotUI>();
            var item = InventoryManager.Instance.slots[slotUI.slotIndex];

            if (item != null)
            {
                ItemData data = InventoryUI.Instance.itemLibrary.GetItem(item.itemID);
                ItemTooltipUI.Instance.Show(data);
            }
        }
    }
    public void OnPointerExit(PointerEventData eventData)
    {
        ItemTooltipUI.Instance.Hide();
    }
}