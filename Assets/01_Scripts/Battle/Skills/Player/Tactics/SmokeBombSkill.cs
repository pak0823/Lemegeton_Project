using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "Battle/Skills/Common/Smoke Bomb", fileName = "SmokeBombSkill")]
public class SmokeBombSkill : SkillAsset, ITargetMapProvider
{
    // 기존 HostilityVisibility / AgilityBuff 유지(호환 목적)
    public enum SmokeEffectMode
    {
        SmokeHiddenBuff,        // 신규: 연막 은신(타겟 불가) 버프 부여
        AgilityBuff             // (옵션) AGI 버프
    }

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
    public AreaPreset areaPreset = AreaPreset.Single;

    [Header("Smoke Settings")]
    [Tooltip("시전자의 턴 시작 기준 지속")]
    public int durationCasterTurns = 2;

    [Header("Effect Mode")]
    [Tooltip("기본값: SmokeHiddenBuff (연막 안에 있는 동안 타겟 불가)")]
    public SmokeEffectMode effectMode = SmokeEffectMode.SmokeHiddenBuff;

    [Header("Smoke Hidden (New)")]
    [Tooltip("연막 안에 있는 동안 부여할 버프(적이 타겟으로 지정 불가)")]
    public UnitStateBuffId smokeHiddenBuffState = UnitStateBuffId.SmokeHidden;

    [Header("Agility Buff (Optional)")]
    [Tooltip("연막 타일에 서 있는 동안 적용할 AGI 배수(예: 1.7)")]
    public float agiMultiplier = 1.7f;
    [Tooltip("AGI 버프에 사용할 버프 ID (StateStatModifierDB에 정의)")]
    public UnitStateBuffId agiBuffState = UnitStateBuffId.Smoke_AgiUp;

    [Header("Targeting")]
    public SkillTargetMode selectionMode = SkillTargetMode.Tile;

    [Header("Training")]
    
    public bool trainingEnableMpRegen = true;
    [Range(-1, 2)] public int routeForMpRegen = 1;
    [Range(0f, 1f)] public float trainingMpRegenRatio = 0.3f;

    public bool trainingUseZoneAreaOverride = true;
    [Range(-1, 2)] public int routeForZoneAreaOverride = 2;
    public AreaPreset trainingZoneAreaPreset = AreaPreset.Ring;

#if UNITY_EDITOR
    void OnValidate() { targetMode = selectionMode; }
#endif

    void OnEnable()
    {
        targetMode = selectionMode;
        power = 0f;
        school = DamageSchool.Physical;
        costResource = SkillCostResource.MP;
    }

    int GetRoute(BattleUnit caster)
    {
        if (!caster) return -1;
        return caster.GetTrainingRouteIndex(this);
    }

    public override IEnumerable<Vector3Int> GetAreaCells(Vector3Int originCell, bool isOdd)
    {
        foreach (var c in AreaShapes.GetCells(originCell, areaPreset, false))
            yield return c;
    }

    public Tilemap GetTargetMap(BattleManager bm, BattleUnit caster)
    {
        var prov = BattleMapManager.Instance;
        if (prov == null) return null;
        return (caster != null && caster.data.team == Team.Player) ? prov.PlayerFloor : prov.EnemyFloor;
    }

    public override IEnumerator Execute(BattleManager bm, BattleUnit caster, BattleUnit targetUnit, Tilemap targetMap, Vector3Int targetCell)
    {
        // 타겟 맵이 없으면 실행 불가
        if (targetMap == null) yield break;

        // 공통 타일 스킬 흐름 (애니메이션 -> ResolveOnTile 호출 -> 쿨다운/턴종료)
        yield return bm.PerformStandardTileSkillFlow(this, targetMap, targetCell, caster);
    }

    public override IEnumerator ResolveOnTile(BattleManager bm, Tilemap map, Vector3Int originCell, BattleUnit caster)
    {
        if (!bm || !caster || !map) yield break;

        var cells = GetAreaCells(originCell, SkillLibrary.IsOddColumn(originCell)).ToList();
        if (cells.Count == 0) yield break;

        CreateSmokeZoneRuntime(bm, map, cells, originCell, caster, durationCasterTurns);

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
        int durationTurns)
    {
        var go = new GameObject("SmokeZoneRuntime");
        var comp = go.AddComponent<SmokeZoneRuntime>();
        go.transform.SetParent(bm.transform, false);


        comp.Initialize(bm, map, cells, caster, durationTurns);

        int route = GetRoute(caster);

        // --- MP 회복 옵션 (지정 루트) ---
        if (trainingEnableMpRegen &&
            routeForMpRegen >= 0 &&
            route == routeForMpRegen)
        {
            comp.enableMpRegen = true;
            comp.mpRegenRatio = trainingMpRegenRatio;
        }
        else
        {
            comp.enableMpRegen = false;
            comp.mpRegenRatio = 0f;
        }

        // --- 존 범위 오버라이드 (지정 루트) ---
        if (trainingUseZoneAreaOverride &&
            routeForZoneAreaOverride >= 0 &&
            route == routeForZoneAreaOverride)
        {
            var ringCells = AreaShapes.GetCells(
                centerCell,
                trainingZoneAreaPreset,
                diagUseNEAxis: true
            );
            comp.OverrideAreaCells(ringCells);
        }

        // 효과 모드 설정 (SmokeHidden / HostilityVisibility / AgilityBuff)
        comp.SetEffectMode(effectMode, smokeHiddenBuffState, agiBuffState, agiMultiplier);

        // VFX 배치
        comp.AttachVfx(smokeVfxPrefab, vfxYOffset, vfxSortingLayer, vfxSortingOrder, caster.data.team);
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
