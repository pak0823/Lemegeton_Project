using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

// 상태 이상 부여를 위한 구조체 정의
[System.Serializable]
public struct StatusEffectInfo
{
    public StatusId status;
    public int stack;
    [Tooltip("지속 턴 (StatusController가 지원하는 경우 사용)")]
    public int duration;
}

[CreateAssetMenu(menuName = "Battle/Skills/Common/Parametric Damage", fileName = "ParametricDamageSkill")]
public class ParametricDamageSkill : SkillAsset, IProjectileTileSkill
{
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

    [Header("On Hit Effects (확장 기능)")]
    [Tooltip("적중 시 대상에게 부여할 상태 목록 (예: 중독, 발화)")]
    public List<StatusEffectInfo> applyStatusOnHit = new List<StatusEffectInfo>();

    [Header("Tile Modification (확장 기능)")]
    [Tooltip("스킬 적중 시 해당 타일을 이 타일로 교체 (null이면 변경 안 함)")]
    public TileBase changeTileTo;
    [Tooltip("타일 변경 지속 턴 (기본 2)")]
    public int tileChangeDuration = 2;
    [Tooltip("지대 위에 있는 유닛에게 부여할 상태 (예: Poisoning)")]
    public StatusId zoneStatusId = StatusId.None;
    [Tooltip("지대 부여 상태 스택")]
    public int zoneStatusStack = 1;
    [Tooltip("지대 부여 상태 지속 시간")]
    public int zoneStatusDuration = 3;

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

    // 시전 후 상태 제거 옵션
    [Header("State Consumption(상태 제거 설정)")]
    public bool consumeStateOnCast = false;
    [Tooltip("제거할 상태 목록")]
    public List<UnitStateId> statesToConsume = new List<UnitStateId>();

#if UNITY_EDITOR
    void OnValidate() { targetMode = selectionMode; }  // 선택값 반영
#endif

    public static void ClearFrontlineCache() => _frontlineCache.Clear();

    [Header("Projectile Settings (Ranged Only)")]
    [Tooltip("투사체 프리팹 (ProjectileController 컴포넌트 필수)")]
    public ProjectileController projectilePrefab;

    [Tooltip("투사체 속도 (초당 유닛)")]
    public float projectileSpeed = 10f;

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

    [Header("멀티 히트 설정")]
    [Tooltip("훈련으로 멀티 히트(2타 이상) 효과를 사용할 것인지")]
    public bool trainingUseMultiHit = false;
    [Tooltip("한 번 사용 시 몇 타까지 때릴지 (기본 2타)")]
    [Min(1)] public int trainingHitCount = 2;

    [Header("물리 대미지 버프 설정")]
    [Tooltip("훈련으로: 기술 사용 후 자신의 물리대미지를 일정 턴 동안 강화")]
    public bool trainingUseSelfAtkBuff = false;
    [Tooltip("이 루트가 선택되었을 때 자기 물리대미지 버프를 부여")]
    public int routeForSelfAtkBuff = -1;
    [Tooltip("부여할 버프 ID (StateStatModifierDB.BuffEntry에서 atkMultiplier를 1.4 등으로 설정)")]
    public UnitStateBuffId selfAtkBuffId = UnitStateBuffId.Self_AtkUp;
    [Tooltip("자신의 턴 기준 지속 턴 수 (실제 적용 시 +1 해서 사용 턴을 건너뜀)")]
    public int selfAtkBuffDurationTurns = 1;

    [Header("타겟 민첩 약화 적용 설정")]
    [Tooltip("공격받은 대상의 민첩을 약화")]
    public bool trainingApplyAgiDebuff = false;
    [Tooltip("이 루트가 선택되었을 때 대상에게 민첩 디버프 버프를 건다")]
    public int routeForAgiDebuff = -1;
    [Tooltip("부여할 버프 ID (StateStatModifierDB.BuffEntry에서 agiMultiplier를 0.6 등으로 설정)")]
    public UnitStateBuffId targetAgiDebuffId = UnitStateBuffId.Target_AgiDown;
    [Tooltip("지속 턴수")]
    public int targetAgiDebuffDurationTurns = 1;

