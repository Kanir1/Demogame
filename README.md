# Demo Coin Master: Mini Spin Feature

A Unity 6 prototype of a Coin Master-style spin feature. See `CLAUDE.md` for the architecture and `AI_LOG.md` for how AI was used.

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
- **`GameBootstrap` is the composition root** and runs first (`DefaultExecutionOrder(-1000)`), so other components can read the services in Awake/OnEnable.
