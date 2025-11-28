using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "Battle/Skills/Common/Parametric Damage", fileName = "ParametricDamageSkill")]
public class ParametricDamageSkill : SkillAsset
{
    public enum AreaPreset 
    { 
        Single, //단일 대상
        Ring, //원형(중앙 포함 7칸)
        LineDiagU3, //세로(3칸)
        LineHorizontal, //(가로 3칸)
        LineDiagU7 //(1시 7시 방향 대각선 7칸)
    }

    // 6개 축 방향(표준 축좌표 단위벡터)
    static readonly Vector2Int[] AX_DIRS = new[]{
    new Vector2Int( 1,  0), // E
    new Vector2Int( 1, -1), // NE
    new Vector2Int( 0, -1), // NW
    new Vector2Int(-1,  0), // W
    new Vector2Int(-1,  1), // SW
    new Vector2Int( 0,  1), // SE
};

    [Header("Targeting")]
    public TargetPriorityMode priorityMode = TargetPriorityMode.RandomSurvivor;
    public StatusId preferredStatus = StatusId.Slow; // 우선 상태(예: Slow)
    public AreaPreset areaPreset = AreaPreset.Single;

    [Header("Player Targeting")]
    public bool useProvidedUnitTarget = true;   // 플레이어 스킬: 클릭한 대상 사용

    [System.Serializable]
    public struct ConditionalMultiplier
    {
        public StatusId status;
        public float multiplier; // 예: Slow면 3.0
    }
    [Header("Damage")]
    public float powerOverride = 1f;             // 기본 배수 덮어쓰기(옵션)
    public DamageSchool damageSchool = DamageSchool.Physical;
    public List<ConditionalMultiplier> conditionalMultipliers = new();

    [Header("Mode")]
    public SkillTargetMode selectionMode = SkillTargetMode.Unit; // 인스펙터에서 Unit/Tile 선택
    [Header("Diagonal Options")]
    [SerializeField] private bool diagUseNEAxis = true; //방향 변경

#if UNITY_EDITOR
    void OnValidate() { targetMode = selectionMode; }  // 선택값 반영
#endif

    public static void ClearFrontlineCache() => _frontlineCache.Clear();

    [Header("Training Effects")]
    [Header("범위 변경")]
    [Tooltip("훈련으로 범위 변경을 적용할 것인지")]
    public bool trainingUseAreaOverride = false;
    [Tooltip("범위 프리셋 교체를 활성화시키는 훈련 루트 인덱스 (-1이면 비활성)")]
    [Range(-1, 2)]
    public int routeForAreaOverride = -1;
    public AreaPreset trainingAreaPreset = AreaPreset.Single;
    public bool trainingDiagUseNEAxis = true;


    [Header("제압 부여 설정")]
    [Tooltip("제압을 활성화시키는 훈련 루트 인덱스 (-1이면 비활성)")]
    [Range(-1, 2)]
    public int routeForSuppression = -1;
    [Tooltip("캐스팅 제압 추가 감소량(피격 시)")]
    public int trainingSuppressionOnHit = 0; // 0=미사용, 1 이상이면 그만큼 추가로 suppressCur 감소

    [Header("출혈 부여 설정")]
    [Tooltip("훈련으로 출혈 효과를 적용할 것인지")]
    public bool trainingApplyBleed = false;
    [Tooltip("출혈을 활성화시키는 훈련 루트 인덱스 (-1이면 비활성)")]
    [Range(-1, 2)]
    public int routeForBleed = -1;
    [Range(1, DebuffTuning.MaxStacks)] public int trainingBleedStacks = 1;
    [Min(1)] public int trainingBleedDurationTurns = 2;

    [Header("방어 중첩 버프")]
    [Tooltip("특정 훈련 루트에서 시전자에게 방어 중첩 상태를 부여할지 여부")]
    public bool trainingApplyDefenseStacks = false;
    [Tooltip("방어 중첩 상태를 적용할 훈련 루트(-1이면 미사용, 0~2)")]
    [Range(-1, 2)] public int routeForDefenseStacks = -1;
    [Tooltip("부여할 StatusId (StackableStatusVisualDB에 아이콘/이름 연결 가능)")]
    public StatusId trainingDefenseStatusId = StatusId.None;
    [Tooltip("한 번에 부여할 방어 중첩 수")]
    [Min(1)] public int trainingDefenseStacks = 1;
    [Tooltip("방어 중첩 지속 턴")]
    [Min(1)] public int trainingDefenseDurationTurns = 3;

