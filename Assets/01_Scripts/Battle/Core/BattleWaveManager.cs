using System.Collections;

using System.Collections.Generic;

using System.Linq;

using UnityEngine;

using UnityEngine.Tilemaps;



public class BattleWaveManager : MonoBehaviour

{

    private BattleManager battleManager;

    private IGridProvider grid;

    private IBattleMapProvider map;



    #region Variables

    [Header("Configuration")]

    [SerializeField] private WaveSet waveSet;

    [SerializeField] private StageDatabase stageDB;

    [SerializeField] private bool autoAssignWaveSet = true;

    [SerializeField] private Transform enemyRoot;

    [SerializeField] private float waveTransitionDelay = 1.5f;



    [Header("Debug")]

    [SerializeField] private BattleContext debugContext = BattleContext.TrapEncounter;

    [SerializeField] private int debugStageNumber = -1;
    [SerializeField] private string debugStageID = ""; // 디버그용 스테이지 ID



    // 내부 상태

    private int _currentWaveIndex = -1;

    private GameObject _spawnedEnemyLayout;

    private bool _isWaveTransitioning = false;

    private bool _battleEndedOnce = false;



    // 프로퍼티

    public int CurrentWave => _currentWaveIndex + 1;

    public int TotalWaves => waveSet ? waveSet.waves.Count : 0;

    public bool IsWaveTransitioning => _isWaveTransitioning;

    public WaveSet CurrentWaveSet => waveSet;



    // BattleManager에게 상황을 알릴 이벤트

    // (현재웨이브, 총웨이브, 라벨)

    public event System.Action<int, int, string> OnWaveInfoUpdated;

    // (다음웨이브, 총웨이브) - 전환 연출용

    public event System.Action<int, int> OnWaveTransitionStarted;

    // 웨이브 로드 완료! (적 맵 매니저, 적 오버레이) -> BM이 받아서 유닛 리바인딩 해야 함

    public event System.Action<BattleMapManager, Tilemap, Tilemap> OnWaveLoaded;

    // 모든 웨이브 클리어!

    public event System.Action OnAllWavesCleared;

    #endregion



    public void Initialize(BattleManager _battleManager, IGridProvider _grid, IBattleMapProvider _map)
    {
        battleManager = _battleManager;
        grid = _grid;
        map = _map;
    }

    public void StartFirstWave()
    {
        if (autoAssignWaveSet) AutoResolveWaveSet();

        if (waveSet == null || waveSet.waves == null || waveSet.waves.Count == 0)
        {
            Debug.LogWarning("[WaveManager] 웨이브 세트가 없습니다. 테스트 모드 혹은 배치된 유닛 사용.");
            OnWaveLoaded?.Invoke(null, null, null);
        }
        else

        {

            LoadWave(0);

        }

    }



    public void LoadWave(int index)

