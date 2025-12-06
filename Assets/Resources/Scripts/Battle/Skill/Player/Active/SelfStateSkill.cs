using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 선택 즉시 자기 자신에게 상태를 부여하는 스킬.
/// 지속시간 없음(무기한). 해제는 별도 '해제 스킬'로 처리.
/// </summary>
[CreateAssetMenu(menuName = "Battle/Skills/State/Self State (Permanent)", fileName = "SelfStateSkill")]
public class SelfStateSkill : SkillAsset, ISelfCastSkill
{
    [Header("State")]
    public UnitStateId stateId = UnitStateId.Support;

    [Header("Training Overrides")]
    [Tooltip("MP 비용을 이 값으로 덮어씀 (trainingUseMpOverride가 true일 때만)")]
    public bool trainingUseMpOverride = false;
    public int trainingMpCostRoute0 = 2;   // 예: 훈련 시 2MP로 사용

    [Tooltip("특정 상태를 켰을 때 마법 공격력 버프를 추가로 부여할지 여부")]
    public bool trainingApplyMagicBuffOnRoute1 = false;
    public UnitStateBuffId trainingMagicBuffId = UnitStateBuffId.Smoke_MagicUp;

    [Tooltip("이 스킬을 사용해도 턴을 소비하지 않음(무료 행동)")]
    public bool trainingFreeActionOnRoute2 = false;

    public bool SelfCastOnSelect => true;

#if UNITY_EDITOR
    void OnValidate() { targetMode = SkillTargetMode.Unit; } // 기존 파이프 재사용
#endif
    void OnEnable() { targetMode = SkillTargetMode.Unit; }

    public override IEnumerable<Vector3Int> GetAreaCells(Vector3Int _originCell, bool _isOddRow)
    {
        yield break; // 프리뷰 불필요
    }

    public override System.Collections.IEnumerator ResolveOnUnit(BattleManager _bm, BattleUnit _caster, BattleUnit _target)
    {
        if (!_caster) yield break;

        var usc = _caster.GetComponent<UnitStateController>();
        if (usc == null) usc = _caster.gameObject.AddComponent<UnitStateController>();

        int cost = GetEffectiveMpCost(_caster);
        if (!_caster.TryConsumeMP(cost))
        {
            Debug.Log($"[SelfStateSkill] MP 부족: {displayName} (필요 {cost})");
            yield break;
        }

        // ==== 상태 부여 ====
        usc.Apply(stateId);

        // ==== 훈련 루트별 추가 효과 ====
        int route = _caster.GetTrainingRouteIndex(this);

        // Route 1: 마법 공격력 버프 부여 (StateStatModifierDB의 Buff 통해 MAG ×1.3)
        if (route == 1 && trainingApplyMagicBuffOnRoute1 && trainingMagicBuffId != UnitStateBuffId.None)
        {
            usc.ApplyBuff(trainingMagicBuffId);
            Debug.Log($"[SelfStateSkill] Route1 MAG Buff 적용: {_caster.name}, Buff={trainingMagicBuffId}");
        }

        yield break;
    }

    public override System.Collections.IEnumerator ResolveOnTile(BattleManager _bm, Tilemap _map, Vector3Int _originCell, BattleUnit _caster)
    {
        yield break; // 사용 안 함
    }

    public override int GetEffectiveMpCost(BattleUnit _caster)
    {
        int baseCost = mpCost;
        if (_caster == null) return baseCost;

        int route = _caster.GetTrainingRouteIndex(this);
        // Route 0 일 때 MP 비용 덮어쓰기
        if (route == 0 && trainingUseMpOverride)
        {
            return Mathf.Max(0, trainingMpCostRoute0);
        }

        return baseCost;
    }
    public override string GetFullDescriptionRich(BattleUnit _caster)
    {
        int cost = GetEffectiveMpCost(_caster);
        string mpColor = "#00A2FF";
        string baseDesc;
        if (!string.IsNullOrEmpty(description))
        {
            if (cost > 0)
                baseDesc = $"{description}<size=20%><color=#808080>(MP:<color={mpColor}>{cost}</color>)</color></size>";
            else
                baseDesc = description;
        }
        else
        {
            baseDesc = base.GetFullDescriptionRich(_caster);
        }

        int route = _caster.GetTrainingRouteIndex(this);
        if (route < 0 || trainingRoutes == null || route >= trainingRoutes.Length)
            return baseDesc;

        var info = trainingRoutes[route];

        return SkillTooltipUtil.AppendTrainingRouteDescription(
            baseDesc,
            info.title,
            info.description
        );
    }
}
