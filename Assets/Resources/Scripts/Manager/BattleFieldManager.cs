using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

public class BattleFieldManager : MonoBehaviour
{
    private BattleManager battleManager;

    [System.Serializable]
    public class BeastDomainZone
    {
        public BattleUnit owner;
        public Tilemap map;
        public Vector3Int center;
        public int radius;
        public int remainingTurns;
        public int highlightToken;
    }

    [System.Serializable]
    public class StatusTileZone
    {
        public BattleUnit owner;
        public Tilemap map;
        public Vector3Int cell;
        public int remainingTurns;
        public TileBase originalTile;
        public StatusId effectStatusId;
        public int effectStack;
        public int effectDuration;
    }

    List<StatusTileZone> _statusTileZones = new List<StatusTileZone>();
    List<BeastDomainZone> _beastZones = new List<BeastDomainZone>();

    public void Initialize(BattleManager _battlemanager)
    {
        this.battleManager = _battlemanager;
    }

    // === 턴 시작 시 업데이트 (BM이 호출) ===
    public void OnTurnStart(BattleUnit unit)
    {
        TickBeastDomainOnTurnStart(unit);
        TickStatusTileZonesOnTurnStart(unit);
    }

    // === 턴 종료 시 체크 (BM이 호출) ===
    public void OnTurnEnd(BattleUnit unit)
    {
        CheckStatusTileZoneEffect(unit);
    }

    #region Beast Domain Logic
    public void SpawnBeastDomainZone(Tilemap map, BattleUnit owner, Vector3Int centerCell, int radius, int durationTurns)
    {
        if (!owner || !map) return;

        // 기존 영역 제거 (한 유닛당 하나만 유지한다고 가정)
        for (int i = _beastZones.Count - 1; i >= 0; i--)
        {
            var old = _beastZones[i];
            if (old.owner != owner) continue;
            // 기존 하이라이트 제거
            if (old.highlightToken != 0 && battleManager.inputHandler.beastDomainHighlighter != null)
                battleManager.inputHandler.beastDomainHighlighter.ClearGroup(old.highlightToken);
            _beastZones.RemoveAt(i);
        }

        // 범위 계산
        var cells = new List<Vector3Int>();
        foreach (var c in AreaShapes.BeastDomainArea(centerCell, radius)) cells.Add(c);

        // 하이라이트 생성 (InputHandler 사용)
        int token = 0;
        if (battleManager.inputHandler.beastDomainHighlighter != null)
        {
            token = battleManager.inputHandler.beastDomainHighlighter.CreateGroup();
            battleManager.inputHandler.beastDomainHighlighter.SetGroupCells(token, map, cells);
        }

        var zone = new BeastDomainZone
        {
            owner = owner,
            map = map,
            center = centerCell,
            radius = radius,
            remainingTurns = durationTurns,
            highlightToken = token,
        };
        _beastZones.Add(zone);

        Debug.Log($"[BattleField] {owner.name} 야수의 영역 생성 - token:{token}");
    }

    private void TickBeastDomainOnTurnStart(BattleUnit unitWhoseTurnStarted)
    {
        if (unitWhoseTurnStarted == null) return;

        for (int i = _beastZones.Count - 1; i >= 0; i--)
        {
            var z = _beastZones[i];
            if (z.owner != unitWhoseTurnStarted) continue;

            TryApplyBeastDomainRageTraining(z.owner);
            z.remainingTurns--;

            if (z.remainingTurns <= 0)
            {
                if (z.highlightToken != 0 && battleManager.inputHandler.beastDomainHighlighter != null)
                    battleManager.inputHandler.beastDomainHighlighter.ClearGroup(z.highlightToken);

                _beastZones.RemoveAt(i);
            }
        }
    }

