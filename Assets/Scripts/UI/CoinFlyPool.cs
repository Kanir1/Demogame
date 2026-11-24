using System;
using System.Collections.Generic;
using SpinVillage.Config;
using SpinVillage.Core;
using SpinVillage.Server;
using UnityEngine;
using UnityEngine.Pool;

namespace SpinVillage.UI
{
    /// <summary>
    /// Coins that fly from the reels to the coin counter after a win.
    ///
    /// Performance choices (and the reason this lives in UI, not a Pooling folder: it depends on ReelView and
    /// HudView depends on it, so a separate folder/assembly would create a dependency loop):
    /// - Unity's ObjectPool&lt;T&gt;, pre-filled in Start: coins are reused, never Instantiate/Destroy during play.
    /// - One Update moves every coin. No MonoBehaviour per coin; coins live in a List of structs,
    ///   removed by swap-with-last, so nothing allocates per frame.
    /// - usePooling = false switches to Instantiate/Destroy per coin. It exists only to compare
    ///   GC allocations in the Profiler ("measure before optimizing"). Leave it on.
    ///
    /// Purely cosmetic: the number of coins and their paths never affect the reward.
    /// </summary>
    public class CoinFlyPool : MonoBehaviour
    {
        private struct FlyingCoin
        {
            public RectTransform Coin;
            public bool Pooled;
            public Vector3 Start;
            public Vector3 Control;
            public float StartTime;
        }

        [SerializeField] private GameBootstrap bootstrap;
        [SerializeField] private SpinController spinController;
        [Tooltip("Optional. If set, coins launch after the reels stop; otherwise as soon as the server answers.")]
        [SerializeField] private ReelView reelView;

        [Header("Objects")]
        [Tooltip("Prefab: a small UI Image with Raycast Target OFF.")]
        [SerializeField] private RectTransform coinPrefab;
        [Tooltip("Stretch-anchored parent under the Canvas, above the HUD and below PopupLayer.")]
        [SerializeField] private RectTransform coinLayer;
        [SerializeField] private RectTransform flyFrom;
        [SerializeField] private RectTransform flyTo;

        [Header("Look (canvas units)")]
        [SerializeField] private float spawnSpread = 150f;
        [SerializeField] private float arcHeight = 250f;

        [Header("Profiling")]
        [Tooltip("Turn OFF only to measure: every coin is then Instantiate()d and Destroy()ed.")]
        [SerializeField] private bool usePooling = true;

        private GameConfigData config;
        private ObjectPool<RectTransform> pool;
        private readonly List<FlyingCoin> flying = new List<FlyingCoin>(32);
        private int pendingSpawns;
        private float nextSpawnTime;

        /// <summary>True while coins are waiting to launch or still in the air.</summary>
        public bool IsFlying => pendingSpawns > 0 || flying.Count > 0;

        /// <summary>The last coin of the current burst reached the counter.</summary>
        public event Action OnAllCoinsLanded;

        private void Awake()
        {
            config = bootstrap != null ? bootstrap.Config : null;
            if (config == null || coinPrefab == null || coinLayer == null || flyFrom == null || flyTo == null)
            {
                Debug.LogError("[CoinFlyPool] Bootstrap, prefab, layer or fly points are not assigned.", this);
                enabled = false;
                return;
            }

            pool = new ObjectPool<RectTransform>(
                createFunc: CreateCoin,
                actionOnGet: coin => coin.gameObject.SetActive(true),
                actionOnRelease: coin => coin.gameObject.SetActive(false),
                // The coin may already be gone: on scene unload / leaving Play mode Unity destroys the
                // scene objects first, and Unity 6 then clears every ObjectPool (PoolManager.Reset).
                // A destroyed UnityEngine.Object compares equal to null, so this check catches it.
                actionOnDestroy: coin => { if (coin != null) Destroy(coin.gameObject); },
                collectionCheck: Application.isEditor, // catches double-release bugs in the Editor only
                defaultCapacity: config.CoinFlyMaxIcons,
                maxSize: config.CoinFlyMaxIcons * 2);
        }

