// Assets/Scripts/Combat/WebTrapController.cs
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Linq;

public class WebTrapController : MonoBehaviour
{
    Tilemap _map;
    Vector3Int _cell;
    BattleUnit _owner; // 함정 소유자(적)

    bool _armed = false;
    static BattleUnit s_currentTurnUnit;

    public Tilemap Map => _map;
    public Vector3Int Cell => _cell;

    public void Init(Tilemap map, Vector3Int cell, BattleUnit owner)
    {
        _map = map;
        _cell = cell;
        _owner = owner;
        _armed = true;

        // 소유 적의 "다음 턴 시작"에 자동 만료
        BattleManager.OnAnyUnitTurnStarted += OnAnyTurnStarted;
    }

    void OnDestroy()
    {
        BattleManager.OnAnyUnitTurnStarted -= OnAnyTurnStarted;
    }

    void Update()
    {
        if (!_armed || _map == null) return;

        // 현재 셀을 밟고 있는 플레이어 수색(아무나 1명이라도)
        var players = FindObjectsOfType<BattleUnit>()
            .Where(u => u != null && u.team == Team.Player && !u.IsDead && u.CurrentMap == _map && u.Cell == _cell)
            .ToList();

        if (players.Count > 0)
        {
            var target = players[0];
            var sc = target.GetComponent<StatusController>();
            if (sc == null) sc = target.gameObject.AddComponent<StatusController>();

            sc.ApplyWithTurnContext(StatusId.Slow, 1, 1); // 둔화 1중첩, 1턴

            Destroy(gameObject); // 발동 후 제거
        }
    }

    void OnAnyTurnStarted(BattleUnit who)
    {
        s_currentTurnUnit = who;

        if (!_armed) return;
        if (_owner == null) { Destroy(gameObject); return; }

        // 소유 적 유닛의 다음 턴 시작에 자동 만료
        if (who == _owner)
        {
            Destroy(gameObject);
        }
    }

    public static void RemoveAt(Tilemap map, Vector3Int cell)
    {
        // 같은 타일에 이미 깔려 있는 거미줄 모두 제거
        var olds = FindObjectsOfType<WebTrapController>()
            .Where(t => t != null && t._map == map && t._cell == cell)
            .ToList();
        foreach (var ot in olds) UnityEngine.Object.Destroy(ot.gameObject);
    }
}
