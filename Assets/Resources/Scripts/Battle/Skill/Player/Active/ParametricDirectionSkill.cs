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

    [Header("Animation")]
    [Tooltip("앞으로 이동할 때 쓸 대시 트리거 이름")]
    public string forwardDashTrigger = "DashForward";

    [Tooltip("뒤로 이동(백스텝)할 때 쓸 대시 트리거 이름")]
    public string backwardDashTrigger = "DashBack";

    [Tooltip("앞/뒤 방향에 따라 다른 트리거를 쓸지 여부")]
    public bool useDirectionalDashTrigger = true;

    [Header("Training")]
    [Header("소모값 감소 적용")]
    [Tooltip("훈련에서 자원 비용을 덮어쓸지 여부")]
    public bool trainingUseCostOverride = false;
    [Range(-1, 2)]
    [Tooltip("이 스킬에서 자원 감소가 적용될 훈련 루트 인덱스 (-1이면 미사용)")]
    public int routeForCostOverride = 0;
    [Tooltip("훈련 시 실제 소모 자원")]
    public int trainingCostRoute = 5;

    [Header("적의 감소 적용")]
    [Tooltip("현재 적대감에서 이 값만큼 즉시 감소 (양수 입력)")]
    public float trainingHostilityDeltaRoute = 0.3f;

    [Header("연속 행동 적용")]
    [Tooltip("이 스킬 사용 후 턴을 마치지 않음")]
    public bool trainingFreeActionOnRoute = false;

    void OnEnable()
    {
        targetMode = SkillTargetMode.Tile; // 실수 방지 기본값
        power = 0f;                    // 피해 없음
        school = DamageSchool.Physical;
        costResource = SkillCostResource.Rage;
    }
    // 커스텀 프리뷰 & 타겟 맵 제공
    public IEnumerable<Vector3Int> GetPreviewCells(BattleManager _battlemanager, BattleUnit _caster)
        => (_battlemanager && _caster && _caster.CurrentMap) ? ComputeLandingCandidates(_battlemanager, _caster) : System.Array.Empty<Vector3Int>();
    public override IEnumerable<Vector3Int> GetAreaCells(Vector3Int _originCell, bool _isOddRow)
    {
        yield break;
    }
    public Tilemap GetTargetMap(BattleManager _battlemanager, BattleUnit _caster)
       => _caster ? _caster.CurrentMap : null;

    public override IEnumerator ResolveOnUnit(BattleManager _battlemanager, BattleUnit _caster, BattleUnit _none)
    {
        if (!_battlemanager || !_caster || !_caster.CurrentMap) yield break;

        // 비용 체크
        var res = GetCostResource(_caster);
        int cost = GetEffectiveCost(_caster);
        if (cost > 0 && !_caster.TryConsumeResource(res, cost)) yield break;

        var candidates = ComputeLandingCandidates(_battlemanager, _caster).ToList();
        if (candidates.Count == 0) yield break;
        // 기본은 첫 후보를 사용(원하면 UI에서 먼저 선택하게 해야 함)
        yield return MoveCasterTo(_battlemanager, _caster, _caster.CurrentMap, candidates[0]);
    }

    // 클릭된 타일이 후보 중 하나일 때만 이동 실행
    public override IEnumerator ResolveOnTile(BattleManager _battlemanager, Tilemap _map, Vector3Int _originCell, BattleUnit _caster)
    {
        if (!_battlemanager || !_caster || !_map) yield break;

        // 비용 체크(확정 시 차감)
        var res = GetCostResource(_caster);
        int cost = GetEffectiveCost(_caster);
        if (cost > 0 && !_caster.TryConsumeResource(res, cost)) yield break;

        var valids = new HashSet<Vector3Int>(ComputeLandingCandidates(_battlemanager, _caster));
        if (!valids.Contains(_originCell))
            yield break; // 미리보기 밖 클릭 무시

        // Route 1: Hostility 즉시 감소
        int route = _caster.GetTrainingRouteIndex(this);
        if (route == 1 && trainingHostilityDeltaRoute > 0f)
        {
            float factor = Mathf.Clamp01(trainingHostilityDeltaRoute);   // 보통 0.3
            float current = _caster.Hostility;
            float delta = current * factor;                               // 현재 적의의 30%

            if (delta > 0f)
            {
                _caster.AddHostility(-delta);
                Debug.Log($"[Training-Dir] {_caster.name} Hostility -{delta} (factor={factor}) → {_caster.Hostility}");
            }
        }

        yield return MoveCasterTo(_battlemanager, _caster, _map, _originCell);
    }

    // 핵심 로직: 착지 후보 계산
    IEnumerable<Vector3Int> ComputeLandingCandidates(BattleManager _battlemanager, BattleUnit _caster)
    {
        var map = _caster.CurrentMap;
        var start = _caster.Cell;

        var labels = GetDirectionsFor(_caster.team, start, direction, backMode);
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
                if(IsLandingFree(_battlemanager,map,cell))
                {
                    landing = cell;
                    break;
                }
            }

            // 착지 불가능하거나, 제자리라면 후보에서 제외
            if (landing == start)
                continue;

            results.Add(landing);
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
    List<DirLabel> GetDirectionsFor(Team _team, Vector3Int _start, DirectionMode _dirmode, BackMode _backmode)
    {
        // 팀 기준 전/후 쌍 (라벨)
        DirLabel backA, backB, frontA, frontB;
        if (_team == Team.Player) { backA = DirLabel.W; backB = DirLabel.SW; frontA = DirLabel.E; frontB = DirLabel.NE; }
        else { backA = DirLabel.E; backB = DirLabel.NE; frontA = DirLabel.W; frontB = DirLabel.SW; }

        if (_dirmode == DirectionMode.AbsoluteNegativeX) return new() { DirLabel.W };
        if (_dirmode == DirectionMode.AbsolutePositiveX) return new() { DirLabel.E };

        bool useFront = (_dirmode == DirectionMode.TeamBasedFront);
        var a = useFront ? frontA : backA;
        var b = useFront ? frontB : backB;

        return _backmode switch
        {
            BackMode.W_Only => new() { a },
            BackMode.SW_Only => new() { b },
            BackMode.W_or_SW => new() { a, b },
            // (혹시 E_or_NE를 쓰신다면 여기에 추가)
            _ => new() { a, b }
        };
    }
    Vector3Int GetStepAt(Vector3Int _cell, DirLabel _label)
    {
        bool odd = SkillLibrary.IsOddColumn(_cell);
        switch (_label)
        {
            case DirLabel.W: return new Vector3Int(-1, 0, 0);
            case DirLabel.E: return new Vector3Int(1, 0, 0);
            case DirLabel.SW: return odd ? new Vector3Int(0, -1, 0) : new Vector3Int(-1, -1, 0);
            case DirLabel.NE: return odd ? new Vector3Int(+1, +1, 0) : new Vector3Int(0, +1, 0);
        }
        return Vector3Int.zero;
    }
    DirLabel Opposite(DirLabel _dir)
    {
        switch (_dir)
        {
            case DirLabel.W: return DirLabel.E;
            case DirLabel.E: return DirLabel.W;
            case DirLabel.SW: return DirLabel.NE;
            case DirLabel.NE: return DirLabel.SW;
        }
        return _dir;
    }

    bool IsLandingFree(BattleManager _battlemanager, Tilemap _map, Vector3Int _cell)
    {
        if (!HasTile(_map, _cell)) return false;

        // 한 셀만 영역으로 만들어 BattleManager의 유닛 조회 유틸 사용
        var units = _battlemanager.GetUnitsInArea(_map, new[] { _cell });
        foreach (var u in units)
        {
            if (u != null && !u.IsDead && u.Cell == _cell)
                return false; // 점유 중
        }
        return true;
    }
    bool IsBackMove(Team _team, Vector3Int _start, Vector3Int _dest)
    {
        var backLabels = GetDirectionsFor(_team, _start, DirectionMode.TeamBasedBack, backMode);

        foreach (var label in backLabels)
        {
            var step = GetStepAt(_start, label);                 // 홀짝 컬럼 반영
            var expected = new Vector3Int(_start.x + step.x, _start.y + step.y, _start.z);
            if (expected == _dest)
                return true;
        }

        // 1칸이 아닌 이동(여러 칸) 대비 fallback (기존 로직)
        if (_team == Team.Player) return _dest.x < _start.x;
        else return _dest.x > _start.x;
    }

    bool IsCellWithin(Tilemap _map, Vector3Int _cell) => _map.cellBounds.Contains(_cell);
    bool HasTile(Tilemap _map, Vector3Int _cell) => _map.GetTile(_cell) != null;

    IEnumerator MoveCasterTo(BattleManager _battlemanager, BattleUnit _caster, Tilemap _map, Vector3Int _landing)
    {
        if (!_caster || !_map)
            yield break;

        var endW = _map.GetCellCenterWorld(_landing);
        var startCell = _caster.Cell;

        //이동 시 현재 칸 점유 해제
        if (_battlemanager != null && _battlemanager.grid != null)
            _battlemanager.grid.SetOccupied(_caster.team, startCell, false);

        if (dashAnimate)
        {
            // 1) Animator 트리거
            var anim = _caster.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                string trigger = null;

                if (useDirectionalDashTrigger)
                {
                    bool isBack = IsBackMove(_caster.team, startCell, _landing);
                    trigger = isBack ? backwardDashTrigger : forwardDashTrigger;
                }
                else
                {
                    trigger = forwardDashTrigger;
                }

                if (!string.IsNullOrEmpty(trigger))
                    anim.SetTrigger(trigger);
            }

            // 2) 위치 보간(대시 모션)
            var startW = _caster.transform.position;
            float dur = Mathf.Max(0.0001f, dashDuration);
            float t = 0f;

            while (t < dur)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / dur);

                Vector3 pos = Vector3.Lerp(startW, endW, u);

                if (dashArc > 0f)
                {
                    float h = Mathf.Sin(u * Mathf.PI) * dashArc;
                    pos += Vector3.up * h;
                }

                _caster.transform.position = pos;
                yield return null;
            }
        }

        _caster.MoveTo(_map, _landing); // 최종 스냅(셀 setter 보호)

        // 이동 끝: 새 칸 점유 설정
        if (_battlemanager != null && _battlemanager.grid != null)
            _battlemanager.grid.SetOccupied(_caster.team, _caster.Cell, true);

        yield return null;
    }

    public override int GetEffectiveCost(BattleUnit _caster)
    {
        int baseCost = base.GetEffectiveCost(_caster);
        if (!_caster) return baseCost;

        int route = _caster.GetTrainingRouteIndex(this);
        if (trainingUseCostOverride && route == 0)
            return Mathf.Max(0, trainingCostRoute);

        return baseCost;
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
