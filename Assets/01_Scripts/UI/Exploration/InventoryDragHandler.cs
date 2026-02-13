using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.EventSystems;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

public class InventoryDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
{
    private CanvasGroup canvasGroup;
    private Transform originalParent;
    private Vector3 originalPosition;
    private int fromIndex;

    private AsyncOperationHandle<Sprite> _handle;

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

    public void SetIcon(ItemData data)
    {
        if (_handle.IsValid()) Addressables.Release(_handle);

        _handle = Addressables.LoadAssetAsync<Sprite>(data.GetAtlasKey());
        _handle.Completed += h => {
            if (h.Status == AsyncOperationStatus.Succeeded)
                GetComponent<Image>().sprite = h.Result;
        };
    }
    private void OnDestroy()
    {
        if (_handle.IsValid()) Addressables.Release(_handle);
    }

    public int GetFromIndex() { return fromIndex; }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // 드래그 시작 시점에 현재 부모 슬롯의 인덱스를 확정함 (안전성 확보)
        InventorySlotUI parentSlot = GetComponentInParent<InventorySlotUI>();
        if (parentSlot != null) fromIndex = parentSlot.slotIndex;

        originalParent = transform.parent;
        originalPosition = transform.localPosition;

        // --- 수정: 고스트 이미지 사용 ---
        // --- 수정: 고스트 이미지 사용 ---
        if (InventoryUI.Instance != null)
        {
            Image myImage = GetComponent<Image>();
            if (myImage != null)
            {
                InventoryUI.Instance.StartDrag(myImage.sprite, myImage.rectTransform.rect.size);
            }
        }

        // 2. 내 자신은 반투명하게 (원본 유지)
        if (canvasGroup != null)
        {
             canvasGroup.alpha = 0.5f;
             canvasGroup.blocksRaycasts = false; // 마우스 통과 (드롭 감지용)
        }

        ItemTooltipUI.Instance.Hide();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (InventoryUI.Instance != null)
            InventoryUI.Instance.UpdateDrag(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // --- 수정: 고스트 이미지 종료 및 원상복구 ---
        if (InventoryUI.Instance != null) InventoryUI.Instance.EndDrag();
        
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f; // 불투명 복구
            canvasGroup.blocksRaycasts = true;
        }

        // transform.SetParent(originalParent); // 이동 로직 삭제
        // transform.localPosition = originalPosition;

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