    private void TryApplyBeastDomainRageTraining(BattleUnit owner)
    {
        if (owner == null || owner.data == null || owner.data.skills == null) return;

        // 스킬셋에서 패시브 정보 찾기
        SelfBeastDomainSkill domainSkill = null;
        foreach (var s in owner.data.skills)
        {
            domainSkill = s as SelfBeastDomainSkill;
            if (domainSkill != null) break;
        }
        if (domainSkill == null) return;

        int route = owner.GetTrainingRouteIndex(domainSkill);
        if (!domainSkill.trainingReduceRageOnTurnStart || domainSkill.routeForRageReduceOnTurnStart < 0 || route != domainSkill.routeForRageReduceOnTurnStart) return;

        float amount = owner.MagicDamage * domainSkill.rageReducePerClv;
        if (amount <= 0f) return;

        owner.AddRage(-amount);
    }

    public bool IsBeastDomainFreeMove(BattleUnit unit, Tilemap map, Vector3Int fromCell, Vector3Int toCell)
    {
        if (unit == null || map == null) return false;
        foreach (var z in _beastZones)
        {
            if (z.owner != unit) continue;
            if (z.map != map) continue; // 맵 다르면 무효

            // HexDistance 계산 (BM에 있던 거 가져오거나 Util로 빼야 함. 여기선 로컬로 구현)
            bool fromIn = HexDistance(z.center, fromCell) <= z.radius;
            bool toIn = HexDistance(z.center, toCell) <= z.radius;
            if (fromIn && toIn) return true;
        }
        return false;
    }

    private int HexDistance(Vector3Int a, Vector3Int b)
    {
        var axA = SkillLibrary.OffsetToAxial(a);
        var axB = SkillLibrary.OffsetToAxial(b);
        int dq = Mathf.Abs(axA.x - axB.x);
        int dr = Mathf.Abs(axA.y - axB.y);
        int ds = Mathf.Abs((-axA.x - axA.y) - (-axB.x - axB.y));
        return (dq + dr + ds) / 2;
    }
    #endregion

    #region Status Tile Logic
    public void CreateStatusTileZone(BattleUnit owner, Tilemap map, Vector3Int cell, int zoneDuration, TileBase newTileBase, StatusId statusId, int stack = 1, int statusDuration = 3)
    {
        if (!map.HasTile(cell)) return;

        var existing = _statusTileZones.FirstOrDefault(z => z.map == map && z.cell == cell);
        if (existing != null)
        {
            existing.owner = owner;
            existing.remainingTurns = zoneDuration;
            existing.effectStatusId = statusId;
            existing.effectStack = stack;
            existing.effectDuration = statusDuration;
            if (newTileBase != null) map.SetTile(cell, newTileBase);
            return;
        }

        TileBase oldTile = map.GetTile(cell);
        if (newTileBase != null) map.SetTile(cell, newTileBase);

        var newZone = new StatusTileZone
        {
            owner = owner,
            map = map,
            cell = cell,
            remainingTurns = zoneDuration,
            originalTile = oldTile,
            effectStatusId = statusId,
            effectStack = stack,
            effectDuration = statusDuration
        };
        _statusTileZones.Add(newZone);
    }

    private void TickStatusTileZonesOnTurnStart(BattleUnit unit)
    {
        if (unit == null) return;

        for (int i = _statusTileZones.Count - 1; i >= 0; i--)
        {
            var z = _statusTileZones[i];
            if (z.owner == unit)
            {
                z.remainingTurns--;
                if (z.remainingTurns <= 0)
                {
                    if (z.map != null) z.map.SetTile(z.cell, z.originalTile);
                    _statusTileZones.RemoveAt(i);
                }
            }
        }
    }

    private void CheckStatusTileZoneEffect(BattleUnit unit)
    {
        if (unit == null || unit.IsDead) return;

        foreach (var zone in _statusTileZones)
        {
            if (zone.map == unit.CurrentMap && zone.cell == unit.Cell)
            {
                var sc = unit.GetComponent<StatusController>();
                if (sc != null)
                {
                    sc.ApplyWithTurnContext(zone.effectStatusId, zone.effectStack, zone.effectDuration);
                }
                break;
            }
        }
    }
    #endregion


}
