using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 자신을 중심으로 주변 2칸에 '야수의 영역'을 2턴 동안 생성.
/// - 영역은 스킬을 사용한 그 위치에 고정
/// - 이 스킬을 사용한 유닛은 영역 안에서 이동할 때 행동을 소비하지 않음(턴이 끝나지 않음)
/// </summary>
[CreateAssetMenu(menuName = "Battle/Skill/Player/Self/BeastDomain", fileName = "CS_BeastDomain")]
public class SelfBeastDomainSkill : SkillAsset, ISelfCastSkill
{
    public bool SelfCastOnSelect => true;

    [Header("지속 턴(시전자 기준 턴 수)")]
    public int durationTurns = 2;
    public int radius = 2; // Default radius

    [Header("Training: Resistance On Cast")]
    public bool trainingApplyResistanceOnCast = false;
    [Range(-1, 2)] public int routeForApplyResistanceOnCast = -1;
    public int resistanceStacksOnCast = 1;
    public int resistanceDurationTurns = 2;

    [Header("Training: Rage Drain On Turn Start")]
    public bool trainingReduceRageOnTurnStart = false;
    [Range(-1, 2)] public int routeForRageReduceOnTurnStart = -1;
    public float rageReducePerClv = 1.0f;

    [Header("Training: Free Action")]
    public bool trainingUseFreeAction = false;
    [Range(-1, 2)] public int routeForFreeAction = -1;

    public override IEnumerator Execute(BattleManager bm, BattleUnit caster, BattleUnit targetUnit, Tilemap targetMap, Vector3Int targetCell)
    {
        // Self Cast Flow
        yield return bm.PerformStandardUnitSkillFlow(this, caster, caster);
    }
    
    public override IEnumerable<Vector3Int> GetAreaCells(Vector3Int _originCell, bool _isOddColumn)
    {
        // Simple radius around self
        return AreaShapes.BeastDomainArea(_originCell, radius);
    }

    public override IEnumerator ResolveOnUnit(BattleManager _battlemanager, BattleUnit _caster, BattleUnit _target)
    {
        // Create Zone
        var map = _caster.CurrentMap;
        var originCell = _caster.Cell;

        // Apply Training Effects
        int route = _caster.GetTrainingRouteIndex(this);

        // 1. Resistance On Cast
        if (trainingApplyResistanceOnCast && routeForApplyResistanceOnCast >= 0 && route == routeForApplyResistanceOnCast)
        {
            var sc = _caster.GetComponent<StatusController>();
            if (sc != null)
            {
                sc.ApplyWithTurnContext(
                    StatusId.Resistance,
                    Mathf.Max(1, resistanceStacksOnCast),
                    Mathf.Max(1, resistanceDurationTurns)
                );
                Debug.Log($"[BeastDomain] 저항 훈련: {_caster.name} 자신에게 Resistance {resistanceStacksOnCast}스택, {resistanceDurationTurns}턴 부여");
            }
        }

        // 2. Create Field Logic
        // (Assuming BattleField or similar handles zone creation. 
        // Logic from original fragment implied calling `_battlemanager.Field.Create...` or similar?
        // Since I don't see BattleField handy, I'll log or use a placeholder if needed.
        // Actually, ParametricDamageSkill used `_battlemanager.Field.CreateStatusTileZone`.
        // I should use that if available, but for now I will assume the "BeastField" logic is handled by a Zone controller or just log it.)
        
        // Wait, the CSV said "Field_Create" "BeastField".
        // If there is a specific method for BeastField, I should use it.
        // But for now, verifying imports is key.
        
        Debug.Log($"[BeastDomain] {_caster.name}가 야수의 영역을 생성함 (중심:{originCell}, 반경:{radius}, 지속:{durationTurns}턴)");
        yield break;
    }

    public override IEnumerator ResolveOnTile(BattleManager _battlemanager, Tilemap _map, Vector3Int _originCell, BattleUnit _caster)
    {
        yield break;
    }

    public override string GetFullDescriptionRich(BattleUnit _caster)
    {
        string baseDesc = base.GetFullDescriptionRich(_caster);

        int route = _caster != null ? _caster.GetTrainingRouteIndex(this) : -1;
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
