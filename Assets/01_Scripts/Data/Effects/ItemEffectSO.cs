using UnityEngine;

namespace Project.Data
{
    /// <summary>
    /// 아이템 사용 효과를 정의하는 전략 패턴의 추상 클래스
    /// </summary>
    public abstract class ItemEffectSO : ScriptableObject
    {
        [TextArea] public string effectDescription; // 에디터 설명용

        /// <summary>
        /// 아이템 효과를 실행합니다.
        /// </summary>
        /// <param name="target">효과를 적용할 대상 유닛</param>
        /// <param name="value">결과값 (회복량 등 로그 출력용)</param>
        /// <returns>사용 성공 여부</returns>
        public abstract bool ExecuteEffect(UnitData target, out int value, out string statName);
    }
}
