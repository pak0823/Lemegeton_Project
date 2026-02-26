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



            if (SceneTransitionManager.Instance != null)

            {

                // 맵 프리팹 오버라이드 제거 → 새 게임은 항상 새 맵 결정을 하도록

                SceneTransitionManager.Instance.explorationMapPrefabOverride = null;

                // 스냅샷/귀환지점 제거 → 상태/게이지/오브젝트 복원 금지

                SceneTransitionManager.Instance.ClearExplorationSnapshot();

            }

            // [New] 세이브 데이터 완전 초기화 (새 게임 시작 시 구동)
            if (deleteSaves)
            {
                if (PlayerDataManager.Instance != null)
                {
                    PlayerDataManager.Instance.ClearSaveDataAndReset();
                }
                else
                {
                    // 타이틀 씬 등에서 게임 첫 실행으로 매니저 인스턴스가 아직 없는 경우
                    // 직접 파일 경로를 참조해 물리적으로 세이브 파일을 제거합니다.
                    string savePath = System.IO.Path.Combine(Application.persistentDataPath, "savedata.json");
                    if (System.IO.File.Exists(savePath))
                    {
                        System.IO.File.Delete(savePath);
                        Debug.Log("[GameResetter] 런타임 인스턴스 생성 전, 기존 세이브 데이터를 물리적으로 삭제했습니다.");
                    }
                }
            }

        }

    }

}
