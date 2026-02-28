using UnityEngine;
using UnityEngine.EventSystems;

public class TrashZone : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private GameObject trashVisual; // 로그를 가릴 쓰레기통 이미지

    public void OnPointerEnter(PointerEventData eventData)
    {
        // 아이템을 드래그 중일 때만 시각적 피드백 제공
        if (eventData.dragging && eventData.pointerDrag.CompareTag("InventoryIcon"))
        {
            trashVisual.SetActive(true);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        trashVisual.SetActive(false);
    }

    public void OnDrop(PointerEventData eventData)
    {
        trashVisual.SetActive(false);

        // 드래그 중인 아이템 핸들러 가져오기
        var dragHandler = eventData.pointerDrag.GetComponent<InventoryDragHandler>();
        if (dragHandler != null)
        {
            int index = dragHandler.GetFromIndex();

            // 실제 삭제 및 저장 로직 실행
            InventoryManager.Instance.RemoveItemAtSlot(index);
            PlayerDataManager.Instance.SaveGame();
            Debug.Log("아이템을 쓰레기통에 버렸습니다.");
        }
    }
}