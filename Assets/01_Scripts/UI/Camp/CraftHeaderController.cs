using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using UnityEngine.ResourceManagement.AsyncOperations;

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

    [Header("Navigation Buttons")]
    [SerializeField] private Button arrowLeftButton;
    [SerializeField] private Button arrowRightButton;

    private int currentSelectedIndex = 0;
    private List<AsyncOperationHandle<Sprite>> loadedHandles = new List<AsyncOperationHandle<Sprite>>();

    private void Start()
    {
        for (int i = 0; i < headerSlots.Count; i++)
        {
            int index = i; // 람다식 캡처를 위해 지역 변수에 저장
            var slot = headerSlots[i];

            if (slot.linkedMaterial != null)
            {
                // 아이콘 비동기 로드
                var handle = Addressables.LoadAssetAsync<Sprite>(slot.linkedMaterial.GetAtlasKey());
                handle.Completed += h =>
                {
                    // 구조체 리스트이므로 인덱스로 접근해서 수정 권장
                    if (h.Status == AsyncOperationStatus.Succeeded)
                        headerSlots[index].iconImage.sprite = h.Result;
                };
                loadedHandles.Add(handle);

                // [수정 2] 토글 이벤트 연결
                slot.toggle.onValueChanged.AddListener((isOn) =>
                {
                    if (isOn)
                    {
                        currentSelectedIndex = index;

                        craftPage.FilterRecipesByMaterial(slot.linkedMaterial);
                        Debug.Log($"[CraftUI] {slot.linkedMaterial.itemName} 필터 적용");
                    }
                });
            }
        }

        if (arrowLeftButton) arrowLeftButton.onClick.AddListener(() => SelectNextSlot(-1));
        if (arrowRightButton) arrowRightButton.onClick.AddListener(() => SelectNextSlot(1));

        // 첫 번째 재료 선택은 모든 리스너 등록 후 마지막에 한 번만
        if (headerSlots.Count > 0 && headerSlots[0].toggle != null)
            headerSlots[0].toggle.isOn = true;
    }

    public void SelectNextSlot(int direction)
    {
        if (headerSlots.Count == 0) return;

        int nextIndex = Mathf.Clamp(currentSelectedIndex + direction, 0, headerSlots.Count - 1);
        // 인덱스가 실제로 변했을 때만 토글 변경 (불필요한 호출 방지)
        if (nextIndex != currentSelectedIndex)
        {
            headerSlots[nextIndex].toggle.isOn = true;
            // 토글이 켜지면 위에서 등록한 onValueChanged가 실행되면서 currentSelectedIndex도 자동으로 바뀜
        }
    }

    private void OnDestroy()
    {
        foreach (var handle in loadedHandles)
        {
            if (handle.IsValid()) Addressables.Release(handle);
        }
    }
}