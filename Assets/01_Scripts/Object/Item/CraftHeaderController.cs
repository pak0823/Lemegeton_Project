using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class CraftHeaderController : MonoBehaviour
{
    [System.Serializable]
    public struct HeaderSlot
    {
        public Toggle toggle;
        public ItemData linkedMaterial; // 이 버튼이 담당하는 재료(판자 등)
        public Image iconImage;
    }

    [Header("UI Settings")]
    public ToggleGroup toggleGroup; // 6개 토글을 묶을 그룹
    public List<HeaderSlot> headerSlots;
    public CampCraftPage craftPage;

    private void Start()
    {
        // 토글 이벤트 연결
        foreach (var slot in headerSlots)
        {
            if (slot.linkedMaterial != null)
            {
                // 아이콘 세팅
                if (slot.iconImage) slot.iconImage.sprite = slot.linkedMaterial.itemIcon;

                // 토글 변경 이벤트 (켜질 때만 로직 실행)
                slot.toggle.onValueChanged.AddListener((isOn) =>
                {
                    if (isOn)
                    {
                        // 이 재료가 선택됐다고 알림
                        craftPage.FilterRecipesByMaterial(slot.linkedMaterial);
                        // 디버그용 (확인 후 삭제)
                        Debug.Log($"[CraftUI] {slot.linkedMaterial.itemName} 필터 적용");
                    }
                });
            }
        }

        // 창 켜질 때 첫 번째 재료 강제 선택 (안 그러면 처음에 리스트가 비어있음)
        if (headerSlots.Count > 0 && headerSlots[0].toggle != null)
        {
            headerSlots[0].toggle.isOn = true;
            // isOn = true로 바꾸는 순간 위 리스너가 호출돼서 필터링 됨
        }
    }
}