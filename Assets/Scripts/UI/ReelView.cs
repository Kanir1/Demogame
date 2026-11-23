using System;
using System.Collections;
using SpinVillage.Config;
using SpinVillage.Core;
using SpinVillage.Server;
using TMPro;
using UnityEngine;

namespace SpinVillage.UI
{
    /// <summary>
    /// Animates the reels and stops them on the symbols the server picked.
    ///
    /// - OnSpinStarted: every reel flickers through random symbols (cosmetic only; the server decides the result).
    /// - Once the result has arrived AND a minimum spin time has passed, reels stop one by one, left to right,
    ///   on SpinResult.Symbols.
    /// - When the last reel stops, hold for reelResultHoldSeconds, then OnReelsStopped(result) and OnPresentationEnded.
    ///   Views that reveal the reward (HUD, reward line, popups) wait for these, so nothing spoils the result early.
    /// - On failure the reels stop at once and show the previous symbols (nothing happened on the server).
    ///
    /// All timings come from GameConfig. A coroutine runs the animation: it stops automatically when the object
    /// is disabled or destroyed, and `yield return null` allocates nothing per frame.
    /// </summary>
    public class ReelView : MonoBehaviour
    {
        [SerializeField] private GameBootstrap bootstrap;
        [SerializeField] private SpinController spinController;
        [Tooltip("One label per reel, left to right.")]
        [SerializeField] private TMP_Text[] reelLabels;

        private GameConfigData config;
        private string[] symbolNames; // cached, so showing a symbol never allocates a string
        private string[] previousSymbols;
        private bool[] reelStopped;
        private float[] reelStopTime;

        private SpinResult pendingResult;
        private bool spinFailed;
        private Coroutine spinRoutine;

        /// <summary>True from spin start until the last reel stops (or the spin fails).</summary>
        public bool IsPresenting { get; private set; }

        /// <summary>A successful spin finished animating. Reveal rewards now.</summary>
        public event Action<SpinResult> OnReelsStopped;
        /// <summary>The animation ended for any reason (success or failure).</summary>
        public event Action OnPresentationEnded;

        private void Awake()
        {
            config = bootstrap != null ? bootstrap.Config : null;
            symbolNames = Enum.GetNames(typeof(SymbolType));
            previousSymbols = new string[reelLabels.Length];
            reelStopped = new bool[reelLabels.Length];
            reelStopTime = new float[reelLabels.Length];
        }

        private void OnEnable()
        {
            if (spinController == null || config == null)
            {
                Debug.LogError("[ReelView] SpinController or GameBootstrap is not assigned.", this);
                return;
            }

            spinController.OnSpinStarted += HandleSpinStarted;
            spinController.OnSpinResult += HandleSpinResult;
            spinController.OnSpinFailed += HandleSpinFailed;
        }

        private void OnDisable()
        {
            if (spinController != null)
            {
                spinController.OnSpinStarted -= HandleSpinStarted;
                spinController.OnSpinResult -= HandleSpinResult;
                spinController.OnSpinFailed -= HandleSpinFailed;
            }

            // Disabling stops the coroutine; finish now so listeners aren't left waiting forever.
            FinishImmediately();
        }

        private void HandleSpinStarted(int bet)
        {
            // Defensive: a new spin while the previous one is still stopping snaps the old one to its end.
            FinishImmediately();

            for (int i = 0; i < reelLabels.Length; i++)
            {
                previousSymbols[i] = reelLabels[i].text;
                reelStopped[i] = false;
            }

            pendingResult = null;
            spinFailed = false;
            IsPresenting = true;
            spinRoutine = StartCoroutine(SpinRoutine());
        }

        private void HandleSpinResult(SpinResult result) => pendingResult = result;

        private void HandleSpinFailed(ServerError error) => spinFailed = true;

        private IEnumerator SpinRoutine()
        {
            float startTime = Time.time;
            float nextSymbolChange = 0f;
            bool stopsScheduled = false;

            while (true)
            {
                float now = Time.time;

                if (spinFailed)
                {
                    RestorePreviousSymbols();
                    End(null);
                    yield break;
                }

                // Start stopping only when we have the server's answer and the reels spun long enough.
                if (!stopsScheduled && pendingResult != null && now >= startTime + config.ReelMinSpinSeconds)
                {
                    for (int i = 0; i < reelStopTime.Length; i++)
                        reelStopTime[i] = now + i * config.ReelStopIntervalSeconds;
                    stopsScheduled = true;
                }

                bool changeSymbol = now >= nextSymbolChange;
                if (changeSymbol) nextSymbolChange = now + config.ReelSymbolChangeSeconds;

                int stoppedCount = 0;
                for (int i = 0; i < reelLabels.Length; i++)
                {
                    if (!reelStopped[i])
                    {
                        if (stopsScheduled && now >= reelStopTime[i])
                        {
                            reelLabels[i].text = SymbolName(pendingResult, i);
                            reelStopped[i] = true;
                        }
                        else if (changeSymbol)
                        {
                            // Cosmetic randomness only (UnityEngine.Random). The real result comes from the server.
                            reelLabels[i].text = symbolNames[UnityEngine.Random.Range(0, symbolNames.Length)];
                        }
                    }

                    if (reelStopped[i]) stoppedCount++;
                }

                if (stoppedCount == reelLabels.Length)
                {
                    // Hold on the final symbols so the player registers the match before rewards appear.
                    // IsPresenting stays true, so the HUD, buttons and popups keep waiting.
                    float revealTime = Time.time + config.ReelResultHoldSeconds;
                    while (Time.time < revealTime) yield return null;

                    End(pendingResult);
                    yield break;
                }

                yield return null;
            }
        }

        private void FinishImmediately()
        {
            if (!IsPresenting) return;
            if (spinRoutine != null) StopCoroutine(spinRoutine);

            if (pendingResult != null && !spinFailed)
            {
                for (int i = 0; i < reelLabels.Length; i++)
                    reelLabels[i].text = SymbolName(pendingResult, i);
                End(pendingResult);
            }
            else
            {
                RestorePreviousSymbols();
                End(null);
            }
        }

        private void End(SpinResult resultOrNull)
        {
            IsPresenting = false;
            spinRoutine = null;
            if (resultOrNull != null) OnReelsStopped?.Invoke(resultOrNull);
            OnPresentationEnded?.Invoke();
        }

        private void RestorePreviousSymbols()
        {
            for (int i = 0; i < reelLabels.Length; i++)
                reelLabels[i].text = previousSymbols[i];
        }

        private string SymbolName(SpinResult result, int reel)
        {
            if (result.Symbols == null || reel >= result.Symbols.Length) return "?";
            return symbolNames[(int)result.Symbols[reel]];
        }
    }
}
