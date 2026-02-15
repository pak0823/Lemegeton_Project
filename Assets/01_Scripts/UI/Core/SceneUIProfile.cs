using UnityEngine;



namespace Project.UI

{

    [CreateAssetMenu(menuName = "UI/Profile/Scene", fileName = "SceneUIProfile_XXX")]

    public class SceneUIProfile : ScriptableObject

    {

        [Header("Toggle Modules")]

        public bool showHud;

        public bool showTurnBar;

        public bool showSkillPanel;

        public bool showActionPanel;

        public bool showTimerUI;

        public bool showExplorationResetUi; // TestUi

        public bool showTitleMenu; // Title 전용 메뉴 표시

        public bool showOptionsMenu; // 공용 옵션/일시정지 메뉴



        [Header("Common Options")]

        public bool pauseAvailable = true;



        [Header("(Optional) Theme / Visuals")]

        public Color? themeAccentColor = null; // keep null => unchanged

        public Font themeFont;                 // optional



        [Header("(Optional) Input Hints")]

        public KeyCode pauseKey = KeyCode.Escape;

    }

}