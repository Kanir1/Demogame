# Demo Coin Master: Mini Spin Feature

A Unity 6 prototype of a Coin Master-style spin feature. See `CLAUDE.md` for the architecture and `AI_LOG.md` for how AI was used.

## Decisions (non-obvious)

- **Outcome-first weights.** The server picks an *outcome* by weight (Mixed, 3x Coin, 3x Pig...) and then builds reel symbols that match it. Rolling each reel on its own would make triples very rare and hard to tune. With an outcome table, designers set the chance of each outcome directly.
- **Config data is a plain class (`GameConfigData`) inside the ScriptableObject.** The same shape can be filled from remote JSON with `JsonUtility`. Runtime code works on a copy, because changing a ScriptableObject in Play mode in the Editor saves the change to the asset.
- **Energy won from spins can go above the cap.** Only the timed refill stops at `maxEnergy`. This matches the real game.
- **Shields are not multiplied by bet.** Coin and energy rewards are.
