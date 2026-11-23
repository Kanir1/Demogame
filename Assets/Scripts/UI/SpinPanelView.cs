using DemoCoinMaster.Core;
using DemoCoinMaster.Server;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DemoCoinMaster.UI
{
    /// <summary>
    /// Spin and Bet buttons, the bet label, and a short reward line ("+100 coins").
    /// Wires the buttons in code (not Inspector OnClick), so the hookup is visible in a code review
    /// and can't silently break when someone renames a method.
    /// Errors and big moments (Raid, Attack, Shields full) are shown by PopupManager.
    /// </summary>
    public class SpinPanelView : MonoBehaviour
    {
        [SerializeField] private SpinController spinController;
        [SerializeField] private Button spinButton;
        [SerializeField] private Button betButton;
        [SerializeField] private TMP_Text betLabel;
        [SerializeField] private TMP_Text statusText;
        [Tooltip("Optional. If set, buttons unlock and the reward shows only after the reels stop.")]
        [SerializeField] private ReelView reelView;

        private void OnEnable()
        {
            if (spinController == null)
            {
                Debug.LogError("[SpinPanelView] SpinController is not assigned.", this);
                return;
            }

            spinButton.onClick.AddListener(spinController.RequestSpin);
            betButton.onClick.AddListener(spinController.CycleBet);

            spinController.OnSpinStarted += HandleSpinStarted;
            spinController.OnSpinFailed += HandleSpinFailed;
            spinController.OnBetChanged += HandleBetChanged;
            if (reelView != null) reelView.OnReelsStopped += HandleSpinResult;
            else spinController.OnSpinResult += HandleSpinResult;

            HandleBetChanged(spinController.CurrentBet);
            SetInteractable(!spinController.IsSpinning && (reelView == null || !reelView.IsPresenting));
            statusText.text = string.Empty;
        }

        private void OnDisable()
        {
            if (spinController == null) return;

            spinButton.onClick.RemoveListener(spinController.RequestSpin);
            betButton.onClick.RemoveListener(spinController.CycleBet);

            spinController.OnSpinStarted -= HandleSpinStarted;
            spinController.OnSpinFailed -= HandleSpinFailed;
            spinController.OnBetChanged -= HandleBetChanged;
            if (reelView != null) reelView.OnReelsStopped -= HandleSpinResult;
            else spinController.OnSpinResult -= HandleSpinResult;
        }

        private void HandleSpinStarted(int bet)
        {
            // UI-level double-tap guard. SpinController and MockServer each have their own as well.
            SetInteractable(false);
            statusText.text = string.Empty;
        }

        private void HandleSpinResult(SpinResult result)
        {
            SetInteractable(true);
            statusText.text = DescribeResult(result);
        }

        private void HandleSpinFailed(ServerError error)
        {
            SetInteractable(true);
            statusText.text = string.Empty; // PopupManager shows errors
        }

        private void HandleBetChanged(int bet) => betLabel.text = $"BET x{bet}";

        private void SetInteractable(bool value)
        {
            spinButton.interactable = value;
            betButton.interactable = value;
        }

        private static string DescribeResult(SpinResult r)
        {
            switch (r.Outcome)
            {
                case SpinOutcome.Attack: return $"ATTACK! +{r.CoinsWon:N0} coins";
                case SpinOutcome.Raid:   return $"RAID! +{r.CoinsWon:N0} coins";
                case SpinOutcome.Energy: return $"+{r.EnergyWon} spins";
                case SpinOutcome.Shield:
                    return r.ShieldOverflow ? $"Shields full! +{r.CoinsWon:N0} coins" : "+1 Shield";
                default: return $"+{r.CoinsWon:N0} coins";
            }
        }
    }
}
