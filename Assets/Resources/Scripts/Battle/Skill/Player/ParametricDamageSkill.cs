using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UIElements;
using static UnityEngine.GraphicsBuffer;

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

    void OnEnable()
    {
        school = damageSchool;
        if (powerOverride > 0f) power = powerOverride;
        targetMode = selectionMode;  // 실행 시에도 선택값 반영
    }

    public override IEnumerable<Vector3Int> GetAreaCells(Vector3Int originCell, bool isOddColumn)
    {
        if (areaPreset == AreaPreset.Single) { yield return originCell; yield break; }  //단일

        if (areaPreset == AreaPreset.LineHorizontal)    //가로
        {
            var ax = SkillLibrary.OffsetToAxial(originCell);
            var deltas = new[] { new Vector2Int(-1, 0), new Vector2Int(0, 0), new Vector2Int(1, 0) };
            foreach (var d in deltas)
                yield return SkillLibrary.AxialToOffset(new Vector2Int(ax.x + d.x, ax.y + d.y));
            yield break;
        }

        if (areaPreset == AreaPreset.LineDiagU3)
        {
            var ax = SkillLibrary.OffsetToAxial(originCell);

            // 스위치로 축만 선택
            var dir = diagUseNEAxis
                ? new Vector2Int(1, -1)  // NE/SW 축 (↗↙)
                : new Vector2Int(0, -1); // NW/SE 축 (↖↘)

            for (int i = -1; i <= 1; i++)
            {
                var p = new Vector2Int(ax.x + dir.x * i, ax.y + dir.y * i);
                yield return SkillLibrary.AxialToOffset(p);
            }
            yield break;
        }

        if (areaPreset == AreaPreset.LineDiagU7)
        {
            var ax = SkillLibrary.OffsetToAxial(originCell);
            var dir = diagUseNEAxis ? new Vector2Int(1, -1) : new Vector2Int(0, -1);
            for (int i = -3; i <= 3; i++)
            {
                var p = new Vector2Int(ax.x + dir.x * i, ax.y + dir.y * i);
                yield return SkillLibrary.AxialToOffset(p);
            }
            yield break;
        }

        yield return originCell;
        var axR = SkillLibrary.OffsetToAxial(originCell);
        var deltasR = new[]{ new Vector2Int(1,0), new Vector2Int(1,-1), new Vector2Int(0,-1),
                             new Vector2Int(-1,0), new Vector2Int(-1,1), new Vector2Int(0,1) };
        foreach (var d in deltasR)
            yield return SkillLibrary.AxialToOffset(new Vector2Int(axR.x + d.x, axR.y + d.y));
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
        var area = GetAreaCells(centerCell, SkillLibrary.IsOddColumn(centerCell));
        var victims = bm.GetUnitsInArea(map, area)
                        .Where(u => u != null && !u.IsDead && u.team != caster.team) // 상대팀만
                        .ToList();

        int baseStat = (damageSchool == DamageSchool.Physical) ? caster.PhysicalDamage : caster.MagicDamage;

        foreach (var v in victims)
        {
            float mult = GetMultiplierFor(v);        // 상태기반 추가 배수

            if (useFrontlineBonus && IsInFrontline(caster, frontlineDepth))
                mult *= Mathf.Max(0f, frontlineMultiplier);

            float finalPower = power * mult;
            // raw 계산을 먼저 하고 음수면 0으로 클램프해서 음수 Floor 문제 방지
            float raw = Mathf.Max(0f, baseStat * finalPower);
            // 소수점 내림(Floor) 적용
            int floored = Mathf.FloorToInt(raw);
            // 최소 데미지 보장
            int dmg = Mathf.Max(0, floored);

            // 체력 비례 배율 계산(대미지 적용 전 hp로 계산해야함)
            float healthMultiplier = 1 + (1 - ((float)v.HP / v.MaxHP));
            Debug.Log($"healthMultiplier: {healthMultiplier}");

            v.PlayHit();
            v.TakeDamage(dmg);

            float scaling = v.isBoss == ISBOSS.Boss ? 2.0f : 1.0f;

            // 상태 효과에 따른 배율 가져오기
            float statusMultiplier = caster.HostilityGenerationMultiplier;
            //Debug.Log($"statusMultiplier: {statusMultiplier}");

            // 최종 적대감 생성량 계산
            float hostilityGained = dmg * healthMultiplier * scaling * statusMultiplier;

            // 캐스터(플레이어)의 적대감 증가
            caster.AddHostility(hostilityGained);

            Debug.Log($"{caster.name}이(가) {v.name}에게 {dmg} 피해를 입혀 적대감 {hostilityGained} 획득! (현재 총 적대감: {caster.Hostility})");
        }

        
    }

    public override IEnumerator ResolveOnUnit(BattleManager bm, BattleUnit caster, BattleUnit target)
    {
        if (!bm || !caster) yield break;

        // 플레이어가 지목한 'target'을 그대로 사용
        BattleUnit primary = (useProvidedUnitTarget && target != null && !target.IsDead)
                                ? target
                                : PickPrimaryTarget(); // (적 AI 등에서 재사용)

        if (primary == null) yield break;

        DealAreaDamage(bm, caster, primary.CurrentMap, primary.Cell);
        yield break;
    }
    public override IEnumerator ResolveOnTile(BattleManager bm, Tilemap map, Vector3Int originCell, BattleUnit caster)
    {
        if (!bm || !caster || !map) yield break;

        DealAreaDamage(bm, caster, map, originCell);
        yield break;
    }

}
