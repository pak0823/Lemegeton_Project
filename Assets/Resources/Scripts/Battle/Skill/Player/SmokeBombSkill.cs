using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "Battle/Skills/Common/Smoke Bomb", fileName = "SmokeBombSkill")]
public class SmokeBombSkill : SkillAsset, ITargetMapProvider
{
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
    [Tooltip("연막 타일에 서 있는 동안 보이는 적대감 배수")]
    public float visibilityHostilityFactor = 0.7f;

    [Header("Targeting")]
    public SkillTargetMode selectionMode = SkillTargetMode.Tile; // 타일 지목
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
            bm, map, cells, caster, durationCasterTurns, visibilityHostilityFactor,
            smokeVfxPrefab, vfxYOffset, vfxSortingLayer, vfxSortingOrder
        );

        if (mpCost > 0) caster.TryConsumeMP(mpCost);
        yield break;
    }

    public override IEnumerator ResolveOnUnit(BattleManager bm, BattleUnit caster, BattleUnit target)
    {
        if (!bm || !caster || target == null) yield break;
        var map = target.CurrentMap;
        if (!map) yield break;
        yield return ResolveOnTile(bm, map, target.Cell, caster);
    }

    void CreateSmokeZoneRuntime(BattleManager bm, Tilemap map, List<Vector3Int> cells, BattleUnit caster, int durationTurns, float factor,
        GameObject vfxPrefab, float yOffset, string sortingLayer, int sortingOrder)
    {
        var go = new GameObject("SmokeZoneRuntime");
        var comp = go.AddComponent<SmokeZoneRuntime>();
        go.transform.SetParent(bm.transform, false);
        comp.Initialize(bm, map, cells, caster, durationTurns, factor);
        comp.AttachVfx(vfxPrefab, yOffset, sortingLayer, sortingOrder,caster.team);
    }
}
