# AI Log

What I asked the AI, what it got wrong, and what I fixed. Used for the README and the interview "how do you use AI" answer.

| # | Date | Prompt (short) | What it got wrong / risk | What I did |
| --- | --- | --- | --- | --- |
| 1 | 2026-11-22 | Review plan PDF + CLAUDE.md, start Day 1 | Caught a real issue: ignore file was named `gitignore` (no dot), so Library/ and Temp/ would have been committed | Renamed to `.gitignore` before the first commit |
| 2 | 2026-11-22 | GameConfig data layer | Design choice to verify: outcome-first weights instead of per-reel random | Kept it: designers tune the chance of each outcome directly |
