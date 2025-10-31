using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "Battle/Skills/Common/ParametricDirectionSkill", fileName = "ParametricDirectionSkill")]
public class ParametricDirectionSkill : SkillAsset, ISelfCastSkill
{
    // 선택 즉시 자기 자신에게 발동(타겟팅 불필요)
    public bool SelfCastOnSelect => true;

    public enum DirectionMode
    {
        // 팀 기준: Player는 X-가 뒤/ X+가 앞, Enemy는 반대
        TeamBasedBack,   // 뒤로 이동(후퇴)
        TeamBasedFront,  // 앞으로 이동(전진)

        // 맵 절대축 기준
        AbsoluteNegativeX, // 왼쪽으로
        AbsolutePositiveX, // 오른쪽으로
    }

    [Header("Movement Settings")]
    public DirectionMode direction = DirectionMode.TeamBasedBack;
    [Tooltip("맵 경계까지 최대 스캔 칸 수(0 이하면 무제한)")]
    public int maxScanCells = 0;
    [Tooltip("대시 연출 여부(미체크 시 순간이동)")]
    public bool dashAnimate = false;
    [Tooltip("대시 시간(초)")]
    public float dashDuration = 0.12f;
    [Tooltip("대시 포물선 높이(0이면 직선)")]
    public float dashArc = 0.0f;

    void OnEnable()
    {
        targetMode = SkillTargetMode.Unit; // 실수 방지 기본값
        power = 0f;                    // 피해 없음
        school = DamageSchool.Physical;
    }

    public override IEnumerable<Vector3Int> GetAreaCells(Vector3Int originCell, bool isOddRow)
    {
        yield return originCell;
    }

    public override IEnumerator ResolveOnUnit(BattleManager bm, BattleUnit caster, BattleUnit _ignored)
    {
        if (!bm || !caster) yield break;

        // 선택 즉시 MP 차감(해당 경로는 BM에서 별도 차감 안 하므로 여기서 처리)
        if (mpCost > 0 && !caster.TryConsumeMP(mpCost))
            yield break;

        var map = caster.CurrentMap;
        if (!map) yield break;

        // 1) 이동 방향(step)
        Vector3Int step = DecideStep(caster);

        // 2) 경계까지 직선 스캔(중간 장애물/유닛 무시)
        var start = caster.Cell;
        var endCandidate = ScanToEdge(map, start, step, maxScanCells);

        // 3) 종점이 막혀 있으면 뒤로 물러나며 착지 가능한 셀 찾기
        var landing = FindNearestFreeBackward(bm, map, endCandidate, step);

        // 4) 변화 없으면 종료
        if (landing == start) yield break;

        // 5) 이동(공격/점프 연출 없음)
        yield return MoveCasterTo(bm, caster, map, landing);
    }

    public override IEnumerator ResolveOnTile(BattleManager bm, Tilemap map, Vector3Int originCell, BattleUnit caster)
    {
        if (!bm || !caster) yield break;
        // 항상 자기 자신에게만 쓰므로 캐스터 대상으로 실행
        yield return ResolveOnUnit(bm, caster, caster);
    }

    // ----------------- helpers -----------------

    Vector3Int DecideStep(BattleUnit caster)
    {
        switch (direction)
        {
            case DirectionMode.TeamBasedBack:
                return (caster.team == Team.Player) ? new Vector3Int(-1, 0, 0) : new Vector3Int(+1, 0, 0);

            case DirectionMode.TeamBasedFront:
                return (caster.team == Team.Player) ? new Vector3Int(+1, 0, 0) : new Vector3Int(-1, 0, 0);

            case DirectionMode.AbsoluteNegativeX:
                return new Vector3Int(-1, 0, 0);

            case DirectionMode.AbsolutePositiveX:
                return new Vector3Int(+1, 0, 0);

            default:
                return new Vector3Int(-1, 0, 0);
        }
    }

    Vector3Int ScanToEdge(Tilemap map, Vector3Int start, Vector3Int step, int maxCells)
    {
        var cur = start;
        int guard = 0;

        while (true)
        {
            if (maxCells > 0 && guard >= maxCells) break;
            guard++;

            var next = new Vector3Int(cur.x + step.x, cur.y + step.y, cur.z);

            // 맵/타일 유효성 검사
            if (!IsCellWithin(map, next)) break;
            if (!HasTile(map, next)) break;

            cur = next; // 장애물/유닛은 중간에 있어도 '통과'하므로 무시
        }

        return cur; // 경계 직전 또는 제한치 직전
    }

    Vector3Int FindNearestFreeBackward(BattleManager bm, Tilemap map, Vector3Int endCandidate, Vector3Int step)
    {
        var cur = endCandidate;
        var back = new Vector3Int(-step.x, -step.y, -step.z);

        while (true)
        {
            if (IsLandingFree(bm, map, cur)) return cur;

            var prev = new Vector3Int(cur.x + back.x, cur.y + back.y, cur.z + back.z);
            if (!IsCellWithin(map, prev) || !HasTile(map, prev))
                return cur; // 더 물러설 수 없으면 종점 그대로(시작 셀까지 막혀있을 수 있음)

            cur = prev;
        }
    }

    bool IsLandingFree(BattleManager bm, Tilemap map, Vector3Int cell)
    {
        if (!HasTile(map, cell)) return false;

        // 한 셀만 영역으로 만들어 BattleManager의 유닛 조회 유틸 사용
        var units = bm.GetUnitsInArea(map, new[] { cell });
        foreach (var u in units)
        {
            if (u != null && !u.IsDead && u.Cell == cell)
                return false; // 점유 중
        }
        return true;
    }

    bool IsCellWithin(Tilemap map, Vector3Int cell) => map.cellBounds.Contains(cell);
    bool HasTile(Tilemap map, Vector3Int cell) => map.GetTile(cell) != null;

    IEnumerator MoveCasterTo(BattleManager bm, BattleUnit caster, Tilemap map, Vector3Int landing)
    {
        var endW = map.GetCellCenterWorld(landing);

        if (dashAnimate)
        {
            // ← 스킬이 직접 시간(dashDuration)과 아크(dashArc)를 컨트롤
            var startW = caster.transform.position;
            float t = 0f;

            while (t < dashDuration)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / Mathf.Max(0.0001f, dashDuration));

                Vector3 pos = Vector3.Lerp(startW, endW, u);
                if (dashArc > 0f)
                {
                    float h = Mathf.Sin(u * Mathf.PI) * dashArc;
                    pos += Vector3.up * h;
                }

                caster.transform.position = pos; // 중간 프레임은 위치만 이동(셀은 유지)
                yield return null;
            }
        }

        // 최종 스냅: 위치/셀 동기화는 엔진 메서드로 처리(셀 setter 비공개 보호)
        caster.MoveTo(map, landing);
        yield return null;

    }
}
