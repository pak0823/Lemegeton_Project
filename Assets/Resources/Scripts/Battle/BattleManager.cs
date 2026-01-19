using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum BattleState { Idle, ActionSelect, Moving, Targeting, Resolving, TargetingKnockback, EndTurn }
public enum BattleAction { Move, Attack, Rest, Calm }

public class BattleManager : MonoBehaviour
{
    #region Variables
    public BattleGridManager grid;
    UnitStatusPanelUI _statusPanel;
    public LayerMask unitMask;

    BattleState state = BattleState.Idle;
    BattleUnit acting;
    List<Vector3Int> moveOptions = new();

    // === 추가턴 설계 ===
    [SerializeField] int baseActionsPerTurn = 1; // 기본 행동 토큰(기본 1)
    int remainingActions = 0; // 남은 토큰
    readonly HashSet<BattleAction> usedActions = new(); // 이번 턴에 사용한 행동(중복 금지)

    [Header("Highlighters")]
    public Highlighter moveHighlighter;   // 이동 미리보기
    public Highlighter skillHighlighter;    // 스킬 범위 미리보기
    IBattleMapProvider provider;
    int _skillPreviewHold = 0;            // 웹 캐스팅 등 스킬 프리뷰 유지용

    bool initialized = false; // 중복 Init 방지
    public bool IsPlayerTurn => acting != null && acting.team == Team.Player;
    public bool IsTargeting => state == BattleState.Targeting;
    public bool IsKnockbackTargeting => state == BattleState.TargetingKnockback;
    public BattleUnit SelectedTarget => selectedTarget;
    Coroutine enemyRoutine; // 코루틴 핸들

    [Header("Modules")]
    [SerializeField] private BattleWaveManager waveManager;
    [SerializeField] public BattleSkillProcessor skillProcessor;
    public ATBTurnController turnController;


    // [프로퍼티 연결]
    public int CurrentWave => waveManager.CurrentWave;
    public int TotalWaves => waveManager.TotalWaves;


    public event System.Action<int, int, string> OnWaveChanged; // (cur,total,label)
    public event System.Action OnWaveStarted;   // 웨이브 시작 알림
    public event System.Action<int, int> OnWaveTransition; // 다음 웨이브 전환 안내 (next,total)
    public event System.Action<BattleUnit, string> OnUnitPassiveLabel;
    private bool _battleEndedOnce = false; // 중복 승리 처리 방지


    // === 타겟 선택(표시/순환) ===
    [Header("Targeting")]
    public TargetMarker targetMarker; // 인스펙터에 배치한 TargetMarker 할당
    List<BattleUnit> targetCycle = new(); // 적 리스트(AGI desc)
    int targetIndex = -1; // 현재 인덱스
    BattleUnit selectedTarget; // 현재 선택된 대상
    private Tilemap currentSkillTargetMap;  //스킬이 지정한 맵
    public Tilemap CurrentSkillTargetMap => currentSkillTargetMap;
    private Tilemap customPreviewMap;
    private HashSet<Vector3Int> customPreviewCells;
    public BattleUnit ActingUnit => acting;
    bool _skillConfirmLocked = false;


    // ATB UI 업데이트용 이벤트
    public delegate void OnATBChangedDelegate(BattleUnit unit, float currentATB, float maxATB);
    public event OnATBChangedDelegate OnATBChanged;
    readonly System.Random rng = new System.Random();// 소난수 발생기
    public static event System.Action<BattleUnit> OnAnyUnitTurnStarted;

    public event System.Action OnATBReset; // 턴바 강제 초기화 신호
    public event System.Action<BattleUnit> OnUnitEndTurn;

    [Header("Skill Runtime")]
    public bool isSelectingSkill = false;          // 스킬 선택 패널이 열렸는지
    public SkillDefinition currentSkill;           // 현재 선택된 스킬(선택 전이면 id 미정)
    public Vector3Int selectedCell;                // 타일 스킬용 내부 커서
    public SkillAsset currentSkillSO;                   // 현재 선택된 SO 스킬
    public event System.Action<SkillAsset[]> OnSkillPanelPopulateSO; // SO 목록 UI용

    // UI와 통신용 이벤트
    public event System.Action<bool> OnSkillPanelToggled;  // true=열기/false=닫기
    public event System.Action<string> OnHint;   // UI에 간단한 안내 문구 전달                  
    public event System.Action<BattleUnit> OnUnitTurnLabel;// 유닛 턴 시작 라벨용
    public event System.Action<BattleUnit, string> OnUnitActionLabel; //유닛 스킬 표시용

    //Event 호출
    public event System.Action<BattleUnit> OnOverworkTriggered; // 과로(재행동) 발동 알림 이벤트

    //skill extra move
    bool _isPostSkillMoveInProgress = false;
    private int _reactionLocks = 0; // 실행 중인 리액션 개수

    [Header("DBs")]
    [SerializeField] private StateStatModifierDB stateStatDb;


    [Header("Training")]
    public TrainingDB trainingDB;   // 인스펙터로 TrainingDB 할당
    public TrainingDB Training => trainingDB;


    ////점프 애니메이션 속도 및 높이 값
    //float jumpDuration = 0.08f;     // 시간 기반
    //float jumpArc = 0.2f;

    #endregion

    public static void ClearStatic()
    {
        OnAnyUnitTurnStarted = null;
    }
    public void RegisterReactionLock()
    {
        _reactionLocks++;
    }

    public void UnregisterReactionLock()
    {
        _reactionLocks = Mathf.Max(0, _reactionLocks - 1);
    }

    public void EmitActionLabel(BattleUnit u, string label) => OnUnitActionLabel?.Invoke(u, label);
    public void EmitPassiveLabel(BattleUnit u, string label) => OnUnitPassiveLabel?.Invoke(u, label);
    public void EmitTurnLabel(BattleUnit u) => OnUnitTurnLabel?.Invoke(u);


    UnitStatusPanelUI StatusPanel
    {
        get
        {
            if (_statusPanel == null) _statusPanel = FindObjectOfType<UnitStatusPanelUI>();
            return _statusPanel;
        }
    }

    // 야수의 영역 지대 관리용 클래스
    [System.Serializable]
    public class BeastDomainZone
    {
        public BattleUnit owner;      // 영역을 만든 유닛
        public Tilemap map;          // 영역이 올라간 타일맵
        public Vector3Int center;    // 생성 시 중심 셀
        public int radius;           // 헥사 거리 반경
        public int remainingTurns;   // 남은 턴 (owner의 차례 기준)

        // 이 영역을 표현하는 Highlighter 그룹 토큰(임시)
        public int highlightToken;
    }
    List<BeastDomainZone> _beastZones = new List<BeastDomainZone>();

    // 중독 지대 관리용 클래스
    [System.Serializable]
    public class StatusTileZone
    {
        public BattleUnit owner;        // 생성자
        public Tilemap map;             // 타일맵
        public Vector3Int cell;         // 위치
        public int remainingTurns;      // 지대 유지 턴 수
        public TileBase originalTile;   // 복구할 원래 타일

        // 이 지대가 부여할 상태 정보
        public StatusId effectStatusId; // 부여할 상태 (예: Poisoning, Ignition)
        public int effectStack;         // 부여할 스택 (예: 1)
        public int effectDuration;      // 부여된 상태의 지속 턴 (예: 3)
    }
    List<StatusTileZone> _statusTileZones = new List<StatusTileZone>();
    #region Unity Callbacks
    void Awake()
    {
        provider = Shared.battleMapManager as IBattleMapProvider ?? FindObjectOfType<BattleMapManager>();
        if (provider != null) provider.OnMapsReady += Init;
        else { Debug.LogWarning("[BattleManager] BattleMapManager not ready in Awake. Will retry in Start."); }
        if (Shared.BattleManager == null) Shared.BattleManager = this;

        if (!turnController) turnController = FindObjectOfType<ATBTurnController>();
        turnController.OnTurnReady += HandleTurnReady;

        // 웨이브 매니저 초기화 및 이벤트 연결
        if (waveManager == null) waveManager = GetComponentInChildren<BattleWaveManager>();
        waveManager.OnWaveLoaded += HandleWaveLoaded; // 중요
        waveManager.OnWaveInfoUpdated += (cur, tot, lbl) => OnWaveChanged?.Invoke(cur, tot, lbl); // UI 중계
        waveManager.OnWaveTransitionStarted += (next, tot) => OnWaveTransition?.Invoke(next, tot); // UI 중계
        waveManager.OnAllWavesCleared += HandleVictory; // 승리 처리

        waveManager.Initialize(); // AutoResolve 등 수행

        if (skillProcessor == null) skillProcessor = GetComponentInChildren<BattleSkillProcessor>();

        if (skillProcessor != null)
        {
            skillProcessor.Initialize(this);
        }
        else
        {
            Debug.LogError("[BattleManager] BattleSkillProcessor가 없습니다! 인스펙터나 자식 오브젝트를 확인하세요.");
        }
    }

    void Start()
    {
        if (provider == null)
        {
            provider = Shared.battleMapManager as IBattleMapProvider ?? FindObjectOfType<BattleMapManager>();
            if (provider != null) provider.OnMapsReady += Init;
        }

        // 플레이어 유닛 스폰
        SpawnPlayerUnits();

        if (!initialized)
        {
            // 직접 LoadWave(0) 하지 말고, 웨이브 매니저에게 시킴
            waveManager.StartFirstWave();
        }

        StartCoroutine(Co_RebindBattleInputWhenMapsReady());

        // Input 이벤트 구독
        var input = GetComponent<BattleInput>(); // 또는 FindObject
        if (input != null)
        {
            // 기존 구독 해제 (안전장치)
            input.OnUnitClick -= HandleUnitClick;
            input.OnTileClick -= HandleTileClick;
            input.OnCancelKeyPress -= CancelCurrentAction;
            input.OnUnitHover -= HandleUnitHover;
            input.OnTileHover -= HandleTileHover;

            input.OnUnitClick += HandleUnitClick;
            input.OnTileClick += HandleTileClick;
            input.OnCancelKeyPress += CancelCurrentAction;
            input.OnUnitHover += HandleUnitHover; // (Visual 분리 때 구현)
            input.OnTileHover += HandleTileHover; // (Visual 분리 때 구현)
        }
    }

    // 타겟 유효성 검사 헬퍼 함수
    bool IsValidSkillTarget(BattleUnit target)
    {
        if (target == null || acting == null || currentSkillSO == null) return false;

        // 부활(Revive) 스킬인 경우 죽은 유닛도 타겟팅 허용
        bool isReviveSkill = false;
        if (currentSkillSO is ParametricSupportSkill supportSkill)
        {
            if (supportSkill.mode == SupportSkillMode.Revive)
                isReviveSkill = true;
        }
        // 부활 스킬이 아니면 죽은 유닛은 선택 불가
        if (!isReviveSkill && target.IsDead) return false;

        // 타겟팅 규칙 확인
        switch (currentSkillSO.targetAlignment)
        {
            case SkillTargetAlignment.Enemy:
                return acting.team != target.team; // 팀이 달라야 함

            case SkillTargetAlignment.Ally:
                return acting.team == target.team; // 팀이 같아야 함

            case SkillTargetAlignment.Self:
                return acting == target;           // 나 자신이어야 함

            case SkillTargetAlignment.Any:
                return true;                       // 아무나 OK

            default:
                return false;
        }
    }

    // 유닛 클릭 처리
    void HandleUnitClick(BattleUnit unit)
    {
        if (state == BattleState.Resolving) return; // 실행 중엔 무시

        // 타일 타겟팅 모드일 때 유닛을 클릭하면, 그 유닛의 위치(Tile)를 클릭한 것으로 처리
        if (currentSkillSO != null && currentSkillSO.targetMode == SkillTargetMode.Tile)
        {
            // 유닛이 있는 맵과 셀 좌표를 넘겨줌
            ConfirmSkillOnTile(unit.CurrentMap, unit.Cell);
            return;
        }

        // 스킬 타겟팅 중이라면
        if (currentSkillSO != null && currentSkillSO.targetMode == SkillTargetMode.Unit)
        {
            if (IsValidSkillTarget(unit))
            {
                ConfirmSkillOnUnit(unit);
            }
            else
            {
                Debug.Log("유효하지 않은 대상입니다.");
            }
            return;
        }

        // 그 외: 유닛 정보 보기 or 선택
        OnUnitClicked(unit);
    }