    [Header("공포 상태 부여 설정")]
    [Tooltip("공격받는 대상에게 공포 상태를 부여할지 여부")]
    public bool trainingApplyFear = false;
    [Tooltip("이 루트가 선택되었을 때 대상에게 공포 상태를 건다")]
    public int routeForFear = -1;
    [Tooltip("공포 상태 지속 턴 수")]
    public int fearDurationTurns = 1;

    [Header("자원 반환 설정")]
    [Tooltip("훈련으로: 공격받은 대상의 생명이 0이 되면 소비한 자원을 돌려받음")]
    public bool trainingRefundOnKill = false;
    [Tooltip("이 루트가 선택되었을 때, 이 스킬로 적을 처치하면 MP를 돌려받음")]
    public int routeForRefundOnKill = -1;

    // 적의(Hostility) 감소 훈련
    [Header("적의 감소 훈련")]
    [Tooltip("훈련 시 공격으로 인한 적의 생성량을 감소시킬지 여부")]
    public bool trainingReduceHostility = false;
    [Tooltip("적의 감소를 활성화시키는 훈련 루트 인덱스 (-1이면 비활성)")]
    [Range(-1, 2)] public int routeForReduceHostility = -1;
    [Tooltip("적용될 적의 생성 배율 (예: 0.5 = 50%만 생성 = 0.5만큼 감소)")]
    public float trainingHostilityMultiplier = 0.5f;

    // 총명(Clarity) 강화 훈련
    [Header("Training: Clarity Buff")]
    [Tooltip("총명(Magic Damage) 강화 버프 부여 활성화")]
    public bool trainingApplyClarityBuff = false;
    [Range(-1, 2)] public int routeForClarityBuff = -1;
    public UnitStateBuffId trainingClarityBuffId = UnitStateBuffId.ClarityUp;
    [Min(1)] public int trainingClarityDuration = 1;

    [Header("Frontline Bonus")]
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

