using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Settings
{
    public class ScreenSettingsManager : MonoBehaviour
    {
        public static ScreenSettingsManager Instance { get; private set; }

        private const string PREF_RES_WIDTH = "Screen_Width";
        private const string PREF_RES_HEIGHT = "Screen_Height";
        private const string PREF_FULLSCREEN = "Screen_Fullscreen";

        [Serializable]
        public struct ResolutionData
        {
            public int width;
            public int height;
            public string label;

            public ResolutionData(int w, int h, string l)
            {
                width = w;
                height = h;
                label = l;
            }
        }

        // 지원하는 해상도 목록 (960x540, 1280x720, 1920x1080, 2560x1440)
        public readonly List<ResolutionData> SupportedResolutions = new List<ResolutionData>()
        {
            new ResolutionData(960, 540, "960 x 540"),
            new ResolutionData(1280, 720, "1280 x 720 (HD)"),
            new ResolutionData(1920, 1080, "1920 x 1080 (FHD)"),
            new ResolutionData(2560, 1440, "2560 x 1440 (QHD)")
        };

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                ApplySavedSettings();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInit()
        {
            if (Instance == null)
            {
                GameObject go = new GameObject("[ScreenSettingsManager]");
                go.AddComponent<ScreenSettingsManager>();
            }
        }

        public void ApplySavedSettings()
        {
            int defaultWidth = 1920;
            int defaultHeight = 1080;

            int width = PlayerPrefs.GetInt(PREF_RES_WIDTH, defaultWidth);
            int height = PlayerPrefs.GetInt(PREF_RES_HEIGHT, defaultHeight);

            // 1: 전체화면(FullScreenWindow), 0: 창모드
            bool isFullScreen = PlayerPrefs.GetInt(PREF_FULLSCREEN, 1) == 1;

            SetResolution(width, height, isFullScreen);
        }

        public void SetResolution(int width, int height, bool isFullScreen)
        {
            FullScreenMode mode = isFullScreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            Screen.SetResolution(width, height, mode);

            // 로컬 저장
            PlayerPrefs.SetInt(PREF_RES_WIDTH, width);
            PlayerPrefs.SetInt(PREF_RES_HEIGHT, height);
            PlayerPrefs.SetInt(PREF_FULLSCREEN, isFullScreen ? 1 : 0);
            PlayerPrefs.Save();

            Debug.Log($"[ScreenSettings] 해상도 변경 적용: {width}x{height}, 전체화면: {isFullScreen}");
        }

        public int GetCurrentResolutionIndex()
        {
            int currentWidth = PlayerPrefs.GetInt(PREF_RES_WIDTH, 1920);
            int currentHeight = PlayerPrefs.GetInt(PREF_RES_HEIGHT, 1080);

            for (int i = 0; i < SupportedResolutions.Count; i++)
            {
                if (SupportedResolutions[i].width == currentWidth && SupportedResolutions[i].height == currentHeight)
                {
                    return i;
                }
            }
            // 일치하는 항목이 없으면 기본값인 FHD(2) 리턴
            return 2;
        }

        public bool GetCurrentFullScreenState()
        {
            return PlayerPrefs.GetInt(PREF_FULLSCREEN, 1) == 1;
        }
    }
}