    // 타일 클릭 처리
    void HandleTileClick(Tilemap map, Vector3Int cell)
    {
        // 넉백 타겟팅 중
        if (state == BattleState.TargetingKnockback)
        {
            // 넉백은 후보군 체크만 하면 됨 (맵 일치 여부는 _knockbackMap과 비교해도 좋음)
            if (_knockbackCandidates != null && _knockbackCandidates.Contains(cell))
            {
                _onKnockbackSelected?.Invoke(cell);
            }
            return;
        }

        // 스킬(타일) 타겟팅 중
        if (currentSkillSO != null && currentSkillSO.targetMode == SkillTargetMode.Tile)
        {
            // 여기서 map을 넘겨줌 (올바른 맵인지 확인됨)
            ConfirmSkillOnTile(map, cell);
            return;
        }

        // 일반 이동
        // OnTileClicked에 map과 cell 두 개를 모두 전달
        OnTileClicked(map, cell);
    }
    // 마우스가 유닛 위를 지나갈 때 호출됨
    void HandleUnitHover(BattleUnit unit)
    {
        // 기본 조건 체크
        if (unit == null || state != BattleState.Targeting || currentSkillSO == null)
        {
            targetMarker?.Hide();
            // 유닛에서 벗어나면 프리뷰도 같이 꺼주는 게 자연스러움 (단, 이동 스킬 등 고정 프리뷰 제외)
            if (currentSkillSO == null || currentSkillSO.targetMode == SkillTargetMode.Unit)
                ClearSkillPreview();
            return;
        }

        // Self 스킬: 표시 안 함
        if (currentSkillSO.targetAlignment == SkillTargetAlignment.Self)
        {
            targetMarker?.Hide();
            return;
        }

        // Unit 타겟 스킬: 대상 유효성 체크 후 마커 + 범위 표시
        if (currentSkillSO.targetMode == SkillTargetMode.Unit)
        {
            if (IsValidSkillTarget(unit))
            {
                // 화살표 마커 표시
                targetMarker?.Attach(unit);

                // 스킬 범위도 같이 표시 (단일, 십자 등 설정된 범위대로)
                PreviewSkillAreaOnUnit(unit);
            }
            else
            {
                targetMarker?.Hide();
                ClearSkillPreview();
            }
        }
    }
    void HandleTileHover(Tilemap map, Vector3Int cell)
    {
        // 기본 조건 체크
        if (state != BattleState.Targeting || currentSkillSO == null || map == null) return;

        // Self 스킬 & Unit 타겟 스킬: 타일 호버링 무시
        if (currentSkillSO.targetAlignment == SkillTargetAlignment.Self) return;
        if (currentSkillSO.targetMode == SkillTargetMode.Unit) return;

        // Tile 타겟 스킬
        if (currentSkillSO.targetMode == SkillTargetMode.Tile)
        {
            // 예외: 이동 스킬(ParametricDirectionSkill) 등은 '고정된 범위'를 보여줘야 하므로
            // 마우스 따라다니는 프리뷰가 덮어쓰지 않도록 차단
            if (currentSkillSO is ParametricDirectionSkill) return;

            // 아군/적군 타일맵 구분
            bool isMapValid = false;
            switch (currentSkillSO.targetAlignment)
            {
                case SkillTargetAlignment.Enemy: // 적 대상이면 적 땅만
                    isMapValid = (map == provider.EnemyFloor);
                    break;
                case SkillTargetAlignment.Ally:  // 아군 대상이면 아군 땅만
                    isMapValid = (map == provider.PlayerFloor);
                    break;
                case SkillTargetAlignment.Any:   // 아무 땅이나 OK
                    isMapValid = true;
                    break;
            }

            if (isMapValid)
            {
                // 스킬 데이터에 설정된 범위(GetAreaCells)를 가져와서 그림
                bool isOdd = SkillLibrary.IsOddColumn(cell);
                var cells = currentSkillSO.GetAreaCells(cell, isOdd);

                // 하이라이터로 표시
                skillHighlighter?.ShowCells(map, cells);
            }
            else
            {
                // 엉뚱한 땅(적 스킬인데 아군 땅 등)에 가면 프리뷰 지우기
                // (단, 완전히 지우면 깜빡일 수 있으니 상황에 따라 조절 가능)
                skillHighlighter?.ClearTransient();
            }
        }
    }

    void OnDisable()
    {
        if (provider != null) provider.OnMapsReady -= Init;
    }
    #endregion

    #region Initialization

    void Init() //초기 세팅
    {

        RebindAllUnitsAndInitATB();
    }

    // 진형 정보대로 유닛 소환
    void SpawnPlayerUnits()
    {
        // 데이터 매니저 확인
        if (PlayerDataManager.Instance == null)
        {
            Debug.LogWarning("PlayerDataManager가 없습니다! 테스트용 임시 유닛을 생성하거나 확인하세요.");
            return;
        }

        // 맵 매니저 확인
        var mapManager = Shared.battleMapManager;
        if (mapManager == null) return;

        UnitData[] formation = PlayerDataManager.Instance.formation;

        // 기준점: 보통 (0,0,0)을 맵의 중앙이나 특정 PlayerStart 오브젝트 위치로 잡음
        // 여기서는 간단히 (0,0)을 기준으로 하되, 필요시 오프셋 더하기
        Vector3Int centerOffset = Vector3Int.zero;

        for (int i = 0; i < formation.Length; i++)
        {
            UnitData data = formation[i];

            // 데이터가 있는(배치된) 슬롯만 처리
            if (data != null)
            {
                // 좌표 계산
                Vector3Int cellPos = mapManager.GetFormationSpawnPoint(i) + centerOffset;

                // 프리팹 생성
                GameObject go = Instantiate(data.battlePrefab);

                // 위치 설정 (타일맵 좌표 -> 월드 좌표)
                Vector3 worldPos = mapManager.PlayerFloor.GetCellCenterWorld(cellPos);
                go.transform.position = worldPos;

                // BattleUnit 데이터 주입
                BattleUnit unit = go.GetComponent<BattleUnit>();
                if (unit != null)
                {
                    unit.data = data; // UnitData 연결
                    unit.team = Team.Player; // 아군 설정

                    // (중요) 외형 변경: UnitData에 있는 이미지가 있다면 스프라이트 교체
                    // 만약 프리팹이 Spine이나 애니메이션을 쓴다면 다른 방식 필요
                    var sr = go.GetComponentInChildren<SpriteRenderer>();
                    if (sr != null && data.UnitIcon != null)
                    {
                        sr.sprite = data.UnitIcon; // 임시로 아이콘 사용 (나중엔 전용 모델로)
                    }

                    // 데이터 주입 후, 스탯을 실질적으로 적용하는 함수 강제 호출
                    unit.ApplyData();
                }

                // E. 매니저에 등록 (턴 관리용 리스트 등이 있다면 추가)
                // allUnits.Add(unit); 
            }
        }
    }

    // BattleMapManager(IBattleMapProvider)가 실제로 Floor들을 채운 '이후' 1회만 BattleInput에 통지
    IEnumerator Co_RebindBattleInputWhenMapsReady()
    {
        var provider = Shared.battleMapManager as IBattleMapProvider;
        // provider가 아직 안 잡힌 프레임도 있을 수 있으니 안전 폴링
        while (provider == null)
        {
            yield return null;
            provider = Shared.battleMapManager as IBattleMapProvider ?? FindObjectOfType<BattleMapManager>(true) as IBattleMapProvider;
        }
        // Floor들이 실제로 세팅될 때까지 대기
        while (provider.PlayerFloor == null || provider.EnemyFloor == null)
            yield return null;

        // 맵 준비 완료 → BattleInput에 단 한 번 rebind 지시
        if (Shared.battleInput != null)
            Shared.battleInput.RebindProviders();
    }

    // 씬 내 모든 BattleUnit(플레이어+적)을 재바인드하고 ATB/구독을 초기화
    private void RebindAllUnitsAndInitATB()
    {
        var battleUnit = FindObjectsOfType<BattleUnit>(true).ToList();

        float minAGI = battleUnit.Min(unit => unit.EffectiveAGI);
        float maxAGI = battleUnit.Max(unit => unit.EffectiveAGI);
        
        foreach (var unit in battleUnit)
        {
            var map = (unit.team == Team.Player) ? provider.PlayerFloor : provider.EnemyFloor;
            var cell = map.WorldToCell(unit.transform.position);

            unit.Bind(map, cell);
            grid.SetOccupied(unit.team, unit.Cell, true);
            unit.InitializeATB(minAGI, maxAGI);
            unit.InitPassives(this);

            unit.OnDied -= HandleUnitDied;
            unit.OnDied += HandleUnitDied;
        }

        // ATB 초기화 및 등록은 컨트롤러에게 위임
        turnController.RegisterUnits(battleUnit);

        turnController.OnATBTicked -= ForwardATBTick;
        turnController.OnATBTicked += ForwardATBTick;
    }

    void ForwardATBTick(BattleUnit u)
    {
        OnATBChanged?.Invoke(u, u.ATB, u.MaxATB);
    }
    // 턴 시작 핸들러
    void HandleTurnReady(BattleUnit unit)
    {
        acting = unit;
        StartTurn(unit); // 기존 StartTurn 로직 실행
    }

    /// <summary>모든 유닛 ATB 0, 진행상태/큐 초기화 후 UI에 리셋 신호</summary>
    void ResetATBAndTurnOrder()
    {
        // 컨트롤러에게 ATB 전체 초기화 위임 (Pause 해제 포함)
        turnController.ResetAllATB();

        // 진행 상태 초기화
        acting = null;
        state = BattleState.Idle;

        // UI에 "전체 ATB 리셋" 알림 → 턴바 0으로 재배치
        OnATBReset?.Invoke();
        EmitActionLabel(null, "");   // 전면 리셋 시 라벨도 초기화
    }

    #endregion

    // Grid 점유 해제 시도(맵/그리드 레퍼런스에 맞춰 구현)
    private void TryReleaseGridOccupy(BattleUnit u)
    {
        if (u == null || grid == null) return;
        var map = grid.GetMap(u.team);                     // 팀에 맞는 타일맵
        if (map == null) return;
        Vector3Int cell = u.Cell;                          // 유닛이 기억하는 셀(없다면 map.WorldToCell(u.transform.position))
        grid.SetOccupied(u.team, cell, false);             // 점유 해제
    }

    void HandleWaveLoaded(BattleMapManager localProvider, Tilemap enemyFloor, Tilemap enemyOverlay)
    {
        // 1. 맵 정보 갱신 (BM이 하던 일)
        if (enemyFloor != null)
        {
            var mapMgr = Shared.battleMapManager as BattleMapManager ?? FindObjectOfType<BattleMapManager>(true);
            mapMgr.UseEnemyFloor(enemyFloor, enemyOverlay);
            provider = mapMgr;

            Shared.battleInput?.RebindProviders();
            Shared.battleGridManager?.RebindProvider();
        }

        // 2. 유닛 리바인딩 & ATB 초기화 (BM의 핵심 로직 재사용)
        RebindAllUnitsAndInitATB();

        // 3. 웨이브 시작 알림
        OnWaveStarted?.Invoke();
        ResetATBAndTurnOrder();
    }

