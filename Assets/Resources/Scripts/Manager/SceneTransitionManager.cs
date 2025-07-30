using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine;

public class SceneTransitionManager : MonoBehaviour
{
    [Header("페이드용 CanvasGroup")]
    public CanvasGroup fader;
    [Header("페이드 지속시간")]
    public float fadeDuration = 1f;

    private void Awake()
    {
        if (Shared.SceneTransitionManager == null)
        {
            Shared.SceneTransitionManager = this;
            DontDestroyOnLoad(gameObject);
        }
        else Destroy(gameObject);
    }

    // sceneName 씬으로 페이드아웃 → 로드 → 페이드인
    public void FadeToScene(string sceneName)
    {
        StartCoroutine(FadeCoroutine(sceneName));
    }
    IEnumerator FadeCoroutine(string sceneName)
    {
        // 페이드 아웃 (alpha 0 → 1)
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            fader.alpha = Mathf.Clamp01(t / fadeDuration);
            yield return null;
        }

        // ▼ 씬 로드 (비동기)
        yield return SceneManager.LoadSceneAsync(sceneName);

        // ▼ 페이드 인 (alpha 1 → 0)
        t = fadeDuration;
        while (t > 0f)
        {
            t -= Time.deltaTime;
            fader.alpha = Mathf.Clamp01(t / fadeDuration);
            yield return null;
        }
    }
}