    [Header("턴수 변화 설정")]
    [Tooltip("쿨다운 변화를 활성화시키는 훈련 루트 인덱스 (-1이면 비활성)")]
    [Range(-1, 2)]
    public int routeForCooldown = -1;
    [Tooltip("쿨다운 턴수 변화량(음수면 단축)")]
    public int trainingCooldownDelta = 0;

    [Header("넉백 설정")]
    [Tooltip("훈련으로 넉백 효과를 사용할 것인지")]
    public bool trainingUseKnockback = false;
    [Tooltip("넉백 효과가 활성화될 훈련 루트 인덱스 (-1이면 비활성)")]
    [Range(-1, 2)]
    public int routeForKnockback = -1;

    [Header("추가 이동 설정")]
    [Tooltip("훈련으로 '기술 사용 후 1칸 이동' 효과를 사용할 것인지")]
    public bool trainingUsePostMove = false;
    [Tooltip("추가 이동 효과가 활성화될 훈련 루트 인덱스 (-1이면 비활성, 0~2)")]
    [Range(-1, 2)]
    public int routeForPostMove = -1;
    [Tooltip("기술 사용 후 이동할 수 있는 최대 칸 수(헥사 거리)")]
    [Min(1)] public int trainingPostMoveRange = 1;

    [Header("전체 공격 설정")]
    [Tooltip("이 효과(전체 적군 타격)를 활성화시키는 훈련 루트 인덱스 (-1이면 비활성)")]
    [Range(-1, 2)]
    public int routeForHitAllEnemies = -1;
    [Tooltip("이 스킬을 맵 상의 모든 적군에게 적중시키기")]
    public bool trainingHitAllEnemies = false;

    [Header("Frontline Bonus(전방 보너스)")]
    [SerializeField] private bool useFrontlineBonus = false;   // 전방 보너스 사용 여부
    [SerializeField] private int frontlineDepth = 2;            // "앞 N열"
    [SerializeField] private float frontlineMultiplier = 1.5f;  // 배수(예: 1.5)
    [SerializeField] private bool useManualFrontier = true;

    // 1열 수동 경계
    [SerializeField] private List<Vector3Int> manualFrontierPlayer;
    [SerializeField] private List<Vector3Int> manualFrontierEnemy;

    // 2열 수동 지정(있으면 자동 확장 대신 이걸 우선 사용)
    [SerializeField] private List<Vector3Int> manualSecondLayerPlayer;
    [SerializeField] private List<Vector3Int> manualSecondLayerEnemy;
    [SerializeField] private AxialDir playerFrontlineDir = AxialDir.SW; // 플레이어 전방축
    [SerializeField] private AxialDir enemyFrontlineDir = AxialDir.NE; // 적 전방축

    // 선택된 훈련 루트를 읽어 현재 실행에 반영
    int GetRoute(BattleUnit _caster)
    {
        if (_caster == null) return -1;
        return _caster.GetTrainingRouteIndex(this);
    }

    void OnEnable()
    {
        school = damageSchool;
        if (powerOverride > 0f) power = powerOverride;
        targetMode = selectionMode;  // 실행 시에도 선택값 반영
    }

    public override IEnumerable<Vector3Int> GetAreaCells(Vector3Int _originCell, bool _isOddColumn)
    {
        // 현재 턴의 시전자(플레이어든 적이든)
        var bm = Shared.BattleManager;
        BattleUnit caster = bm != null ? bm.ActingUnit : null;

        int route = GetRoute(caster);
        bool useOverride = trainingUseAreaOverride
                   && routeForAreaOverride >= 0
                   && route == routeForAreaOverride;

        // 기본/훈련 프리셋 선택
        var preset = useOverride ? trainingAreaPreset : areaPreset;
        bool useDiag = useOverride ? trainingDiagUseNEAxis : diagUseNEAxis;

        foreach (var c in AreaShapes.GetCells(_originCell, preset, useDiag))
            yield return c;
    }

