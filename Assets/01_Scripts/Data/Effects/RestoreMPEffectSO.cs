using UnityEngine;

namespace Project.Data
{
    [CreateAssetMenu(menuName = "Data/Effect/RestoreMP")]
    public class RestoreMPEffectSO : ItemEffectSO
    {
        [Range(0f, 1f)]
        public float restoreRatio = 0.3f; // 기본 30%

        public override bool ExecuteEffect(UnitData target, out int value, out string statName)
        {
            value = 0;
            statName = "Mp";

            if (target == null) return false;

            value = PlayerDataManager.Instance.RestoreMP(target, restoreRatio);
            return value > 0;
        }
    }
}
