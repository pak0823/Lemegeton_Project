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

        var leftovers = FindObjectsOfType<BattleUnit>().Where(u => u.data.team == Team.Enemy).ToList();

        foreach (var u in leftovers)

        {

            Destroy(u.gameObject);

        }

    }



    private void AutoResolveWaveSet()

    {

        if (stageDB == null) stageDB = Resources.Load<StageDatabase>("DB/StageDatabase");



        // 싱글톤 참조 (BM에 있던 로직 그대로)

        int stageNo = StageRuntimeContext.Instance != null && StageRuntimeContext.Instance.CurrentStageNumber >= 0

            ? StageRuntimeContext.Instance.CurrentStageNumber

            : debugStageNumber;



        var ctx = StageRuntimeContext.Instance != null

            ? StageRuntimeContext.Instance.CurrentBattleContext

            : debugContext;



        if (stageDB == null || stageNo < 0) return;



        StageNormalMapData found = stageDB.normalStages.FirstOrDefault(s => s != null && s.stageNumber == stageNo);



        if (found != null)

        {

            waveSet = (ctx == BattleContext.TrapEncounter) ? found.trapEncounterWave : found.postPuzzleWave;

            Debug.Log($"[WaveManager] Auto-assigned: Stage {stageNo}, {ctx} -> {waveSet?.name}");

        }

    }

}