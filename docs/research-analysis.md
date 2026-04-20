# EmojiWar Research Analysis

## Source analyzed

- User concept/spec in the prompt
- `c:\Users\coron\Downloads\Emoji Wars Competitive Tactics Mobile Game Research Report.pdf`

## Executive read

# Superseded Implementation Note

This research summary still reflects the original duel-first interpretation of the game loop.

As of `2026-04-05`, the live implementation direction is the `5v5 auto-battle` version documented in [README.md](c:\Users\coron\Projects\EmojiWar\README.md). The research findings remain useful, but any duel-specific product assumptions in this file are no longer launch requirements.

The research supports the core product thesis. EmojiWar has a credible market gap if it stays focused on short competitive matches, hidden simultaneous picks, instant WHY explanations, and shareable replay moments. The report consistently points away from power progression and toward readability, fairness, and fast loop completion.

## What the report validates

### Market positioning

- The strongest differentiation is not "emoji game" alone. It is the combination of emoji-first readability, short ladder-friendly matches, explainable hidden interactions, and clip-first sharing.
- Existing emoji games skew casual, action, or puzzle. Existing competitive tactics games skew heavier, slower, or less socially readable.
- The report's core gap statement is effectively: "competitive tactics, explained and shareable."

### Audience and session fit

- The report ties the target audience to Gen Z and teen behavior across social video and gaming.
- The cited benchmark summary used in the report gives global median mobile metrics around `D1 ~22.9%`, `D7 ~4.2%`, `D28 ~0.85%`, and `session length ~4.45 minutes`.
- That supports a design target of roughly 4 minutes of active play for a full duel when both players are engaged.
- The research launch gate recommendation is stronger than market median:
  - `D1 >= 30%`
  - `D7 >= 8%`
  - `D30-like >= 2%`

### Core game loop

- Hidden picks must stay hidden until both players lock. The report treats this as essential to avoid second-mover advantage.
- Hidden mechanics must be explainable. The WHY overlay plus Codex is not optional polish; it is the main anti-randomness system.
- The replay/share layer is not a side feature. It is part of the product loop and should exist early, even if the first version is templated.

### Content strategy

- A small roster can work if it is built around reusable interaction verbs and backed by a complete interaction matrix.
- The report explicitly recommends:
  - Finalizing the 12-emoji MVP roster
  - Writing one interaction spec per emoji
  - Building an emoji x emoji outcome matrix
  - Tagging each interaction with a reason code for WHY and Codex

### Tech stack

- The report's recommended MVP stack matches the concept:
  - Unity client
  - Supabase backend
  - RevenueCat for purchases/subscriptions
  - Firebase for analytics, crash reporting, and push
- That stack is a strong fit because authoritative gameplay payloads are tiny and deterministic.

### Monetization

- The report supports a low-friction season pass at `$4.99 / 28 days`.
- Rewarded ads only is the right launch posture.
- Cosmetics, banners, flair, KO animations, and replay frames fit the product without harming fairness.

### Security and live operations

- Resolution must be server-authoritative.
- Row-level security is required for match access and turn writes.
- Timeouts and deterministic forfeits are needed to prevent async stall abuse.
- Crashlytics and core analytics must exist before public testing.

### Legal and art

- The report is clear that vendor emoji artwork should not be shipped as product art.
- The game should use original "battlemoji" art inspired by emoji readability, not copied emoji sets.

## Where the current concept is strongest

- The one-line pitch is clear and commercially legible.
- The WHY/Codex system is aligned with the report and should be treated as a first-class system, not UI copy.
- The 12-emoji roster has good shape for onboarding plus mind-games.
- The rules engine priority order is a good starting point for a deterministic resolver.
- The anti-dominance rules are the right balancing philosophy.

## Critical design gaps uncovered during analysis

### 1. Match format math is currently inconsistent

Current concept:

- Build a deck of 5
- Ban 1
- Remaining 4 are live
- Used emojis are exhausted
- Draws also consume both emojis
- Match is best-of-5 / first to 3

Problem:

- After a ban, each player only has 4 live emojis.
- With draw consumption, the system can run out of pieces before a clean first-to-3 conclusion.

Recommended resolution:

- Competitive deck size becomes `6`.
- Pre-match ban removes `1`.
- `5` live emojis remain, which cleanly supports best-of-5 and draw consumption.

### 2. Status and ramp are underdefined in a one-shot system

Current concept includes burn, poison, cleanse, and growth, but also says played emojis are exhausted for the match.

Problem:

- If units are one-shot and all state dies with them, `🐍`, `🧼`, `🌱`, and part of `🔥` lose their strategic reason to exist.

Recommended resolution:

- Persistent effects are stored on `player side state`, not on exhausted emoji pieces.
- Example categories:
  - next-round debuffs
  - delayed triggers
  - cleanse windows
  - growth/setup flags

### 3. "4-5 minute match" and "async PvP" need one product framing

Problem:

- Async matches can take longer in wall-clock time even if the active decision time is short.

Recommended resolution:

- Build the mode as `hybrid async`.
- If both players stay present, the match resolves in one short session.
- If someone leaves, the match continues asynchronously with push reminders and deadlines.

## Product implications for planning

- Build the rules engine first and make it testable outside the client.
- Treat WHY text, reason codes, and Codex unlocks as engine outputs.
- Keep the initial roster small and the matrix complete.
- Do not ship ranked without hidden simultaneous lock and timeout handling.
- Do not ship public art until the battlemoji pipeline is approved.
- Do not scale UA until the fairness and replay-sharing signals are confirmed in playtests.

## Recommended priorities before coding the full game

1. Lock the corrected competitive match format.
2. Define side-state status rules for MVP.
3. Author the full 12 x 12 interaction matrix with reason codes.
4. Define the engine I/O contract and versioning model.
5. Build a local simulation harness before the full Unity front end.
