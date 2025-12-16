using UnityEngine;

namespace Project
{
    public static class GameResetter
    {
        /// <summary>
        /// deleteSaves=true면 저장 데이터까지 전부 삭제(공장초기화).
        /// false면 런타임 상태만 초기화(새 게임 시작용).
        /// </summary>
        public static void ResetAll(bool deleteSaves = false)
        {
            // 타임/일시정지
            GamePause.IsPaused = false; // 전역 일시정지 해제
            Time.timeScale = 1f;        // 혹시 모를 배속 잔상 제거

            var speed = Object.FindObjectOfType<GameSpeedController>();
            if (speed) speed.SetSpeedIndex(0); // 배속/고정델타 원복

            // 정적 이벤트/캐시 비우기 (씬 간 잔상 방지)
            BattleManager.ClearStatic();

            if (Shared.SceneTransitionManager != null)
            {
                // 맵 프리팹 오버라이드 제거 → 새 게임은 항상 새 맵 결정을 하도록
                Shared.SceneTransitionManager.explorationMapPrefabOverride = null;
                // 스냅샷/귀환지점 제거 → 상태/게이지/오브젝트 복원 금지
                Shared.SceneTransitionManager.ClearExplorationSnapshot();
            }

            // 남아있을 수 있는 런타임 싱글톤 제거(있을 때만)
            if (StageRuntimeContext.Instance != null)
                Object.Destroy(StageRuntimeContext.Instance.gameObject);

            if (Shared.PuzzleManager != null)
            {
                Shared.PuzzleManager.ClearMaps(); // 타일맵/목표/캐시 비우기
            }

            ExplorationTimerUi.ClearSavedRuntime();   // 탐험 타이머 정적 메모리도 초기화

            // 저장 데이터 삭제(선택)
            if (deleteSaves)
            {
                PlayerPrefs.DeleteAll();
                PlayerPrefs.Save(); // 동기 플러시
            }
        }
    }
}
