# Spin Village: Mini Spin Feature

A Unity 6 learning prototype of a slot-machine spin feature, inspired by Coin Master. See `CLAUDE.md` for the architecture and `AI_LOG.md` for how AI was used.

> Personal, non-commercial learning project. Not affiliated with or endorsed by Moon Active. Coin Master is a trademark of Moon Active. All code is original, and no Coin Master art, audio or other assets are used.

## Profiling result: coin pool

Measured with `CoinFlyPool` → **Debug: Fly max coins** (the same 15-coin burst, no spin), 3 bursts per mode, in the Unity 6 Editor (2026-11-24). The first burst is excluded as warm-up.

| 15-coin burst | Pooling ON | Pooling OFF (`Instantiate`/`Destroy` per coin) |
| --- | --- | --- |
| CPU in `CoinFlyPool`, whole burst | 2.51–2.62 ms (avg 2.58) | 3.39–4.07 ms (avg 3.68), **+43%** |
| Worst single frame | 0.14–0.23 ms (avg 0.19) | 0.50–0.83 ms (avg 0.67), **~3.5× spike** |
| Managed bytes in `CoinFlyPool` | 0 B | 0 B (inconclusive, see below) |

- **Result:** pooling removes the per-coin creation cost and, more importantly on mobile, the frame spikes. The OFF numbers *under*-count, because `Destroy`'s work happens at the end of the frame, outside the timer.
- **First attempt was inconclusive:** the engine's whole-frame "GC Allocated In Frame" counter read ~2.4–3.4 MB per burst in *both* modes. In the Editor it's dominated by Editor allocations (~15 KB/frame), which hides a few coins. Fix: measure only the script's own work (`Stopwatch` + `GC.GetAllocatedBytesForCurrentThread()` around `CoinFlyPool`'s update).
- **Bytes not proven:** the scoped allocation counter read 0 B even with pooling OFF, where `Instantiate` should allocate *something*, so that API likely isn't supported in this runtime. The memory benefit is therefore not claimed; it would be confirmed in the Profiler's GC Alloc column on a development build.
- **Caveat:** Editor numbers are for comparing ON vs OFF, not absolute. Real numbers come from a development build on a target phone.

## Aspect ratio check

Canvas Scaler: Scale With Screen Size, reference 1080×1920. Tested in the Game view at 1080×1920 (16:9), 1080×2400 (20:9), 1170×2532 (iPhone, 19.5:9) and 1536×2048 (iPad, 4:3).

- **Found:** with Match = 0.5, tall 20:9 phones shrink the canvas's usable width to ~966 units, and the three bottom buttons (~1,000 units side by side) overlapped by ~17 units (~19 px). Predicted from the scaler maths first, then confirmed at 1× zoom. At 0.25× zoom it was invisible, and a mistyped 1080×2040 test size also hid it.
- **Fixed:** Match = 0 (match width). For a portrait-only game the layout always gets the full 1080 units of width; taller phones get extra vertical space. Re-checked all four sizes, including the iPad (shortest canvas, ~1440 units tall), with no overlaps and popups fitting.
- **Not done:** safe area (notches / rounded corners). Next step would be a small component that fits the HUD inside `Screen.safeArea`, checked in the Device Simulator.

## Decisions (non-obvious)

