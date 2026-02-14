using UnityEngine;
using System.Collections.Generic;

namespace Project.Data
{
    /// <summary>
    /// 성공 효과: 아이템 보상을 지급합니다.
    /// </summary>
    [CreateAssetMenu(menuName = "Data/Interaction/Outcome/Reward")]
    public class RewardOutcomeSO : InteractionOutcomeSO
    {
        [Header("지급할 보상 테이블")]
        public RewardTableSO rewardTable;

        public override void Execute(UnitData user)
        {
            if (InventoryManager.Instance != null && rewardTable != null)
            {
                // 보상 지급 및 로그 출력 위임
               List<string> logs = InventoryManager.Instance.GiveReward(rewardTable);
               
               if (logs != null)
               {
                   foreach (var log in logs)
                   {
                       if (ExplorationLogUI.Instance != null)
                           ExplorationLogUI.Instance.Push(log, pause: false);
                   }
               }
            }
            else
            {
                Debug.LogWarning("[RewardOutcome] InventoryManager or RewardTable is missing.");
            }
        }
    }
}
