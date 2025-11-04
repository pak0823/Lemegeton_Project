using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "Battle/Skills/Common/ParametricDirectionSkill", fileName = "ParametricDirectionSkill")]
public class ParametricDirectionSkill : SkillAsset, ISkillCustomPreview, ITargetMapProvider, IInstantTileSkill
{
    public enum BackMode
    {
        W_Only,          // 플레이어 기준 W로만
        SW_Only,         // 플레이어 기준 SW로만
        W_or_SW          // 플레이어 기준 W 또는 SW (적은 E 또는 NE)
    }
    // 선택 즉시 자기 자신에게 발동(타겟팅 불필요)
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
    public BackMode backMode = BackMode.W_or_SW;
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
        targetMode = SkillTargetMode.Tile; // 실수 방지 기본값
        power = 0f;                    // 피해 없음
        school = DamageSchool.Physical;
    }
    // 커스텀 프리뷰 & 타겟 맵 제공
    public IEnumerable<Vector3Int> GetPreviewCells(BattleManager bm, BattleUnit caster)
        => (bm && caster && caster.CurrentMap) ? ComputeLandingCandidates(bm, caster) : System.Array.Empty<Vector3Int>();
    public override IEnumerable<Vector3Int> GetAreaCells(Vector3Int originCell, bool isOddRow)
    {
        yield break;
    }
    public Tilemap GetTargetMap(BattleManager bm, BattleUnit caster)
       => caster ? caster.CurrentMap : null;

    public override IEnumerator ResolveOnUnit(BattleManager bm, BattleUnit caster, BattleUnit _)
    {
        if (!bm || !caster || !caster.CurrentMap) yield break;
        var candidates = ComputeLandingCandidates(bm, caster).ToList();
        if (candidates.Count == 0) yield break;
        // 기본은 첫 후보를 사용(원하면 UI에서 먼저 선택하게 해야 함)
        yield return MoveCasterTo(bm, caster, caster.CurrentMap, candidates[0]);
    }

    // 클릭된 타일이 후보 중 하나일 때만 이동 실행
    public override IEnumerator ResolveOnTile(BattleManager bm, Tilemap map, Vector3Int originCell, BattleUnit caster)
    {
        if (!bm || !caster || !map) yield break;

        // 비용 체크(확정 시 차감)
        if (mpCost > 0 && !caster.TryConsumeMP(mpCost))
            yield break;

        var valids = new HashSet<Vector3Int>(ComputeLandingCandidates(bm, caster));
        if (!valids.Contains(originCell))
            yield break; // 미리보기 밖 클릭 무시

        yield return MoveCasterTo(bm, caster, map, originCell);
    }

    // 핵심 로직: 착지 후보 계산
    IEnumerable<Vector3Int> ComputeLandingCandidates(BattleManager bm, BattleUnit caster)
    {
        var map = caster.CurrentMap;
        var start = caster.Cell;

        // 플레이어/적 별 “뒤” 방향 후보 집합
        var dirs = GetBackDirectionsFor(caster.team, start);
        var results = new HashSet<Vector3Int>();

        foreach (var step in dirs)
        {
            var endCandidate = ScanToEdge(map, start, step, maxScanCells);
            var landing = FindNearestFreeBackward(bm, map, endCandidate, step, start);

            if (landing == start || !IsLandingFree(bm, map, landing))
                continue;
            
            var delta = landing - start;
            int dot = delta.x * step.x + delta.y * step.y;
            if (dot <= 0) continue;   // step과 같은 방향 성분이 없으면 제외

            // 최종 통과한 후보만 추가
            results.Add(landing);
        }

        return results;
    }

    // ----------------- helpers -----------------
    // 플레이어는 W(-1,0)/SW(-1,1), 적은 E(+1,0)/NE(+1,-1)
    List<Vector3Int> GetBackDirectionsFor(Team t, Vector3Int start)
    {
        bool odd = SkillLibrary.IsOddColumn(start);

        // odd-q(세로 기준)에서 W는 고정, SW/NE는 홀짝에 따라 다름
        Vector3Int W = new(-1, 0, 0);
        Vector3Int SW = odd ? new Vector3Int(0, -1, 0) : new Vector3Int(-1, -1, 0);
        Vector3Int E = new(1, 0, 0);
        Vector3Int NE = odd ? new Vector3Int(+1, +1, 0) : new Vector3Int(0, +1, 0);

        if (t == Team.Player)
        {
            switch (backMode)
            {
                case BackMode.W_Only: return new() { W };
                case BackMode.SW_Only: return new() { SW };
                default: return new() { W, SW };
            }
        }
        else // Enemy
        {
            switch (backMode)
            {
                case BackMode.W_Only: return new() { E };
                case BackMode.SW_Only: return new() { NE };
                default: return new() { E, NE };
            }
        }
    }

    Vector3Int ScanToEdge(Tilemap map, Vector3Int start, Vector3Int step, int maxCells)
    {
        var cur = start; int guard = 0;
        while (true)
        {
            if (maxCells > 0 && guard >= maxCells) break;
            guard++;

            var next = new Vector3Int(cur.x + step.x, cur.y + step.y, cur.z);
            if (!IsCellWithin(map, next)) break;
            if (!HasTile(map, next)) break;

            cur = next; // 중간 장애물/유닛은 통과(무시)
        }
        return cur;
    }

    Vector3Int FindNearestFreeBackward(BattleManager bm, Tilemap map, Vector3Int endCandidate, Vector3Int step, Vector3Int start)
    {
        var cur = endCandidate;
        var back = new Vector3Int(-step.x, -step.y, -step.z);

        while (true)
        {
            if (IsLandingFree(bm, map, cur)) return cur;

            var prev = new Vector3Int(cur.x + back.x, cur.y + back.y, cur.z + back.z);

            // 맵 밖/타일 없음 → 더 이상 물러날 수 없음 = 착지 불가
            if (!IsCellWithin(map, prev) || !HasTile(map, prev))
                return start;

            // 출발점까지 왔는데도 착지 불가면 포기
            if (prev == start)
                return start;

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
                caster.transform.position = pos;
                yield return null;
            }
        }

        caster.MoveTo(map, landing); // 최종 스냅(셀 setter 보호)
        yield return null;
    }
}