        private void Start()
        {
            if (!enabled || !usePooling) return;

            // Pre-fill: create every coin now (at load) instead of during the first win.
            var warm = new RectTransform[config.CoinFlyMaxIcons];
            for (int i = 0; i < warm.Length; i++) warm[i] = pool.Get();
            for (int i = 0; i < warm.Length; i++) pool.Release(warm[i]);
        }

        private void OnEnable()
        {
            if (spinController == null) return;
            if (reelView != null) reelView.OnReelsStopped += HandleSpinResult;
            else spinController.OnSpinResult += HandleSpinResult;
        }

        private void OnDisable()
        {
            if (reelView != null) reelView.OnReelsStopped -= HandleSpinResult;
            else if (spinController != null) spinController.OnSpinResult -= HandleSpinResult;

            LandAllNow(); // don't leave HudView waiting for coins that will never land
        }

        private void OnDestroy()
        {
            // Empty the pool while we still own it, so it holds no references to destroyed coins
            // and the editor's pool reset on leaving Play mode has nothing left to clean up.
            pool?.Clear();
        }

        private void HandleSpinResult(SpinResult result)
        {
            if (result.CoinsWon <= 0) return;

            int icons = Mathf.Clamp(
                Mathf.CeilToInt(result.CoinsWon / (float)config.CoinFlyCoinsPerIcon), 1, config.CoinFlyMaxIcons);

            pendingSpawns += icons;
            if (nextSpawnTime < Time.time) nextSpawnTime = Time.time;
        }

        private void Update()
        {
            if (!IsFlying) return;

            float now = Time.time;

            while (pendingSpawns > 0 && now >= nextSpawnTime)
            {
                Launch(now);
                pendingSpawns--;
                nextSpawnTime += config.CoinFlySpawnIntervalSeconds;
            }

            Vector3 end = flyTo.position; // read every frame: the target may move with layout changes
            for (int i = flying.Count - 1; i >= 0; i--)
            {
                FlyingCoin c = flying[i];
                // Progress comes from elapsed time, so speed is the same at any frame rate.
                float t = (now - c.StartTime) / config.CoinFlyDurationSeconds;

                if (t >= 1f)
                {
                    Return(c);
                    flying[i] = flying[flying.Count - 1]; // swap-with-last: O(1), no shifting
                    flying.RemoveAt(flying.Count - 1);
                    continue;
                }

                float e = t * t; // ease-in: speeds up toward the counter
                c.Coin.position = QuadraticBezier(c.Start, c.Control, end, e);
            }

            if (!IsFlying) OnAllCoinsLanded?.Invoke();
        }

        private void Launch(float now)
        {
            RectTransform coin = usePooling ? pool.Get() : CreateCoin(true);

            // Canvas units -> world units (Screen Space Overlay: world == screen pixels).
            float scale = coinLayer.lossyScale.x;
            Vector3 start = flyFrom.position + (Vector3)(UnityEngine.Random.insideUnitCircle * (spawnSpread * scale));
            Vector3 end = flyTo.position;
            Vector3 control = (start + end) * 0.5f
                              + Vector3.up * (arcHeight * scale)
                              + Vector3.right * (UnityEngine.Random.Range(-0.5f, 0.5f) * arcHeight * scale);

            coin.position = start;
            flying.Add(new FlyingCoin { Coin = coin, Pooled = usePooling, Start = start, Control = control, StartTime = now });
        }

        private void Return(FlyingCoin c)
        {
            if (c.Pooled) pool.Release(c.Coin);
            else Destroy(c.Coin.gameObject); // profiling mode only
        }

        private void LandAllNow()
        {
            bool wasFlying = IsFlying;
            for (int i = 0; i < flying.Count; i++) Return(flying[i]);
            flying.Clear();
            pendingSpawns = 0;
            if (wasFlying) OnAllCoinsLanded?.Invoke();
        }

        private RectTransform CreateCoin() => CreateCoin(false);

        private RectTransform CreateCoin(bool active)
        {
            RectTransform coin = Instantiate(coinPrefab, coinLayer);
            coin.gameObject.SetActive(active);
            return coin;
        }

        private static Vector3 QuadraticBezier(Vector3 a, Vector3 b, Vector3 c, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }
    }
}
