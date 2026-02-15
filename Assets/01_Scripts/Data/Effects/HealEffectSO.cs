using UnityEngine;

namespace Project.Data
{
    [CreateAssetMenu(menuName = "Data/Effect/Heal")]
    public class HealEffectSO : ItemEffectSO
    {
        [Range(0f, 1f)]
        public float healRatio = 0.3f; // 기본 30%

        public override bool ExecuteEffect(UnitData target, out int value, out string statName)
        {
            value = 0;
            statName = "Hp";

            if (target == null) return false;

            value = PlayerDataManager.Instance.HealUnit(target, healRatio);
            return value > 0;
        }
    }
}
