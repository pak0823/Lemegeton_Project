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
                return PickHighestHostility(players);

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

    // 공통 상수 (축 방향)
    static readonly Vector2Int DIR_NE = new Vector2Int(1, -1);
    static readonly Vector2Int DIR_SW = new Vector2Int(-1, 1);

    // 맵별/팀별/깊이별 캐시
    static readonly Dictionary<(Tilemap, int, int), HashSet<Vector3Int>> _frontlineCache
      = new();

    // team: 0=Player, 1=Enemy (enum 캐스팅), depth: frontlineDepth
    HashSet<Vector3Int> GetFrontlineSet(Tilemap map, Team team, int depth)
    {
        if (!map || depth <= 0) return null;
        var key = (map, (int)team, depth);
        if (_frontlineCache.TryGetValue(key, out var cached)) return cached;

        // 1) 팀별 전진 방향 f 선정
        Vector2Int f = (team == Team.Player) ? DIR_SW : DIR_NE;

        // 2) 맵 내 모든 유효 타일 수집(+축좌표로 변환)
        var b = map.cellBounds;
        var cells = new List<(Vector3Int off, Vector2Int ax)>(128);
        for (int y = b.yMin; y < b.yMax; y++)
            for (int x = b.xMin; x < b.xMax; x++)
            {
                var c = new Vector3Int(x, y, 0);
                if (!map.HasTile(c)) continue;
                var ax = SkillLibrary.OffsetToAxial(c);
                cells.Add((c, ax));
            }
        if (cells.Count == 0) return null;

        // 3) f축으로의 스칼라 투영 t = q*f.x + r*f.y
        int tMin = int.MaxValue, tMax = int.MinValue;
        var tMap = new Dictionary<Vector3Int, int>(cells.Count);
        foreach (var e in cells)
        {
            int t = e.ax.x * f.x + e.ax.y * f.y;
            tMap[e.off] = t;
            if (t < tMin) tMin = t;
            if (t > tMax) tMax = t;
        }

        // 여기서 컷오프 방향만 교정
        int edge = (team == Team.Player) ? tMax : tMin;

        // 앞 depth 레이어 범위
        int lo = (team == Team.Player) ? edge - (depth - 1) : edge;
        int hi = (team == Team.Player) ? edge : edge + (depth - 1);

        var result = new HashSet<Vector3Int>();
        foreach (var kv in tMap)
            if (kv.Value >= lo && kv.Value <= hi)
                result.Add(kv.Key);

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
            float mult = GetMultiplierFor(v);        // 상태기반 추가 배수(옵션)

            if (useFrontlineBonus && IsInFrontline(caster, frontlineDepth))
                mult *= Mathf.Max(0f, frontlineMultiplier);

            float finalPower = power * mult;
            int dmg = Mathf.Max(1, Mathf.RoundToInt(baseStat * finalPower));
            v.PlayHit();
            v.TakeDamage(dmg);
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
