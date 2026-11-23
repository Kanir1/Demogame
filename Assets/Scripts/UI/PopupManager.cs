using System.Collections.Generic;
using SpinVillage.Core;
using SpinVillage.Server;
using UnityEngine;

namespace SpinVillage.UI
{
    public enum PopupType
    {
        Attack,
        Raid,
        ShieldsFull,
        NotEnoughEnergy,
        Error
    }

    /// <summary>
    /// Turns spin events into popups and shows them one at a time, in order.
    ///
    /// Edge cases handled:
    /// - Queue: several popups from one spin (or quick events) are shown in order, never on top of each other.
    /// - Duplicates: a type that is already showing or queued is not queued again (no stack of identical errors).
    /// - Double tap: each popup ignores close taps for a short time (config), so one double tap can't close two popups.
    /// - Input behind the popup: PopupView's full-screen blocker catches taps, so Spin can't be pressed.
    /// </summary>
    public class PopupManager : MonoBehaviour
    {
        private readonly struct PopupRequest
        {
            public readonly PopupType Type;
            public readonly string Title;
            public readonly string Message;

            public PopupRequest(PopupType type, string title, string message)
            {
                Type = type;
                Title = title;
                Message = message;
            }
        }

        [SerializeField] private GameBootstrap bootstrap;
        [SerializeField] private SpinController spinController;
        [SerializeField] private PopupView popupView;
        [Tooltip("Optional. If set, reward popups (Raid, Attack, Shields full) wait until the reels stop.")]
        [SerializeField] private ReelView reelView;

        private readonly Queue<PopupRequest> queue = new Queue<PopupRequest>();
        private bool isShowing;
        private PopupType currentType;
        private float closeAllowedAt;

        public bool IsShowing => isShowing;

        private void OnEnable()
        {
            if (bootstrap == null || spinController == null || popupView == null)
            {
                Debug.LogError("[PopupManager] Bootstrap, SpinController or PopupView is not assigned.", this);
                return;
            }

            spinController.OnSpinFailed += HandleSpinFailed;
            popupView.OnCloseClicked += HandleCloseClicked;
            if (reelView != null) reelView.OnReelsStopped += HandleSpinResult;
            else spinController.OnSpinResult += HandleSpinResult;
        }

        private void OnDisable()
        {
            if (spinController != null)
            {
                spinController.OnSpinFailed -= HandleSpinFailed;
                if (reelView == null) spinController.OnSpinResult -= HandleSpinResult;
            }
            if (reelView != null) reelView.OnReelsStopped -= HandleSpinResult;
            if (popupView != null) popupView.OnCloseClicked -= HandleCloseClicked;
        }

        /// <summary>Queue a popup. Public so other features (village, events) can reuse the same queue.</summary>
        public void Enqueue(PopupType type, string title, string message)
        {
            if (IsShowingOrQueued(type)) return;

            queue.Enqueue(new PopupRequest(type, title, message));
            if (!isShowing) ShowNext();
        }

        private void HandleSpinResult(SpinResult r)
        {
            switch (r.Outcome)
            {
                case SpinOutcome.Attack:
                    Enqueue(PopupType.Attack, "ATTACK!", $"You attacked a village and earned {r.CoinsWon:N0} coins.");
                    break;
                case SpinOutcome.Raid:
                    Enqueue(PopupType.Raid, "RAID!", $"You raided a coin stash and earned {r.CoinsWon:N0} coins.");
                    break;
                case SpinOutcome.Shield when r.ShieldOverflow:
                    Enqueue(PopupType.ShieldsFull, "Shields full",
                            $"You already have the maximum shields. The extra shield became {r.CoinsWon:N0} coins.");
                    break;
            }
        }

        private void HandleSpinFailed(ServerError error)
        {
            switch (error)
            {
                case ServerError.RequestInProgress:
                    break; // a guard, not something the player needs to see
                case ServerError.NotEnoughEnergy:
                    Enqueue(PopupType.NotEnoughEnergy, "Out of energy",
                            "Wait for energy to refill, or lower your bet.");
                    break;
                default:
                    Enqueue(PopupType.Error, "Connection problem",
                            "Something went wrong. Nothing was charged. Please try again.");
                    break;
            }
        }

        private void HandleCloseClicked()
        {
            if (Time.unscaledTime < closeAllowedAt) return; // the same double tap that closed the previous popup
            ShowNext();
        }

        private void ShowNext()
        {
            if (queue.Count == 0)
            {
                isShowing = false;
                popupView.Hide();
                return;
            }

            PopupRequest next = queue.Dequeue();
            isShowing = true;
            currentType = next.Type;
            // Unscaled time, so a paused game (timeScale 0) can't lock the popup forever.
            closeAllowedAt = Time.unscaledTime + bootstrap.Config.PopupInputLockSeconds;
            popupView.Show(next.Title, next.Message);
        }

        private bool IsShowingOrQueued(PopupType type)
        {
            if (isShowing && currentType == type) return true;
            foreach (PopupRequest r in queue) // Queue<T> enumerator is a struct: no allocation
                if (r.Type == type) return true;
            return false;
        }

        [ContextMenu("Debug: Queue 3 popups")]
        private void DebugQueueThree()
        {
            Enqueue(PopupType.Raid, "RAID!", "Test popup 1 of 3.");
            Enqueue(PopupType.Attack, "ATTACK!", "Test popup 2 of 3.");
            Enqueue(PopupType.Error, "Connection problem", "Test popup 3 of 3.");
            Enqueue(PopupType.Error, "Duplicate", "You should NOT see this one.");
        }
    }
}
