using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using UnityEngine.ResourceManagement.AsyncOperations;

public class CraftResultPopup : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Text titleText;    // "결과" 타이틀 텍스트
    [SerializeField] private Image resultIcon;  // 결과 아이템 아이콘
    [SerializeField] private Button btnCollect; // 모두 획득 버튼
    [SerializeField] private Button btnDiscard; // 포기 버튼
    [SerializeField] private Text warningText;  // 인벤토리 부족 경고 (Re-added)

    private AsyncOperationHandle<Sprite> _handle;
    private ItemData _currentItem; // 현재 결과 아이템

    private void Start()
    {
        // 버튼 이벤트 등록
        if (btnCollect != null) btnCollect.onClick.AddListener(OnClickCollect);
        if (btnDiscard != null) btnDiscard.onClick.AddListener(OnClickDiscard);

        // 시작할 땐 꺼둠
        gameObject.SetActive(false);
        if (warningText != null) warningText.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_handle.IsValid()) Addressables.Release(_handle);
    }

    public void Show(ItemData item)
    {
        if (item == null) return;
        
        // 이동 잠금 (Camp UI와 별도로 팝업이 켜져있는 동안 확실하게 잠금)
        PlayerMovement.Instance?.LockMovementIndefinite();
        
        _currentItem = item;

        if (_handle.IsValid()) Addressables.Release(_handle);

        // 데이터 세팅
        if (item.GetAtlasKey() != null)
        {
            _handle = Addressables.LoadAssetAsync<Sprite>(item.GetAtlasKey());
            _handle.Completed += h => {
                if (h.Status == AsyncOperationStatus.Succeeded) resultIcon.sprite = h.Result;
            };
        }

        // 타이틀 설정 (User requested "결과" title)
        if (titleText != null) titleText.text = "제작 결과";

        // 경고 숨김 (초기화)
        if (warningText != null) warningText.gameObject.SetActive(false);

        // 팝업 켜기
        gameObject.SetActive(true);
    }

    private void OnClickCollect()
    {
        if (_currentItem == null) return;

        // 인벤토리 체크
        if (!InventoryManager.Instance.CanAddItem(_currentItem.itemID, 1))
        {
            if (warningText != null)
            {
                warningText.text = "인벤토리가 가득 찼습니다.";
                warningText.gameObject.SetActive(true);
            }
            return;
        }

        // 아이템 지급
        InventoryManager.Instance.AddItem(_currentItem.itemID, 1);
        Debug.Log($"[CraftResult] {_currentItem.itemName} 획득 완료.");

        ClosePopup();
    }

    private void OnClickDiscard()
    {
        // 그냥 닫기 (재료는 이미 소모됨)
        ClosePopup();
    }

    private void ClosePopup()
    {
        gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        // 팝업이 꺼질 때 잠금 해제 (ClosePopup 호출 혹은 부모 비활성화 시 모두 대응)
        _currentItem = null; // 초기화
        PlayerMovement.Instance?.UnlockMovementIndefinite();
    }
}