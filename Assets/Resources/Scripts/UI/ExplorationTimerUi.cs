using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Project.UI;

public class ExplorationTimerUi : MonoBehaviour
{
    public Text timerText;
    public Text endMessageText;

    private List<int> fibonacciMinutes = new List<int> { 5, 3, 2, 1 };
    private int currentStage = 0;

    private float timeLeft = 0f;
    private bool isRunning = false;
    private bool isFinished = false;

    // (임시) 인지 게이지 표기. 주석대로 공통 HUD로 이전 가능.
    public Text objectGaugeText;
    public Text perceiveGaugeText;

    private void OnEnable()
    {
        currentStage = 0;
        isFinished = false;
        StartNextStage();
    }

    private void OnDisable()
    {
        // 깔끔한 정리를 위해 비활성화 시 실행 중지
        isRunning = false;
    }

    public void OnUiShown()
    {
        // 오브젝트가 켜진 타이밍에 1회 초기화
        currentStage = 0;
        isFinished = false;
        StartNextStage();
    }
    public void OnUiHidden()
    {
        // 꺼질 때 안전 정리
        isRunning = false;
        if (timerText) timerText.gameObject.SetActive(false);
        if (objectGaugeText) objectGaugeText.gameObject.SetActive(false);
        if (perceiveGaugeText) perceiveGaugeText.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!isRunning || isFinished)
        {
            if (timerText && timerText.gameObject.activeSelf)
            {
                timerText.gameObject.SetActive(false);
                if (objectGaugeText) objectGaugeText.gameObject.SetActive(false);  // 임시용
                if (perceiveGaugeText) perceiveGaugeText.gameObject.SetActive(false);  // 임시용
            }            
            return;
        }

        timeLeft -= Time.deltaTime;

        if (timeLeft <= 0f)
        {
            timeLeft = 0f;
            currentStage++;

            if (currentStage < fibonacciMinutes.Count)
            {
                StartNextStage();
                Shared.ObjectGaugeManager.IncrementAwarenessByTimer();//인지 게이지 증가
            }
            else
            {
                StartCoroutine(ShowEndMessage());
                isRunning = false;
                isFinished = true;
            }
        }

        UpdateTimerUI();
        GaugeTextUi();
    }

    private void StartNextStage()
    {
        timeLeft = fibonacciMinutes[currentStage] * 60f;
        isRunning = true;
        isFinished = false;
        if (endMessageText) endMessageText.gameObject.SetActive(false);
        UpdateTimerUI();
    }

    void UpdateTimerUI()
    {

        if(timeLeft > 60f)
        {
            int minutes = Mathf.FloorToInt(timeLeft / 60f);
            int seconds = Mathf.FloorToInt(timeLeft % 60f);
            timerText.text = $"{minutes:D2}:{seconds:D2}";
        }
        else
        {
            int seconds = Mathf.FloorToInt(timeLeft);
            int milliseconds = Mathf.FloorToInt((timeLeft - seconds) * 1000);
            timerText.text = $"{seconds:D2}:{milliseconds:D3}";
        }
    }

    IEnumerator ShowEndMessage()
    {
        endMessageText.gameObject.SetActive(true);
        endMessageText.text = "나를 주시하던 무언가의 기척이 사라졌습니다.";

        Color color = endMessageText.color;
        color.a = 0f;
        endMessageText.color = color;

        float duration = 0.5f;
        float elapsed = 0f;

        //Fade In
        while(elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Clamp01(elapsed / duration);
            color.a = alpha;
            endMessageText.color = color;
            yield return null;
        }

        // 유지 시간
        yield return new WaitForSeconds(3f);

        //Fade Out
        elapsed = 0f;
        while(elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - Mathf.Clamp01(elapsed / duration); 
            color.a = alpha;
            endMessageText.color = color;
            yield return null;
        }

        endMessageText.gameObject.SetActive(false);
    }

    //임시용 함수
    public void GaugeTextUi()
    {
        float percent = Shared.ObjectGaugeManager.GetGaugePercent();
        objectGaugeText.text = $"ObjectGauge: {(percent * 100):F1}%"; // 소수점 1자리
    }
}