- **Outcome-first weights.** The server picks an *outcome* by weight (Mixed, 3x Coin, 3x Pig...) and then builds reel symbols that match it. Rolling each reel on its own would make triples very rare and hard to tune. With an outcome table, designers set the chance of each outcome directly.
- **Config data is a plain class (`GameConfigData`) inside the ScriptableObject.** The same shape can be filled from remote JSON with `JsonUtility`. Runtime code works on a copy, because changing a ScriptableObject in Play mode in the Editor saves the change to the asset.
- **Energy won from spins can go above the cap.** Only the timed refill stops at `maxEnergy`. This matches the real game.
- **Shields are not multiplied by bet.** Coin and energy rewards are.
- **The server checks for double requests too.** The client disables the button, but `MockServer` also rejects a request that arrives while another is in flight (`RequestInProgress`). The server never trusts the client.
- **A simulated failure happens before any state change.** A failed spin never charges or rewards. In a real backend the harder case is when the server applied the spin but the response was lost. That needs a request ID so a retry isn't applied twice (idempotency).
- **Lazy energy refill.** The server keeps no timer. On each request it adds the energy earned since `lastEnergyRefillTime`, using server time. This is how real backends avoid running one timer per player.
- **`MockServer` is plain C#, not a MonoBehaviour.** It can be unit-tested with a fixed seed and a fake clock, and swapped for a real backend in `GameBootstrap` alone.
- **`PlayerSnapshot` is a readonly struct.** Server responses carry balances without heap allocations, and the client only overwrites its copy and never edits it.
- **`SpinController` has no UI references.** The Spin button calls `RequestSpin()` through its Inspector OnClick, and views subscribe to `OnSpinStarted` / `OnSpinResult` / `OnSpinFailed` / `OnBetChanged`. The double-tap guard (`IsSpinning`) is set before the first `await`, so a second tap in the same frame is ignored.
- **No client-side energy check before spinning.** The server adds refill energy lazily, so the client's energy can be out of date. A local check could block a spin the server would allow. The server decides, and a rejection comes back as `NotEnoughEnergy`.
- **Balances are applied on rule rejections but not on `NetworkError`.** A rejection carries the server's real balances. A network failure means no response arrived, so there's nothing trustworthy to apply.
- **Three layers of double-tap protection.** The UI disables the buttons, `SpinController` checks `IsSpinning`, and `MockServer` rejects concurrent requests. Each layer covers a different failure (UI bug, code path that skips the UI, a client that can't be trusted).
- **Views wire buttons in code (`onClick.AddListener` in OnEnable, `RemoveListener` in OnDisable), not in the Inspector.** The hookup is visible in code review and can't silently break when a method is renamed.
- **Startup order is explicit:** `GameBootstrap` (-1000), then `SpinController` (-500), then views (0), set with `DefaultExecutionOrder`. Views can safely read services and the current bet in OnEnable.
- **The energy countdown uses server time, never the device clock.** `EnergyTimer` estimates server now as `snapshot.ServerTime + (realtimeSinceStartup - when that snapshot arrived)`. The local part is a monotonic clock the player can't change. Each snapshot re-anchors the estimate, so drift doesn't build up.
- **At 0 the client asks the server; it doesn't add +1 itself.** Only the server changes balances. If the estimate ran slightly ahead and the server hasn't refilled yet, a short retry delay from config stops the client from asking every frame.
- **Out-of-order responses are ignored.** `PlayerState.ApplySnapshot` drops a snapshot older than the current one (compared by `ServerTime`), for example an energy sync that was sent before a spin but arrives after it.
- **Popups are split into a manager and a view.** `PopupManager` decides what to show (it maps spin events to popups and owns the queue). `PopupView` only draws one popup. `Enqueue()` is public, so later features (village, events) reuse the same queue.
- **Popup edge cases:** one popup at a time in order; no duplicate of a type already showing or queued; each new popup ignores taps for `popupInputLockSeconds` (config), so one double tap can't close two popups; a full-screen blocker stops taps reaching Spin behind the popup. The lock uses unscaled time, so a paused game can't freeze it.
- **"The server answered" and "the player saw it" are separate events.** `SpinController.OnSpinResult` fires when the server responds. `ReelView.OnReelsStopped` fires when the last reel lands. The HUD, reward line and reward popups listen to the second one, so nothing reveals the result before the reels do. `PlayerState` still updates immediately (it's the truth); only the display waits. Errors don't wait.
- **Reel animation:** reels flicker through random symbols (cosmetic `UnityEngine.Random`, never used for the result), spin at least `reelMinSpinSeconds`, then stop left to right every `reelStopIntervalSeconds` on the server's symbols, then hold for `reelResultHoldSeconds` before rewards appear (playtesting showed the Attack popup covered the third hammer before the match registered). A minimum spin time hides fast server responses and keeps the feel consistent. Runs in a coroutine (stops automatically on disable/destroy, `yield return null` allocates nothing), and symbol names are cached so no strings are built per frame.
- **Remote config is layered over the asset.** `ConfigLoader` starts from a copy of the `GameConfig` asset, applies `Resources/Config/game_config.json` with `JsonUtility.FromJsonOverwrite` (only keys in the JSON change), then validates. A missing, malformed or invalid JSON is discarded and the asset copy is used, so a bad remote push can't break the game. `configVersion` is logged at startup so bug reports show which config was active. In production the JSON would be downloaded, cached on disk, and the last good cache used offline.
- **`Validate()` checks every rule in code.** `[Min]` and `[Range]` only guard the Inspector; JSON bypasses them.
- **Village upgrade mirrors the spin flow.** `VillageController` (Core) has a double-tap guard, calls `IServerApi.UpgradeAsync`, applies the snapshot and raises events; `BuildingView` (UI) shows the level and cost. The cost turns red when unaffordable, but the button stays tappable so the server's `NotEnoughCoins` is what the player sees (as a popup). The rule lives on the server, not in a greyed-out button.
- **One in-flight lock per action on the server.** Originally `MockServer` had a single lock, so tapping Upgrade during a spin was rejected as `RequestInProgress` and the upgrade silently failed. The lock exists to stop a double *spin*, not to block different actions. Each action now has its own lock; overlap is safe because each changes state in one synchronous step, and `PlayerState` ignores out-of-order snapshots.
- **Coin fly effect is pooled and driven by one `Update`.** `CoinFlyPool` uses Unity's `ObjectPool<T>`, pre-filled at startup, so coins are reused rather than instantiated during play. All flying coins are moved by a single `Update` over a `List` of structs (removed by swap-with-last), with no MonoBehaviour per coin, and nothing allocates per frame. A `usePooling` toggle switches to `Instantiate`/`Destroy` per coin purely to measure the difference in the Profiler. The HUD counter waits for the last coin to land.
- **`CoinFlyPool` lives in `UI`, not `Pooling`.** It depends on `ReelView`, and `HudView` depends on it; a separate folder (and later an assembly definition) would create a dependency loop.
- **`GameBootstrap` is the composition root** and runs first (`DefaultExecutionOrder(-1000)`), so other components can read the services in Awake/OnEnable.