    {

        if (waveSet == null || waveSet.waves == null || index < 0 || index >= waveSet.waves.Count)

        {

            Debug.LogError($"[WaveManager] Invalid Wave Index: {index}");

            return;

        }



        // 1. 기존 적 청소

        CleanupEnemiesAndLayouts();



        _currentWaveIndex = index;

        var w = waveSet.waves[index];



        // 2. 적 레이아웃 스폰

        Tilemap waveEnemyFloor = null;

        Tilemap waveEnemyOverlay = null;

        BattleMapManager localProvider = null;



        if (w.enemyLayoutPrefab)
        {
            _spawnedEnemyLayout = Instantiate(w.enemyLayoutPrefab, enemyRoot ? enemyRoot : transform);

            // 맵 프로바이더 탐색
            localProvider = _spawnedEnemyLayout.GetComponentInChildren<BattleMapManager>(true);

            if (localProvider != null)
            {
                waveEnemyFloor = localProvider.EnemyFloor;
            }
            else
            {
                // Fallback: 이름으로 찾기
                waveEnemyFloor = _spawnedEnemyLayout.GetComponentsInChildren<Tilemap>(true)
                    .FirstOrDefault(t => t.name.IndexOf("Enemy", System.StringComparison.OrdinalIgnoreCase) >= 0);
            }

            // 오버레이 찾기
            waveEnemyOverlay = _spawnedEnemyLayout.GetComponentsInChildren<Tilemap>(true)
                .FirstOrDefault(t => t.name.IndexOf("Overlay_Skill", System.StringComparison.OrdinalIgnoreCase) >= 0);
        }
        else if (w.enemySpawns != null && w.enemySpawns.Count > 0)
        {
            // 동적 소환을 위해 임시 레이아웃 생성
            _spawnedEnemyLayout = new GameObject("[DynamicEnemyLayout]");
            _spawnedEnemyLayout.transform.SetParent(enemyRoot ? enemyRoot : transform, false);

            // 베이스 맵은 없으므로 일단 널
            localProvider = null;
            waveEnemyFloor = null;
            waveEnemyOverlay = null;

            // BattleManager의 Grid 맵 인스턴스를 활용하여 위치 지정
            var bm = BattleManager.Instance;
            var gridManager = UnityEngine.Object.FindObjectOfType<BattleGridManager>();

            if (bm != null && gridManager != null && gridManager.GetMap(Team.Enemy) != null)
            {
                foreach (var spawnInfo in w.enemySpawns)
                {
                    if (spawnInfo.enemyPrefab == null) continue;

                    GameObject obj = Instantiate(spawnInfo.enemyPrefab, _spawnedEnemyLayout.transform);
                    Vector3 worldPos = gridManager.GetMap(Team.Enemy).GetCellCenterWorld(spawnInfo.spawnCell);
                    worldPos.z = 0; // z 보정
                    obj.transform.position = worldPos;

                    var unit = obj.GetComponent<BattleUnit>();
                    if (unit != null)
                    {
                        // Set the unit's cell position if a setter exists, or use reflection if private.
                        // For now we assume the Mover will handle it when initialized, or we just set transform.
                        // Since Cell is get-only, rely on Mover or external initialization setting it.
                        // unit.Cell = spawnInfo.spawnCell;
                    }
                }
            }
            else
            {
                Debug.LogError("[WaveManager] BattleGridManager or Enemy map is missing, cannot place dynamic enemies onto grid!");
            }
        }



        // 3. UI 알림 이벤트 발송

        OnWaveInfoUpdated?.Invoke(CurrentWave, TotalWaves, w.label);



        // 4. BM에게 "야, 맵이랑 적 깔아놨으니까 네가 처리해(Rebind)"라고 신호 보냄

        OnWaveLoaded?.Invoke(localProvider, waveEnemyFloor, waveEnemyOverlay);



        Debug.Log($"[WaveManager] Wave {CurrentWave}/{TotalWaves} 로드 완료 - {w.label}");

    }



    public void TryAdvanceToNextWave()

    {

        if (_isWaveTransitioning) return;

        StartCoroutine(Co_NextWave());

    }



    private IEnumerator Co_NextWave()

    {

        _isWaveTransitioning = true;

        int nextIndex = _currentWaveIndex + 1;



        // 다음 웨이브가 있다면 연출

        if (waveSet != null && waveSet.waves != null && nextIndex < TotalWaves)

        {

            OnWaveTransitionStarted?.Invoke(nextIndex + 1, TotalWaves);

            yield return new WaitForSecondsRealtime(Mathf.Max(0f, waveTransitionDelay));

        }



        // 웨이브 끝?

        if (waveSet == null || waveSet.waves == null || nextIndex >= TotalWaves)

        {

            _isWaveTransitioning = false;

            if (!_battleEndedOnce)

            {

                _battleEndedOnce = true;

                OnAllWavesCleared?.Invoke(); // 승리 처리는 BM이 함

            }

            yield break;

        }



        LoadWave(nextIndex);

        _isWaveTransitioning = false;

    }



    private void CleanupEnemiesAndLayouts()

