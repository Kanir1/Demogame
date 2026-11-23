using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DemoCoinMaster.UI
{
    /// <summary>
    /// Draws one popup: a full-screen blocker, a title, a message and an OK button.
    /// Knows nothing about the game; PopupManager decides what to show and when.
    ///
    /// Put this on an always-active object and assign a child as Panel. Toggling only the child
    /// keeps this component enabled, so its button listener stays registered.
    /// </summary>
    public class PopupView : MonoBehaviour
    {
        [Tooltip("Child object toggled on/off. Should contain a full-screen Image (raycast target) to block taps behind the popup.")]
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private Button closeButton;

        /// <summary>The player tapped OK.</summary>
        public event Action OnCloseClicked;

        private void Awake()
        {
            Hide();
        }

        private void OnEnable()
        {
            closeButton.onClick.AddListener(HandleCloseClicked);
        }

        private void OnDisable()
        {
            closeButton.onClick.RemoveListener(HandleCloseClicked);
        }

        public void Show(string title, string message)
        {
            titleText.text = title;
            messageText.text = message;
            panel.SetActive(true);
        }

        public void Hide()
        {
            panel.SetActive(false);
        }

        private void HandleCloseClicked() => OnCloseClicked?.Invoke();
    }
}