    #region Turn Management
    void StartTurn(BattleUnit _unit)
    {
        if (_unit == null) return;
        acting = _unit;
        remainingActions = baseActionsPerTurn;
        usedActions.Clear();
        ClearAllPreviews();
        ClearTargetSelection();
        OnAnyUnitTurnStarted?.Invoke(_unit);
        EmitActionLabel(null, "");
        OnHint?.Invoke(string.Empty);
        EmitTurnLabel(_unit);

        Debug.Log($"[Battle] StartTurn -> {acting.name}");

        var sc = _unit.GetComponent<StatusController>();
        var usc = _unit.GetComponent<UnitStateController>();

        // 수면 상태 체크: 수면 중이면 행동 불가 -> 수면 해제 후 턴 종료
        if (usc != null && usc.Has(UnitStateId.Sleep))
        {
            Debug.Log($"[Sleep] {_unit.name} 수면 상태이므로 턴을 넘깁니다.");

            // 수면 상태 제거 (1턴 지속이므로 턴이 오면 바로 해제)
            usc.Remove(UnitStateId.Sleep);

            // 행동 없이 턴 종료 처리
            if (_unit.team == Team.Player)
                EndPlayerTurn();
            else
                EndEnemyTurn(_unit);
            return;
        }


        // 이 턴 시작 시점에 Fear가 있었는지 먼저 체크
        bool hadFear = (usc != null && usc.Has(UnitStateId.Fear));

        if (sc != null) sc.OnTurnStart();
        if (usc != null) usc?.OnTurnStart();

        var ambushSkill = GetAmbushSkillFor(_unit);
        if (ambushSkill != null)
        {
            TryApplyAmbushTurnStartHeal(_unit, ambushSkill);
        }

        // 공포 상태 처리: 다른 행동보다 우선 적용
        if (hadFear)
        {
            Debug.Log($"[Fear] {_unit.name} 공포 상태 턴 시작 → 강제 후퇴 진행");
            state = BattleState.Resolving;
            remainingActions = 0;
            usedActions.Clear();

            StartCoroutine(Co_HandleFearTurn(_unit));
            return; // 여기서 턴 로직 종료 (이동/공격 선택 화면 안 뜸)
        }

        // 턴 시작 시 영역 시간 감소
        TickBeastDomainOnTurnStart(_unit);
        TickStatusTileZonesOnTurnStart(_unit);

        // 캐스팅 성공 턴 소비 처리
        if (_unit.team == Team.Enemy)
        {
            var ecs = _unit.GetComponent<EnemyCastState>();
            if (ecs != null && ecs.TryTakeReady(out var pending))   // 준비된 캐스팅 성공 확인
            {
                // 적 행동 루틴 대신, '웹 발사→생성→소비' 코루틴 실행
                StartCoroutine(Co_EnemyFireWebThenConsume(_unit, pending));
                return; // EnemyTurnRoutine 시작하지 않음
            }
        }

        if (_unit.team == Team.Player)
        {
            state = BattleState.ActionSelect; // 플레이어 입력 허용
            //Debug.Log($"[PlayerTurn] {unit.name} 턴 시작 → ATB 정지");
        }
        else
        {
            state = BattleState.Resolving; // 입력 잠금
            //Debug.Log($"[EnemyTurn] {unit.name} 턴 시작 → ATB 정지");
            StartCoroutine(EnemyTurnRoutine(_unit));
        }
    }

    public void OnClickRest()
    {
        if (acting == null || !IsPlayerTurn) return;
        if (remainingActions <= 0) return;

        // 프리뷰/타겟팅 정리
        ClearAllPreviews();
        ClearTargetSelection();

        // 휴식: 최대 HP의 10% 회복
        float before = acting.HP;
        acting.HealPercent(0.10f);
        float after = acting.HP;

        if (after > before)
        {
            Debug.Log($"[Rest] {acting.name} 휴식: HP {before} -> {after} (+{after - before})");
        }
        else
        {
            Debug.Log($"[Rest] {acting.name} 휴식: 회복 없음 (이미 최대 체력)");
        }

        // 행동 1회 소비 → 지금 구조에서는 baseActionsPerTurn=1이라 사실상 턴 종료와 동일
        OnActionConsumed(BattleAction.Rest);
    }
    public void OnClickCalm()
    {
        if (acting == null || !IsPlayerTurn) return;
        if (remainingActions <= 0) return;

        ClearAllPreviews();
        ClearTargetSelection();

        float maxMP = acting.MaxMP;
        float maxRage = acting.MaxRage;

        float beforeMP = acting.MP;
        float beforeRage = acting.Rage;

        // MP 회복량: MaxMP의 10% (내림, 최소 1 보장)
        int mpGain = Mathf.FloorToInt(maxMP * 0.10f);
        if (mpGain <= 0 && maxMP > 0f)
            mpGain = 1;

        // Rage가 0이어도 최소 MP 회복 0.1 보장
        if (acting.Rage <= 0f)
        {
            acting.GainMP(mpGain);
            Debug.Log($"[Calm] {acting.name} Rage=0 → 최소 회복 MP +0.1");
            OnActionConsumed(BattleAction.Calm);
            return;
        }

        if (mpGain > 0)
        {
            acting.GainMP(mpGain);   // GainMP 안에서 클램프 수행
        }

        float afterMP = acting.MP;

        if (afterMP > beforeMP)
        {
            Debug.Log($"[Calm] {acting.name} 진정: MP {beforeMP} -> {afterMP} (+{afterMP - beforeMP})");
        }
        else
        {
            Debug.Log($"[Calm] {acting.name} 진정: MP 회복 없음 (이미 최대 MP이거나 Gain=0)");
        }

        // 최대 Rage의 10%"를 목표로 사용
        // 현재 Rage가 그보다 적으면 '있던 Rage 전부 소모'
        float rageCostTarget = maxRage * 0.10f;

        // MaxRage가 0이거나 음수면, 현재 Rage 전체를 소모 대상으로 본다 (안전 폴백)
        if (rageCostTarget <= 0f)
            rageCostTarget = beforeRage;

        float spend = Mathf.Min(beforeRage, rageCostTarget);   // 실제로 쓸 Rage 양

        if (spend > 0f)
        {
            acting.AddRage(-spend);    // AddRage 헬퍼 사용 (로그 + 클램프) 
        }

        float afterRage = acting.Rage;

        Debug.Log(
            $"[Calm] {acting.name} 진정: Rage {beforeRage:F2} -> {afterRage:F2} " +
            $"(소모 {spend:F2} / 목표 {rageCostTarget:F2}, MaxRage={maxRage:F2})"
        );

        OnActionConsumed(BattleAction.Calm);
    }
    #endregion

    #region Movement
    public void OnClickMove()
    {
        if (acting == null || !IsPlayerTurn) return; // 적 턴 금지
        if (usedActions.Contains(BattleAction.Move) || remainingActions <= 0) return; // 중복/토큰 없음

        // 스킬 패널/선택 상태 정리
        CloseSkillPanel();       // 패널 열려있으면 닫기
        ClearAllPreviews();    // 남아있을지 모를 프리뷰 정리
        ClearTargetSelection();  // 타겟 마커 숨김

        state = BattleState.Moving;
        moveOptions = grid.GetAdjacentWalkable(acting.team, acting.Cell).ToList();
        ShowMovePreview(acting.CurrentMap, moveOptions);
    }

    public void OnTileClicked(Tilemap clickedMap, Vector3Int clickedCell)
    {
        if (!IsPlayerTurn) return;

        if (state == BattleState.Moving)
        {
            if (clickedMap == acting.CurrentMap && moveOptions.Contains(clickedCell))
            {
                state = BattleState.Resolving; // 입력 잠금
                if (_isPostSkillMoveInProgress)
                {
                    // 스킬 사용 후 추가 이동
                    StartCoroutine(Co_MoveAfterSkillThenConsume(acting, clickedMap, clickedCell));
                }
                else
                {
                    // 일반 이동
                    StartCoroutine(Co_MoveThenConsume(acting, clickedMap, clickedCell, BattleAction.Move));
                }
                ClearAllPreviews();
                moveOptions.Clear();
                return;
            }
        }
        else if (state == BattleState.Targeting)
        {
            if (state == BattleState.Targeting
                && currentSkillSO != null
                && currentSkillSO.targetMode == SkillTargetMode.Tile
                && clickedMap == (currentSkillTargetMap ?? provider.EnemyFloor))
            {
                ConfirmSkillOnTile(clickedMap, clickedCell);
            }
        }
        else if (state == BattleState.TargetingKnockback)
        {
            if (_knockbackMap == null || _knockbackCandidates == null || _onKnockbackSelected == null)
                return;

            if (clickedMap != _knockbackMap) return;
            if (!_knockbackCandidates.Contains(clickedCell)) return;

            // 선택 완료
            var cb = _onKnockbackSelected;
            EndKnockbackSelection();

            state = BattleState.Resolving;
            cb?.Invoke(clickedCell);
            return;
        }
    }

    IEnumerator Co_MoveThenConsume(BattleUnit unit, Tilemap map, Vector3Int toCell, BattleAction act)
    {
        if (unit == null || map == null) yield break;

        Vector3Int fromCell = unit.Cell;

        // 실제로 이동(애니메이션 + 점유 갱신)
        grid.SetOccupied(unit.team, fromCell, false);
        yield return unit.AnimateMoveTo(map, toCell);
        grid.SetOccupied(unit.team, unit.Cell, true);

        // 야수의 영역 안에서의 이동인지 체크
        bool freeMove = IsBeastDomainFreeMove(unit, map, fromCell, toCell);

        if (freeMove)
        {
            Debug.Log($"[BeastDomain] {unit.name} 야수의 영역 안 이동: 행동 토큰 소비 없음");

            if (unit.team == Team.Player)
            {
                state = BattleState.ActionSelect;
                EmitActionLabel(unit, "");
            }
            yield break;
        }
        // 리액션 대기
        while (_reactionLocks > 0)
        {
            yield return null;
        }

        // 평소처럼 이동 행동 1회 소비
        OnActionConsumed(act);
    }
    #endregion

    #region Attack
    public void OnClickAttack()
    {
        if (!IsPlayerTurn || acting == null) return; // 적 턴 금지
        if (usedActions.Contains(BattleAction.Attack) || remainingActions <= 0) return; // 중복/토큰 없음

        ClearAllPreviews();
        ClearTargetSelection();
        OpenSkillPanel();
    }

    public void OnUnitClicked(BattleUnit target)
    {
        if (!IsPlayerTurn) return;
        if (!IsTargeting || currentSkillSO == null || currentSkillSO.targetMode != SkillTargetMode.Unit) return;
        if (target == null || target.team == acting.team) return;

        bool isAlly = (acting != null && target.team == acting.team);
        bool allowAlly = (currentSkillSO is AllyRetreatSwapSkill);

        // 이 스킬이 아니면 아군은 대상 불가
        if (isAlly && !allowAlly) return;

        // 적을 대상으로 하는 스킬인데 아군이면 막기
        if (!isAlly && allowAlly) return; // (원한다면 아군 전용으로 제한)

        ConfirmSkillOnUnit(target);
    }
    #endregion

    #region Action Consumption
    void OnActionConsumed(BattleAction act)
    {
        if (acting == null)
        {
            // 이미 HandleUnitDied/Retreat에서 턴/ATB 정리 끝난 상태이므로
            return;
        }

        usedActions.Add(act);
        remainingActions = Mathf.Max(0, remainingActions - 1);

        // 남은 행동이 있으면 플레이어 입력 대기
        if (remainingActions > 0)
        {
            if (IsPlayerTurn)
            {
                state = BattleState.ActionSelect; // 플레이어 선택 허용
            }
            else
            {
                // 적 턴이면 EnemyTurnRoutine 재개
                if (enemyRoutine != null) StopCoroutine(enemyRoutine);
                enemyRoutine = StartCoroutine(EnemyTurnRoutine(acting));
            }
        }
        else
        {
            // 행동 토큰 모두 소진 → 턴 종료 처리
            if (IsPlayerTurn)
            {
                EndPlayerTurn();
            }
            else
            {
                EndEnemyTurn(acting);
            }
        }
    }

    // 턴 종료 시 위에 있는 유닛에게 효과 부여
    void CheckStatusTileZoneEffect(BattleUnit unit)
    {
        if (unit == null || unit.IsDead) return;

        // 현재 유닛 위치에 지대가 있는지 확인
        foreach (var zone in _statusTileZones)
        {
            if (zone.map == unit.CurrentMap && zone.cell == unit.Cell)
            {
                var sc = unit.GetComponent<StatusController>();
                if (sc != null)
                {
                    // 지대에 설정된 효과(StatusId)를 그대로 부여
                    sc.ApplyWithTurnContext(zone.effectStatusId, zone.effectStack, zone.effectDuration);
                    Debug.Log($"[StatusZone] {unit.name} 지대({zone.effectStatusId}) 위 턴 종료 -> 상태 부여");
                }
                break; // 한 타일에 지대가 겹치지 않는다는 가정 (겹치면 리스트 구조 변경 필요)
            }
        }
    }

