using UnityEditor;
using UnityEngine;
using System.Linq;

namespace Project.UI
{
    public class SceneUIOrchestrator : MonoBehaviour
    {
        [Header("Profile")]
        [SerializeField] private SceneUIProfile profile;

        [Header("Modules")]
        [SerializeField] private HudController hud;           // Battle
        [SerializeField] private TurnBarUI turnBar;           // Battle
        [SerializeField] private SkillPanelUI skillPanel;     // Battle
        [SerializeField] private ActionPanelUI actionPanel;   // Battle
        [SerializeField] private ExplorationResetUi explorationReset;     // Exploration
        [SerializeField] private TitleMenuUI titleMenu; // Title
        [SerializeField] private OptionsMenuUI optionsMenu;   // Common (Exploration/Battle)

        [Header("(Optional) Wiring")]
        [SerializeField] private bool handlePauseInput = true;

        public SceneUIProfile CurrentProfile => profile;

        [ContextMenu("UI/Auto Wire Modules (Search In Children)")]
        public void AutoWireModules()
        {
            // 같은 오브젝트 또는 자식에서 찾아 할당
            hud = hud ? hud : GetComponentInChildren<HudController>(true);
            turnBar = turnBar ? turnBar : GetComponentInChildren<TurnBarUI>(true);
            skillPanel = skillPanel ? skillPanel : GetComponentInChildren<SkillPanelUI>(true);
            actionPanel = actionPanel ? actionPanel : GetComponentInChildren<ActionPanelUI>(true);
            explorationReset = explorationReset ? explorationReset : GetComponentInChildren<ExplorationResetUi>(true);
            optionsMenu = optionsMenu ? optionsMenu : GetComponentInChildren<OptionsMenuUI>(true);

#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
            Debug.Log("[SceneUIOrchestrator] Auto-wired modules.", this);
        }

        private void Awake()
        {
            if (profile == null)
            {
                Debug.LogWarning("[SceneUIOrchestrator] No profile assigned. UI modules will keep their current states.", this);
                return;
            }

            ApplyProfile();
        }

        private void Update()
        {
            if (!handlePauseInput || profile == null) return;
            if (profile.pauseAvailable && Input.GetKeyDown(profile.pauseKey))
            {
                var mgr = UiModalManager.Instance;
                if (mgr != null) mgr.OnEscape(optionsMenu);
                else BroadcastMessage("OnPauseKeyPressed", SendMessageOptions.DontRequireReceiver); // 폴백
            }
        }

        // Public API to replace profile at runtime (e.g., right after scene load).
        public void SetProfile(SceneUIProfile newProfile, bool applyImmediately = true)
        {
            profile = newProfile;
            if (applyImmediately && profile != null)
                ApplyProfile();
        }

        // Apply the currently assigned profile to all registered modules.
        public void ApplyProfile()
        {
            if (profile == null)
            {
                Debug.LogWarning("[SceneUIOrchestrator] ApplyProfile called without a profile.");
                return;
            }

            if (hud)
            {
                if (profile.showHud) hud.Show();
                else hud.Hide();
            }

            SetActive(turnBar, profile.showTurnBar);
            SetActive(skillPanel, profile.showSkillPanel);
            SetActive(actionPanel, profile.showActionPanel);
            SetActive(explorationReset, profile.showExplorationResetUi);
            SetActive(titleMenu, profile.showTitleMenu);
            SetActive(optionsMenu, profile.showOptionsMenu);

            // Theme propagation (interface 기반으로만 안전하게 전파)
            if (profile.themeAccentColor.HasValue || profile.themeFont)
            {
                var themed = GetComponentsInChildren<IThemedUiModule>(true);
                foreach (var t in themed)
                {
                    t.ApplyTheme(profile.themeAccentColor, profile.themeFont);
                }
            }
        }

        private static void SetActive(Behaviour comp, bool on)
        {
            if (!comp) return;
            var go = comp.gameObject;
            var mod = comp as ISceneUiModule;

            if (on)
            {
                // 이미 active여도 수명주기 훅은 보장 호출
                if (!go.activeSelf) go.SetActive(true);
                mod?.OnUiShown();
            }
            else
            {
                mod?.OnUiHidden();
                if (go.activeSelf) go.SetActive(false);
            }
        }

#if UNITY_EDITOR
        private void Reset()
        {
            // 컴포넌트가 방금 붙었을 때 자동 배선
            AutoWireModules();
        }
        private void OnValidate()
        {
            // Keep play mode experience tidy when tweaking in inspector.
            if (Application.isPlaying && isActiveAndEnabled && profile != null)
            {
                ApplyProfile();
            }
        }
#endif
    }
}