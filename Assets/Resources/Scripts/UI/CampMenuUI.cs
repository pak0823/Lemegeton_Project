using Project.UI;
using Unity.VisualScripting.Antlr3.Runtime;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Project.UI
{
    public class CampMenuUI : ModalWindowBase
    {
        [SerializeField] private Button closeButton;

        [Header("(Optional) Behavior")]
        [SerializeField] private KeyCode toggleKey = KeyCode.Tab;

        [Header("Pause")]
        [SerializeField] bool pauseTimerWhileOpen = true;   // 창이 떠 있는 동안 탐험 일시정지
        bool isOpen = false;


        protected override void Awake()
        {
            base.Awake();
            if (closeButton) closeButton.onClick.AddListener(() => Toggle());
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey)) Toggle();
        }

        public override void Show()
        {
            base.Show();

            if (!isOpen && pauseTimerWhileOpen)
            {
                Shared.ExplorationTimerUi?.Pause();
                Shared.PlayerMovement?.LockMovementIndefinite();
            }

            isOpen = true;
        }

        public override void Hide()
        {
            base.Hide();

            if (isOpen && pauseTimerWhileOpen)
            {
                Shared.ExplorationTimerUi?.Resume();
                Shared.PlayerMovement?.UnlockMovementIndefinite();
            }

            isOpen = false;
        }
    }
}

