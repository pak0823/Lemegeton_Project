using UnityEngine;



public class InventoryUI : MonoBehaviour

{

    public static InventoryUI Instance;

    public InventorySlotUI[] uiSlots; // 12개 슬롯을 드래그해서 넣어라

    public ItemLibrary itemLibrary; // 아이템 DB (ItemData 찾기용)



    private void Awake()

    {

        if (Instance == null) Instance = this;

        else Destroy(gameObject);



        // 자식 오브젝트들 InventorySlotUI를 모두 찾아서 자동 할당

        uiSlots = GetComponentsInChildren<InventorySlotUI>(true);



        // 슬롯 인덱스 순서대로 정렬 (인스펙터에서 설정한 slotIndex 기준)

        System.Array.Sort(uiSlots, (a, b) => a.slotIndex.CompareTo(b.slotIndex));





    }



    private void Start()

    {

        // 슬롯 개수가 maxSlots와 일치하는지 확인

        if (uiSlots.Length != InventoryManager.Instance.maxSlots)

        {

            Debug.LogWarning($"슬롯 개수 불일치! UI: {uiSlots.Length}, Data: {InventoryManager.Instance.maxSlots}");

        }



        // 라이브러리 초기화 (딕셔너리 빌드)

        if (itemLibrary != null) itemLibrary.Init();



        InventoryManager.Instance.OnInventoryChanged += Refresh;

        Refresh();

    }



    public void Refresh()

    {

        for (int i = 0; i < uiSlots.Length; i++)

        {

            var itemDataInManager = InventoryManager.Instance.slots[i];



            if (itemDataInManager != null && itemLibrary != null)

            {

                ItemData dataSO = itemLibrary.GetItem(itemDataInManager.itemID);

                // 슬롯 UI에 최신 데이터 객체를 전달하여 텍스트/아이콘 강제 갱신

                uiSlots[i].UpdateSlot(itemDataInManager, dataSO);

            }

            else

            {

                uiSlots[i].UpdateSlot(null, null);

            }

        }

    }


    // --- 드래그 고스트 이미지 ---
    [Header("Drag Visual")]
    public UnityEngine.UI.Image dragGhostImage; // 인스펙터 할당 필요

    private Canvas _cachedCanvas;

    public void StartDrag(Sprite sprite, Vector2 size)
    {
        if (dragGhostImage == null) return;

        if (_cachedCanvas == null) _cachedCanvas = GetComponentInParent<Canvas>();

        dragGhostImage.sprite = sprite;
        
        // 앵커를 중앙으로 강제 설정 (sizeDelta가 절대 크기로 작동하도록)
        dragGhostImage.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        dragGhostImage.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        dragGhostImage.rectTransform.pivot = new Vector2(0.5f, 0.5f);

        dragGhostImage.rectTransform.sizeDelta = size; // 크기 동기화
        dragGhostImage.gameObject.SetActive(true);
        
        dragGhostImage.transform.SetAsLastSibling();
        dragGhostImage.raycastTarget = false;
    }

    public void UpdateDrag(Vector2 screenPosition)
    {
        if (dragGhostImage == null) return;

        if (_cachedCanvas == null)
        {
             // 혹시 StartDrag를 안 거치고 왔을 경우 대비
             _cachedCanvas = GetComponentInParent<Canvas>();
        }

        // 캔버스 렌더 모드에 따라 카메라 설정 (Overlay면 null, Camera면 worldCamera)
        Camera uiCamera = (_cachedCanvas != null && _cachedCanvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : (_cachedCanvas != null ? _cachedCanvas.worldCamera : null);

        // 부모 RectTransform 기준으로 로컬 좌표 변환
        RectTransform parentRect = dragGhostImage.transform.parent as RectTransform;
        
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPosition, uiCamera, out Vector2 localPoint))
        {
            dragGhostImage.transform.localPosition = localPoint;
        }
    }

    public void EndDrag()
    {
        if (dragGhostImage == null) return;
        dragGhostImage.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnInventoryChanged -= Refresh;
        }
    }
}