using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Tilemaps;
using static SmokeBombSkill;

public class SmokeZoneRuntime : MonoBehaviour
{
    // ==== 정적 레지스트리 ====
    static readonly List<SmokeZoneRuntime> Active = new List<SmokeZoneRuntime>();
    SmokeBombSkill.SmokeEffectMode mode = SmokeBombSkill.SmokeEffectMode.HostilityVisibility;
    UnitStateBuffId agiBuffState = UnitStateBuffId.None;
    float agiMul = 1f;

    [Header("Training Options")]
    public bool enableMpRegen = false;
    [Range(0f, 1f)] public float mpRegenRatio = 0f;  // 예: 0.3f = MaxMP * 0.3


    public static float GetVisibilityMultiplier(BattleUnit unit)
    {
        if (unit == null || unit.IsDead) return 1f;

        float m = 1f;
        // 중첩 존이 있다면 가장 강한(가장 작은) 배수를 적용
        foreach (var z in Active)
        {
            if (z == null || z.map == null) continue;
            if (unit.CurrentMap != z.map) continue;
            if (!z.cells.Contains(unit.Cell)) continue;
            m = Mathf.Min(m, z.factor);
        }
        return m;
    }
    public void SetEffectMode(SmokeBombSkill.SmokeEffectMode m, UnitStateBuffId stateForAgi, float mul)
    {
        mode = m;
        agiBuffState = stateForAgi;
        agiMul = mul;
    }

    // ==== 인스턴스 ====
    BattleManager battleManager;
    Tilemap map;
    HashSet<Vector3Int> cells;
    BattleUnit caster;   // 시전자
    int turnsLeft;       // 시전자 턴 기준
    float factor;        // 0.7 등

    // ==== VFX 관련 ====
    Transform vfxRoot;
    readonly List<GameObject> vfxObjects = new List<GameObject>();

    public void Initialize(BattleManager _battlemanager, Tilemap map, IEnumerable<Vector3Int> areaCells, BattleUnit caster, int durationCasterTurns, float visibilityFactor)
    {
        this.battleManager = _battlemanager;
        this.map = map;
        this.cells = new HashSet<Vector3Int>(areaCells);
        this.caster = caster;
        this.turnsLeft = Mathf.Max(1, durationCasterTurns);
        this.factor = Mathf.Clamp(visibilityFactor, 0f, 1f);


        if (_battlemanager != null) _battlemanager.OnUnitEndTurn += HandleUnitEndTurn;

        // 턴 시작 이벤트 구독
        BattleManager.OnAnyUnitTurnStarted += HandleUnitTurnStart;

        Active.Add(this);
    }

    StateStatModifierDB ResolveDB(BattleUnit _battleunit)
    {
        if (_battleunit != null) return _battleunit.stateStatDB;
        if(caster != null && caster.stateStatDB != null) return caster.stateStatDB;
        return null;    // 없으면 배수만 로그
    }

