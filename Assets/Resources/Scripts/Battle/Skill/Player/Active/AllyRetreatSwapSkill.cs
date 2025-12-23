using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "Battle/Skills/Common/AllyRetreatSwapSkill", fileName = "AllyRetreatSwapSkill")]
public class AllyRetreatSwapSkill : SkillAsset
{
    [Header("Retreat Settings")]
    [Tooltip("아군이 뒤로 물러날 수 있는 방향 (ParametricDirection과 동일 개념)")]
    public ParametricDirectionSkill.BackMode backMode = ParametricDirectionSkill.BackMode.W_or_SW;

    [Tooltip("뒤로 몇 칸까지 허용할지 (보통 1칸만 사용)")]
    public int maxRetreatCells = 1;

    [Tooltip("이동 연출: 대시/점프를 쓸지 여부 (false면 순간 이동처럼)")]
    public bool useDashAnimate = false;
    public float dashDuration = 0.12f;
    public float dashArc = 0.0f;

    [Header("Training")]
    [Header("방어 중첩 버프")]
    [Tooltip("특정 훈련 루트에서 대상에게 방어 중첩 상태를 부여할지 여부")]
    public bool trainingApplyDefenseStacks = false;
    [Tooltip("방어 중첩 상태를 적용할 훈련 루트(-1이면 미사용, 0~2)")]
    [Range(-1, 2)] public int routeForDefenseStacks = -1;
    [Tooltip("부여할 StatusId (StackableStatusVisualDB에 아이콘/이름 연결 가능)")]
    public StatusId trainingDefenseStatusId = StatusId.None;
    [Tooltip("한 번에 부여할 방어 중첩 수(예: 3중첩)")]
    [Min(1)] public int trainingDefenseStacks = 3;
    [Tooltip("방어 중첩 지속 턴(예: 3턴)")]
    [Min(1)] public int trainingDefenseDurationTurns = 3;

    [Header("쿨다운 단축")]
    [Tooltip("특정 훈련 루트에서 이 스킬의 재사용 대기 턴을 바꿀지 여부")]
    public bool trainingUseCooldownOverride = false;
    [Tooltip("쿨다운을 덮어쓸 훈련 루트(-1이면 비활성, 0~2)")]
    [Range(-1, 2)] public int routeForCooldownOverride = -1;
    [Tooltip("해당 루트에서 사용할 재사용 대기 턴 수(기본 쿨보다 작게 설정)")]
    [Min(0)] public int trainingCooldownTurns = 0;

    [Header("적의 감소")]
    [Tooltip("특정 훈련 루트에서 대상의 적의를 감소시킬지 여부")]
    public bool trainingApplyHostilityDelta = false;
    [Tooltip("적의 감소를 적용할 훈련 루트(-1이면 비활성, 0~2)")]
    [Range(-1, 2)] public int routeForHostilityDelta = -1;
    [Tooltip("대상 유닛의 적의를 이 값만큼 즉시 감소 (양수 입력, 내부에서 음수로 적용)")]
    public float trainingHostilityDelta = 0.5f;

    void OnEnable()
    {
        targetMode = SkillTargetMode.Unit;
        power = 0f;                 // 피해 없음
        school = DamageSchool.Physical;
        costResource = SkillCostResource.MP;
    }
    int GetRoute(BattleUnit _caster)
    {
        if (!_caster) return -1;
        return _caster.GetTrainingRouteIndex(this); // 기존 TrainingDB/UnitData 구조 재사용
    }

    // 이 스킬은 범위 피해가 없으므로 AreaCells는 비워둔다.
    public override IEnumerable<Vector3Int> GetAreaCells(Vector3Int _originCell, bool _isOddRow)
    {
        yield break;
    }

