using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BattleRewardManager : MonoBehaviour
{
    public static BattleRewardManager Instance;

    [Header("Dependencies")]
    public ItemLibrary itemLibrary;

    [Header("Reward Config")]
    public int minRewardTypes = 3;
    public int maxRewardTypes = 5;

    public int minMaterialCount = 1;
    public int maxMaterialCount = 6;

    public int minConsumableCount = 1;
    public int maxConsumableCount = 3;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public List<RewardData> GenerateRewards(WaveSet.RewardProfile profile = null)
    {
        List<RewardData> rewards = new List<RewardData>();

        if (itemLibrary == null)
        {
            Debug.LogError("[BattleRewardManager] ItemLibrary is not assigned!");
            return rewards;
        }

        // 1. 대상 아이템 풀 수집 (Material, Consumable)
        var materials = itemLibrary.GetItemsByType(ItemType.Material);
        var consumables = itemLibrary.GetItemsByType(ItemType.Consumable);

        var pool = new List<ItemData>();
        pool.AddRange(materials);
        pool.AddRange(consumables);

        if (pool.Count == 0)
        {
            Debug.LogWarning("[BattleRewardManager] No items found in library for rewards.");
            return rewards;
        }

        // 2. 보상 종류 개수 결정 (3~5개)
        int rewardTypeCount = Random.Range(minRewardTypes, maxRewardTypes + 1);

        // 풀에서 랜덤하게 선택 (중복 없이)
        // Fisher-Yates Shuffle or similar approach
        var selectedItems = pool.OrderBy(x => Random.value).Take(rewardTypeCount).ToList();

        // 3. 각 아이템별 수량 결정
        foreach (var item in selectedItems)
        {
            int count = 1;

            if (item.itemType == ItemType.Material)
            {
                count = Random.Range(minMaterialCount, maxMaterialCount + 1);
            }
            else if (item.itemType == ItemType.Consumable)
            {
                count = Random.Range(minConsumableCount, maxConsumableCount + 1);
            }

            if (profile != null)
            {
                count = Mathf.CeilToInt(count * profile.rewardMultiplier);
            }

            rewards.Add(new RewardData(item.itemID, count));
        }

        // 4. 추가 확정 보상 (Elite, Boss 등에서 지정된 보상)
        if (profile != null && profile.guaranteedRewards != null)
        {
            foreach (var gr in profile.guaranteedRewards)
            {
                var existing = rewards.Find(r => r.itemID == gr.itemID);
                if (existing != null)
                {
                    existing.count += gr.count;
                }
                else
                {
                    rewards.Add(new RewardData(gr.itemID, gr.count));
                }
            }
        }

        return rewards;
    }
}
