using System.Collections.Generic;
using UnityEngine;

namespace Project.Data
{
    /// <summary>
    /// 채집/상호작용 오브젝트의 데이터 설정입니다.
    /// 기획서의 각 행(Row)에 해당합니다.
    /// </summary>
    [CreateAssetMenu(menuName = "Data/Definitions/Gatherable Object Data")]
    public class GatherableDataSO : ScriptableObject
    {
        [Header("기본 정보")]
        public string objectName; // Name_KOR
        [TextArea] public string description; // Observation_KOR
        [Min(0)] public int vigorCost = 1; // 상호작용 시 소모할 활기

        [System.Serializable]
        public class WeightedOutcome
        {
            public InteractionOutcomeSO outcome; // 결과 로직 (보상/함정/꽝)
            [Range(0f, 1f)] public float probability; // 확률 (0.6, 0.2, 0.2 등)
            [TextArea] public string resultText; // 결과 텍스트 (로그 출력용)
        }

        [Header("상호작용 결과 분기 (3개: 성공, 실패, 함정)")]
        public List<WeightedOutcome> outcomes = new List<WeightedOutcome>();

        /// <summary>
        /// 확률에 따라 결과를 하나 추첨합니다.
        /// </summary>
        public WeightedOutcome PickOutcome()
        {
            if (outcomes == null || outcomes.Count == 0) return null;

            float roll = Random.value; // 0.0 ~ 1.0
            float current = 0f;

            foreach (var item in outcomes)
            {
                current += item.probability;
                if (roll <= current)
                {
                    return item;
                }
            }

            // 부동소수점 오차로 인해 마지막 항목이 선택되지 않을 경우, 마지막 항목 반환
            return outcomes[outcomes.Count - 1];
        }
    }
}