    // Enemy AI가 쓴다거나, 특수 상황에서 그냥 자동으로 쓰고 싶을 때 사용할 fallback
    public override IEnumerator ResolveOnUnit(BattleManager _battlemanager, BattleUnit _caster, BattleUnit _target)
    {
        if (!_battlemanager || _caster == null || _target == null) yield break;
        if (_caster.IsDead || _target.IsDead) yield break;
        if (_caster.team != _target.team) yield break;
        if (_target.CurrentMap == null || _caster.CurrentMap != _target.CurrentMap) yield break;

        // 이동 후보 계산
        var candidates = GetRetreatCandidates(_battlemanager, _target).ToList();
        if (candidates.Count == 0) yield break;   // 뒤로 빠질 수 있는 칸이 없다면 실패

        // MP 체크 (훈련 MP 반영)
        var res = GetCostResource(_caster);
        int cost = GetEffectiveCost(_caster);
        if (cost > 0 && !_caster.TryConsumeResource(res, cost)) yield break;

        // 일단 첫 후보로 자동 처리 (플레이어용은 BattleManager에서 타일 선택 코루틴 사용)
        var dest = candidates[0];
        yield return ResolveSwapWithDest(_battlemanager, _caster, _target, dest);
    }

    // 타일 지목형이 아니므로 비워둔다.
    public override IEnumerator ResolveOnTile(BattleManager _battlemanager, Tilemap _map, Vector3Int _originCell, BattleUnit _caster)
    {
        yield break;
    }

    /// <summary>
    /// target(아군)이 '뒤로' 물러날 수 있는 후보 타일들을 반환
    /// </summary>
    public IEnumerable<Vector3Int> GetRetreatCandidates(BattleManager _battlemanager, BattleUnit _teamunit)
    {
        var results = new List<Vector3Int>();
        if (_battlemanager == null || _teamunit == null || _teamunit.CurrentMap == null) return results;

        var map = _teamunit.CurrentMap;
        var start = _teamunit.Cell;
        bool odd = SkillLibrary.IsOddColumn(start);

        // 팀 기준 뒤/앞 쌍(ParametricDirectionSkill.GetDirectionsFor와 동일 개념) :contentReference[oaicite:0]{index=0}
        // Player: 뒤 = W, SW / Enemy: 뒤 = E, NE
        Vector3Int backA, backB;

        if (_teamunit.team == Team.Player)
        {
            // 플레이어 기준: 뒤(W, SW)
            backA = new Vector3Int(-1, 0, 0); // W
            backB = odd ? new Vector3Int(0, -1, 0) : new Vector3Int(-1, -1, 0); // SW
        }
        else
        {
            // 적 기준: 뒤(E, NE)
            backA = new Vector3Int(+1, 0, 0); // E
            backB = odd ? new Vector3Int(+1, +1, 0) : new Vector3Int(0, +1, 0); // NE
        }

        var dirs = new List<Vector3Int>();
        switch (backMode)
        {
            case ParametricDirectionSkill.BackMode.W_Only:
            case ParametricDirectionSkill.BackMode.SW_Only:
                // 단일 방향만 쓰고 싶다면 여기서 세분화 가능하지만,
                // 일단은 W_Only/SW_Only도 1개씩으로 취급
                dirs.Add(backA);
                break;
            case ParametricDirectionSkill.BackMode.W_or_SW:
            default:
                dirs.Add(backA);
                dirs.Add(backB);
                break;
        }

        foreach (var step in dirs)
        {
            var cur = start;
            int guard = 0;

            while (true)
            {
                if (maxRetreatCells > 0 && guard >= maxRetreatCells)
                    break;
                guard++;

                var next = new Vector3Int(cur.x + step.x, cur.y + step.y, cur.z);
                if (!map.cellBounds.Contains(next)) break;
                if (!map.HasTile(next)) break;

                if (IsCellFree(_battlemanager, map, next))
                {
                    results.Add(next);
                    break; // 이 방향에서 제일 가까운 칸만 사용
                }

                cur = next;
            }
        }

        return results;
    }

    bool IsCellFree(BattleManager _battlemanager, Tilemap _map, Vector3Int _cell)
    {
        if (!_map.HasTile(_cell)) return false;
        var units = _battlemanager.GetUnitsInArea(_map, new[] { _cell });  // BattleManager 유틸 재사용 :contentReference[oaicite:1]{index=1}
        foreach (var u in units)
        {
            if (u != null && !u.IsDead && u.Cell == _cell)
                return false;
        }
        return true;
    }