    public bool UseFrontlineBonus => useFrontlineBonus;
    public int FrontlineDepth => frontlineDepth;
    public float FrontlineMultiplier => frontlineMultiplier;
    public bool CheckFrontline(BattleUnit unit)
        => IsInFrontline(unit, frontlineDepth);

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
        costResource = SkillCostResource.MP;
    }

    public ProjectileController GetProjectilePrefab(BattleUnit caster)
    {
        return projectilePrefab;
    }

    public float GetProjectileSpeed(BattleUnit caster)
    {
        return projectileSpeed;
    }

    public override IEnumerator Execute(BattleManager bm, BattleUnit caster, BattleUnit targetUnit, Tilemap targetMap, Vector3Int targetCell)
    {
        // 1. 훈련 루트 확인: 넉백을 사용하는가?
        int route = caster.GetTrainingRouteIndex(this);
        bool useKnockback = trainingUseKnockback && routeForKnockback >= 0 && route == routeForKnockback;

        if (useKnockback && targetUnit != null)
        {
            // 2. 넉백 후보 계산
            var candidates = GetKnockbackCandidates(bm, caster, targetUnit);

            if (candidates != null && candidates.Count > 0)
            {
                // 3. 사용자 선택 대기
                Vector3Int? chosen = null;
                yield return bm.WaitForCellSelection(targetUnit.CurrentMap, candidates, (res) => chosen = res);

                // 4. 선택됨 -> Pending 등록
                if (chosen.HasValue)
                {
                    bm.SetPendingKnockback(this, targetUnit, chosen.Value);
                }
                else
                {
                    // 취소 시 스킬 자체를 취소할지, 넉백 없이 공격할지 결정.
                    // 보통은 취소면 스킬 취소.
                    bm.CancelCurrentAction();
                    yield break;
                }
            }
        }

        // 타겟 유닛이 있으면 유닛 흐름, 없으면(타일 타겟팅이면) 타일 흐름 실행
        if (targetUnit != null)
        {
            yield return bm.PerformStandardUnitSkillFlow(this, caster, targetUnit);
        }
        else
        {
            // 타일 대상 스킬 흐름 (투사체 or 즉발 등은 BattleManager가 판단)
            yield return bm.PerformStandardTileSkillFlow(this, targetMap, targetCell, caster);
        }
    }

    public override IEnumerable<Vector3Int> GetAreaCells(Vector3Int _originCell, bool _isOddColumn)
    {
        // 현재 턴의 시전자(플레이어든 적이든)
        var bm = BattleManager.Instance;
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
            .Where(u => u && u.data.team == Team.Player && !u.IsDead).ToList();
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
    Vector2Int DirToAx(AxialDir _d) => AX_DIRS[(int)_d];
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

    public List<Vector3Int> GetKnockbackCandidates(BattleManager _battlemanager, BattleUnit _caster, BattleUnit _target)
    {
        var result = new List<Vector3Int>();
        if (!_battlemanager || !_caster || !_target) return result;

        var map = _target.CurrentMap;
        if (!map) return result;

        var start = _target.Cell;

        // caster -> target 방향 (넉백은 "caster에서 target으로"의 연장선 방향)
        Vector3 casterW = map.GetCellCenterWorld(_caster.Cell);
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
            var units = _battlemanager.Grid.GetUnitsInArea(map, new[] { cell });
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
        var set = GetFrontlineSet(_battleunit.CurrentMap, _battleunit.data.team, _depth);
        return set != null && set.Contains(_battleunit.Cell);
    }

    // 중심 셀 기준으로 범위 유닛들을 찾아 데미지 적용(팀 반대편만)
    void DealAreaDamage(BattleManager _battlemanager, BattleUnit _caster, Tilemap _map, Vector3Int _centerCell)
    {
        if (!_battlemanager || !_caster) return;

        int route = GetRoute(_caster);

        // 범위 계산
        var area = GetAreaCells(_centerCell, SkillLibrary.IsOddColumn(_centerCell));

        // === 멀티 히트 횟수 계산 ===
        int hits = trainingUseMultiHit ? Mathf.Max(1, trainingHitCount) : 1;

        List<BattleUnit> victims;
        if (trainingHitAllEnemies && routeForHitAllEnemies >= 0 && route == routeForHitAllEnemies)
        {
            victims = Object.FindObjectsOfType<BattleUnit>()
                .Where(u => u != null && !u.IsDead && u.data.team != _caster.data.team && u.CurrentMap == _map).ToList();
        }
        else
        {
            victims = _battlemanager.Grid.GetUnitsInArea(_map, area)
                            .Where(u => u != null && !u.IsDead && u.data.team != _caster.data.team).ToList();
        }

        // 타일 변경 및 지대(Zone) 생성 로직
        if (changeTileTo != null && _map != null)
        {
            // 공격 범위(area) 전체를 순회하며 지대 생성
            foreach (var cell in area)
            {
                _battlemanager.Field.CreateStatusTileZone(
                    _caster,
                    _map,
                    cell,
                    tileChangeDuration, // 지속 턴
                    changeTileTo,       // 변경할 타일 이미지
                    zoneStatusId,       // 부여할 상태
                    zoneStatusStack,    // 스택
                    zoneStatusDuration  // 상태 지속
                );
            }
        }

        // 방어 중첩 훈련 (시전자)
        if (trainingApplyDefenseStacks && routeForDefenseStacks >= 0 && route == routeForDefenseStacks && trainingDefenseStatusId != StatusId.None)
        {
            _caster.GetComponent<StatusController>()?.ApplyWithTurnContext(
                trainingDefenseStatusId, Mathf.Max(1, trainingDefenseStacks), Mathf.Max(1, trainingDefenseDurationTurns)
            );
        }

        // 실제 타격 (멀티 히트)
        for (int i = 0; i < hits; i++)
        {
            _battlemanager.ExecuteSkillDamage(_caster, victims, this, _map, _centerCell);
        }

        foreach (var v in victims)
        {
            ApplyStatusEffects(v);
        }

        if (trainingUseSelfAtkBuff && routeForSelfAtkBuff >= 0 && route == routeForSelfAtkBuff && selfAtkBuffId != UnitStateBuffId.None)
        {
            _caster.GetComponent<UnitStateController>()?.ApplyBuffForTurns(selfAtkBuffId, selfAtkBuffDurationTurns + 1);
        }

        // 총명 강화 버프 적용
        if (trainingApplyClarityBuff && routeForClarityBuff >= 0 && route == routeForClarityBuff && trainingClarityBuffId != UnitStateBuffId.None)
        {
            var usc = _caster.GetComponent<UnitStateController>();
            if (usc != null)
            {
                // 지정된 턴 수만큼 버프 부여
                usc.ApplyBuffForTurns(trainingClarityBuffId, trainingClarityDuration + 1); // +1은 현재 턴 소모 보정
                Debug.Log($"[ParametricDamage] Clarity Enhanced: {_caster.name}, Duration={trainingClarityDuration}");
            }
        }
    }

    public override IEnumerator ResolveOnUnit(BattleManager _battlemanager, BattleUnit _caster, BattleUnit _target)
    {
        if (!_battlemanager || !_caster) yield break;

        // MP 소비
        var res = GetCostResource(_caster);
        int cost = GetEffectiveCost(_caster);
        if (cost > 0 && !_caster.TryConsumeResource(res, cost)) yield break;

        BattleUnit primary = (useProvidedUnitTarget && _target != null && !_target.IsDead)
                                ? _target
                                : PickPrimaryTarget();

        if (primary == null) yield break;

        int route = GetRoute(_caster);
        Debug.Log($"[Training] {name} by {_caster.name} route={route}, useAreaOverride={trainingUseAreaOverride}");

        // 범위 계산은 GetAreaCells 안에서 route를 보고 알아서 처리
        DealAreaDamage(_battlemanager, _caster, primary.CurrentMap, primary.Cell);
        // 상태 이상 부여
        ApplyStatusEffects(_target);
        yield break;
    }
    public override IEnumerator ResolveOnTile(BattleManager _battlemanager, Tilemap _map, Vector3Int _originCell, BattleUnit _caster)
    {
        if (!_battlemanager || !_caster || !_map) yield break;

        // MP 소비
        var res = GetCostResource(_caster);
        int cost = GetEffectiveCost(_caster);
        if (cost > 0 && !_caster.TryConsumeResource(res, cost)) yield break;

        int route = GetRoute(_caster);
        Debug.Log($"[Training] (Tile) {name} by {_caster.name} route={route}, useAreaOverride={trainingUseAreaOverride}");

        DealAreaDamage(_battlemanager, _caster, _map, _originCell);

        // 시전 후 상태 제거
        ConsumeStates(_caster);

        yield break;
    }

    protected void ApplyStatusEffects(BattleUnit target)
    {
        if (applyStatusOnHit == null || applyStatusOnHit.Count == 0) return;
        if (target == null) return;

        var statusCtrl = target.GetComponent<StatusController>();
        if (statusCtrl != null)
        {
            foreach (var effect in applyStatusOnHit)
            {
                if (effect.status != StatusId.None)
                {
                    // Duration이 0보다 크면 턴 컨텍스트 포함 적용, 아니면 단순 스택 설정
                    if (effect.duration > 0)
                    {
                        statusCtrl.ApplyWithTurnContext(effect.status, effect.stack, effect.duration);
                        Debug.Log($"[Status] {target.name}에게 {effect.status} 부여: {effect.stack}스택 / {effect.duration}턴");
                    }
                    else
                    {
                        statusCtrl.SetStacks(effect.status, effect.stack);
                    }
                }
            }
        }
    }

    // 상태 제거
    void ConsumeStates(BattleUnit caster)
    {
        if (!consumeStateOnCast || statesToConsume == null) return;
        var usc = caster.GetComponent<UnitStateController>();
        if (usc == null) return;

        foreach (var s in statesToConsume)
        {
            if (s != UnitStateId.None)
                usc.Remove(s);
        }
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
