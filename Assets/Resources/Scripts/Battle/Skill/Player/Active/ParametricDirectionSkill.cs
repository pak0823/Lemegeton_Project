using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "Battle/Skills/Common/ParametricDirectionSkill", fileName = "ParametricDirectionSkill")]
public class ParametricDirectionSkill : SkillAsset, ISkillCustomPreview, ITargetMapProvider, IInstantTileSkill
{
    public enum BackMode
    {
        W_Only,          // 플레이어 기준 W로만
        SW_Only,         // 플레이어 기준 SW로만
        W_or_SW,         // 플레이어 기준 W 또는 SW (적은 E 또는 NE)
    }
    enum DirLabel { W, E, SW, NE }
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

    [Header("Training")]
    [Tooltip("Route 0에서 MP 비용을 덮어쓸지 여부")]
    public bool trainingUseMpOverride = false;

    [Tooltip("선택 시 실제 소모 MP")]
    public int trainingMpCostRoute0 = 5;

    [Tooltip("현재 적대감에서 이 값만큼 즉시 감소 (양수 입력)")]
    public float trainingHostilityDeltaRoute1 = 0.3f;

    [Tooltip("이 스킬 사용 후 턴을 마치지 않음")]
    public bool trainingFreeActionOnRoute2 = false;

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
        int cost = GetEffectiveMpCost(caster);
        if (cost > 0 && !caster.TryConsumeMP(cost))
            yield break;

        var valids = new HashSet<Vector3Int>(ComputeLandingCandidates(bm, caster));
        if (!valids.Contains(originCell))
            yield break; // 미리보기 밖 클릭 무시

        // Route 1: Hostility 즉시 감소
        int route = caster.GetTrainingRouteIndex(this);
        if (route == 1 && trainingHostilityDeltaRoute1 > 0f)
        {
            float factor = Mathf.Clamp01(trainingHostilityDeltaRoute1);   // 보통 0.3
            float current = caster.Hostility;
            float delta = current * factor;                               // 현재 적의의 30%

            if (delta > 0f)
            {
                caster.AddHostility(-delta);
                Debug.Log($"[Training-Dir] {caster.name} Hostility -{delta} (factor={factor}) → {caster.Hostility}");
            }
        }

