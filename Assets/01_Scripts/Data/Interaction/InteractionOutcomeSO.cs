using UnityEngine;

namespace Project.Data
{
    /// <summary>
    /// 상호작용 결과의 기본 클래스입니다.
    /// 성공(보상), 실패(꽝), 함정(스탯 감소) 등의 결과를 정의합니다.
    /// </summary>
    public abstract class InteractionOutcomeSO : ScriptableObject
    {
        /// <summary>
        /// 상호작용 결과를 실행합니다.
        /// </summary>
        /// <param name="user">상호작용한 유닛 (필요 시)</param>
        public abstract void Execute(UnitData user);
    }
}
