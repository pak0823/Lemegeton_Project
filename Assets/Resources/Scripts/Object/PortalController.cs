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

    public string GetHintLabel() => "이동";

    public void UsePortal()
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
            StageRuntimeContext.Instance.SetBattleContext(battleContextWhenUsed);
        }

        Shared.SceneTransitionManager.FadeToScene(targetScene);
        Debug.Log("[PortalController] UsePortal -> scene transition");
    }
}
