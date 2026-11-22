using DemoCoinMaster.Core;
using DemoCoinMaster.Server;
using TMPro;
using UnityEngine;

namespace DemoCoinMaster.UI
{
    /// <summary>
    /// Shows the symbols the server picked. Day 1: plain text labels.
    /// Day 2: animate the reels while waiting and stop on the server's result.
    /// The view never picks symbols itself; it only displays SpinResult.Symbols.
    /// </summary>
    public class ReelView : MonoBehaviour
    {
        [SerializeField] private SpinController spinController;
        [Tooltip("One label per reel, left to right.")]
        [SerializeField] private TMP_Text[] reelLabels;
        [SerializeField] private string spinningText = "...";

        private string[] lastSymbols;

        private void Awake()
        {
            lastSymbols = new string[reelLabels.Length];
        }

        private void OnEnable()
        {
            if (spinController == null)
            {
                Debug.LogError("[ReelView] SpinController is not assigned.", this);
                return;
            }

            spinController.OnSpinStarted += HandleSpinStarted;
            spinController.OnSpinResult += HandleSpinResult;
            spinController.OnSpinFailed += HandleSpinFailed;
        }

        private void OnDisable()
        {
            if (spinController == null) return;
            spinController.OnSpinStarted -= HandleSpinStarted;
            spinController.OnSpinResult -= HandleSpinResult;
            spinController.OnSpinFailed -= HandleSpinFailed;
        }

        private void HandleSpinStarted(int bet)
        {
            for (int i = 0; i < reelLabels.Length; i++)
            {
                lastSymbols[i] = reelLabels[i].text;
                reelLabels[i].text = spinningText;
            }
        }

        private void HandleSpinResult(SpinResult result)
        {
            int count = Mathf.Min(reelLabels.Length, result.Symbols.Length);
            for (int i = 0; i < count; i++)
                reelLabels[i].text = result.Symbols[i].ToString();
        }

        private void HandleSpinFailed(ServerError error)
        {
            // Nothing happened on the server, so put the previous symbols back.
            for (int i = 0; i < reelLabels.Length; i++)
                reelLabels[i].text = lastSymbols[i];
        }
    }
}
