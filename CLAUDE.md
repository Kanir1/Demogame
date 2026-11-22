# Demo Coin Master: Mini Spin Feature

A small Unity 6 (2D, mobile) prototype of a Coin Master-style slot machine: spin energy, a bet multiplier, symbol outcomes and one village building to upgrade.
It is a portfolio project for a job interview, so a clean, explainable architecture matters more than visuals. Placeholder art is fine.

## Features
- Slot machine with 3 reels. Symbols: Coin, CoinBag, Hammer, Pig, Shield, Energy.
- Spin energy: each spin costs 1 energy x bet. Energy refills 1 every N seconds up to a cap (values come from config).
- Bet multiplier: x1 / x2 / x3. Multiplies both cost and reward.
- Outcomes (3 matching symbols):
  - Coin: small coin reward. CoinBag: big coin reward. Mixed symbols: tiny coin reward.
  - Shield: +1 shield, max 3. A shield beyond the cap converts to coins.
  - Hammer: "Attack" popup + coin reward (simplified).
  - Pig: "Raid" popup + big coin reward (simplified).
  - Energy: +spins.
- Village: one building with 5 levels. Upgrade costs coins (per-level costs from config).

## Architecture
Flow of one spin:
Spin button -> SpinController -> IServerApi.Spin(bet) -> SpinResult -> OnSpinResult event -> ReelView, HudView, PopupManager, CoinFlyPool react.

| Script | Responsibility |
| --- | --- |
| GameConfig (ScriptableObject + JSON) | Symbol weights, rewards, bet options, energy cap and refill time, building costs. JSON loaded at startup simulates remote config; the ScriptableObject is the fallback. |
| IServerApi / MockServer | The only place that changes balances. Validates energy and coins, picks results by weight, returns results. Simulates network delay and random failures. |
| PlayerState | Client-side copy of coins, energy, shields, building level. Updated only from server responses. Raises OnBalanceChanged. |
| SpinController | Handles the Spin button and bet. Blocks double taps. Calls the server. Raises OnSpinStarted, OnSpinResult, OnSpinFailed. |
| ReelView | Animates the reels and stops on the server's result. |
| PopupManager | Queues popups: Raid, Attack, Shields full, Not enough energy, Error. |
| HudView | Shows coins, energy, shields and the refill countdown. |
| EnergyTimer | Refill countdown based on a server timestamp, never the device clock. |
| CoinFlyPool | Object pool of coins that fly to the coin counter. |
| VillageController / BuildingView | Upgrade button, cost check through IServerApi.Upgrade(), level display. |

## Coding rules
- Unity 6. Use the new Input System only. Never use UnityEngine.Input.
- Never call Find, FindObjectOfType or GetComponent inside Update. Cache references in Awake or assign them in the Inspector.
- Use [SerializeField] private fields, not public fields.
- Subscribe to events in OnEnable and unsubscribe in OnDisable.
- Logic and data classes never reference UI/view classes. Views listen to events.
- Only the server (MockServer) changes balances. The client never decides a reward or a cost.
- No hardcoded tuning values in scripts. Rewards, costs and timings live in GameConfig.
- Use object pooling for anything spawned repeatedly (coins, effects). No Instantiate/Destroy in hot paths.
- Multiply movement by Time.deltaTime. Apply physics in FixedUpdate.
- Prefer coroutines or Unity's Awaitable over plain Task. Any async work must stop when its object is destroyed (destroyCancellationToken).

## Project structure
- Assets/Scripts/Config: GameConfig and config loading
- Assets/Scripts/Server: IServerApi, MockServer, result and request types
- Assets/Scripts/Core: PlayerState, SpinController, EnergyTimer, VillageController
- Assets/Scripts/UI: ReelView, HudView, PopupManager, BuildingView
- Assets/Scripts/Pooling: CoinFlyPool
- Assets/Resources/Config: config JSON
- Assets/Scenes: Main.unity

## How to work in this project
- Work in small steps: one feature or script per task. Explain the plan before large changes.
- Do not edit or create files in Library/, Temp/, Logs/, obj/ or UserSettings/.
- Do not create or edit .meta files by hand. Unity generates them.
- Do not edit .unity scene or .prefab files directly. If a change needs scene or Inspector setup, list the manual steps for me (which object, which component, which field to assign).
- After writing a script, list which GameObject it goes on and which Inspector fields need to be assigned.
- If you are unsure whether an API exists in Unity 6, say so instead of guessing.
- Keep a short note of non-obvious decisions in the README.

## Edge cases to always handle
- Double taps on Spin and Upgrade.
- Not enough energy or coins.
- Server failure mid-spin: no charge, no reward, show an error popup.
- Shield cap of 3.
- Different phone aspect ratios (Canvas Scaler + anchors).
