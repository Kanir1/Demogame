using SpinVillage.Core;
using SpinVillage.Server;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpinVillage.UI
{
    /// <summary>
    /// The village building: level, next upgrade cost and the Upgrade button.
    /// The cost turns red when the player can't afford it, but the button stays tappable:
    /// the server decides, and its NotEnoughCoins answer becomes a popup (PopupManager).
    /// Redraws on events only; like HudView it can wait for the reels so it doesn't spoil a win.
    /// </summary>
    public class BuildingView : MonoBehaviour
    {
        [SerializeField] private GameBootstrap bootstrap;
        [SerializeField] private VillageController villageController;
        [SerializeField] private Button upgradeButton;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text costText;
        [SerializeField] private Color affordableColor = Color.white;
        [SerializeField] private Color unaffordableColor = new Color(1f, 0.45f, 0.45f);

        [Header("Optional: wait for reels before showing new balances")]
        [SerializeField] private ReelView reelView;

        private PlayerState playerState;
        private bool refreshPending;

        private void OnEnable()
        {
            playerState = bootstrap != null ? bootstrap.PlayerState : null;
            if (playerState == null || villageController == null)
            {
                Debug.LogError("[BuildingView] GameBootstrap or VillageController is not assigned.", this);
                return;
            }

            upgradeButton.onClick.AddListener(villageController.RequestUpgrade);
            playerState.OnBalanceChanged += HandleBalanceChanged;
            villageController.OnUpgradeStarted += HandleUpgradeStarted;
            villageController.OnUpgradeSucceeded += HandleUpgradeSucceeded;
            villageController.OnUpgradeFailed += HandleUpgradeFailed;
            if (reelView != null) reelView.OnPresentationEnded += HandlePresentationEnded;

            upgradeButton.interactable = !villageController.IsUpgrading;
            if (playerState.HasSynced) Draw();
            else levelText.text = "Building ...";
        }

        private void OnDisable()
        {
            if (playerState != null) playerState.OnBalanceChanged -= HandleBalanceChanged;
            if (villageController != null)
            {
                upgradeButton.onClick.RemoveListener(villageController.RequestUpgrade);
                villageController.OnUpgradeStarted -= HandleUpgradeStarted;
                villageController.OnUpgradeSucceeded -= HandleUpgradeSucceeded;
                villageController.OnUpgradeFailed -= HandleUpgradeFailed;
            }
            if (reelView != null) reelView.OnPresentationEnded -= HandlePresentationEnded;
        }

        private void HandleBalanceChanged(PlayerState state)
        {
            if (reelView != null && reelView.IsPresenting)
            {
                refreshPending = true;
                return;
            }
            Draw();
        }

        private void HandlePresentationEnded()
        {
            if (!refreshPending) return;
            refreshPending = false;
            Draw();
        }

        // UI-level double-tap guard; VillageController and MockServer each have their own.
        private void HandleUpgradeStarted() => upgradeButton.interactable = false;

        private void HandleUpgradeSucceeded(UpgradeResult result) => upgradeButton.interactable = true;

        private void HandleUpgradeFailed(ServerError error) => upgradeButton.interactable = true;

        private void Draw()
        {
            int level = villageController.Level;
            int max = villageController.MaxLevel;
            levelText.text = $"Building Lv {level}/{max}";

            if (villageController.IsMaxLevel)
            {
                costText.text = "MAX";
                costText.color = affordableColor;
                upgradeButton.gameObject.SetActive(false); // nothing left to buy
                return;
            }

            int cost = villageController.NextUpgradeCost;
            costText.text = $"Upgrade: {cost:N0} coins";
            costText.color = playerState.Coins >= cost ? affordableColor : unaffordableColor;
            upgradeButton.gameObject.SetActive(true);
        }
    }
}
