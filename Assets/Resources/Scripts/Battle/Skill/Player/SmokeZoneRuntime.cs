using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Tilemaps;
using UnityEngine.tvOS;
using static SmokeBombSkill;

public class SmokeZoneRuntime : MonoBehaviour
{
    // ==== 정적 레지스트리 ====
    static readonly List<SmokeZoneRuntime> Active = new List<SmokeZoneRuntime>();

    SmokeBombSkill.SmokeEffectMode mode = SmokeBombSkill.SmokeEffectMode.AgilityBuff;

    // 모드별 파라미터
    UnitStateBuffId smokeHiddenBuffState = UnitStateBuffId.SmokeHidden;
    UnitStateBuffId agiBuffState = UnitStateBuffId.None;
    float agiMul = 1f;

    [Header("Training Options")]
    public bool enableMpRegen = false;
    [Range(0f, 1f)] public float mpRegenRatio = 0f;


    // ==== 인스턴스 ====
    BattleManager battleManager;
    Tilemap map;
    HashSet<Vector3Int> cells;
    BattleUnit caster;
    int turnsLeft;

    // ==== VFX 관련 ====
    Transform vfxRoot;
    readonly List<GameObject> vfxObjects = new List<GameObject>();

    // 현재 존 내부 유닛
    readonly HashSet<BattleUnit> inside = new HashSet<BattleUnit>();

    // 디버그 로그 on/off (효과 적용과는 무관)
    public bool debugLogTransitions = true;

    public void SetEffectMode(
        SmokeBombSkill.SmokeEffectMode m,
        UnitStateBuffId hiddenBuff,
        UnitStateBuffId stateForAgi,
        float mul)
    {
        mode = m;
        smokeHiddenBuffState = hiddenBuff;
        agiBuffState = stateForAgi;
        agiMul = mul;
    }

    public void Initialize(
        BattleManager _battlemanager,
        Tilemap map,
        IEnumerable<Vector3Int> areaCells,
        BattleUnit caster,
        int durationCasterTurns)
    {
        this.battleManager = _battlemanager;
        this.map = map;
        this.cells = new HashSet<Vector3Int>(areaCells);
        this.caster = caster;
        this.turnsLeft = Mathf.Max(1, durationCasterTurns);

        if (_battlemanager != null) _battlemanager.OnUnitEndTurn += HandleUnitEndTurn;
        BattleManager.OnAnyUnitTurnStarted += HandleUnitTurnStart;

        Active.Add(this);
    }

    public void OverrideAreaCells(IEnumerable<Vector3Int> _newCells)
    {
        if (_newCells == null) return;
        cells = new HashSet<Vector3Int>(_newCells);
    }

    void HandleUnitTurnStart(BattleUnit unit)
    {
        // 시전자의 턴 시작 기준 지속턴 감소
        if (unit == caster)
        {
            turnsLeft--;
            if (turnsLeft <= 0)
                Destroy(gameObject);
        }
    }

    void HandleUnitEndTurn(BattleUnit unit)
    {
        // 연막존 안에서 턴을 끝낸 아군의 MP 회복
        if (enableMpRegen && unit != null && caster != null && unit.team == caster.team)
        {
            if (map != null && cells != null && cells.Contains(unit.Cell))
            {
                float ratio = Mathf.Clamp01(mpRegenRatio);
                int amount = Mathf.FloorToInt(unit.MaxMP * ratio);
                if (amount > 0)
                {
                    unit.GainMP(amount);
                    if (debugLogTransitions)
                        Debug.Log($"[Smoke MP Regen] {unit.name} +{amount} MP (ratio={ratio})");
                }
            }
        }
    }

    void OnDestroy()
    {
        if (battleManager != null)
        {
            battleManager.OnUnitEndTurn -= HandleUnitEndTurn;
            BattleManager.OnAnyUnitTurnStarted -= HandleUnitTurnStart;
        }

        Active.Remove(this);

        // 남아있는 버프 정리
        foreach (var u in inside)
        {
            if (!u) continue;
            var usc = u.GetComponent<UnitStateController>();
            if (usc == null) continue;

            if (mode == SmokeEffectMode.AgilityBuff && agiBuffState != UnitStateBuffId.None)
                usc.RemoveBuff(agiBuffState);

            if (mode == SmokeEffectMode.SmokeHiddenBuff && smokeHiddenBuffState != UnitStateBuffId.None)
                usc.RemoveBuff(smokeHiddenBuffState);
        }
        inside.Clear();

        CleanupPlayableGraphs();

        if (vfxObjects.Count > 0)
        {
            foreach (var go in vfxObjects)
                if (go) Destroy(go);
            vfxObjects.Clear();
        }
        if (vfxRoot) Destroy(vfxRoot.gameObject);
    }

