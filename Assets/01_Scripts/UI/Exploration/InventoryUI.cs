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
}