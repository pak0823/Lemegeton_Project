using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewRewardTable", menuName = "Data/RewardTable")]
public class RewardTableSO : ScriptableObject
{
    [System.Serializable]
    public class DropEntry
    {
        public ItemData item;
        [Min(1)] public int minCount = 1;
        [Min(1)] public int maxCount = 1;
        [Range(0, 100)] public int weight = 10;
    }

    [Header("드롭 설정")]
    public bool guaranteedDrop = false; // true면 반드시 하나 이상(가중치 기반) 드롭
    public int pickCount = 1; // 몇 번 추첨할 것인가

    [Header("메시지 설정 (Flavor Text)")]
    [TextArea] public string successText; // 아이템 획득 성공 시 출력

    [Header("드롭 목록")]
    public List<DropEntry> entries = new List<DropEntry>();

    /// <summary>
    /// 가중치 기반으로 아이템을 추첨하여 반환 (인벤토리에 넣지는 않음)
    /// </summary>
    public List<ItemStack> PickRewards()
    {
        List<ItemStack> results = new List<ItemStack>();
        if (entries == null || entries.Count == 0) return results;

        // 가중치 총합 계산
        int totalWeight = 0;
        foreach (var e in entries)
            if (e.item != null && e.weight > 0) totalWeight += e.weight;

        if (totalWeight <= 0) return results;

        for (int i = 0; i < pickCount; i++)
        {
            int roll = Random.Range(0, totalWeight);
            int current = 0;

            foreach (var e in entries)
            {
                if (e.item == null || e.weight <= 0) continue;

                current += e.weight;
                if (roll < current)
                {
                    // 당첨
                    int count = Random.Range(e.minCount, e.maxCount + 1);
                    results.Add(new ItemStack(e.item, count));
                    break;
                }
            }
        }
        return results;
    }

    // 간단한 데이터 전달용 구조체
    public struct ItemStack
    {
        public ItemData data;
        public int count;
        public ItemStack(ItemData d, int c) { data = d; count = c; }
    }
}
