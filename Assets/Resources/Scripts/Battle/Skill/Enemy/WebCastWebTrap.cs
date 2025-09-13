// Assets/Scripts/Skills/EA_WebCastWebTrap.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "Battle/SkillAsset/Enemy/WebCast")]
public class WebCastWebTrap : SkillAsset
{
    [Header("Web Trap")]
    public ProjectileController projectilePrefab;   // 스킬 전용 투사체 지정
    public float projectileSpeed = 3f;
    public WebTrapController trapPrefab; // 프리팹 필요(간단한 스프라이트/빈 오브젝트여도 OK)
    public string previewTagText = "WEB-CAST"; // 상태패널 등 라벨용(선택)

#if UNITY_EDITOR
    void OnValidate() { targetMode = SkillTargetMode.Unit; }  // 에디터에서 항상 Unit로 고정
#endif

    public override IEnumerable<Vector3Int> GetAreaCells(Vector3Int originCell, bool isOddRow)
    {
        yield return originCell; // 캐스팅/설치 지점 1셀
    }

    // 유닛 대상으로 “캐스팅 예정” 등록
    public override IEnumerator ResolveOnUnit(BattleManager bm, BattleUnit caster, BattleUnit target)
    {
        if (bm == null || caster == null || target == null || target.IsDead) yield break;

        // 현재 타겟의 셀을 기록(시점 고정)
        Tilemap map = target.CurrentMap;
        Vector3Int cell = target.Cell;

        // 캐스팅 상태 진입(소유 적의 다음 턴 시작에 생성)
        var cast = caster.GetComponent<EnemyCastState>();
        if (cast == null) cast = caster.gameObject.AddComponent<EnemyCastState>();
        cast.BeginCasting(new EnemyCastState.PendingCast
        {
            owner = caster,
            bm = bm,
            map = map,
            cell = cell,
            trapPrefab = trapPrefab,
            projectilePrefab = projectilePrefab,
            projectileSpeed = projectileSpeed
        });

        // 캐스팅 제스처(원거리 포즈 등)
        caster.SetCasting(true);


        yield break;
    }

    //타일 대상으로도 바로 캐스팅 가능하도록(no-op 아님)
    public override IEnumerator ResolveOnTile(BattleManager bm, Tilemap map, Vector3Int originCell, BattleUnit caster)
    {
        //if (bm == null || caster == null || map == null) yield break;

        //bm.ShowSkillPreview(map, new[] { originCell });

        //var cast = caster.GetComponent<EnemyCastState>();
        //if (cast == null) cast = caster.gameObject.AddComponent<EnemyCastState>();
        //cast.BeginCasting(new EnemyCastState.PendingCast
        //{
        //    owner = caster,
        //    bm = bm,
        //    map = map,
        //    cell = originCell,
        //    trapPrefab = trapPrefab,
        //    projectilePrefab = projectilePrefab,
        //    projectileSpeed = projectileSpeed
        //});

        //yield return caster.AnimateRanged();
        yield return null;
    }
}
