using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Events;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

public class CraftSlotUI : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Text nameText;
    [SerializeField] private Button button;
    [SerializeField] private GameObject highlightObj; // 선택됐을 때 켜질 테두리 같은 거 (옵션)

    private AsyncOperationHandle<Sprite> _handle;

    // 데이터 세팅 함수
    public void Setup(CraftRecipe recipe, UnityAction onClickAction)
    {
        if (recipe.resultItem != null)
        {
            nameText.text = recipe.resultItem.itemName;

            // 비동기 로드
            if (_handle.IsValid()) Addressables.Release(_handle);
            _handle = Addressables.LoadAssetAsync<Sprite>(recipe.resultItem.GetAtlasKey());
            _handle.Completed += h => { if (h.Status == AsyncOperationStatus.Succeeded) iconImage.sprite = h.Result; };
        }

        // 클릭 이벤트 연결
        // 기존 리스너 제거 (재사용 시 중복 방지)
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(onClickAction);

        // 클릭 시 하이라이트 갱신 로직 등은 여기서 추가 가능
    }
    private void OnDestroy()
    {
        if (_handle.IsValid()) Addressables.Release(_handle);
    }

    // 선택 표시 켜고 끄기 (필요하면 호출)
    public void SetSelected(bool isSelected)
    {
        if (highlightObj) highlightObj.SetActive(isSelected);
    }
}