    public IEnumerable<BattleUnit> GetLivingAlliesOf(BattleUnit unit)   //살아 있는 아군 유닛 확인
    {
        if (unit == null) yield break;

        var all = FindObjectsOfType<BattleUnit>();
        foreach (var u in all)
        {
            if (u == null) continue;
            if (u.IsDead || u.IsRetreated) continue;
            if (u.team != unit.team) continue;

            yield return u;
        }
    }

    public IEnumerable<BattleUnit> GetLivingEnemiesOf(BattleUnit _battleunit)   //살아 있는 적 유닛 확인
    {
        if (_battleunit == null) yield break;

        // 씬에 존재하는 모든 유닛 기준으로 계산
        var currentUnit = FindObjectsOfType<BattleUnit>();

        foreach (var units in currentUnit)
        {
            if (units == null) continue;
            if (units == _battleunit) continue;
            if (units.team == _battleunit.team) continue;
            if (units.IsDead || units.IsRetreated) continue;

            yield return units;
        }
    }

    public BattleUnit GetUnitAt(Vector3Int cell)
    {
        if (grid == null) return null;

        // 플레이어 맵을 기준으로 월드 좌표 변환 (적 맵과 그리드는 공유하므로 상관없음)
        var map = grid.GetMap(Team.Player);
        if (map == null) return null;

        // 타일 중앙의 월드 좌표 구하기
        Vector3 worldPos = map.GetCellCenterWorld(cell);

        // 해당 위치에 유닛이 있는지 물리적으로 체크 (unitMask 사용)
        Collider2D hit = Physics2D.OverlapCircle(worldPos, 0.2f, unitMask);

        if (hit != null)
        {
            return hit.GetComponentInParent<BattleUnit>();
        }

        return null;
    }

    /// <summary>
    /// 과로(Overwork) 상태를 체크하고 재행동을 처리
    /// 리턴값: true면 재행동(턴 종료 중단), false면 정상 종료 진행
    /// </summary>
    private bool TryProcessOverwork()
    {
        if (acting == null) return false;

        var statusCtrl = acting.GetComponent<StatusController>();
        var usc = acting.GetComponent<UnitStateController>();
        if (statusCtrl == null) return false;


        // 과로 스택 확인
        int overworkStacks = statusCtrl.GetStacks(StatusId.Overwork);

        // 스택이 없으면 과로 처리 안 함
        if (overworkStacks <= 0) return false;
            

        // 스택 1 감소 (비용 지불)
        int nextStack = overworkStacks - 1;
        statusCtrl.SetStacks(StatusId.Overwork, nextStack);

        // 3. 마지막 스택 소모 시 (예: 1 -> 0)
        // 재행동을 주지 않고, 수면 상태를 부여한 뒤 정상적으로 턴을 종료시킵니다.
        if (nextStack == 0)
        {
            // 수면 면제 훈련 체크
            bool skipSleep = false;

            // 유닛이 가진 스킬 중 '과로'를 부여하는 ParametricSupportSkill이 있는지,
            // 그리고 그 스킬의 '수면 면제 훈련'이 켜져 있는지 확인
            if (acting.data != null && acting.data.skills != null)
            {
                foreach (var s in acting.data.skills)
                {
                    if (s is ParametricSupportSkill pss && pss.buffStatus == StatusId.Overwork)
                    {
                        int route = acting.GetTrainingRouteIndex(pss);
                        if (pss.trainingNoSleepOnOverworkEnd &&
                            pss.routeForNoSleepOnOverworkEnd >= 0 &&
                            route == pss.routeForNoSleepOnOverworkEnd)
                        {
                            skipSleep = true;
                            break;
                        }
                    }
                }
            }

            if (usc != null)
            {
                if (!skipSleep)
                {
                    Debug.Log($"[BattleManager] {acting.name} 과로 종료 -> 수면 상태 부여 (다음 턴 휴식)");
                    usc.Apply(UnitStateId.Sleep);
                }
                else
                {
                    Debug.Log($"[BattleManager] {acting.name} 과로 종료 -> 훈련 효과로 수면 면제!");
                }
            }
            // false를 반환해야 EndPlayerTurn이 진행되어 ATB 대기 상태로 넘어감
            return false;
        }

        Debug.Log($"[BattleManager] {acting.name} 과로 발동! (남은 스택: {nextStack}) -> 즉시 턴 획득");

        // 행동력 리필 & 재행동 시작
        remainingActions = baseActionsPerTurn;
        usedActions.Clear();
        state = BattleState.ActionSelect;

        OnOverworkTriggered?.Invoke(acting);

        return true; // 턴 종료를 막고 다시 행동 기회 부여
    }

    // 플레이어 턴 종료 처리
    void EndPlayerTurn()
    {
        // 과로 체크 및 재행동 처리
        if (TryProcessOverwork()) return;

        // 영역 체크
        CheckStatusTileZoneEffect(acting);

        ClearAllPreviews();
        ClearTargetSelection();

        // ATB 재개(다음 턴은 Update()가 자동 감지)
        OnUnitEndTurn?.Invoke(acting);
        turnController.CompleteTurn(acting);
        acting = null;
        state = BattleState.Idle;
    }

    public void CancelCurrentAction()
    {
        UnlockSkillConfirm();

        // 넉백 타겟팅 취소 처리 우선
        if (state == BattleState.TargetingKnockback && currentSkillSO != null)
        {
            // 코루틴 깨우기: null 선택으로 알림
            if (_onKnockbackSelected != null)
            {
                var cb = _onKnockbackSelected;
                EndKnockbackSelection();   // 내부 필드 정리 + 프리뷰 클리어
                ClearTargetSelection();
                cb(null);                  // done = true, chosen = null
            }
            
            currentSkillSO = null;
            currentSkillTargetMap = null;
            customPreviewCells = null;
            customPreviewMap = null;
            state = BattleState.ActionSelect;
            UpdateTargetingHint();
            if (!isSelectingSkill) OpenSkillPanel();
            return;
        }
        // 타겟팅 중(스킬 선택됨) → '스킬만 해제', 패널은 유지
        if (state == BattleState.Targeting && currentSkillSO != null)
        {
            ClearSkillPreview();
            ClearTargetSelection();
            currentSkillSO = null;
            currentSkillTargetMap = null;
            customPreviewCells = null;
            customPreviewMap = null;
            state = BattleState.ActionSelect;
            UpdateTargetingHint();
            if (!isSelectingSkill) OpenSkillPanel();
            return;
        }
        if (state == BattleState.Moving)
        {
            // 스킬 후 보너스 이동을 취소했을 때
            if (_isPostSkillMoveInProgress)
            {
                ClearMovePreview();
                ClearTargetSelection();
                customPreviewCells = null;
                customPreviewMap = null;

                _isPostSkillMoveInProgress = false;

                // 이동은 하지 않고, 스킬은 이미 사용했으므로
                // 공격 토큰을 소비하고 턴을 진행
                OnActionConsumed(BattleAction.Attack);
                return;
            }

            ClearMovePreview();
            ClearTargetSelection();
            customPreviewCells = null;
            customPreviewMap = null;
            state = BattleState.ActionSelect;
            UpdateTargetingHint();
            return;
        }
        // 그밖의 상황에서 패널이 열려 있다면(= 취소 2회째) 패널 닫기
        if (isSelectingSkill)
        {
            ClearAllPreviews();
            CloseSkillPanel();
            state = BattleState.ActionSelect;
            UpdateTargetingHint();
            return;
        }
    }
    #endregion

    #region Targeting
    void BuildTargetCycle()
    {
        var all = FindObjectsOfType<BattleUnit>();

        // 현재 선택된 스킬이 AllyRetreatSwapSkill이면 → "아군 리스트"로 타겟 사이클 구성
        if (currentSkillSO is AllyRetreatSwapSkill && acting != null)
        {
            targetCycle = all
                .Where(u => u.team == acting.team          // 같은 팀(아군)
                            && u != acting                 // 자기 자신 제외(원하면 포함도 가능)
                            && !u.IsDead)
                .OrderByDescending(u => u.EffectiveAGI)
                .ToList();
        }
        else
        {
            // 그 외 스킬은 기존처럼 "적 리스트"
            if (acting != null)
            {
                targetCycle = all
                    .Where(u => u.team != acting.team && !u.IsDead)
                    .OrderByDescending(u => u.EffectiveAGI)
                    .ToList();
            }
            else
            {
                targetCycle = all
                    .Where(u => u.team == Team.Enemy && !u.IsDead)
                    .OrderByDescending(u => u.EffectiveAGI)
                    .ToList();
            }
        }
        targetIndex = -1;
        selectedTarget = null;
    }

    // 유닛을 직접 지정하여 TargetCycle에서 선택(없으면 false)
    public bool SelectTargetByUnit(BattleUnit unit)
    {
        if (unit == null || targetCycle == null || targetCycle.Count == 0)
            return false;

        if (selectedTarget == unit)
            return true; // 이미 선택됨

        int idx = targetCycle.IndexOf(unit);
        if (idx < 0) return false; // 사이클에 없는 유닛이면 무시(적만 포함 등 규칙 유지)

        SelectTarget(idx); // SelectTarget 내부에서 마커 + 하이라이트까지 갱신
        return true;
    }

    void SelectTarget(int index)
    {
        if (targetCycle.Count == 0) { ClearTargetSelection(); return; }
        int n = targetCycle.Count;
        targetIndex = ((index % n) + n) % n; // 안전한 모듈로
        selectedTarget = targetCycle[targetIndex];
        targetMarker?.Attach(selectedTarget);

        // 스킬이 Unit형으로 선택된 상태라면, 선택된 타겟 기준으로 범위 미리보기 갱신
        if (currentSkillSO != null && currentSkillSO.targetMode == SkillTargetMode.Unit)
        {
            PreviewSkillAreaOnUnit(selectedTarget);
        }
    }

    public void CycleTarget(int dir)
    {
        if (!IsPlayerTurn || !IsTargeting || targetCycle.Count == 0) return;
        SelectTarget(targetIndex + dir); // dir=+1(→), -1(←)
    }

    public void ConfirmTarget()
    {
        if (!IsPlayerTurn || !IsTargeting || selectedTarget == null) return;
        if (currentSkillSO == null || currentSkillSO.targetMode != SkillTargetMode.Unit) return;

        ClearSkillPreview();

        // AllyRetreatSwapSkill는 유닛 확정 시에도 전용 플로우 사용
        if (currentSkillSO is AllyRetreatSwapSkill)
        {
            ConfirmSkillOnUnit(selectedTarget);
            return;
        }

        var skill = currentSkillSO;
        bool doGapClose = skill.ShouldGapCloseToTarget(acting, selectedTarget);

        StartCoroutine(skillProcessor.PerformStandardUnitSkillFlow(skill, acting, selectedTarget));
    }

    void ClearTargetSelection()
    {
        selectedTarget = null;
        targetIndex = -1;
        targetMarker?.Hide();
    }
    #endregion

    #region Death Handling
    void HandleUnitDied(BattleUnit dead)
    {
        grid.SetOccupied(dead.team, dead.Cell, false);

        if (dead == acting)
        {
            turnController.ResumeTime();

            acting = null;
            state = BattleState.Idle;
        }

        // 중복 구독 방지
        dead.OnDied -= HandleUnitDied;
        EmitActionLabel(dead, "");   // 라벨 비우기

        StartCoroutine(Co_DieThenDestroy(dead));
    }