    public void AttachVfx(GameObject prefab, float yOffset, string sortingLayer, int sortingOrder, Team team)
    {
        if (prefab == null || map == null || cells == null || cells.Count == 0) return;

        vfxRoot = new GameObject("VFX").transform;
        vfxRoot.SetParent(transform, false);

        foreach (var cell in cells)
        {
            var w = map.GetCellCenterWorld(cell);
            w.y += yOffset;

            var inst = Instantiate(prefab, w, Quaternion.identity, vfxRoot);

            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                var psr = r as ParticleSystemRenderer;
                if (psr != null)
                {
                    psr.sortingLayerName = sortingLayer;
                    psr.sortingOrder = sortingOrder;
                }

                var sr = r as SpriteRenderer;
                if (sr != null)
                {
                    sr.sortingLayerName = sortingLayer;
                    sr.sortingOrder = sortingOrder;
                }
            }

            vfxObjects.Add(inst);
        }
    }

    void CleanupPlayableGraphs()
    {
        if (vfxRoot == null) return;

        foreach (var dir in vfxRoot.GetComponentsInChildren<PlayableDirector>(true))
        {
            try
            {
                dir.Stop();
                var g = dir.playableGraph;
                if (g.IsValid()) g.Destroy();
            }
            catch { }
        }

        foreach (var anim in vfxRoot.GetComponentsInChildren<Animator>(true))
        {
            try
            {
                var g = anim.playableGraph;
                if (g.IsValid()) g.Destroy();
            }
            catch { }
        }
    }

    void OnDisable()
    {
        CleanupPlayableGraphs();
    }

    void Update()
    {
        if (battleManager == null || map == null || cells == null || cells.Count == 0) return;

        // 현재 존 위 유닛 수집
        var nowSet = new HashSet<BattleUnit>();
        var units = battleManager.gridManager.GetUnitsInArea(map, cells);

        foreach (var u in units)
        {
            if (u == null || u.IsDead) continue;
            if (u.CurrentMap != map) continue;
            if (!cells.Contains(u.Cell)) continue;
            nowSet.Add(u);
        }

        // Enter
        foreach (var unit in nowSet)
        {
            if (inside.Contains(unit)) continue;

            var usc = unit.GetComponent<UnitStateController>();
            if (usc != null)
            {
                if (mode == SmokeEffectMode.SmokeHiddenBuff && smokeHiddenBuffState != UnitStateBuffId.None)
                {
                    usc.ApplyBuff(smokeHiddenBuffState);
                    if (debugLogTransitions)
                        Debug.Log($"[SmokeHidden ENTER] {unit.name} +{smokeHiddenBuffState}");
                }
                else if (mode == SmokeEffectMode.AgilityBuff && agiBuffState != UnitStateBuffId.None)
                {
                    float beforeAgi = unit.EffectiveAGI;
                    bool added = usc.ApplyBuff(agiBuffState);
                    float afterAgi = unit.EffectiveAGI;

                    if (debugLogTransitions)
                    {
                        Debug.Log(
                        $"[Smoke AGI ENTER] {unit.name} buff={agiBuffState} added={added} " +
                        $"AGI {beforeAgi:0.###} -> {afterAgi:0.###} " +
                        $"(zone agiMul={agiMul:0.###})"
                                                );
                    }
                }
            }
        }

        // Exit
        foreach (var unit in inside)
        {
            if (nowSet.Contains(unit)) continue;

            var usc = unit ? unit.GetComponent<UnitStateController>() : null;
            if (usc != null)
            {
                if (mode == SmokeEffectMode.SmokeHiddenBuff && smokeHiddenBuffState != UnitStateBuffId.None)
                {
                    usc.RemoveBuff(smokeHiddenBuffState);
                    if (debugLogTransitions)
                        Debug.Log($"[SmokeHidden EXIT ] {unit.name} -{smokeHiddenBuffState}");
                }
                else if (mode == SmokeEffectMode.AgilityBuff && agiBuffState != UnitStateBuffId.None)
                {
                    float beforeAgi = unit.EffectiveAGI;
                    bool removed = usc.RemoveBuff(agiBuffState);
                    float afterAgi = unit.EffectiveAGI;

                    if (debugLogTransitions)
                    {
                        Debug.Log(
                        $"[Smoke AGI EXIT ] {unit.name} buff={agiBuffState} removed={removed} " +
                        $"AGI {beforeAgi:0.###} -> {afterAgi:0.###}"
                                                );
                    }
                }
            }
        }

        inside.Clear();
        foreach (var u in nowSet) inside.Add(u);
    }
}
