using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TimerUI : MonoBehaviour
{
    public Text timerText;
    public Text endMessageText;

    private List<int> fibonacciMinutes = new List<int> { 1, 2, 3, 5 };
    private int currentStage = 0;

    private float timeLeft = 0;
    private bool isRunning = false;
    private bool isFinished = false;

    private void Start()
    {
        StartNextStage();
    }

    private void Update()
    {
        if (!isRunning || isFinished) return;

        timeLeft -= Time.deltaTime;

        if (timeLeft <= 0f)
        {
            timeLeft = 0f;
            currentStage++;

            if (currentStage < fibonacciMinutes.Count)
            {
                StartNextStage();
                //StartCoroutine(ShowEndMessage()); // -> 테스트로 빠르게 확인하기 위해 넣어둠.
            }
            else
            {
                StartCoroutine(ShowEndMessage());
                isRunning = false;
                isFinished = true;
            }
        }

        UpdateTimerUI();
    }

    void StartNextStage()
    {
        timeLeft = fibonacciMinutes[currentStage] * 60f;
        isRunning = true;
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
        endMessageText.text = "플레이어를 찾지 못해 적이 돌아갔습니다.";

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
}