    IEnumerator Co_DieThenDestroy(BattleUnit u)
    {
        if (u == null) { CheckBattleEnd(); yield break; }

        // 1) 사망 연출을 "수동 이터레이션"으로 소비 (중간 파괴에 안전)
        var routine = u.PlayDieAndWait(1.0f);
        if (routine != null)
        {
            // u가 중간에 파괴되거나 routine이 끝나면 루프 종료
            while (u != null)
            {
                // 다음 프레임으로 진행할 수 없으면 종료
                if (!routine.MoveNext()) break;

                // 현재 yield 값 전달
                yield return routine.Current;

                // 중간에 오브젝트가 파괴되면 자연 종료
                if (u == null || u.gameObject == null) break;
            }
        }

        // 2) 최종 파괴(여러 경로로 Destroy가 한 번 더 호출돼도 안전)
        if (u != null && u.gameObject != null)
            Destroy(u.gameObject);

        // 3) 사망 정리가 끝난 뒤 전투 종료 판정
        CheckBattleEnd();
    }
    #endregion

    #region Battle End
    void HandleVictory()
    {
        Debug.Log("[Battle] 승리! (모든 웨이브 완료)");
        if (Shared.PuzzleManager.IsPuzzleComplete)
            Shared.SceneTransitionManager.FadeToScene("EndScene");
        else
            Shared.SceneTransitionManager.ReturnToSavedPoint();
    }
    void CheckBattleEnd()
    {
        var units = FindObjectsOfType<BattleUnit>();
        bool anyPlayer = units.Any(u => u.team == Team.Player && !u.IsDead);
        bool anyEnemy = units.Any(u => u.team == Team.Enemy && !u.IsDead);

        if (!anyEnemy)
        {
            if (waveManager.IsWaveTransitioning) return;

            // 아직 웨이브 남았으면 진행, 없으면 승리 이벤트 발생
            waveManager.TryAdvanceToNextWave();
            return;
        }
        else if (!anyPlayer)
        {
            if (_battleEndedOnce) return;              //  중복 가드
            _battleEndedOnce = true;                   //  가드
            Debug.Log("[Battle] 패배...");

            if (Shared.PuzzleManager.IsPuzzleComplete)
                Shared.SceneTransitionManager.FadeToScene("EndScene");
            else
                Shared.SceneTransitionManager.ReturnToSavedPoint();
        }
    }
    #endregion

    bool IsAmbushHiddenTarget(BattleUnit u)
    {
        if (!u) return false;
        var usc = u.GetComponent<UnitStateController>();
        if (usc == null) return false;

        // 잠복 상태면 적이 타겟팅할 수 없음
        return usc.Has(UnitStateId.Ambush) || usc.HasBuff(UnitStateBuffId.SmokeHidden);
    }
    SelfAmbushSkill GetAmbushSkillFor(BattleUnit unit)
    {
        if (unit == null || unit.data == null || unit.data.skills == null)
            return null;

        foreach (var s in unit.data.skills)
        {
            if (s is SelfAmbushSkill ambush)
                return ambush;
        }
        return null;
    }

    void TryApplyAmbushTurnStartHeal(BattleUnit unit, SelfAmbushSkill skill)
    {
        if (unit == null || skill == null) return;

        var usc = unit.GetComponent<UnitStateController>();
        if (usc == null || !usc.Has(UnitStateId.Ambush))
            return;

        int route = unit.GetTrainingRouteIndex(skill);

        if (!skill.trainingHealOnTurnStart ||
            skill.routeForHealOnTurnStart < 0 ||
            route != skill.routeForHealOnTurnStart)
            return;

        int amount = skill.ComputeTurnStartHeal(unit);
        if (amount <= 0) return;

        float before = unit.HP;
        unit.Heal(amount);
        float after = unit.HP;

        if (after > before)
        {
            Debug.Log($"[Ambush] Route(Heal={skill.routeForHealOnTurnStart}): {unit.name} 턴 시작 회복 +{after - before} (healPerClv={skill.healPerClv})");
        }
    }

    #region Enemy AI
    IEnumerator EnemyTurnRoutine(BattleUnit enemy)
    {
        yield return new WaitForSeconds(0.5f); // 살짝 텀

        // 생존 플레이어 수집
        var players = Object.FindObjectsOfType<BattleUnit>()
            .Where(u => u != null && u.team == Team.Player && !u.IsDead)
            .Where(u => !SkillAsset.IsUntargetableByEnemy(u)) // 잠복연막 은신 모두 제외
            .ToList();

        // 대상 지정 가능 플레이어가 아무도 없으면 이번 적 턴은 할 대상이 없다고 보고 종료
        if (players.Count == 0) { EndEnemyTurn(enemy); yield break; }

        BattleUnit target = players[Random.Range(0, players.Count)]; // 랜덤 1인 지정

        // 미리 선정해둔 스킬을 꺼내서 사용
        var ai = enemy.GetComponent<EnemyAI>();
        SkillAsset so = (ai != null) ? ai.ConsumePlannedSkillOrPick() : null;

        // 턴이 시작된 지금, 이 턴에 쓸 스킬명을 방송
        if (so != null) EmitActionLabel(enemy, so.displayName);

        if (so != null)
        {
            // 이미 표시 중인 예정 스킬명이 실행됨
            if (so.targetMode == SkillTargetMode.Unit)
            {
                yield return StartCoroutine(so.ResolveOnUnit(this, enemy, target));
                FinishActionAfterSkill();
                yield break;
            }
            else if(so.targetMode == SkillTargetMode.Tile)
            {
                //Tile지정 스킬 추가 예정
                yield break;
            }
        }

        // so가 없으면 (null) 기본 종료/대기 등
        EndEnemyTurn(enemy);
    }

    // === Enemy AI Helpers ===
    // 적 유닛 대상형 스킬 실행
    IEnumerator Co_EnemyResolveSkillOnUnit_NoMove(SkillDefinition def, BattleUnit caster, BattleUnit target)
    {
        state = BattleState.Resolving;

        bool impactDone = false;
        System.Action impact = null;
        impact = () =>
        {
            caster.OnAttackImpact -= impact;
            impactDone = true;
            // 대상 유닛의 셀을 원점으로 범위 계산/피해 적용
            ResolveSkillAtCell(def, target.CurrentMap, target.Cell, caster);
        };
        caster.OnAttackImpact += impact;

        yield return caster.AnimateAttack(target, null); // 제자리 근접 모션

        if (!impactDone) // 애니 이벤트 누락 대비
            ResolveSkillAtCell(def, target.CurrentMap, target.Cell, caster);

        FinishActionAfterSkill(); // 토큰 소모/턴 진행(공격으로 간주)
    }

    IEnumerator Co_EnemyFireWebThenConsume(BattleUnit caster, EnemyCastState.PendingCast p) //실뿜기 스킬 실행 루틴
    {
        state = BattleState.Resolving;

        // 캐스팅 루프 종료 → 발사 애니
        caster.SetCasting(false);

        // 발사 타이밍을 '임팩트 이벤트'로 맞춘다
        bool fired = false;
        bool arrived = false;

        //어떤 투사체를 쓸지 결정
        ProjectileController projPrefab =
              p.projectilePrefab
           ?? caster.defaultProjectilePrefab
           ?? null; // (BM 전역을 유지하고 싶다면 ?? this.projectilePrefab)

        // 투사체 발사 → 도착 시 트랩 생성
        Vector3 startW = caster.transform.position;
        Vector3 targetW = p.map.GetCellCenterWorld(p.cell);

        void FireOnce()
        {
            if (fired) return;
            fired = true;

            void OnArrive()
            {
                if (p.trapPrefab != null && p.map != null)
                {
                    WebTrapController.RemoveAt(p.map, p.cell);
                    var trap = Instantiate(p.trapPrefab, targetW, Quaternion.identity);
                    trap.Init(p.map, p.cell, p.owner);
                }
                arrived = true;
            }

            if (projPrefab != null)
            {
                var go = Instantiate(projPrefab, startW, Quaternion.identity);
                var pc = go.GetComponent<ProjectileController>();
                if (pc != null) pc.Init(startW, targetW, OnArrive, p.projectileSpeed);
                else OnArrive();
            }
            else
            {
                OnArrive();
            }
        }

        System.Action onFire = null;
        onFire = () =>
        {
            caster.OnAttackImpact -= onFire; // 반드시 해제
            FireOnce();
        }
        ;
        caster.OnAttackImpact += onFire;

        // 발사 모션 시작
        yield return caster.AnimateShootWeb();  // 애니 끝까지 대기 (발사는 이미 중간에 실행됨)

        // 애니 중 임팩트 이벤트가 안 왔다면, 애니 종료 시 1회 강제 발사
        if (!fired)
        {
            caster.OnAttackImpact -= onFire; // 혹시 남았으면 해제
            FireOnce();
        }

        // 투사체 도착까지 대기(안전 타임아웃)
        float timeout = 3f;
        while (!arrived && timeout > 0f) { timeout -= Time.deltaTime; yield return null; }

        // 프리뷰 토큰/홀드 해제
        ClearSkillPreview();

        var ecs = caster.GetComponent<EnemyCastState>();
        ecs?.ClearPreviewAndFinalize(this);   // 토큰 삭제 + 홀드 해제 + pending 정리

        // 행동 토큰 소비로 턴 종료
        OnActionConsumed(BattleAction.Attack);
    }

    void EndEnemyTurn(BattleUnit enemy)
    {
        if (enemy == null)
        {
            Debug.LogWarning("[Battle] EndEnemyTurn called with null enemy");
            state = BattleState.Idle;
            return;
        }

        if (TryProcessOverwork()) return;

        enemyRoutine = null;

        //영역 체크
        CheckStatusTileZoneEffect(enemy);

        // 캐스팅 중이면 다음 스킬 선점/라벨 갱신 금지
        var ecs = enemy.GetComponent<EnemyCastState>();
        if (ecs == null || !ecs.IsCasting)
        {
            EmitActionLabel(enemy, ""); // 라벨 비우기
            var ai = enemy.GetComponent<EnemyAI>();
            if (ai != null) ai.PlanNextSkill();  // 다음 턴용 스킬 미리 선정
        }

        enemy.ResetATB();
        OnUnitEndTurn?.Invoke(enemy);

        turnController.CompleteTurn(enemy);

        if (acting == enemy) acting = null;
        state = BattleState.Idle;
    }
    #endregion

    // 도망가기(버튼/F1 공용)
    public async void OnClickEscape()
    {
        // 플레이어 턴에서만 허용, 해 resolving 중(피해 계산 등)에는 금지
        if (acting == null || acting.team != Team.Player) return;
        if (state == BattleState.Resolving) return;

        // 2) 성공 확률 계산 = (해당 유닛 AGI) / (생존한 적군 전체 AGI 합)
        var aliveEnemies = FindObjectsOfType<BattleUnit>()
            .Where(u => u.team == Team.Enemy && !u.IsDead)
            .ToList();
        float enemyAgiSum = Mathf.Max(0.0001f, aliveEnemies.Sum(u => u.EffectiveAGI));
        float successChance01 = Mathf.Clamp01(acting.EffectiveAGI / enemyAgiSum);

        // 퍼센트 변환
        int percent = Mathf.FloorToInt(successChance01 * 100f);

        // 공통 확인 팝업
        string unitName = GetUnitLabel(acting);
        string safeName = unitName.Replace("<", "&lt;").Replace(">", "&gt;");
        string msg = $"<color=#C60004>{safeName}</color> 유닛을 전투에서 제외합니다. 진행할까요?\n" + 
                     $"(탈출 성공 확률: {percent}%)";

        bool ok = await PopupManager.Instance.ConfirmRetreatAsync(msg, successChance01);
        if (!ok) return;    // 사용자가 취소
        // 최종 실행: 성공/실패 롤
        bool success = (Random.value < successChance01);

        if (success)
        {
            // 성공 메시지 → 곧바로 퇴각 처리
            await PopupManager.Instance.ConfirmAsync("탈출에 성공했습니다.", "확인", ""); // 확인만
            RetreatCurrentUnit(acting); // 기존 퇴각 함수 재사용
        }
        else
        {
            // 실패 메시지 → 행동 없이 '턴만 종료'
            await PopupManager.Instance.ConfirmAsync("탈출에 실패했습니다.", "확인", "");
            EndPlayerTurn(); // 플레이어 턴 종료만 수행
        }

        // 진행 중이던 선택/표시 정리
        CancelCurrentAction();
        ClearAllPreviews();
        ClearTargetSelection();
    }

