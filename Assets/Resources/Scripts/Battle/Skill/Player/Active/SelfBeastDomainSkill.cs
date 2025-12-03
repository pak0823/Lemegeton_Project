using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 자신을 중심으로 주변 2칸에 '야수의 영역'을 2턴 동안 생성.
/// - 영역은 스킬을 사용한 그 위치에 고정
/// - 이 스킬을 사용한 유닛은 영역 안에서 이동할 때 행동을 소비하지 않음(턴이 끝나지 않음)
/// </summary>
[CreateAssetMenu(
    menuName = "Battle/Skills/Zone/Self Beast Domain",
    fileName = "SelfBeastDomainSkill")]
public class SelfBeastDomainSkill : SkillAsset, ISelfCastSkill
{
    [Header("지속 턴(시전자 기준 턴 수)")]
    public int durationTurns = 2;

    [Header("영역 반경 (타일 거리)")]
    public int radius = 2;

    public bool SelfCastOnSelect => true;

#if UNITY_EDITOR
    void OnValidate()
    {
        targetMode = SkillTargetMode.Unit;
    }
#endif

    void OnEnable()
    {
        // 자기 자신 대상, 데미지는 없지만 기본 물리 스쿨로 맞춰둠
        targetMode = SkillTargetMode.Unit;
        school = DamageSchool.Physical;
    }

    int GetRoute(BattleUnit caster)
    {
        if (!caster) return -1;
        return caster.GetTrainingRouteIndex(this);
    }

    /// <summary>
    /// 범위 프리뷰: 캐스터 위치 기준 반경 2 원형.
    /// 실제로는 ResolveOnUnit에서 BattleManager 쪽에 영역을 등록한다.
    /// </summary>
    public override IEnumerable<Vector3Int> GetAreaCells(Vector3Int originCell, bool _ /*unused*/)
    {
        foreach (var c in AreaShapes.BeastDomainArea(originCell, radius))
            yield return c;
    }

    public override int GetEffectiveMpCost(BattleUnit caster)
    {
        // 필요하다면 나중에 훈련 루트별 MP 변형 추가 가능
        return mpCost;
    }

    public override IEnumerator ResolveOnUnit(BattleManager bm, BattleUnit caster, BattleUnit target)
    {
        if (!bm) yield break;
        if (!caster) yield break;

        target = caster;

        // MP 소모
        int cost = GetEffectiveMpCost(caster);
        if (cost > 0 && !caster.TryConsumeMP(cost))
            yield break;

        // 캐스터가 바인드된 타일맵과 셀을 그대로 사용
        Tilemap map = caster.CurrentMap;
        if (!map)
        {
            Debug.LogWarning("[BeastDomain] 캐스터의 CurrentMap이 없습니다.");
            yield break;
        }

        Vector3Int originCell = caster.Cell;
        if (!map.HasTile(originCell))
        {
            Debug.LogWarning($"[BeastDomain] 중심 셀에 타일이 없습니다: {originCell}");
            yield break;
        }

        // BattleManager에 영역 생성 요청
        bm.SpawnBeastDomainZone(map, caster, originCell, radius, durationTurns);

        Debug.Log($"[BeastDomain] {caster.name}가 야수의 영역을 생성함 (중심:{originCell}, 반경:{radius}, 지속:{durationTurns}턴)");

        yield break;
    }

    public override IEnumerator ResolveOnTile(BattleManager bm, Tilemap map, Vector3Int originCell, BattleUnit caster)
    {
        // 이 스킬은 타일 지정이 아니라 자기 자신 대상이라 여기서는 아무것도 안 함
        yield break;
    }

    public override string GetFullDescriptionRich(BattleUnit caster)
    {
        int cost = GetEffectiveMpCost(caster);
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
            baseDesc = base.GetFullDescriptionRich(caster);
        }

        // 이 스킬은 아직 별도의 훈련 루트 효과가 없으니 그대로 반환
        return baseDesc;
    }
}
