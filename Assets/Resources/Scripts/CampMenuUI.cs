using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Project.UI;

namespace Project.UI
{
    public class CampMenuUI : ModalWindowBase
    {
        [SerializeField] private Button closeButton;

        [Header("(Optional) Behavior")]
        [SerializeField] private KeyCode toggleKey = KeyCode.Tab;


        protected override void Awake()
        {
            base.Awake();
            if (closeButton) closeButton.onClick.AddListener(() => Toggle());
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey)) Toggle();
        }
    }
}

