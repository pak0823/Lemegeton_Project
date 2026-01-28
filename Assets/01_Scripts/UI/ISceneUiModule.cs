using UnityEngine;

namespace Project.UI
{
    // 씬 공통 UI 모듈의 최소 계약
    public interface ISceneUiModule
    {
        void OnUiShown();   // gameObject가 켜진 직후 1회
        void OnUiHidden();  // gameObject가 꺼지기 직전 1회
    }

    public interface IThemedUiModule
    {
        void ApplyTheme(Color? accent = null, Font font = null);
    }
}