    BattleUnit PickPrimaryTarget()
    {
        var players = Object.FindObjectsOfType<BattleUnit>()
            .Where(u => u && u.team == Team.Player && !u.IsDead).ToList();
        if (players.Count == 0) return null;

        switch (priorityMode)
        {
            case TargetPriorityMode.HighestHostility:
                return PickTargetByWeightedHostility(players);

            case TargetPriorityMode.PreferredStatusThenHighestHostility:
                return PickPreferredStatusThenHighestHostility(players, preferredStatus);

            default:
                return players[Random.Range(0, players.Count)];
        }
    }

    float GetMultiplierFor(BattleUnit _victim)
    {
        if (conditionalMultipliers == null || conditionalMultipliers.Count == 0) return 1f;
        var sc = _victim ? _victim.GetComponent<StatusController>() : null;
        if (sc == null) return 1f;

        float mult = 1f;
        foreach (var c in conditionalMultipliers)
            if (sc.Has(c.status)) mult *= c.multiplier; // 여러 조건 중첩 시 곱연산
        return mult;
    }

    
    public enum AxialDir { E, NE, NW, W, SW, SE }
    Vector2Int DirToAx(AxialDir d) => AX_DIRS[(int)d];
    // 맵별/팀별/깊이별 캐시
    static readonly Dictionary<(Tilemap, int, int), HashSet<Vector3Int>> _frontlineCache
        = new Dictionary<(Tilemap, int, int), HashSet<Vector3Int>>();

    HashSet<Vector3Int> GetFrontlineSet(Tilemap _map, Team _team, int _depth)
    {
        if (!_map || _depth <= 0) return null;
        var key = (_map, (int)_team, _depth);
        if (_frontlineCache.TryGetValue(key, out var cached)) return cached;

        // 맵 존재 타일 수집
        var b = _map.cellBounds;
        var all = new HashSet<Vector3Int>();
        for (int y = b.yMin; y < b.yMax; y++)
            for (int x = b.xMin; x < b.xMax; x++)
            { var c = new Vector3Int(x, y, 0); if (_map.HasTile(c)) all.Add(c); }

        // 전방축 f (이미 수동축 SW/NE를 쓰고 계신다면 그대로)
        Vector2Int f = (_team == Team.Player) ? DirToAx(playerFrontlineDir)
                                             : DirToAx(enemyFrontlineDir);

        // 1) 1열: 수동 경계 우선, 없으면 자동 frontier
        var frontier = new HashSet<Vector3Int>();
        var srcFront = (_team == Team.Player) ? manualFrontierPlayer : manualFrontierEnemy;
        if (useManualFrontier && srcFront != null && srcFront.Count > 0)
        {
            foreach (var c in srcFront) if (_map.HasTile(c)) frontier.Add(c);
        }
        else
        {
            // 자동 frontier: f로 한 칸 나가면 타일 없음
            foreach (var c in all)
            {
                var ax = SkillLibrary.OffsetToAxial(c);
                var axF = new Vector2Int(ax.x + f.x, ax.y + f.y);
                var offF = SkillLibrary.AxialToOffset(axF);
                if (!_map.HasTile(offF)) frontier.Add(c);
            }
        }

        // 최종 결과에 1열 추가
        var result = new HashSet<Vector3Int>(frontier);

        if (_depth >= 2)
        {
            // 2) 2열: 수동 2열이 있으면 우선 사용
            var secondManual = (_team == Team.Player) ? manualSecondLayerPlayer : manualSecondLayerEnemy;
            if (secondManual != null && secondManual.Count > 0)
            {
                foreach (var c in secondManual) if (_map.HasTile(c)) result.Add(c);
            }
            else
            {
                // 없으면 기존처럼 -f로 1열에서 한 칸 안쪽 확장
                var layer = new HashSet<Vector3Int>(frontier);
                var next = new HashSet<Vector3Int>();
                foreach (var c in layer)
                {
                    var ax = SkillLibrary.OffsetToAxial(c);
                    var axBk = new Vector2Int(ax.x - f.x, ax.y - f.y); // -f
                    var offBk = SkillLibrary.AxialToOffset(axBk);
                    if (_map.HasTile(offBk)) next.Add(offBk);
                }
                foreach (var n in next) result.Add(n);
            }
        }

        _frontlineCache[key] = result;
        return result;
    }

