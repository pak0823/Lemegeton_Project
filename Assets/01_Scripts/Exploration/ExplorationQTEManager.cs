using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

public class ExplorationQTEManager : MonoBehaviour
{
    // [핵심] 구체적인 클래스 대신 추상 클래스로 선언.
    // 인스펙터에는 SimpleQTEController가 달린 오브젝트를 드래그앤드롭 하면 됨. (다형성)
    public static ExplorationQTEManager Instance { get; private set; }  // 싱글톤 패턴

    [Header("System References")]
    [SerializeField] private BaseQTEController qteController; // QTE UI 컨트롤러
    public PlayerMovement playerMovement;   // 플레이어 움직임을 멈추기 위한 참조

    [Header("Settings")]
    [SerializeField] private int targetScore = 6; // 최종 성공 점수
    [SerializeField] private int failScoreCondition = -6; // 최종 실패 점수

    private bool _isEventPlaying = false;

    private void Awake()
    {
        // 싱글톤 초기화
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // QTE UI는 꺼두고 시작
        if (qteController != null) qteController.Init();
    }
    /// <summary>
    /// 외부 오브젝트(보물상자 등)가 이 함수를 호출해서 QTE를 시작함.
    /// </summary>
    /// <param name="onSuccess">성공 시 실행할 로직 (아이템 획득 등)</param>
    /// <param name="onFail">실패 시 실행할 로직 (데미지 입음 등)</param>
    public void StartExplorationEvent(Action onSuccess, Action onFail)
    {
        if (_isEventPlaying) return; // 이미 진행 중이면 무시

        StartCoroutine(Co_ProcessLoopEvent(onSuccess, onFail));
    }
    private IEnumerator Co_ProcessLoopEvent(Action onSuccess, Action onFail)
    {
        _isEventPlaying = true;
        int currentScore = 0;
        int roundCount = 0;

        Debug.Log($"[System] QTE 시작! 목표: {targetScore} / 패배조건: {failScoreCondition}");

        //Player 이동 정지
        if (playerMovement != null)
        {
            playerMovement.HaltImmediately();       // 가던 길 멈춤
            playerMovement.LockMovementIndefinite(); // 입력 잠금 토큰 증가
        }

        // 목표 점수 도달할 때까지 반복
        while (currentScore < targetScore)
        {
            roundCount++;
            Debug.Log($"[Round {roundCount}] 현재 점수: {currentScore} (목표: {targetScore}, 패배: {failScoreCondition})");

            bool isFinished = false;
            QTEResult roundResult = QTEResult.Fail;

            // QTE 실행
            qteController.StartQTE((QTEResult result) => {
                roundResult = result;
                isFinished = true;
            });

            while (!isFinished) yield return null;

            // 점수 계산
            switch (roundResult)
            {
                case QTEResult.Perfect:
                    currentScore += 2;
                    Debug.Log($" >> 대성공! (+2점) | 합계: {currentScore}");
                    break;
                case QTEResult.Success:
                    currentScore += 1;
                    Debug.Log($" >> 성공! (+1점) | 합계: {currentScore}");
                    break;
                case QTEResult.Fail:
                    currentScore -= 1;
                    // 점수가 음수가 되는 걸 방지하고 싶으면 아래 주석 해제
                    // if (currentScore < 0) currentScore = 0;
                    Debug.Log($" >> 실패... (-1점) | 합계: {currentScore}");
                    break;
            }

            // 목표 달성 시 (성공)
            if (currentScore >= targetScore)
            {
                Debug.Log($"[System] 최종 승리! (점수: {currentScore})");
                onSuccess?.Invoke();
                break; // 루프 탈출
            }

            // 패배 조건 도달 시 (실패)
            if (currentScore <= failScoreCondition)
            {
                Debug.Log($"[System] 최종 패배... (점수: {currentScore})");
                onFail?.Invoke();
                break; // 루프 탈출
            }

            // 아직 안 끝났으면 쿨타임 후 다음 라운드
            float waitTime = Random.Range(3.0f, 6.0f);
            Debug.Log($"[System] 다음 라운드까지 {waitTime:F1}초 대기...");
            yield return new WaitForSeconds(waitTime);
        }

        if (playerMovement != null)
        {
            playerMovement.UnlockMovementIndefinite();
        }

        _isEventPlaying = false;
    }
}