    private string GetUnitLabel(BattleUnit u)
    {
        // 프로젝트에 따라 이름 필드가 다를 수 있어요:
        // DisplayName / UnitName / CharacterName 등 사용 중인 것을 우선적으로 반환
        if (!string.IsNullOrEmpty(u.name)) return u.name;
        if (!string.IsNullOrEmpty(u.name)) return u.name;
        // 없으면 GameObject 이름 fallback
        return u.name;
    }

    //탈출 실행
    void RetreatCurrentUnit(BattleUnit _battleunit)
    {
        if (_battleunit == null) return;

        // 그리드 점유 해제
        TryReleaseGridOccupy(_battleunit);

        // HUD/턴바/기타 UI에 알림
        _battleunit.Retreat(); // UnitStatusPanelUI / TurnBarUI가 이 이벤트로 자기 UI를 제거

        // 유닛 오브젝트 제거
        Destroy(_battleunit.gameObject);

        // 현재 턴 정리 및 ATB 재개
        if (_battleunit == acting)
        {
            turnController.CompleteTurn(_battleunit);
            acting = null;
            state = BattleState.Idle;
        }

        // 전투 종료 체크(전원 퇴각/전멸 등)
        CheckBattleEnd();
    }


    public void OpenSkillPanel()
    {
        if (!IsPlayerTurn || acting == null) return;
        isSelectingSkill = true;

        var raw = acting?.data?.skills ?? System.Array.Empty<SkillAsset>();
        // 표시용은 상태 기반으로 해석된 SO를 전달 → 버튼 라벨이 즉시 반영됨
        var view = new SkillAsset[raw.Length];
        for (int i = 0; i < raw.Length; i++)
        {
            var s = raw[i];
            if (s is ISkillForStateResolver resolver)
                view[i] = resolver.ResolveForCaster(acting) ?? s;
            else view[i] = s;
        }

        OnSkillPanelPopulateSO?.Invoke(view);
        OnSkillPanelToggled?.Invoke(true);
        UpdateTargetingHint();
    }
    public void CloseSkillPanel()
    {
        isSelectingSkill = false;
        OnSkillPanelToggled?.Invoke(false);
        UpdateTargetingHint();
    }

    public void SelectSkill(int index)
    {
        //Debug.Log($"[BattleManager] SelectSkill({index}) 호출");

        var list = acting?.data?.skills;
        if (list == null || index < 0 || index >= list.Length) return;

        var picked = list[index];

        // 상태 기반 스킬 치환(어댑터/라우터가 있으면 실제 사용할 SO로 교체)
        if (picked is ISkillForStateResolver resolver)
            picked = resolver.ResolveForCaster(acting) ?? picked;

        currentSkillSO = picked;

        EnterSkillTargeting(currentSkillSO);
    }
    private void EnterSkillTargeting(SkillAsset skill)
    {
        if (skill == null) return;

        // MP 부족 사전 차단
        int effectiveCost = skill.GetEffectiveCost(acting);
        if (!acting.HasMP(effectiveCost))
        {
            Debug.Log($"[Skill] MP 부족: {skill.displayName} (필요 {effectiveCost})");
            return; // 타겟팅 진입 안 함
        }

        // 쿨다운 사전 차단
        if (acting.IsSkillOnCooldown(skill))
        {
            Debug.Log($"[Skill] 쿨다운: {skill.displayName} (남은 턴 {acting.GetCooldownRemaining(skill)})");
            return;
        }

        if (skill is ISelfCastSkill self && self.SelfCastOnSelect)
        {
            if (skillProcessor.IsResolvingSelfCast)
            {
                Debug.LogWarning($"[SelfCast] 이미 처리 중인 self-cast 스킬입니다...");
                return;
            }

            // 실제 MP 소비는 SelfStateSkill.ResolveOnUnit 내부에서 처리
            bool isFreeAction = false;

            // 이 스킬(=legacy 그룹)에 대해 선택된 훈련 루트
            int route = acting.GetTrainingRouteIndex(skill);

            // 무료 행동 체크 로직
            if (skill is SelfStateSkill sss)
            {
                if (sss.trainingUseFreeAction && sss.routeForFreeAction >= 0 && route == sss.routeForFreeAction)
                    isFreeAction = true;
            }
            else if (skill is SelfStateCleanseSkill scs)
            {
                if (scs.trainingUseFreeAction && scs.routeForFreeAction >= 0 && route == scs.routeForFreeAction)
                    isFreeAction = true;
            }

            // HostilitySpikeSkill: routeForFreeAction 기반
            if (!isFreeAction && skill is HostilitySpikeSkill hss)
            {
                if (hss.trainingUseFreeAction &&
                    hss.routeForFreeAction >= 0 &&
                    route == hss.routeForFreeAction)
                {
                    isFreeAction = true;
                }
            }

            // SelfBeastDomainSkill: routeForFreeAction 기반
            if (!isFreeAction && skill is SelfBeastDomainSkill bds)
            {
                if (bds.trainingUseFreeAction &&
                    bds.routeForFreeAction >= 0 &&
                    route == bds.routeForFreeAction)
                {
                    isFreeAction = true;
                }
            }

            // 코루틴으로 처리해서, 끝난 뒤 무료/일반 행동을 분기
            StartCoroutine(skillProcessor.PerformSelfCastFlow(skill, acting, isFreeAction));
            return;
        }

        // 스킬 타겟팅 모드로 진입
        state = BattleState.Targeting;
        ClearAllPreviews();
        ClearTargetSelection(); // 기존 선택/마커 초기화
        UpdateTargetingHint();

        // Unit 타겟형이면: AGI 내림차순 사이클 구성 후 첫 타겟으로 마커 표시
        if (skill.targetMode == SkillTargetMode.Unit)
        {
            BuildTargetCycle();           // Enemy만, AGI desc
            //if (targetCycle.Count > 0)
            //    SelectTarget(0);          // 첫 타겟(=가장 빠른 AGI)으로 마커/미리보기
        }
        else // Tile 타겟형: 내부 타일 커서를 1회 세팅하고 프리뷰 유지
        {
            currentSkillTargetMap = (skill as ITargetMapProvider)?.GetTargetMap(this, acting) ?? provider?.EnemyFloor;

            if (skill is ISkillCustomPreview customPrev)
            {
                var map = customPrev.GetTargetMap(this, acting) ?? currentSkillTargetMap;
                var cells = customPrev.GetPreviewCells(this, acting);

                currentSkillTargetMap = map;
                customPreviewMap = map;
                customPreviewCells = (cells != null) ? new HashSet<Vector3Int>(cells) : new HashSet<Vector3Int>();

                if (map != null && customPreviewCells != null && customPreviewCells.Count > 0)
                    ShowSkillPreview(map, customPreviewCells);
            }
            else
            {
                customPreviewCells = null;
                customPreviewMap = null;

                //if (customPreviewCells != null && customPreviewCells.Count > 0)
                //{
                //    // 커스텀 프리뷰 잠금 상태에서는 호버 프리뷰를 갱신하지 않음
                //    return;
                //}

                var map = currentSkillTargetMap;

                if (map != null)
                {
                    var cam = Camera.main;
                    var world = cam ? cam.ScreenToWorldPoint(Input.mousePosition) : Vector3.zero;
                    world.z = 0f;
                    var hover = map.WorldToCell(world);
                    if (map.HasTile(hover))
                    {
                        selectedCell = hover;
                        PreviewSkillAreaOnTile(map, selectedCell);
                    }
                }
            }
        }
    }

    private void UpdateTargetingHint()
    {
        // 스킬이 확정되어 타게팅 상태일 때만 힌트 노출
        if (state == BattleState.Targeting && currentSkillSO != null)
        {
            if (currentSkillSO.targetMode == SkillTargetMode.Tile)
                OnHint?.Invoke("위치를 선택하세요");
            else
                OnHint?.Invoke("대상을 선택하세요");
        }
        else
            OnHint?.Invoke(string.Empty);
    }

    // 현재 선택된 스킬의 범위를 "유닛 기준"으로 미리보기
    public void PreviewSkillAreaOnUnit(BattleUnit unit)
    {
        if (currentSkillSO == null || currentSkillSO.targetMode != SkillTargetMode.Unit) { ClearAllPreviews(); return; }
        if (unit == null) { ClearAllPreviews(); return; }
        if (!(currentSkillSO is AllyRetreatSwapSkill) && acting != null && unit.team == acting.team) // 적 대상 스킬일 때만 아군 프리뷰 금지
        {
            ClearAllPreviews();
            return;
        }

        var origin = unit.Cell;                            // 유닛의 현재 셀
        var cells = currentSkillSO.GetAreaCells(origin, SkillLibrary.IsOddColumn(origin));
        var map = unit.CurrentMap;

        // 맵 경계 바깥 셀은 제외
        var validCells = cells.Where(c => map.HasTile(c)).ToList();

        if (validCells.Count == 0)
        {
            // 유효한 셀이 없으면 하이라이트/유닛 강조도 지움
            skillHighlighter?.ClearTransient();
            StatusPanel?.ClearHighlights();
            return;
        }

        // 범위 내 유닛 수집 → 패널 하이라이트
        skillHighlighter.ShowCells(map, validCells);
        var victims = GetUnitsInArea(map, validCells);
        StatusPanel?.HighlightUnits(victims);
    }

    // 현재 선택된 스킬의 범위를 "타일 기준"으로 미리보기
    public void PreviewSkillAreaOnTile(Tilemap map, Vector3Int originCell)
    {
        if (customPreviewCells != null) // 커스텀 프리뷰(후보 잠금)가 활성화되어 있다면,
            return;                    // 호버 기반 프리뷰 갱신을 전면 차단


        if (currentSkillSO == null || currentSkillSO.targetMode != SkillTargetMode.Tile) { ClearAllPreviews(); return; }
        if (map == null) { ClearAllPreviews(); return; }

        var cells = currentSkillSO.GetAreaCells(originCell, SkillLibrary.IsOddColumn(originCell));

        // 맵 경계 바깥 셀은 제외
        var validCells = cells.Where(c => map.HasTile(c)).ToList();

        if (validCells.Count == 0)
        {
            skillHighlighter?.ClearTransient();
            StatusPanel?.ClearHighlights();
            return;
        }

        skillHighlighter.ShowCells(map, validCells);

        // 범위 내 유닛 수집 → 패널 하이라이트
        var victims = GetUnitsInArea(map, validCells);
        StatusPanel?.HighlightUnits(victims);
    }

