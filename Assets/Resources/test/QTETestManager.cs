using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class QTETestManager : MonoBehaviour
{
    [Header("QTE")]
    public SimpleQTEController qteController; // 인스펙터에서 할당하세요.
    private bool _isTestRunning = false;
    private IEnumerator Co_ExecuteSkillWithQTE()
    {
        _isTestRunning = true;

        // 1. QTE 시작
        bool qteFinished = false;
        bool qteSuccess = false;

        // QTE UI를 켜고 콜백을 등록합니다.
        qteController.StartQTE((bool result) => {
            qteSuccess = result;
            qteFinished = true;
        });

        // 2. 플레이어가 QTE를 마칠 때까지 대기
        while (!qteFinished)
        {
            yield return null;
        }

        // 3. 결과에 따른 분기 처리
        if (qteSuccess)
        {
            Debug.Log("QTE 성공!");
        }
        else
        {
            Debug.Log("QTE 실패...");
        }

        _isTestRunning = false;
    }

    private void Update()
    {
        if (!_isTestRunning && Input.GetKeyDown(KeyCode.T))
        {
            StartCoroutine(Co_ExecuteSkillWithQTE());
        }
    }
}