    public List<Vector3Int> GetKnockbackCandidates(BattleManager bm, BattleUnit caster, BattleUnit target)
    {
        var result = new List<Vector3Int>();
        if (!bm || !caster || !target) return result;

        var map = target.CurrentMap;
        if (!map) return result;

        var start = target.Cell;

        // caster -> target 방향 (넉백은 "caster에서 target으로"의 연장선 방향)
        Vector3 casterW = map.GetCellCenterWorld(caster.Cell);
        Vector3 targetW = map.GetCellCenterWorld(start);
        Vector2 awayDir = (Vector2)(targetW - casterW);
        if (awayDir.sqrMagnitude < 1e-6f) return result;
        awayDir.Normalize();

        bool oddCol = SkillLibrary.IsOddColumn(start);

        // BattleManager.TryGetFrontCellOfTarget에서 쓰는 이웃 오프셋 그대로 복붙 
        Vector3Int[] neighOffsetsEven = {
        new Vector3Int(+1, 0, 0), new Vector3Int( 0,+1,0),
        new Vector3Int(-1,+1, 0), new Vector3Int(-1, 0,0),
        new Vector3Int(-1,-1, 0), new Vector3Int( 0,-1,0)
    };
        Vector3Int[] neighOffsetsOdd = {
        new Vector3Int(+1, 0, 0), new Vector3Int(+1,+1,0),
        new Vector3Int( 0,+1, 0), new Vector3Int(-1, 0,0),
        new Vector3Int( 0,-1, 0), new Vector3Int(+1,-1,0)
    };

        var neighs = oddCol ? neighOffsetsOdd : neighOffsetsEven;
        var scored = new List<(float score, Vector3Int cell)>();

        foreach (var off in neighs)
        {
            var cand = new Vector3Int(start.x + off.x, start.y + off.y, start.z);
            if (!map.HasTile(cand)) continue;

            // target -> candidate 방향
            Vector3 candW = map.GetCellCenterWorld(cand);
            Vector2 dir = (Vector2)(candW - targetW);
            if (dir.sqrMagnitude < 1e-6f) continue;
            dir.Normalize();

            // awayDir(캐스터에서 타겟쪽)과 가장 비슷한(=dot가 큰) 이웃 2개 선택
            float dot = Vector2.Dot(awayDir, dir);
            scored.Add((dot, cand));
        }

        // dot 큰 순으로 정렬
        scored.Sort((a, b) => b.score.CompareTo(a.score));

        // === 상위 2개만 후보 슬롯으로 고정 ===
        int maxSlots = Mathf.Min(2, scored.Count);
        for (int i = 0; i < maxSlots; i++)
        {
            var cell = scored[i].cell;

            // 타일 없으면 이 슬롯은 그냥 스킵 (대체 방향 X)
            if (!map.HasTile(cell))
                continue;

            // 점유 여부 체크
            var units = bm.GetUnitsInArea(map, new[] { cell });
            bool occupied = false;
            foreach (var u in units)
            {
                if (u != null && !u.IsDead && u.Cell == cell)
                {
                    occupied = true;
                    break;
                }
            }

            if (!occupied)
                result.Add(cell);
        }

        // result에는 0~2개의 셀만 들어감 (대체 방향 없음)
        return result;
    }

    bool IsInFrontline(BattleUnit _battleunit, int _depth)
    {
        if (!_battleunit || !_battleunit.CurrentMap) return false;
        var set = GetFrontlineSet(_battleunit.CurrentMap, _battleunit.team, _depth);
        return set != null && set.Contains(_battleunit.Cell);
    }

