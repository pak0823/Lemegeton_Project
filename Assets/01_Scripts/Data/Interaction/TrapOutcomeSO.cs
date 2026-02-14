using UnityEngine;

namespace Project.Data
{
    /// <summary>
    /// 함정 효과: 플레이어 유닛의 스탯을 영구적으로 감소시킵니다.
    /// </summary>
    [CreateAssetMenu(menuName = "Data/Interaction/Outcome/Trap")]
    public class TrapOutcomeSO : InteractionOutcomeSO
    {
        [Header("감소 대상 스탯 (예: STR, muscles, mind...)")]
        public string targetStat;

        [Header("감소량 (양수로 입력하면 감소 처리됨)")]
        public int reductionAmount;

        [TextArea]
        public string logMessage = "{0}의 {1}이(가) {2}만큼 감소했습니다.";

        public override void Execute(UnitData user)
        {
            if (user == null) return;

            // 스탯 감소 적용 (음수로 변환하여 전달)
            PlayerDataManager.Instance.ApplyStatModifier(user, targetStat, -reductionAmount);

            // 로그 출력
            string finalStatName = GetLocalizedStatName(targetStat);
            string msg = string.Format(logMessage, user.DisplayName, finalStatName, reductionAmount);
            
            if (ExplorationLogUI.Instance != null)
                ExplorationLogUI.Instance.Push(msg);
                
            Debug.Log($"[Trap] {user.DisplayName} triggered trap. {targetStat} -{reductionAmount}");
        }

        private string GetLocalizedStatName(string code)
        {
            // 필요 시 로컬라이징 테이블 연동. 지금은 코드 그대로 반환하거나 간단 매핑.
            switch (code.ToUpper())
            {
                case "STR": return "근력";
                case "AGI": return "민첩";
                case "CLV": return "총명";
                case "BDY": return "신체";
                case "MND": return "정신";
                case "INS": return "통찰";
                default: return code;
            }
        }
    }
}
