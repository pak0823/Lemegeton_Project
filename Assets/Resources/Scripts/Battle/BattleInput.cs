using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class BattleInput : MonoBehaviour
{
    public Camera cam;
    public BattleManager battle;
    public LayerMask unitMask;
    IBattleMapProvider provider;

    void Awake()
    {
        if (cam == null) cam = Camera.main;
        provider = Shared.battleMapManager as IBattleMapProvider ?? FindObjectOfType<BattleMapManager>();
    }
    void Start()
    {
        if (provider == null)
            provider = Shared.battleMapManager as IBattleMapProvider ?? FindObjectOfType<BattleMapManager>();
    }

    void Update()
    {
        // 맵 준비 전이면 입력 무시
        if (provider == null || provider.PlayerFloor == null || provider.EnemyFloor == null)
            return;

        if (Input.GetMouseButtonDown(0))
        {
            Vector3 world = cam.ScreenToWorldPoint(Input.mousePosition);
            world.z = 0f;

            // 1) 유닛부터 체크 (단일 대상 클릭 우선)
            var hit = Physics2D.OverlapCircle(world, 0.15f, unitMask);
            if (hit && hit.TryGetComponent(out BattleUnit unit))
            {
                battle.OnUnitClicked(unit);
                return;
            }

            // 2) 타일 클릭 → 우선순위: 적맵(타게팅) → 아군맵(이동)
            if (TryCell(provider.EnemyFloor, world, out var enemyCell))
            {
                battle.OnTileClicked(provider.EnemyFloor, enemyCell);
                return;
            }
            if (TryCell(provider.PlayerFloor, world, out var playerCell))
            {
                battle.OnTileClicked(provider.PlayerFloor, playerCell);
                return;
            }
        }

        // 우클릭/ESC로 취소(선택사항)
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
        {
            battle.CancelCurrentAction();
        }
    }

    bool TryCell(Tilemap map, Vector3 world, out Vector3Int cell)
    {
        cell = map.WorldToCell(world);
        return map.cellBounds.Contains(cell) && map.HasTile(cell);
    }
}