    // 중심 셀 기준으로 범위 유닛들을 찾아 데미지 적용(팀 반대편만)
    void DealAreaDamage(BattleManager _bm, BattleUnit _caster, Tilemap _map, Vector3Int _centerCell)
    {
        if (!_bm || !_caster) return;

        int route = GetRoute(_caster);

        //전체 적군 공격 (이 스킬에서 trainingHitAllEnemiesOnRoute0를 켠 경우)
        if (trainingHitAllEnemies && routeForHitAllEnemies >= 0 && route == routeForHitAllEnemies)
        {
            var allUnits = Object.FindObjectsOfType<BattleUnit>();
            var victims = allUnits
                .Where(u => u != null
                            && !u.IsDead
                            && u.team != _caster.team
                            && u.CurrentMap == _map)
                .ToList();

            _bm.ExecuteSkillDamage(_caster, victims, this, _map, _centerCell);
            return;
        }

        // 기본/다른 훈련 루트: 기존 범위 로직 사용
        var area = GetAreaCells(_centerCell, SkillLibrary.IsOddColumn(_centerCell));
        var areaVictims = _bm.GetUnitsInArea(_map, area)
                            .Where(u => u != null && !u.IsDead && u.team != _caster.team)
                            .ToList();

        // 방어 중첩 훈련 처리 (시전자에게 부여)
        if (trainingApplyDefenseStacks &&
            routeForDefenseStacks >= 0 &&
            route == routeForDefenseStacks &&
            trainingDefenseStatusId != StatusId.None)
        {
            var sc = _caster.GetComponent<StatusController>();
            if (sc != null)
            {
                sc.ApplyWithTurnContext(
                    trainingDefenseStatusId,
                    Mathf.Max(1, trainingDefenseStacks),
                    Mathf.Max(1, trainingDefenseDurationTurns)
                );
            }
        }

        _bm.ExecuteSkillDamage(_caster, areaVictims, this, _map, _centerCell);
    }

    public override int ComputeDamage(BattleUnit _caster, BattleUnit _target, in SkillRuntime _skillruntime)
    {
        // 기본 산식(속성/저항 포함)은 부모 호출
        int baseDmg = base.ComputeDamage(_caster, _target, _skillruntime);

        // 추가 배수: 상태 기반
        float mult = GetMultiplierFor(_target);

        // 추가 배수: 전방 보너스
        if (useFrontlineBonus && _caster != null && IsInFrontline(_caster, frontlineDepth))
            mult *= Mathf.Max(0f, frontlineMultiplier);

        // 최종
        int dmg = Mathf.Max(0, Mathf.FloorToInt(baseDmg * mult));
        return dmg;
    }

    public override IEnumerator ResolveOnUnit(BattleManager _bm, BattleUnit _caster, BattleUnit _target)
    {
        if (!_bm || !_caster) yield break;

        BattleUnit primary = (useProvidedUnitTarget && _target != null && !_target.IsDead)
                                ? _target
                                : PickPrimaryTarget();

        if (primary == null) yield break;

        int route = GetRoute(_caster);
        Debug.Log($"[Training] {name} by {_caster.name} route={route}, useAreaOverride={trainingUseAreaOverride}");

        // 범위 계산은 GetAreaCells 안에서 route를 보고 알아서 처리
        DealAreaDamage(_bm, _caster, primary.CurrentMap, primary.Cell);
        yield break;
    }
    public override IEnumerator ResolveOnTile(BattleManager _bm, Tilemap _map, Vector3Int _originCell, BattleUnit _caster)
    {
        if (!_bm || !_caster || !_map) yield break;

        int route = GetRoute(_caster);
        Debug.Log($"[Training] (Tile) {name} by {_caster.name} route={route}, useAreaOverride={trainingUseAreaOverride}");

        DealAreaDamage(_bm, _caster, _map, _originCell);
        yield break;
    }

    public override int GetSuppressionOnHit(BattleUnit _caster)
    {
        int route = GetRoute(_caster);
        if (trainingSuppressionOnHit <= 0) return 0;
        if (routeForSuppression < 0) return 0;
        return (route == routeForSuppression) ? Mathf.Max(0, trainingSuppressionOnHit) : 0;
    }

    public override int GetEffectiveCooldownTurns(BattleUnit _caster)
    {
        int cd = cooldownTurns;

        int route = GetRoute(_caster);
        if (trainingCooldownDelta != 0 && routeForCooldown >= 0 && route == routeForCooldown)
        {
            cd = Mathf.Max(0, cd + trainingCooldownDelta);
        }

        return cd;
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