    public void ConfirmSkillOnUnit(BattleUnit target)
    {
        if (!IsPlayerTurn || acting == null || target == null) return;
        if (currentSkillSO == null || currentSkillSO.targetMode != SkillTargetMode.Unit) return;

        bool isAlly = (acting != null && target.team == acting.team);
        bool allowAlly = (currentSkillSO is AllyRetreatSwapSkill);
        if (IsPlayerTurn && isAlly && !allowAlly) return;

        // 중복 실행 방지
        if (_skillConfirmLocked) return;
        _skillConfirmLocked = true;
        state = BattleState.Resolving;

        // 스킬에게 실행 위임
        StartCoroutine(RunSkillExecute(currentSkillSO, acting, target, null, default));
    }
    public void ConfirmSkillOnTile(Tilemap map, Vector3Int originCell)
    {
        if (!IsPlayerTurn || acting == null || map == null) return;
        if (currentSkillSO == null) return;
        if (_skillConfirmLocked) return;

        // (커스텀 프리뷰 체크 로직은 여기에 남겨도 되고, 스킬 내부로 가져가도 됨. 일단 둠.)
        if (customPreviewCells != null && !customPreviewCells.Contains(originCell)) return;

        _skillConfirmLocked = true;
        state = BattleState.Resolving;

        // 스킬에게 실행 위임
        StartCoroutine(RunSkillExecute(currentSkillSO, acting, null, map, originCell));
    }
    IEnumerator RunSkillExecute(SkillAsset skill, BattleUnit caster, BattleUnit target, Tilemap map, Vector3Int cell)
    {
        ClearSkillPreview(); // 프리뷰 정리

        // 스킬 본연의 Execute 실행
        yield return skill.Execute(this, caster, target, map, cell);

        // 스킬 실행이 끝났는데도(혹은 중단됐는데도) 락이 걸려있으면 강제로 푼다.
        if (_skillConfirmLocked)
        {
            Debug.LogWarning("[BattleManager] 스킬 실행 후 락이 해제되지 않아 강제로 해제합니다.");
            _skillConfirmLocked = false;

            // 상태가 여전히 Resolving이면 Idle로 복구 (상황에 따라 다를 수 있음)
            if (state == BattleState.Resolving)
            {
                state = BattleState.Idle;
                CancelCurrentAction(); // 혹은 state = BattleState.Idle;

                // 만약 행동력이 남아있고 플레이어 턴이면 입력 대기 상태로 복구
                if (IsPlayerTurn && remainingActions > 0)
                    state = BattleState.ActionSelect;
            }
        }
    }
    // 1) 서브 타겟 선택 대기 (넉백, 후퇴 등에서 사용)
    // 기존의 BeginKnockbackSelection 같은 걸 일반화 함
    public IEnumerator WaitForCellSelection(Tilemap map, List<Vector3Int> candidates, System.Action<Vector3Int?> onResult)
    {
        bool done = false;
        Vector3Int? selected = null;

        // UI 모드 전환
        var prevState = state;
        state = BattleState.TargetingKnockback; // 이름은 Knockback이지만 '서브 타겟팅' 용도로 씀

        // OnTileClicked가 유효성 검사를 하려면 이 변수들을 반드시 채워야 함
        _knockbackMap = map;
        _knockbackCandidates = candidates;

        // 힌트 및 하이라이트
        ShowSkillPreview(map, candidates);
        OnHint?.Invoke("위치를 선택하세요");

        // 콜백 연결 (OnTileClicked 등에서 호출해줘야 함)
        // 이를 위해 임시 델리게이트 변수 필요
        System.Action<Vector3Int> selectionHandler = (cell) => {
            selected = cell;
            done = true;
        };

        // *주의: BattleManager의 OnTileClicked에서 TargetingKnockback 상태일 때
        // 이 selectionHandler를 호출하도록 연결 고리가 필요함.
        // 기존 _onKnockbackSelected 델리게이트를 재활용하면 됨.
        _onKnockbackSelected = (cell) => selectionHandler(cell.Value);

        // 대기
        while (!done)
        {
            // 취소 체크 (Q키 등) -> CancelCurrentAction에서 이 코루틴을 깨우거나 상태를 바꿔야 함
            if (state != BattleState.TargetingKnockback)
            {
                onResult(null);
                yield break;
            }
            yield return null;
        }

        // 정리
        _onKnockbackSelected = null;
        _knockbackMap = null;
        _knockbackCandidates = null;
        ClearSkillPreview();
        OnHint?.Invoke(string.Empty);
        state = prevState; // 상태 복구 (Resolving으로 돌아감)

        onResult(selected);
    }

    // 표준 흐름 실행 위임
    public IEnumerator PerformStandardUnitSkillFlow(SkillAsset skill, BattleUnit caster, BattleUnit target)
    {
        yield return skillProcessor.PerformStandardUnitSkillFlow(skill, caster, target);
    }
    public IEnumerator PerformStandardTileSkillFlow(SkillAsset skill, Tilemap map, Vector3Int cell, BattleUnit caster)
    {
        yield return skillProcessor.PerformStandardTileSkillFlow(skill, map, cell, caster);
    }

    // 턴/행동 토큰/스킬 패널에 영향을 주지 않는 "무료 반응 공격"으로 동작
    public Coroutine StartReactiveAttack(BattleUnit caster, BattleUnit target, SkillAsset skill, bool doGapClose)
    {
        if (caster == null || target == null || skill == null) return null;

        // 프로세서에게 위임
        return StartCoroutine(skillProcessor.Co_ReactiveAttackFlow(skill, caster, target, doGapClose));
    }

    // 스킬 범위를 계산해, 같은 맵에 있는 유닛들 중 해당 셀에 위치한 유닛에게 피해 적용
    public void ResolveSkillAtCell(SkillDefinition def, Tilemap map, Vector3Int originCell, BattleUnit caster)
    {
        // 범위 셀들 계산 (axial 변환은 SkillLibrary 내부에서 처리됨)
        var area = def.GetAreaCells(originCell, SkillLibrary.IsOddColumn(originCell));

        // 피격 대상 수집 (같은 맵 + 셀 일치)
        var victims = GetUnitsInArea(map, area);
    }

    // === 유닛 점유/워커블 헬퍼 ===
    bool IsCellOccupied(Tilemap map, Vector3Int cell)
    {
        // 맵→팀 판별 규칙이 명확하면 팀별로 조회
        var team = (map == provider?.PlayerFloor) ? Team.Player : Team.Enemy;
        return grid != null && grid.IsOccupied(team, cell);
    }
    bool IsWalkableCell(Tilemap map, Vector3Int cell)
    {
        if (!map.HasTile(cell)) return false;
        var team = (map == provider?.PlayerFloor) ? Team.Player : Team.Enemy;
        return grid != null && !grid.IsOccupied(team, cell);
    }

    public IEnumerable<BattleUnit> GetUnitsInArea(Tilemap map, IEnumerable<Vector3Int> cells)
    {
        // 맵 경계 바깥 셀 제외(있으면)
        var valid = new HashSet<Vector3Int>(cells
            .Where(c => map.HasTile(c))); // HasTile 체크가 필요 없다면 이 줄은 빼도 됨

        // 씬의 모든 유닛 중 같은 맵에 있고, 셀 좌표가 area 안에 있는 유닛만
        foreach (var u in FindObjectsOfType<BattleUnit>())
        {
            if (u == null || u.CurrentMap != map) continue;
            if (valid.Contains(u.Cell))
                yield return u;
        }
    }

    public int GetFinalSkillDamage(BattleUnit caster, BattleUnit target, SkillAsset source, float baseDamage)
    {
        // 계산은 전문가에게
        return skillProcessor.GetFinalSkillDamage(caster, target, source, baseDamage);
    }

    public void ExecuteSkillDamage(BattleUnit caster, IEnumerable<BattleUnit> victims, SkillAsset source, Tilemap map, Vector3Int originCell)
    {
        // 실행도 전문가에게
        skillProcessor.ExecuteSkillDamage(caster, victims, source, map, originCell);
    }

    public void SetPendingKnockback(ParametricDamageSkill skill, BattleUnit target, Vector3Int dest)
    {
        skillProcessor.SetPendingKnockback(skill, target, dest);
    }

    List<Vector3Int> _knockbackCandidates;
    Tilemap _knockbackMap;
    System.Action<Vector3Int?> _onKnockbackSelected;

    void BeginKnockbackSelection(Tilemap map, List<Vector3Int> candidates, System.Action<Vector3Int?> callback)
    {
        _knockbackMap = map;
        _knockbackCandidates = candidates;
        _onKnockbackSelected = callback;

        state = BattleState.TargetingKnockback;

        // 후보 셀 하이라이트
        ShowSkillPreview(map, _knockbackCandidates);
        OnHint?.Invoke("밀어낼 위치를 선택하세요");
    }

    void EndKnockbackSelection()
    {
        _knockbackMap = null;
        _knockbackCandidates = null;
        _onKnockbackSelected = null;

        ClearSkillPreview();
        ClearTargetSelection();
        OnHint?.Invoke(string.Empty);
    }

    bool ShouldOfferPostSkillMove(BattleUnit unit, SkillAsset skill)
    {
        if (unit == null || skill == null) return false;
        if (!IsPlayerTurn) return false; // 적 턴에는 사용하지 않음

        if (skill is ParametricDamageSkill dmg)
        {
            if (!dmg.trainingUsePostMove) return false;

            int route = unit.GetTrainingRouteIndex(dmg);
            if (dmg.routeForPostMove < 0) return false;

            return route == dmg.routeForPostMove;
        }

        return false;
    }

    IEnumerator Co_PostSkillMoveThenConsume(BattleUnit unit)
    {
        if (unit == null || grid == null)
        {
            // 안전상 바로 행동 소비
            OnActionConsumed(BattleAction.Attack);
            yield break;
        }

        _isPostSkillMoveInProgress = true;

        // 이동 후보: 기본 Move와 동일하게 인접 워커블 셀
        state = BattleState.Moving;
        moveOptions = grid.GetAdjacentWalkable(unit.team, unit.Cell).ToList();

        // 갈 수 있는 칸이 하나도 없으면 → 그냥 행동 소비 후 턴 진행
        if (moveOptions.Count == 0)
        {
            _isPostSkillMoveInProgress = false;
            OnActionConsumed(BattleAction.Attack);
            yield break;
        }

        ShowMovePreview(unit.CurrentMap, moveOptions);

        // 플레이어가 칸을 선택할 때까지 대기
        while (_isPostSkillMoveInProgress)
            yield return null;
    }
    IEnumerator Co_MoveAfterSkillThenConsume(BattleUnit unit, Tilemap map, Vector3Int toCell)
    {
        var fromCell = unit.Cell;
        grid.SetOccupied(unit.team, fromCell, false);

        // 이동 애니메이션 + MoveTo 호출 (여기서 OnMoved 이벤트 발생 -> 패시브 발동)
        yield return unit.AnimateMoveTo(map, toCell);

        grid.SetOccupied(unit.team, unit.Cell, true);

        // 이동 선택 종료
        _isPostSkillMoveInProgress = false;

        // 패시브 리액션이 걸려있다면, 끝날 때까지 대기
        while (_reactionLocks > 0)
        {
            yield return null;
        }

        // 모든 리액션이 끝난 후 행동 소비 및 턴 종료
        OnActionConsumed(BattleAction.Attack);
    }

    public Highlighter beastDomainHighlighter; // 야수의 영역 임시 하이라이트

    public void SpawnBeastDomainZone(Tilemap map, BattleUnit owner, Vector3Int centerCell, int radius, int durationTurns)
    {
        if (!owner || !map) return;


        // 같은 유닛이 만든 이전 영역 제거 + 하이라이트도 함께 삭제
        for (int i = _beastZones.Count - 1; i >= 0; i--)
        {
            var old = _beastZones[i];
            if (old.owner != owner) continue;

            if (old.highlightToken != 0 && beastDomainHighlighter != null)
                beastDomainHighlighter.ClearGroup(old.highlightToken);   // Highlighter 그룹 제거

            _beastZones.RemoveAt(i);
        }

        // 이번 영역의 셀 집합 계산
        var cells = new List<Vector3Int>();
        foreach (var c in AreaShapes.BeastDomainArea(centerCell, radius))
            cells.Add(c);

        // 하이라이트 그룹 생성 후 셀 지정
        int token = 0;
        if (cells.Count > 0 && beastDomainHighlighter != null)
        {
            token = beastDomainHighlighter.CreateGroup();
            beastDomainHighlighter.SetGroupCells(token, map, cells);
        }

        // 4) 영역 등록
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

        Debug.Log(
            $"[BeastDomain] {owner.name} 야수의 영역 생성 - " +
            $"center:{centerCell}, r:{radius}, turns:{durationTurns}, token:{token}");
    }

