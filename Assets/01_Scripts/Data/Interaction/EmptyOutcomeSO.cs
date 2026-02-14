using UnityEngine;

namespace Project.Data
{
    /// <summary>
    /// 꽝 효과: 아무 일도 일어나지 않고 로그만 출력합니다.
    /// </summary>
    [CreateAssetMenu(menuName = "Data/Interaction/Outcome/Empty")]
    public class EmptyOutcomeSO : InteractionOutcomeSO
    {
        [TextArea]
        public string message = "아무 일도 일어나지 않았습니다.";

        public override void Execute(UnitData user)
        {
            if (ExplorationLogUI.Instance != null)
                ExplorationLogUI.Instance.Push(message);
                
            Debug.Log("[Empty] Interaction resulted in nothing.");
        }
    }
}