    {

        // 1) 레이아웃 프리팹 제거

        if (_spawnedEnemyLayout)

        {

            var enemyUnits = _spawnedEnemyLayout.GetComponentsInChildren<BattleUnit>(true);

            foreach (var u in enemyUnits)

            {

                if (u == null) continue;

                if (u.Stats != null && !u.IsDead && u.Mover != null)

                {

                    grid?.SetOccupied(u.data.team, u.Cell, false);

                }

                Destroy(u.gameObject);

            }

            Destroy(_spawnedEnemyLayout);

            _spawnedEnemyLayout = null;

        }



        // 2) 잔여 적 제거 (혹시 레이아웃 밖에서 생성된 놈들)
        // [Optimization] Use registry
        var bm = BattleManager.Instance;
        var leftovers = (bm != null)
            ? bm.GetAliveUnits(Team.Enemy).ToList()
            : FindObjectsOfType<BattleUnit>().Where(u => u.data.team == Team.Enemy).ToList();

        foreach (var u in leftovers)

        {

            Destroy(u.gameObject);

        }

    }



    private void AutoResolveWaveSet()
    {
        if (stageDB == null) stageDB = StageDatabase.Instance;

        // 1. 싱글톤에서 우선적으로 값 가져오기
        string stageId = StageRuntimeContext.Instance != null && !string.IsNullOrEmpty(StageRuntimeContext.Instance.CurrentStageID)
            ? StageRuntimeContext.Instance.CurrentStageID
            : "";
        int stageNo = StageRuntimeContext.Instance != null && StageRuntimeContext.Instance.CurrentStageNumber >= 0
            ? StageRuntimeContext.Instance.CurrentStageNumber
            : -1;
        var ctx = StageRuntimeContext.Instance != null && StageRuntimeContext.Instance.CurrentStageNumber >= 0
            ? StageRuntimeContext.Instance.CurrentBattleContext
            : debugContext;

        // 싱글톤에서 아무것도 건지지 못한 경우 (주로 전투씬 다이렉트 테스트 시) 인스펙터 디버그 값 활용
        if (string.IsNullOrEmpty(stageId) && stageNo < 0)
        {
            stageId = debugStageID;
            stageNo = debugStageNumber;
            ctx = debugContext;
        }

        if (stageDB == null) return;

        StageNormalMapData found = null;

        // 2. ID로 먼저 검색
        if (!string.IsNullOrEmpty(stageId))
        {
            found = stageDB.GetStage(stageId);
        }

        // 3. ID 검색 실패 시 번호로 검색 (Legacy)
        if (found == null && stageNo >= 0)
        {
            found = stageDB.normalStages.FirstOrDefault(s => s != null && s.stageNumber == stageNo);
        }

        // 4. [안전 장치] 테스트 중 스테이지 정보가 잘못되어도 테스트용으로 첫번째 스테이지 강제 할당
        if (found == null && stageDB.normalStages != null && stageDB.normalStages.Length > 0)
        {
            found = stageDB.normalStages[0];
            Debug.LogWarning($"[WaveManager] 지정된(또는 디버그) 스테이지를 찾을 수 없어 DB의 첫 번째 스테이지(No.{found.stageNumber})를 임시 할당합니다.");
        }

        // 5. 웨이브 셋 결정
        if (found != null)
        {
            // Context로 조회, 없으면 레거시 필드 반환
            waveSet = found.GetWaveSet(ctx);

            // [안전 장치] 테스트 중 (예: Trap Encounter) 해당 컨텍스트가 DB에 없을 경우 아무 웨이브라도 할당하기 위해 폴백
            if (waveSet == null && found.contextWaves != null && found.contextWaves.Count > 0)
            {
                Debug.LogWarning($"[WaveManager] '{ctx}' 컨텍스트에 할당된 웨이브가 없어 빈 오브젝트를 방지하기 위해 첫 번째 컨텍스트의 웨이브를 가져옵니다.");
                waveSet = found.GetWaveSet(found.contextWaves[0].contextType);
            }

            Debug.Log($"[WaveManager] Auto-assigned: Stage '{found.stageId}' (No.{found.stageNumber}), {ctx} -> {waveSet?.name}");
        }
    }

}
