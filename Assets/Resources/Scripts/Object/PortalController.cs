using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PortalController : MonoBehaviour
{
    [Header("이동할 씬 이름")]
    public string targetScene = "BattleScene";

    [Header("전투 컨텍스트 전달(선택)")]
    [Tooltip("이 포탈을 사용할 때 전투 컨텍스트를 세팅할지 여부 (BattleScene으로 갈 때만 의미 있음)")]
    [SerializeField] private bool setBattleContextOnUse = false;
    [SerializeField] private BattleContext battleContextWhenUsed = BattleContext.AfterPuzzle;
    [SerializeField] private StageNormalMapData currentStageData; // 현재 스테이지 데이터
    [SerializeField] private int stageNumberOverride = -1;        // 데이터가 없으면 이 값 사용

    private bool playerInRange; //플레이어 감지

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
            Debug.Log("playerInRange is True");
            // 화면에 "F: 포탈로 이동" 같은 UI 띄우기 추가 가능
        }
    }
    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            Debug.Log("playerInRange is False");
            // UI 숨기기 추가 가능
        }
    }

    void Update()
    {
        if (playerInRange && Input.GetKeyDown(KeyCode.F))
        {
            // 전투씬으로 이동하는 포탈이라면 컨텍스트를 먼저 세팅
            if (setBattleContextOnUse && targetScene == "BattleScene")
            {
                if (StageRuntimeContext.Instance == null)
                    new GameObject("StageRuntimeContext").AddComponent<StageRuntimeContext>();

                int stageNo = (currentStageData != null) ? currentStageData.stageNumber :
                (stageNumberOverride >= 0 ? stageNumberOverride : -1);
                if (stageNo < 0)
                    Debug.LogWarning("[PortalController] stage number not set. (currentStageData or stageNumberOverride)");

                StageRuntimeContext.Instance.SetStageNumber(stageNo);
                StageRuntimeContext.Instance.SetBattleContext(battleContextWhenUsed); // 기본 AfterPuzzle
            }

            // 페이드 전환 호출
            Shared.SceneTransitionManager.FadeToScene(targetScene);
            Debug.Log("씬 전환함");
        }
    }
}
