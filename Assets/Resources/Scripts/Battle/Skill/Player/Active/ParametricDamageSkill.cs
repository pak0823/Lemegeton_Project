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

    [Header("Frontline Bonus")]
    [SerializeField] private bool useFrontlineBonus = false;   // 전방 보너스 사용 여부
    [SerializeField] private int frontlineDepth = 2;            // "앞 N열"
    [SerializeField] private float frontlineMultiplier = 1.5f;  // 배수(예: 1.5)

#if UNITY_EDITOR
    void OnValidate() { targetMode = selectionMode; }  // 선택값 반영
#endif

    public static void ClearFrontlineCache() => _frontlineCache.Clear();

    [Header("Training Effects")]
    [Tooltip("훈련 루트 0: 범위 프리셋을 이 값으로 교체")]
    public bool trainingUseAreaOverride = false;
    public AreaPreset trainingAreaPreset = AreaPreset.Single;
    public bool trainingDiagUseNEAxis = true;

    [Tooltip("훈련 루트 1: 캐스팅 제압 추가 감소량(피격 시)")]
    public int trainingSuppressionOnHit = 0; // 0=미사용, 1 이상이면 그만큼 추가로 suppressCur 감소

    [Tooltip("훈련 루트 2: 출혈 부여 설정")]
    public bool trainingApplyBleed = false;
    [Range(1, DebuffTuning.SlowMaxStacks)] public int trainingBleedStacks = 1;
    [Min(1)] public int trainingBleedDurationTurns = 2;

    [Tooltip("쿨다운 턴수 변화량(음수면 단축)")]
    public int trainingCooldownDeltaRoute2 = 0;

    [Tooltip("이 스킬을 맵 상의 모든 적군에게 적중시키기")]
    public bool trainingHitAllEnemiesOnRoute0 = false;

    // 선택된 훈련 루트를 읽어 현재 실행에 반영
    int GetRoute(BattleUnit caster)
    {
        if (caster == null) return -1;
        return caster.GetTrainingRouteIndex(this);
    }

    void OnEnable()
    {
        school = damageSchool;
        if (powerOverride > 0f) power = powerOverride;
        targetMode = selectionMode;  // 실행 시에도 선택값 반영
    }

    public override IEnumerable<Vector3Int> GetAreaCells(Vector3Int originCell, bool isOddColumn)
    {
        // 현재 턴의 시전자(플레이어든 적이든)
        var bm = Shared.BattleManager;
        BattleUnit caster = bm != null ? bm.ActingUnit : null;

        int route = GetRoute(caster);
        bool useOverride = (route == 0 && trainingUseAreaOverride);

        // 기본/훈련 프리셋 선택
        var preset = useOverride ? trainingAreaPreset : areaPreset;
        bool useDiag = useOverride ? trainingDiagUseNEAxis : diagUseNEAxis;

        foreach (var c in AreaShapes.GetCells(originCell, preset, useDiag))
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

    float GetMultiplierFor(BattleUnit victim)
    {
        if (conditionalMultipliers == null || conditionalMultipliers.Count == 0) return 1f;
        var sc = victim ? victim.GetComponent<StatusController>() : null;
        if (sc == null) return 1f;

        float mult = 1f;
        foreach (var c in conditionalMultipliers)
            if (sc.Has(c.status)) mult *= c.multiplier; // 여러 조건 중첩 시 곱연산
        return mult;
    }

    [SerializeField] private bool useManualFrontier = true;

    // 1열 수동 경계
    [SerializeField] private List<Vector3Int> manualFrontierPlayer;
    [SerializeField] private List<Vector3Int> manualFrontierEnemy;

    // 2열 수동 지정(있으면 자동 확장 대신 이걸 우선 사용)
    [SerializeField] private List<Vector3Int> manualSecondLayerPlayer;
    [SerializeField] private List<Vector3Int> manualSecondLayerEnemy;
    [SerializeField] private AxialDir playerFrontlineDir = AxialDir.SW; // 플레이어 전방축
    [SerializeField] private AxialDir enemyFrontlineDir = AxialDir.NE; // 적 전방축
    public enum AxialDir { E, NE, NW, W, SW, SE }
    Vector2Int DirToAx(AxialDir d) => AX_DIRS[(int)d];
    // 맵별/팀별/깊이별 캐시
    static readonly Dictionary<(Tilemap, int, int), HashSet<Vector3Int>> _frontlineCache
        = new Dictionary<(Tilemap, int, int), HashSet<Vector3Int>>();

    HashSet<Vector3Int> GetFrontlineSet(Tilemap map, Team team, int depth)
    {
        if (!map || depth <= 0) return null;
        var key = (map, (int)team, depth);
        if (_frontlineCache.TryGetValue(key, out var cached)) return cached;

        // 맵 존재 타일 수집
        var b = map.cellBounds;
        var all = new HashSet<Vector3Int>();
        for (int y = b.yMin; y < b.yMax; y++)
            for (int x = b.xMin; x < b.xMax; x++)
            { var c = new Vector3Int(x, y, 0); if (map.HasTile(c)) all.Add(c); }

        // 전방축 f (이미 수동축 SW/NE를 쓰고 계신다면 그대로)
        Vector2Int f = (team == Team.Player) ? DirToAx(playerFrontlineDir)
                                             : DirToAx(enemyFrontlineDir);

        // 1) 1열: 수동 경계 우선, 없으면 자동 frontier
        var frontier = new HashSet<Vector3Int>();
        var srcFront = (team == Team.Player) ? manualFrontierPlayer : manualFrontierEnemy;
        if (useManualFrontier && srcFront != null && srcFront.Count > 0)
        {
            foreach (var c in srcFront) if (map.HasTile(c)) frontier.Add(c);
        }
        else
        {
            // 자동 frontier: f로 한 칸 나가면 타일 없음
            foreach (var c in all)
            {
                var ax = SkillLibrary.OffsetToAxial(c);
                var axF = new Vector2Int(ax.x + f.x, ax.y + f.y);
                var offF = SkillLibrary.AxialToOffset(axF);
                if (!map.HasTile(offF)) frontier.Add(c);
            }
        }

        // 최종 결과에 1열 추가
        var result = new HashSet<Vector3Int>(frontier);

        if (depth >= 2)
        {
            // 2) 2열: 수동 2열이 있으면 우선 사용
            var secondManual = (team == Team.Player) ? manualSecondLayerPlayer : manualSecondLayerEnemy;
            if (secondManual != null && secondManual.Count > 0)
            {
                foreach (var c in secondManual) if (map.HasTile(c)) result.Add(c);
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
                    if (map.HasTile(offBk)) next.Add(offBk);
                }
                foreach (var n in next) result.Add(n);
            }
        }

        _frontlineCache[key] = result;
        return result;
    }

    bool IsInFrontline(BattleUnit u, int depth)
    {
        if (!u || !u.CurrentMap) return false;
        var set = GetFrontlineSet(u.CurrentMap, u.team, depth);
        return set != null && set.Contains(u.Cell);
    }

    // 중심 셀 기준으로 범위 유닛들을 찾아 데미지 적용(팀 반대편만)
    void DealAreaDamage(BattleManager bm, BattleUnit caster, Tilemap map, Vector3Int centerCell)
    {
        if (!bm || !caster) return;

        int route = GetRoute(caster);

        //전체 적군 공격 (이 스킬에서 trainingHitAllEnemiesOnRoute0를 켠 경우)
        if (route == 0 && trainingHitAllEnemiesOnRoute0)
        {
            var allUnits = Object.FindObjectsOfType<BattleUnit>();
            var victims = allUnits
                .Where(u => u != null
                            && !u.IsDead
                            && u.team != caster.team
                            && u.CurrentMap == map)
                .ToList();

            bm.ExecuteSkillDamage(caster, victims, this, map, centerCell);
            return;
        }

        // 기본/다른 훈련 루트: 기존 범위 로직 사용
        var area = GetAreaCells(centerCell, SkillLibrary.IsOddColumn(centerCell));
        var areaVictims = bm.GetUnitsInArea(map, area)
                            .Where(u => u != null && !u.IsDead && u.team != caster.team)
                            .ToList();

        bm.ExecuteSkillDamage(caster, areaVictims, this, map, centerCell);
    }

    public override int ComputeDamage(BattleUnit caster, BattleUnit target, in SkillRuntime ctx)
    {
        // 기본 산식(속성/저항 포함)은 부모 호출
        int baseDmg = base.ComputeDamage(caster, target, ctx);

        // 추가 배수: 상태 기반
        float mult = GetMultiplierFor(target);

        // 추가 배수: 전방 보너스
        if (useFrontlineBonus && caster != null && IsInFrontline(caster, frontlineDepth))
            mult *= Mathf.Max(0f, frontlineMultiplier);

        // 최종
        int dmg = Mathf.Max(0, Mathf.RoundToInt(baseDmg * mult));
        return dmg;
    }

    public override IEnumerator ResolveOnUnit(BattleManager bm, BattleUnit caster, BattleUnit target)
    {
        if (!bm || !caster) yield break;

        BattleUnit primary = (useProvidedUnitTarget && target != null && !target.IsDead)
                                ? target
                                : PickPrimaryTarget();

        if (primary == null) yield break;

        int route = GetRoute(caster);
        Debug.Log($"[Training] {name} by {caster.name} route={route}, useAreaOverride={trainingUseAreaOverride}");

        // 범위 계산은 GetAreaCells 안에서 route를 보고 알아서 처리
        DealAreaDamage(bm, caster, primary.CurrentMap, primary.Cell);
        yield break;
    }
    public override IEnumerator ResolveOnTile(BattleManager bm, Tilemap map, Vector3Int originCell, BattleUnit caster)
    {
        if (!bm || !caster || !map) yield break;

        int route = GetRoute(caster);
        Debug.Log($"[Training] (Tile) {name} by {caster.name} route={route}, useAreaOverride={trainingUseAreaOverride}");

        DealAreaDamage(bm, caster, map, originCell);
        yield break;
    }

    public override int GetSuppressionOnHit(BattleUnit caster)
    {
        // 루트1일 때만 trainingSuppressionOnHit 값을 돌려줌
        return (GetRoute(caster) == 1) ? Mathf.Max(0, trainingSuppressionOnHit) : 0;
    }

    public override int GetEffectiveCooldownTurns(BattleUnit caster)
    {
        int cd = cooldownTurns;

        int route = GetRoute(caster);
        if (route == 2 && trainingCooldownDeltaRoute2 != 0)
        {
            cd = Mathf.Max(0, cd + trainingCooldownDeltaRoute2);
        }

        return cd;
    }

}