    // 범용 상태 지대 생성 함수
    public void CreateStatusTileZone(
        BattleUnit owner,
        Tilemap map,
        Vector3Int cell,
        int zoneDuration,
        TileBase newTileBase,
        StatusId statusId,
        int stack = 1,
        int statusDuration = 3)
    {
        if (!map.HasTile(cell)) return;

        // 이미 해당 위치에 지대가 있다면 갱신 (종류가 달라도 덮어쓰기 or 같은 종류면 턴 연장)
        var existing = _statusTileZones.FirstOrDefault(z => z.map == map && z.cell == cell);

        if (existing != null)
        {
            // 기존 지대 갱신 (주인, 지속시간, 그리고 효과 타입도 덮어씌움)
            existing.owner = owner;
            existing.remainingTurns = zoneDuration;
            existing.effectStatusId = statusId; // 독 -> 화염으로 바뀔 수도 있으니
            existing.effectStack = stack;
            existing.effectDuration = statusDuration;

            // 타일 이미지가 다르다면 교체
            if (newTileBase != null) map.SetTile(cell, newTileBase);

            Debug.Log($"[StatusZone] ({cell}) 갱신: {statusId}, {zoneDuration}턴");
            return;
        }

        // 새 지대 생성
        TileBase oldTile = map.GetTile(cell);

        // 시각적 타일 변경
        if (newTileBase != null)
            map.SetTile(cell, newTileBase);

        var newZone = new StatusTileZone
        {
            owner = owner,
            map = map,
            cell = cell,
            remainingTurns = zoneDuration,
            originalTile = oldTile,

            // 효과 설정
            effectStatusId = statusId,
            effectStack = stack,
            effectDuration = statusDuration
        };

        _statusTileZones.Add(newZone);
        Debug.Log($"[StatusZone] ({cell}) 생성: {statusId}, {zoneDuration}턴 (Tile Changed)");
    }
    void TickBeastDomainOnTurnStart(BattleUnit unitWhoseTurnStarted)
    {
        if (unitWhoseTurnStarted == null) return;

        for (int i = _beastZones.Count - 1; i >= 0; i--)
        {
            var z = _beastZones[i];
            if (z.owner != unitWhoseTurnStarted)
                continue;

            // 훈련: 영역 주인의 턴 시작에 '자기 자신' Rage 감소
            TryApplyBeastDomainRageTraining(z.owner);

            z.remainingTurns--;
            Debug.Log($"[BeastDomain] {z.owner.name} 턴 시작 - 야수의 영역 남은 턴: {z.remainingTurns}");

            if (z.remainingTurns <= 0)
            {
                Debug.Log($"[BeastDomain] {z.owner.name} 야수의 영역이 사라집니다 (center:{z.center})");

                // 영역 끝날 때 하이라이트 제거
                if (z.highlightToken != 0 && beastDomainHighlighter != null)
                    beastDomainHighlighter.ClearGroup(z.highlightToken);

                _beastZones.RemoveAt(i);
            }
        }
    }

    // 턴 시작 시 지대 시간 감소 및 복구
    void TickStatusTileZonesOnTurnStart(BattleUnit unit)
    {
        if (unit == null) return;

        for (int i = _statusTileZones.Count - 1; i >= 0; i--)
        {
            var z = _statusTileZones[i];

            // 생성자의 턴이 돌아올 때마다 시간 감소
            if (z.owner == unit)
            {
                z.remainingTurns--;
                if (z.remainingTurns <= 0)
                {
                    // 타일 복구
                    if (z.map != null)
                        z.map.SetTile(z.cell, z.originalTile);

                    _statusTileZones.RemoveAt(i);
                    Debug.Log($"[StatusZone] ({z.cell}) 해제 및 타일 복구");
                }
            }
        }
    }
    void TryApplyBeastDomainRageTraining(BattleUnit owner)
    {
        if (owner == null) return;
        if (owner.data == null || owner.data.skills == null) return;

        // 이 유닛이 들고 있는 SelfBeastDomainSkill 찾기
        SelfBeastDomainSkill domainSkill = null;
        foreach (var s in owner.data.skills)
        {
            domainSkill = s as SelfBeastDomainSkill;
            if (domainSkill != null)
                break;
        }
        if (domainSkill == null) return;

        int route = owner.GetTrainingRouteIndex(domainSkill);

        if (!domainSkill.trainingReduceRageOnTurnStart ||
            domainSkill.routeForRageReduceOnTurnStart < 0 ||
            route != domainSkill.routeForRageReduceOnTurnStart)
            return;

        float amount = 0f;
        float clv = owner.MagicDamage; 
        amount = clv * domainSkill.rageReducePerClv;

        if (amount <= 0f) return;

        owner.AddRage(-amount);
        Debug.Log($"[BeastDomain] Rage 훈련: {owner.name} 자신에게 Rage {amount:F2} 감소");
    }

    // 공포(Fear) 턴 처리
    public IEnumerator Co_HandleFearTurn(BattleUnit unit)
    {
        if (unit == null)
            yield break;

        var usc = unit.GetComponent<UnitStateController>();
        if (usc == null)
            yield break;

        var map = unit.CurrentMap;
        if (map == null || grid == null)
        {
            Debug.LogWarning("[Fear] 맵 또는 그리드가 없습니다.");
            yield break;
        }

        // 뒤로 한 칸 이동 가능한 후보들 계산
        var candidates = GetFearRetreatCandidates(unit);
        if (candidates == null || candidates.Count == 0)
        {
            Debug.Log($"[Fear] {unit.name} 공포 상태지만 뒤로 이동할 수 있는 칸이 없습니다.");
        }
        else
        {
            // 후보 중 랜덤 하나 선택
            var dest = candidates[Random.Range(0, candidates.Count)];

            var from = unit.Cell;
            grid.SetOccupied(unit.team, from, false);
            yield return unit.AnimateMoveTo(map, dest);
            grid.SetOccupied(unit.team, unit.Cell, true);

            Debug.Log($"[Fear] {unit.name} 이(가) {from} → {dest} 로 후퇴했습니다.");
        }

        // 이 턴은 공포 때문에 아무 행동도 못 하고 바로 종료
        if (unit.team == Team.Player)
            EndPlayerTurn();
        else
            EndEnemyTurn(unit);
    }
    // 공포 상태일 때 "한 칸 뒤"로 물러날 수 있는 후보 칸들 계산
    List<Vector3Int> GetFearRetreatCandidates(BattleUnit unit)
    {
        var result = new List<Vector3Int>();
        if (unit == null || grid == null)
            return result;

        var map = unit.CurrentMap;
        if (map == null)
            return result;

        var origin = unit.Cell;

        // 팀별 "뒤쪽" 방향 오프셋 정의
        // 예시 기준:
        // - 플레이어: (-1,0), (-1,-1)
        // - 적군:    ( 1,0), ( 0, 1)
        Vector3Int[] offsets;
        if (unit.team == Team.Player)
        {
            offsets = new[]
            {
            new Vector3Int(-1, 0, 0),
            new Vector3Int(-1, -1, 0),
        };
        }
        else // Team.Enemy
        {
            offsets = new[]
            {
            new Vector3Int(1, 0, 0),
            new Vector3Int(0, 1, 0),
        };
        }

        foreach (var off in offsets)
        {
            var dest = origin + off;

            // 타일이 존재하고, 해당 팀 기준으로 워커블인지 검사
            if (!map.HasTile(dest))
                continue;

            if (grid.IsOccupied(Team.Player, dest) || grid.IsOccupied(Team.Enemy, dest))
                continue;

            result.Add(dest);
        }

        return result;
    }

    int HexDistance(Vector3Int a, Vector3Int b)
    {
        var axA = SkillLibrary.OffsetToAxial(a);
        var axB = SkillLibrary.OffsetToAxial(b);

        int dq = Mathf.Abs(axA.x - axB.x);
        int dr = Mathf.Abs(axA.y - axB.y);
        int ds = Mathf.Abs((-axA.x - axA.y) - (-axB.x - axB.y));

        return (dq + dr + ds) / 2;
    }
    bool IsBeastDomainFreeMove(BattleUnit unit, Tilemap map, Vector3Int fromCell, Vector3Int toCell)
    {
        if (unit == null || map == null) return false;

        foreach (var z in _beastZones)
        {
            if (z.owner != unit) continue;
            if (z.map != map) continue;

            bool fromIn = HexDistance(z.center, fromCell) <= z.radius;
            bool toIn = HexDistance(z.center, toCell) <= z.radius;

            if (fromIn && toIn)
                return true;
        }

        return false;
    }


    bool IsEnemyOf(BattleUnit a, BattleUnit b)
    {
        // 프로젝트에 따라 팀 판별 방법이 다르면 이곳을 연결
        return a != null && b != null && a.team != b.team;
    }

    public void UnlockSkillConfirm()
    {
        _skillConfirmLocked = false;
    }

    public void FinishActionAfterSkill()
    {
        // 어떤 스킬이었는지, 누구 차례였는지 로컬로 잡아둔다
        var skill = currentSkillSO;
        var unit = acting;

        // 하이라이트/선택 상태 정리
        ClearSkillPreview();
        // 스킬 실행 완료 → 패널 닫기 + 스킬 선택 해제
        CloseSkillPanel();   // 이벤트까지 함께 발행됨

        // 스킬 처리 종료 시 입력 락 해제
        UnlockSkillConfirm();

        // 기술 사용 후 1칸 이동
        if (unit != null && skill != null && ShouldOfferPostSkillMove(unit, skill))
        {
            // 스킬 쿨다운/MP는 이미 처리된 상태이므로,
            // 여기서는 '공격 토큰'을 바로 소모하지 않고 이동 기회를 먼저 준다.
            currentSkill = default;
            currentSkillSO = null;
            currentSkillTargetMap = null;
            customPreviewCells = null;
            customPreviewMap = null;
            UpdateTargetingHint();

            StartCoroutine(Co_PostSkillMoveThenConsume(unit));
            return;
        }

        // 스킬은 '공격'으로 간주하여 행동 토큰 소모 로직 재사용
        OnActionConsumed(BattleAction.Attack);

        currentSkill = default;           // 레거시
        currentSkillSO = null;            // SO도 클리어
        currentSkillTargetMap = null;
        customPreviewCells = null;
        customPreviewMap = null;
        UpdateTargetingHint();
    }


    // 타겟팅 취소/종료 시 미리보기 지우기
    public void ShowMovePreview(Tilemap baseMap, IEnumerable<Vector3Int> cells)
    {
        moveHighlighter?.ShowCells(baseMap, cells);
    }
    public void ShowSkillPreview(Tilemap baseMap, IEnumerable<Vector3Int> cells)
    {
        if (customPreviewCells != null)
        {
            // 후보가 0개면 아무 것도 그리지 않음
            if (customPreviewCells.Count == 0) return;

            // 락에서 전달한 동일 참조가 아닌 임의 셀은 무시(호버 등 외부 호출 차단)
            if (!object.ReferenceEquals(cells, customPreviewCells)) return;
        }

        skillHighlighter?.ShowCells(baseMap, cells);
    }

    public void ClearMovePreview()
        => moveHighlighter?.ClearTransient();
    public void HoldSkillPreview()
        => _skillPreviewHold++;
    public void ReleaseSkillPreview()
        => _skillPreviewHold = Mathf.Max(0, _skillPreviewHold - 1);
    public void ClearSkillPreview()
    {
        if (_skillPreviewHold == 0)
            skillHighlighter?.ClearTransient();

        StatusPanel?.ClearHighlights();
    }
    public void ClearAllPreviews()
    {
        ClearMovePreview();
        ClearSkillPreview(); // (hold 중이면 지워지지 않음)
        StatusPanel?.ClearHighlights();
    }

    // === 지속(토큰) 스킬 프리뷰 API ===
    public int CreateSkillPreviewToken() => skillHighlighter != null ? skillHighlighter.CreateGroup() : 0;

    public void SetSkillPreviewForToken(int token, Tilemap map, IEnumerable<Vector3Int> cells)
        => skillHighlighter?.SetGroupCells(token, map, cells);

    public void ClearSkillPreviewToken(int token)
        => skillHighlighter?.ClearGroup(token);

    // “n초 후 자동으로 지운다” 버전
    public void EmitPassiveLabelAutoClear(BattleUnit u, string label, float seconds = 1.0f)
    {
        OnUnitPassiveLabel?.Invoke(u, label);
        StartCoroutine(Co_ClearPassiveLabelAfter(seconds));
    }
    IEnumerator Co_ClearPassiveLabelAfter(float t)
    {
        yield return new WaitForSeconds(t);
        OnUnitPassiveLabel?.Invoke(null, ""); // 빈 라벨 = 클리어 신호
    }

    // 스킬 프로세서 등에서 '무료 행동' 처리 후 상태를 리셋할 때 호출
    public void ResetSkillSelectionState()
    {
        ClearSkillPreview();
        CloseSkillPanel();

        currentSkill = default;
        currentSkillSO = null;
        currentSkillTargetMap = null;
        customPreviewCells = null;
        customPreviewMap = null;

        UpdateTargetingHint();
        UnlockSkillConfirm(); // 락 해제

        if (IsPlayerTurn)
            state = BattleState.ActionSelect;
    }
}