    /// <summary>
    /// 선택된 retreatCell로 아군을 후퇴시키고, 캐스터는 그 자리에 이동
    /// </summary>
    public IEnumerator ResolveSwapWithDest(BattleManager _battlemanager, BattleUnit _caster, BattleUnit _teamunit, Vector3Int _retreatCell)
    {
        if (_battlemanager == null || _caster == null || _teamunit == null) yield break;
        var map = _teamunit.CurrentMap;
        if (!map || _caster.CurrentMap != map) yield break;

        // 여전히 타일이 유효하고 비어있는지 마지막으로 확인
        if (!map.HasTile(_retreatCell)) yield break;
        if (!IsCellFree(_battlemanager, map, _retreatCell)) yield break;

        var allyStartCell = _teamunit.Cell;
        var casterStartCell = _caster.Cell;

        // 그리드 점유 해제
        if (_battlemanager.grid != null)
        {
            _battlemanager.grid.SetOccupied(_caster.team, casterStartCell, false);
            _battlemanager.grid.SetOccupied(_teamunit.team, allyStartCell, false);
        }

        // 우선 아군 후퇴  그 다음 캐스터 이동
        if (useDashAnimate)
        {
            // 캐스터 고유 트리거
            _caster.PlayTrigger("Moving"); // Animator에 추가할 트리거

            // 캐스터 점프/대시 이동 연출 (dashDuration/dashArc 활용)
            Vector3 casterToW = map.GetCellCenterWorld(allyStartCell);
            yield return _caster.AnimateJumpToWorld(
                casterToW,
                durationOverride: dashDuration,
                speedUnitsPerSec: null,
                arcHeight: dashArc
            );

            // 이동 후 셀 스냅(점프는 transform만 옮기므로 Cell/Map 갱신 필요)
            _caster.MoveTo(map, allyStartCell);

            // 아군 기존 기본 이동 애니메이션(Move bool) 유지
            yield return _teamunit.AnimateMoveTo(map, _retreatCell);
        }
        else
        {
            // 연출 없이 즉시 이동
            _teamunit.MoveTo(map, _retreatCell);
            _caster.MoveTo(map, allyStartCell);
            yield return null;
        }

        int route = GetRoute(_caster);

        // 방어 중첩 부여
        if (trainingApplyDefenseStacks &&
            routeForDefenseStacks >= 0 &&
            route == routeForDefenseStacks &&
            trainingDefenseStatusId != StatusId.None &&
            _teamunit != null)
        {
            var sc = _teamunit.GetComponent<StatusController>();
            if (sc != null)
            {
                sc.ApplyWithTurnContext(
                    trainingDefenseStatusId,
                    Mathf.Max(1, trainingDefenseStacks),
                    Mathf.Max(1, trainingDefenseDurationTurns));
            }
        }

        // 대상의 적의 감소
        if (trainingApplyHostilityDelta &&
            routeForHostilityDelta >= 0 &&
            route == routeForHostilityDelta &&
            _teamunit != null &&
            trainingHostilityDelta > 0f)
        {
            // AddHostility는 0 아래로는 깎이지 않도록 내부에서 막고 있음
            _teamunit.AddHostility(-Mathf.Abs(trainingHostilityDelta));
        }

        // 그리드 점유 재설정
        if (_battlemanager.grid != null)
        {
            _battlemanager.grid.SetOccupied(_teamunit.team, _teamunit.Cell, true);
            _battlemanager.grid.SetOccupied(_caster.team, _caster.Cell, true);
        }
    }

    public override int GetEffectiveCooldownTurns(BattleUnit _caster)
    {
        int baseCd = base.GetEffectiveCooldownTurns(_caster);
        if (!trainingUseCooldownOverride || _caster == null)
            return baseCd;

        int route = GetRoute(_caster);
        if (routeForCooldownOverride >= 0 && route == routeForCooldownOverride)
        {
            // 훈련에서 지정한 쿨다운으로 덮어씀
            return Mathf.Max(0, trainingCooldownTurns);
        }

        return baseCd;
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
