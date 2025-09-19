using System.Collections.Generic;
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

    public bool SelfCastOnSelect => true;

#if UNITY_EDITOR
    void OnValidate() { targetMode = SkillTargetMode.Unit; } // 기존 파이프 재사용
#endif
    void OnEnable() { targetMode = SkillTargetMode.Unit; }

    public override IEnumerable<Vector3Int> GetAreaCells(Vector3Int originCell, bool isOddRow)
    {
        yield break; // 프리뷰 불필요
    }

    public override System.Collections.IEnumerator ResolveOnUnit(BattleManager bm, BattleUnit caster, BattleUnit target)
    {
        if (!caster) yield break;

        var usc = caster.GetComponent<UnitStateController>();
        if (usc == null) usc = caster.gameObject.AddComponent<UnitStateController>();

        usc.Apply(stateId);
        // 필요 시 연출/사운드 트리거만 추가
        yield break;
    }

    public override System.Collections.IEnumerator ResolveOnTile(BattleManager bm, Tilemap map, Vector3Int originCell, BattleUnit caster)
    {
        yield break; // 사용 안 함
    }
}