        yield return MoveCasterTo(bm, caster, map, originCell);
    }

    // 핵심 로직: 착지 후보 계산
    IEnumerable<Vector3Int> ComputeLandingCandidates(BattleManager bm, BattleUnit caster)
    {
        var map = caster.CurrentMap;
        var start = caster.Cell;

        var labels = GetDirectionsFor(caster.team, start, direction, backMode);
        var results = new HashSet<Vector3Int>();

        foreach (var label in labels)
        {
            // start에서 label 방향으로 쭉 한 줄(ray)을 만든다.
            var ray = CollectRay(map, start, label, maxScanCells);
            if(ray.Count == 0)
                continue;

            // 가장 먼 칸부터 거꾸로 오면서 착지 가능한 칸을 찾는다.
            Vector3Int landing = start;
            for(int i=ray.Count - 1;i >=0;--i)
            {
                var cell = ray[i];
                if(IsLandingFree(bm,map,cell))
                {
                    landing = cell;
                    break;
                }
            }

            // 착지 불가능하거나, 제자리라면 후보에서 제외
            if (landing == start)
                continue;

            results.Add(landing);
            //var endCandidate = ScanToEdge(map, start, label, maxScanCells);
            //var landing = FindNearestFreeBackward(bm, map, endCandidate, label, start);

            //if (landing == start || !IsLandingFree(bm, map, landing))
            //    continue;

            //results.Add(landing);
        }
        return results;
    }

    List<Vector3Int> CollectRay(Tilemap _map, Vector3Int _start, DirLabel _label, int _maxcells)
    {
        //start에서 label 방향으로 최대 maxCells만큼 스캔해서 그 라인에 있는 칸들을 순서대로 반환한다.
        var list = new List<Vector3Int>();
        var cur = _start;
        int guard = 0;

        while(true)
        {
            if(_maxcells > 0 && guard >= _maxcells)
                break;
            guard++;

            var step = GetStepAt(cur, _label);
            var next = new Vector3Int(cur.x + step.x, cur.y + step.y, cur.z);

            if(!IsCellWithin(_map,next))
                break;
            if(!HasTile(_map,next))
                break;

            list.Add(next);
            cur = next;
        }

        return list;
    }

    // ----------------- helpers -----------------
    // 플레이어는 W(-1,0)/SW(-1,1), 적은 E(+1,0)/NE(+1,-1)
    List<DirLabel> GetDirectionsFor(Team t, Vector3Int start, DirectionMode dirMode, BackMode backMode)
    {
        // 팀 기준 전/후 쌍 (라벨)
        DirLabel backA, backB, frontA, frontB;
        if (t == Team.Player) { backA = DirLabel.W; backB = DirLabel.SW; frontA = DirLabel.E; frontB = DirLabel.NE; }
        else { backA = DirLabel.E; backB = DirLabel.NE; frontA = DirLabel.W; frontB = DirLabel.SW; }

        if (dirMode == DirectionMode.AbsoluteNegativeX) return new() { DirLabel.W };
        if (dirMode == DirectionMode.AbsolutePositiveX) return new() { DirLabel.E };

        bool useFront = (dirMode == DirectionMode.TeamBasedFront);
        var a = useFront ? frontA : backA;
        var b = useFront ? frontB : backB;

        return backMode switch
        {
            BackMode.W_Only => new() { a },
            BackMode.SW_Only => new() { b },
            BackMode.W_or_SW => new() { a, b },
            // (혹시 E_or_NE를 쓰신다면 여기에 추가)
            _ => new() { a, b }
        };
    }
    Vector3Int GetStepAt(Vector3Int cell, DirLabel label)
    {
        bool odd = SkillLibrary.IsOddColumn(cell);
        switch (label)
        {
            case DirLabel.W: return new Vector3Int(-1, 0, 0);
            case DirLabel.E: return new Vector3Int(1, 0, 0);
            case DirLabel.SW: return odd ? new Vector3Int(0, -1, 0) : new Vector3Int(-1, -1, 0);
            case DirLabel.NE: return odd ? new Vector3Int(+1, +1, 0) : new Vector3Int(0, +1, 0);
        }
        return Vector3Int.zero;
    }
    DirLabel Opposite(DirLabel l)
    {
        switch (l)
        {
            case DirLabel.W: return DirLabel.E;
            case DirLabel.E: return DirLabel.W;
            case DirLabel.SW: return DirLabel.NE;
            case DirLabel.NE: return DirLabel.SW;
        }
        return l;
    }

    // 라벨 기반 스캔
    //Vector3Int ScanToEdge(Tilemap map, Vector3Int start, DirLabel label, int maxCells)
    //{
    //    var cur = start; int guard = 0;
    //    while (true)
    //    {
    //        if (maxCells > 0 && guard >= maxCells) break;
    //        guard++;

    //        var step = GetStepAt(cur, label);             // ★ 현재 칸 기준으로 step 재계산
    //        var next = new Vector3Int(cur.x + step.x, cur.y + step.y, cur.z);
    //        if (!IsCellWithin(map, next)) break;
    //        if (!HasTile(map, next)) break;

    //        cur = next;
    //    }
    //    return cur;
    //}


    // 라벨 기반 역방향 탐색(착지 가능 지점 찾기)
    //Vector3Int FindNearestFreeBackward(BattleManager bm, Tilemap map, Vector3Int endCandidate, DirLabel label, Vector3Int start)
    //{
    //    var cur = endCandidate;
    //    var backLabel = Opposite(label);

    //    while (true)
    //    {
    //        if (IsLandingFree(bm, map, cur)) return cur;

    //        var backStep = GetStepAt(cur, backLabel);     // ★ 여기서도 동적 역스텝
    //        var prev = new Vector3Int(cur.x + backStep.x, cur.y + backStep.y, cur.z);

    //        if (!IsCellWithin(map, prev) || !HasTile(map, prev))
    //            return start;
    //        if (prev == start)
    //            return start;

    //        cur = prev;
    //    }
    //}

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

        //이동 시 현재 칸 점유 해제
        var startCell = caster.Cell;
        if (bm != null && bm.grid != null)
            bm.grid.SetOccupied(caster.team, startCell, false);

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

        // 이동 끝: 새 칸 점유 설정
        if (bm != null && bm.grid != null)
            bm.grid.SetOccupied(caster.team, caster.Cell, true);

        yield return null;
    }

    public override int GetEffectiveMpCost(BattleUnit caster)
    {
        int cost = mpCost;
        if (caster == null) return cost;

        int route = caster.GetTrainingRouteIndex(this);
        if (route == 0 && trainingUseMpOverride)
        {
            cost = Mathf.Max(0, trainingMpCostRoute0);
        }

        return cost;
    }
    public override string GetFullDescriptionRich(BattleUnit caster)
    {
        int cost = GetEffectiveMpCost(caster);

        if (!string.IsNullOrEmpty(description))
        {
            if (cost > 0)
            {
                string mpColor = "#00A2FF";
                return $"{description}<size=20%><color=#808080>(MP:<color={mpColor}>{cost}</color>)</color></size>";
            }
            else
            {
                return description;
            }
        }

        return base.GetFullDescriptionRich(caster);
    }

}