    void OnDestroy()
    {
        if (battleManager != null)
        {
            battleManager.OnUnitEndTurn -= HandleUnitEndTurn;
            BattleManager.OnAnyUnitTurnStarted -= HandleUnitTurnStart;
        }
            
        Active.Remove(this);

        // AGI 버프 모드일 때 남아있는 유닛에서 정리
        if (mode == SmokeEffectMode.AgilityBuff && agiBuffState != UnitStateBuffId.None)
        {
            foreach (var u in inside)
            {
                var usc = u ? u.GetComponent<UnitStateController>() : null;
                if (usc != null) usc.RemoveBuff(agiBuffState);
            }
            inside.Clear();
        }

        CleanupPlayableGraphs();

        // VFX 정리
        if (vfxObjects.Count > 0)
        {
            foreach (var go in vfxObjects)
                if (go) Destroy(go);
            vfxObjects.Clear();
        }
        if (vfxRoot) Destroy(vfxRoot.gameObject);
    }
    public void AttachVfx(GameObject prefab, float yOffset, string sortingLayer, int sortingOrder,Team team)
    {
        // 프리팹 없으면 시각화 스킵
        if (prefab == null || map == null || cells == null || cells.Count == 0) return;

        vfxRoot = new GameObject("VFX").transform;
        vfxRoot.SetParent(transform, false);

        foreach (var cell in cells)
        {
            var w = map.GetCellCenterWorld(cell);
            w.y += yOffset; // 살짝 띄워서 가려짐 방지

            var inst = Instantiate(prefab, w, Quaternion.identity, vfxRoot);

            // 모든 하위 Renderer의 정렬/색 적용
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
                // 머티리얼 기반 컬러가 필요하면 r.material.color = color; (공유 머티리얼 주의)
            }

            vfxObjects.Add(inst);
        }
    }
    void CleanupPlayableGraphs()
    {
        if (vfxRoot == null) return;

        // Timeline (PlayableDirector) 정리
        foreach (var dir in vfxRoot.GetComponentsInChildren<PlayableDirector>(true))
        {
            try
            {
                dir.Stop();
                var g = dir.playableGraph;
                if (g.IsValid()) g.Destroy();
            }
            catch { /* ignore */ }
        }

        // Animator에 Playables가 물려있다면 정리
        foreach (var anim in vfxRoot.GetComponentsInChildren<Animator>(true))
        {
            try
            {
                var g = anim.playableGraph;
                if (g.IsValid()) g.Destroy();
            }
            catch { /* ignore */ }
        }
    }

    void OnDisable()
    {
        // 도메인 리로드/씬 전환 시에도 안전하게 정리
        CleanupPlayableGraphs();
    }
    void HandleUnitTurnStart(BattleUnit unit)
    {
        // 시전자의 턴 시작 기준으로만 지속턴 감소
        if (unit == caster)
        {
            turnsLeft--;
            if (turnsLeft <= 0)
            {
                // 2번째 턴 시작 시(예: durationCasterTurns=2) 바로 삭제
                Destroy(gameObject);
            }
        }
    }
    void HandleUnitEndTurn(BattleUnit unit)
    {
        // 연막존 안에서 턴을 끝낸 아군의 MP 회복
        if (enableMpRegen && unit != null && unit.team == caster.team)
        {
            if (map != null && cells != null && cells.Contains(unit.Cell))
            {
                float ratio = Mathf.Clamp01(mpRegenRatio); // 보통 0.3
                                                           // MaxMP 프로퍼티가 있다고 가정 (없다면 유사 필드 사용)
                int amount = Mathf.FloorToInt(unit.MaxMP * ratio);
                if (amount > 0)
                {
                    unit.GainMP(amount);
                    Debug.Log($"[Smoke MP Regen] {unit.name} +{amount} MP  (ratio={ratio})");
                }
            }
        }
    }

    public static float GetHostilityGenerationMultiplier(BattleUnit unit)
    {
        // 기본은 1
        if (!unit) return 1f;

        float mult = 1f;

        foreach (var z in Active)   // 이미 가지고 있는 active 리스트
        {
            if (z == null) continue;
            if (z.mode != SmokeEffectMode.HostilityVisibility) continue;
            if (z.map != unit.CurrentMap) continue;

            // 연막 안에 있을 때만
            if (z.cells.Contains(unit.Cell))
            {
                mult *= z.factor;  // 인스펙터에서 0.7로 세팅해 둔 값
            }
        }

        return mult;
    }

    // Route2에서 범위 바꿀 때 사용할 헬퍼
    public void OverrideAreaCells(IEnumerable<Vector3Int> _newCells)
    {
        if (_newCells == null) return;
        cells = new HashSet<Vector3Int>(_newCells);
    }


    // “특정 존 제외” 버전 (before/after 계산용)
    static float GetVisibilityMultiplierExcluding(SmokeZoneRuntime exclude, BattleUnit unit)
    {
        if (unit == null || unit.IsDead) return 1f;
        float m = 1f;
        foreach (var z in Active)
        {
            if (z == null || z == exclude) continue;
            if (z.map == null) continue;
            if (unit.CurrentMap != z.map) continue;
            if (!z.cells.Contains(unit.Cell)) continue;
            m = Mathf.Min(m, z.factor);
        }
        return m;
    }
    // 디버그 로그 on/off
    public bool debugLogTransitions = true;

    // 현재 존 내부에 있는 유닛 스냅샷
    readonly HashSet<BattleUnit> inside = new HashSet<BattleUnit>();

    // 출입 감지 & 디버그 로그
    void Update()
    {
        if (!debugLogTransitions) return;
        if (battleManager == null || map == null || cells == null || cells.Count == 0) return;

        // 현재 존 영역 위의 유닛 수집
        var nowSet = new HashSet<BattleUnit>();
        var units = battleManager.GetUnitsInArea(map, cells); // 존 영역에서 유닛 목록 가져오기
        foreach (var u in units)
        {
            if (u == null || u.IsDead) continue;
            if (u.CurrentMap != map) continue;
            if (!cells.Contains(u.Cell)) continue;
            nowSet.Add(u);
        }

        // Enter: nowSet - inside
        foreach (var _unit in nowSet)
        {
            if (inside.Contains(_unit)) continue;

            if (mode == SmokeEffectMode.HostilityVisibility)
            {
                float baseMult = GetVisibilityMultiplierExcluding(this, _unit); // 연막 '들어가기 전' (이 존 제외)
                float afterMult = Mathf.Min(baseMult, this.factor);         // 연막 '들어간 후' (이 존 포함)
                float raw = _unit.Hostility;
                float beforeVis = raw * baseMult;
                float afterVis = raw * afterMult;
                Debug.Log($"[SmokeZone ENTER] {_unit.name}  Hostility raw={raw:0.###}  before={beforeVis:0.###} (x{baseMult:0.###})  after={afterVis:0.###} (x{afterMult:0.###})");
            }
            else // AgilityBuff
            {
                UnitStateController unitstatecontroller = _unit.GetComponent<UnitStateController>();
                if (unitstatecontroller != null && agiBuffState != UnitStateBuffId.None)
                {
                    StateStatModifierDB database = ResolveDB(_unit); // 유닛 기준 DB
                    if (database != null)
                    {
                        var baseAGI = _unit.AGI;
                        var beforeMul = database.ComputeMultipliers(unitstatecontroller).agi;
                        var beforeAGI = baseAGI * beforeMul;

                        unitstatecontroller.ApplyBuff(agiBuffState);

                        var afterMul = database.ComputeMultipliers(unitstatecontroller).agi;
                        var afterAGI = baseAGI * afterMul;

                        Debug.Log($"[Smoke AGI ENTER] {_unit.name}  agiMul {beforeMul:0.###}→{afterMul:0.###}  " +
                                  $"AGI {beforeAGI:0.###}→{afterAGI:0.###}  (zone x{agiMul:0.###})");
                    }
                    else
                    {
                        // DB가 없으면 배수만(상대지표) 출력
                        Debug.Log($"[Smoke AGI ENTER] {_unit.name}  agiMul before x1.000 → after x{agiMul:0.###}  (zone)");
                        unitstatecontroller.ApplyBuff(agiBuffState);
                    }
                }
            }

        }


        // Exit: inside - nowSet
        foreach (var _unit in inside)
        {
            if (nowSet.Contains(_unit)) continue;   // 여전히 안에 있는 유닛은 스킵

            if (mode == SmokeEffectMode.HostilityVisibility)
            {
                float withThis = Mathf.Min(GetVisibilityMultiplierExcluding(this, _unit), this.factor);
                float afterOut = GetVisibilityMultiplierExcluding(this, _unit);
                float raw = _unit.Hostility;
                Debug.Log($"[Smoke EXIT ] {_unit.name} Hostility before={raw * withThis:0.###}→after={raw * afterOut:0.###}");
            }
            else // AgilityBuff
            {
                UnitStateController unitstatecontroller = _unit.GetComponent<UnitStateController>();
                if (unitstatecontroller != null && agiBuffState != UnitStateBuffId.None)
                {
                    StateStatModifierDB database = ResolveDB(_unit);
                    if (database != null)
                    {
                        var baseAGI = _unit.AGI;
                        var beforeMul = database.ComputeMultipliers(unitstatecontroller).agi;
                        var beforeAGI = baseAGI * beforeMul;

                        unitstatecontroller.RemoveBuff(agiBuffState);

                        var afterMul = database.ComputeMultipliers(unitstatecontroller).agi;
                        var afterAGI = baseAGI * afterMul;

                        Debug.Log($"[Smoke AGI EXIT ] {_unit.name}  agiMul {beforeMul:0.###}→{afterMul:0.###}  " +
                                  $"AGI {beforeAGI:0.###}→{afterAGI:0.###}");
                    }
                    else
                    {
                        Debug.Log($"[Smoke AGI EXIT ] {_unit.name}  agiMul before x{agiMul:0.###} → after x1.000  (zone)");
                        unitstatecontroller.RemoveBuff(agiBuffState);
                    }
                }
            }
        }

        // 스냅샷 갱신
        inside.Clear();
        foreach (var u in nowSet) inside.Add(u);
    }
}
