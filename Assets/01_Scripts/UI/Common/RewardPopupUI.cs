using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

public class RewardPopupUI : MonoBehaviour
{
    public static RewardPopupUI Instance;

    [Header("UI References")]
    public Transform slotRoot;         // 슬롯들이 생성될 부모 Transform (Grid Layout Group)
    public GameObject slotPrefab;      // RewardSlotUI 프리팹
    public Button receiveAllButton;    // 모두 받기 버튼
    public Button confirmButton;       // 확인/닫기 버튼

    [Header("Dependencies")]
    public ItemLibrary itemLibrary;    // 아이템 정보 로드용

    private List<RewardData> _currentRewards = new List<RewardData>();
    private List<RewardSlotUI> _activeSlots = new List<RewardSlotUI>();
    private Action _onClosed;

    private void Awake()
    {
        Instance = this;
    }

    public void Open(List<RewardData> rewards, Action onClosed)
    {
        _currentRewards = rewards;
        _onClosed = onClosed;

        // 버튼 리스너 초기화
        if (receiveAllButton != null)
        {
            receiveAllButton.onClick.RemoveAllListeners();
            receiveAllButton.onClick.AddListener(OnReceiveAllClicked);
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(OnConfirmClicked);
        }

        RefreshUI();
    }

    private void RefreshUI()
    {
        // 기존 슬롯 제거 (풀링 미사용, 간단 구현)
        foreach (Transform child in slotRoot)
        {
            Destroy(child.gameObject);
        }
        _activeSlots.Clear();

        if (_currentRewards == null) return;

        // 아이템 라이브러리 fallback
        if (itemLibrary == null && InventoryUI.Instance != null)
        {
            itemLibrary = InventoryUI.Instance.itemLibrary;
        }

        foreach (var reward in _currentRewards)
        {
            // 수량이 0 이하면 표시 안 함
            if (reward.count <= 0) continue;

            GameObject go = Instantiate(slotPrefab, slotRoot);
            RewardSlotUI slotUI = go.GetComponent<RewardSlotUI>();
            
            ItemData data = null;
            if (itemLibrary != null) data = itemLibrary.GetItem(reward.itemID);

            slotUI.SetReward(reward, data);
            _activeSlots.Add(slotUI);
        }
    }

    private void OnReceiveAllClicked()
    {
        // 모든 보상을 순회하며 인벤토리에 넣기 시도
        bool anyChanged = false;

        foreach (var reward in _currentRewards)
        {
            if (reward.count <= 0) continue;

            // AddPartialItem: 넣고 남은 수량 반환
            // InventoryManager를 통해 넣음. 
            // 주의: InventoryManager.Instance가 필요함.
            if (InventoryManager.Instance != null)
            {
                int remaining = InventoryManager.Instance.AddPartialItem(reward.itemID, reward.count);
                if (remaining != reward.count)
                {
                    reward.count = remaining;
                    anyChanged = true;
                }
            }
        }

        if (anyChanged)
        {
            RefreshUI(); // UI 갱신 (남은 수량 표시하거나 제거)
        }

        // 모든 보상을 다 받았는지 확인하여 팝업 닫기
        bool allReceived = true;
        foreach (var reward in _currentRewards)
        {
            if (reward.count > 0)
            {
                allReceived = false;
                break;
            }
        }

        if (allReceived)
        {
            OnConfirmClicked();
        }
    }

    private void OnConfirmClicked()
    {
        // 닫기 시 남은 보상은 버려짐 (기획 의도 확인됨)
        _onClosed?.Invoke();
        Destroy(gameObject);
    }
}
