// Assets/Scripts/Skills/EA_WebCastWebTrap.cs

using System.Collections;

using System.Collections.Generic;

using UnityEngine;

using UnityEngine.Tilemaps;



[CreateAssetMenu(menuName = "Battle/Skill/Enemy/WebCast")]

public class WebCastWebTrap : EnemySkill, IProjectileTileSkill

{

    [Header("Web Trap")]

    public ProjectileController projectilePrefab;   // 스킬 전용 투사체 지정

    public float projectileSpeed = 3f;

    public WebTrapController trapPrefab; // 프리팹 필요(간단한 스프라이트/빈 오브젝트여도 OK)

    public string previewTagText = "WEB-CAST"; // 상태패널 등 라벨용(선택)



    public ProjectileController GetProjectilePrefab(BattleUnit caster) => projectilePrefab;

    public float GetProjectileSpeed(BattleUnit caster) => projectileSpeed;



#if UNITY_EDITOR

    void OnValidate() { targetMode = SkillTargetMode.Unit; }  // 에디터에서 항상 Unit로 고정

#endif



    public override IEnumerable<Vector3Int> GetAreaCells(Vector3Int originCell, bool isOddRow)

    {

        yield return originCell; // 캐스팅/설치 지점 1셀

    }



    // 유닛 대상으로 “캐스팅 예정” 등록

    public override IEnumerator ResolveOnUnit(BattleManager _battlemanager, BattleUnit _caster, BattleUnit _target)

    {

        if (_battlemanager == null || _caster == null || _target == null || _target.IsDead) yield break;



        // 현재 타겟의 셀을 기록(시점 고정)

        Tilemap map = _target.CurrentMap;

        Vector3Int cell = _target.Cell;



        // 캐스팅 상태 진입(소유 적의 다음 턴 시작에 생성)

        var cast = _caster.GetComponent<EnemyCastState>();

        if (cast == null) cast = _caster.gameObject.AddComponent<EnemyCastState>();

        cast.BeginCasting(new EnemyCastState.PendingCast

        {

            owner = _caster,

            bm = _battlemanager,

            map = map,

            cell = cell,

            trapPrefab = trapPrefab,

            projectilePrefab = projectilePrefab,

            projectileSpeed = projectileSpeed,

            skillSO = this  //이번에 시전 중인 스킬 SO 지정

        });



        // 캐스팅 제스처(원거리 포즈 등)

        _caster.SetCasting(true);





        yield break;

    }



    //타일 대상으로도 바로 캐스팅 가능하도록(no-op 아님)

    public override IEnumerator ResolveOnTile(BattleManager bm, Tilemap map, Vector3Int originCell, BattleUnit caster)

    {

        yield return null;

    }

}

