using System.Collections.Generic;
using UnityEngine;
using System;

public class InventoryManager : MonoBehaviour, IInventory
{
    public static InventoryManager Instance;

    [Header("설정")]
    public int maxSlots = 12;
    public int maxStack = 6;

    // 실제 아이템들이 담길 배열 (빈칸은 null)
    public InventoryItem[] slots;

    // [Optimization] O(1) 검색을 위한 캐시
    private Dictionary<string, int> _itemCountCache = new Dictionary<string, int>();

    // UI 갱신 등을 위한 이벤트
    public event Action OnInventoryChanged;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            slots = new InventoryItem[maxSlots];
            DontDestroyOnLoad(gameObject); // 매니저 객체 유지 보장
        }
        else 
        {
            // 중복 생성 시 객체 전체 파괴 (InventoryManager + PlayerDataManager가 같이 있다면 통째로 정리됨)
            Destroy(gameObject);
        }
    }

    // 캐시 전체 재구축 (초기화나 로드 시 사용)
    private void RefreshCache()
    {
        _itemCountCache.Clear();
        foreach (var item in slots)
        {
            if (item != null)
            {
                if (!_itemCountCache.ContainsKey(item.itemID))
                    _itemCountCache[item.itemID] = 0;
                _itemCountCache[item.itemID] += item.count;
            }
        }
    }

    // 캐시 단일 업데이트 헬퍼
    private void UpdateCache(string id, int delta)
    {
        if (string.IsNullOrEmpty(id)) return;
        
        if (!_itemCountCache.ContainsKey(id))
            _itemCountCache[id] = 0;
        
        _itemCountCache[id] += delta;
        
        if (_itemCountCache[id] <= 0)
            _itemCountCache.Remove(id);
    }

    // 아이템 추가 로직 (중첩 및 빈자리 찾기)
    // 아이템 추가 로직 (중첩 및 빈자리 찾기)
    public void AddItem(string id, int amount)
    {
        AddPartialItem(id, amount);
    }

    /// <summary>
    /// 아이템을 넣고 남은 수량을 반환 (보상 시스템용)
    /// </summary>
    public int AddPartialItem(string id, int amount)
    {
        int remaining = amount;

        // 1. 기존에 같은 아이템이 있고, 여유 공간이 있는 슬롯이 있는지 확인
        for (int i = 0; i < maxSlots; i++)
        {
            if (slots[i] != null && slots[i].itemID == id && slots[i].count < maxStack)
            {
                int canAdd = maxStack - slots[i].count;
                int addAmount = Mathf.Min(canAdd, remaining);

                slots[i].count += addAmount;
                remaining -= addAmount;

                if (remaining <= 0) break;
            }
        }

        // 2. 남은 수량이 있다면 빈 슬롯에 새로 추가
        if (remaining > 0)
        {
            for (int i = 0; i < maxSlots; i++)
            {
                if (slots[i] == null)
                {
                    int addAmount = Mathf.Min(maxStack, remaining);
                    slots[i] = new InventoryItem(id, addAmount, i);
                    remaining -= addAmount;

                    if (remaining <= 0) break;
                }
            }
        }

        // 실제 추가된 양 계산 (요청량 - 남은량)
        int actualAdded = amount - remaining;
        if (actualAdded > 0)
        {
            UpdateCache(id, actualAdded);
            OnInventoryChanged?.Invoke();
        }

        return remaining;
    }

    // 슬롯 위치 변경 (드래그 앤 드롭용)
    public void SwapSlots(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= maxSlots || toIndex < 0 || toIndex >= maxSlots) return;

        // 데이터 교체
        InventoryItem temp = slots[fromIndex];
        slots[fromIndex] = slots[toIndex];
        slots[toIndex] = temp;

        // 중요: 바뀐 자리에 맞게 데이터 내부의 slotIndex 정보도 동기화
        if (slots[fromIndex] != null) slots[fromIndex].slotIndex = fromIndex;
        if (slots[toIndex] != null) slots[toIndex].slotIndex = toIndex;

        // UI 전체 갱신 신호 발송
        OnInventoryChanged?.Invoke();
    }

    public bool ConsumeItem(string id, int amount)
    {
        if (GetItemCount(id) < amount) return false;

        int remaining = amount;
        for (int i = 0; i < maxSlots; i++)
        {
            if (slots[i] != null && slots[i].itemID == id)
            {
                if (slots[i].count > remaining)
                {
                    slots[i].count -= remaining;
                    remaining = 0;
                }
                else
                {
                    remaining -= slots[i].count;
                    slots[i] = null;
                }
            }
            if (remaining <= 0) break;
        }

        UpdateCache(id, -amount); // 소모된 만큼 캐시 차감
        OnInventoryChanged?.Invoke();
        return true;
    }

    // 제작 시스템 등을 위한 아이템 개수 확인 [Optimized]
    public int GetItemCount(string id)
    {
        if (_itemCountCache.TryGetValue(id, out int count))
            return count;
        return 0;
    }

    /// <summary>
    /// 아이템을 추가할 수 있는지 확인 (스택 + 빈슬롯 고려)
    /// </summary>
    public bool CanAddItem(string id, int amount)
    {
        int remaining = amount;

        // 1. 스택 가능 여부 확인
        for (int i = 0; i < maxSlots; i++)
        {
            if (slots[i] != null && slots[i].itemID == id && slots[i].count < maxStack)
            {
                remaining -= (maxStack - slots[i].count);
                if (remaining <= 0) return true;
            }
        }

        // 2. 빈 슬롯 확인
        for (int i = 0; i < maxSlots; i++)
        {
            if (slots[i] == null)
            {
                remaining -= maxStack;
                if (remaining <= 0) return true;
            }
        }

        return remaining <= 0;
    }

    public bool HasSpace()
    {
        for (int i = 0; i < maxSlots; i++)
        {
            if (slots[i] == null) return true;
        }
        return false;
    }

    // 인벤에서 아이템을 버릴 시 
    public void RemoveItemAtSlot(int index)
    {
        if (index >= 0 && index < maxSlots && slots[index] != null)
        {
            // 캐시 업데이트
            UpdateCache(slots[index].itemID, -slots[index].count);
            
            slots[index] = null;
            OnInventoryChanged?.Invoke();
        }
    }

    public List<InventoryItem> GetSaveData()
    {
        List<InventoryItem> saveList = new List<InventoryItem>();
        foreach (var item in slots)
        {
            if (item != null) saveList.Add(item);
        }
        return saveList;
    }

    public void LoadData(List<InventoryItem> savedItems)
    {
        Array.Clear(slots, 0, slots.Length);

        // [Fix] 세이브 데이터가 없거나 null일 경우 초기화만 하고 종료
        if (savedItems == null)
        {
            RefreshCache();
            OnInventoryChanged?.Invoke();
            return;
        }

        foreach (var item in savedItems)
        {
            if (item != null && item.slotIndex >= 0 && item.slotIndex < maxSlots)
                slots[item.slotIndex] = item;
        }
        RefreshCache(); // 로드 후 캐시 재구축
        OnInventoryChanged?.Invoke();
    }

    /// <summary>
    /// 보상 테이블을 처리하고 (Flavor Text 포함) 결과 로그를 반환
    /// </summary>
    public List<string> GiveReward(RewardTableSO table)
    {
        var logs = new List<string>();
        if (table == null) return logs;

        var items = table.PickRewards();
        bool hasReward = items.Count > 0;

        // 1. 성공 멘트 추가
        if (hasReward && !string.IsNullOrWhiteSpace(table.successText))
        {
            logs.Add(table.successText);
        }

        // 2. 아이템 지급 및 한 줄 로그 작성
        if (hasReward)
        {
            List<string> itemLogParts = new List<string>();

            foreach (var stack in items)
            {
                AddItem(stack.data.itemID, stack.count);
                itemLogParts.Add($"{stack.data.itemName} x{stack.count}");
            }

            // "획득: 사과 x1, 철 x2" 형태로 조립
            if (itemLogParts.Count > 0)
            {
                logs.Add($"획득: {string.Join(", ", itemLogParts)}");
            }
        }

        return logs;
    }
}