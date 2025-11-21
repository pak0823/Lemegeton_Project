using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "Battle/Skills/Common/Smoke Bomb", fileName = "SmokeBombSkill")]
public class SmokeBombSkill : SkillAsset, ITargetMapProvider
{
    public enum SmokeEffectMode { HostilityVisibility, AgilityBuff }

    [Header("VFX")]
    [Tooltip("연막 타일에 표시할 VFX 프리팹 (파티클/스프라이트 등)")]
    public GameObject smokeVfxPrefab;
    [Tooltip("타일 센터에서 Y 오프셋(카메라 각도/가려짐 보정)")]
    public float vfxYOffset = 0f;
    [Tooltip("정렬 레이어 (SpriteRenderer/ParticleSystemRenderer에 적용)")]
    public string vfxSortingLayer = "Effects";
    [Tooltip("정렬 순서")]
    public int vfxSortingOrder = 0;
    [Header("Area")]
    public ParametricDamageSkill.AreaPreset areaPreset = ParametricDamageSkill.AreaPreset.Single;

    [Header("Smoke Settings")]
    [Tooltip("시전자(caster)의 턴 종료 횟수 기준 지속")]
    public int durationCasterTurns = 2;

    [Header("Effect Mode")]
    public SmokeEffectMode effectMode = SmokeEffectMode.HostilityVisibility;
    [Tooltip("연막 타일에 서 있는 동안 적용할 Hostility 배수")]
    public float visibilityHostilityFactor = 0.7f;
    [Tooltip("연막 타일에 서 있는 동안 적용할 AGI 배수(예: 1.7)")]
    public float agiMultiplier = 1.7f;
    [Tooltip("AGI 버프에 사용할 상태 ID (StateStatModifierDB에 정의)")]
    public UnitStateBuffId agiBuffState = UnitStateBuffId.AgiUp; // 새로 추가할 상태 아이디
   
    [Header("Targeting")]
    public SkillTargetMode selectionMode = SkillTargetMode.Tile; // 타일 지목

    [Header("Training")]
    [Tooltip("Route 0에서 MP 비용을 덮어쓸지 여부")]
    public bool trainingUseMpOverride = false;
    [Tooltip("Route 2에서 범위를 덮어쓸지 여부")]
    public bool trainingUseZoneAreaOverride = true;

    [Tooltip("Route 0 선택 시 실제 소모 MP")]
    public int trainingMpCostRoute0 = 5;

    [Tooltip("Route 1 선택 시 증가할 수치 값")]
    [Range(0f, 1f)] public float trainingMpRegenRatio = 0.3f;

    [Tooltip("Training Route2 (Zone Area Override)")]

    public ParametricDamageSkill.AreaPreset trainingZoneAreaPreset =
        ParametricDamageSkill.AreaPreset.Ring;

#if UNITY_EDITOR
    void OnValidate() { targetMode = selectionMode; }
#endif
    void OnEnable()
    {
        targetMode = selectionMode;
        power = 0f; // 피해 없음
        school = DamageSchool.Physical;
    }

    public override IEnumerable<Vector3Int> GetAreaCells(Vector3Int originCell, bool isOdd)
    {
        foreach (var c in AreaShapes.GetCells(originCell, areaPreset, false))
            yield return c;
    }

    public Tilemap GetTargetMap(BattleManager bm, BattleUnit caster)
    {
        var prov = Shared.battleMapManager; // 프로젝트 맵 프로바이더 사용
        if (prov == null) return null;
        return (caster != null && caster.team == Team.Player) ? prov.PlayerFloor : prov.EnemyFloor;
    }

    public override IEnumerator ResolveOnTile(BattleManager bm, Tilemap map, Vector3Int originCell, BattleUnit caster)
    {
        if (!bm || !caster || !map) yield break;

        var cells = GetAreaCells(originCell, SkillLibrary.IsOddColumn(originCell)).ToList();
        if (cells.Count == 0) yield break;

        CreateSmokeZoneRuntime(
             bm, map, cells, originCell, caster, durationCasterTurns, visibilityHostilityFactor,
            smokeVfxPrefab, vfxYOffset, vfxSortingLayer, vfxSortingOrder
        );

        int cost = GetEffectiveMpCost(caster);
        if (cost > 0) caster.TryConsumeMP(cost);

        yield break;
    }

    public override IEnumerator ResolveOnUnit(BattleManager bm, BattleUnit caster, BattleUnit target)
    {
        if (!bm || !caster || target == null) yield break;
        var map = target.CurrentMap;
        if (!map) yield break;
        yield return ResolveOnTile(bm, map, target.Cell, caster);
    }

    void CreateSmokeZoneRuntime(
    BattleManager bm,
    Tilemap map,
    List<Vector3Int> cells,
    Vector3Int centerCell,
    BattleUnit caster,
    int durationTurns,
    float factor,
    GameObject vfxPrefab,
    float yOffset,
    string sortingLayer,
    int sortingOrder)
    {
        var go = new GameObject("SmokeZoneRuntime");
        var comp = go.AddComponent<SmokeZoneRuntime>();
        go.transform.SetParent(bm.transform, false);

        // 모드에 따라 파라미터 전달
        comp.Initialize(bm,
        map,
        cells,
        caster,
        durationTurns,
        effectMode == SmokeEffectMode.HostilityVisibility ? visibilityHostilityFactor : 1f);

        int route = caster.GetTrainingRouteIndex(this);
        // --- route1: MP 회복 활성화 ---
        if (route == 1)
        {
            comp.enableMpRegen = true;
            comp.mpRegenRatio = trainingMpRegenRatio;
        }
        else
        {
            comp.enableMpRegen = false;
            comp.mpRegenRatio = 0f;
        }

        // --- route2: 존 범위 오버라이드 ---
        if (route == 2 && trainingUseZoneAreaOverride)
        {
            // 중심 기준 Ring 셀 목록 계산
            var ringCells = AreaShapes.GetCells(
                centerCell,
                trainingZoneAreaPreset,   // 보통 Ring
                diagUseNEAxis: true
            );
            comp.OverrideAreaCells(ringCells);
        }

        // Hostility / AGI 모드 설정
        comp.SetEffectMode(effectMode, agiBuffState, agiMultiplier);
        // VFX 배치
        comp.AttachVfx(smokeVfxPrefab, vfxYOffset, vfxSortingLayer, vfxSortingOrder, caster.team);
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
