using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 자기 자신에게 걸린 UnitState를 해제하는 스킬.
/// - removeAll = true: 전부 제거
/// - removeAll = false: 지정된 stateIds만 제거
/// </summary>
[CreateAssetMenu(menuName = "Battle/Skills/State/Self State Cleanse", fileName = "SelfStateCleanseSkill")]
public class SelfStateCleanseSkill : SkillAsset, ISelfCastSkill
{
    [Header("Cleanse Options")]
    public bool removeAll = true;
    public List<UnitStateId> stateIds = new(); // removeAll=false일 때만 사용

    public bool SelfCastOnSelect => true;

#if UNITY_EDITOR
    void OnValidate() { targetMode = SkillTargetMode.Unit; }
#endif
    void OnEnable() { targetMode = SkillTargetMode.Unit; }

    public override IEnumerable<Vector3Int> GetAreaCells(Vector3Int originCell, bool isOddRow)
    {
        yield break;
    }

    public override System.Collections.IEnumerator ResolveOnUnit(BattleManager bm, BattleUnit caster, BattleUnit target)
    {
        if (!caster) yield break;

        var usc = caster.GetComponent<UnitStateController>();
        if (usc == null) yield break;

        if (removeAll)
        {
            usc.RemoveAll();
        }
        else
        {
            foreach (var id in stateIds)
                usc.Remove(id);
        }
        yield break;
    }

    public override System.Collections.IEnumerator ResolveOnTile(BattleManager bm, Tilemap map, Vector3Int originCell, BattleUnit caster)
    {
        yield break;
    }
}
