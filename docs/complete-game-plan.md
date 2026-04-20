# EmojiWar Complete Game Plan

## Summary

# Superseded Notice

This document was written for the earlier duel-first version of EmojiWar.

As of `2026-04-05`, the launch direction has changed to:

- `5v5 auto-battle`
- `16 launch emojis`
- ranked `6 pick -> blind ban 1 -> final 5`
- deterministic battle simulator instead of duel-round resolution

Use [README.md](c:\Users\coron\Projects\EmojiWar\README.md) as the current source of truth for launch architecture and scope. This file is retained as historical product context only.

EmojiWar should be built as a mobile-first hybrid async tactics game where each round is one hidden simultaneous pick, every outcome is explained immediately, and the long-term retention loop comes from mastery, Codex discovery, competitive rank, and shareable replays rather than power progression.

This plan keeps the report's strongest recommendations and resolves the current spec gaps. The recommended competitive ruleset is:

- `6-emoji deck`
- `1 simultaneous ban per player`
- `5 live emojis after bans`
- `best-of-5, first to 3 wins`
- `used emojis are exhausted`
- `draws consume both emojis`
- `persistent status/ramp live on side-state across rounds`

## Product decisions to lock

### Match format

- Ranked and bot duel both use the same round structure to avoid split learning.
- Both players reveal their 6-emoji deck during ban phase.
- Bans are simultaneous and server-held.
- After bans, each side has 5 live emojis for the match.
- Each round uses one hidden pick from remaining live emojis.
- When both picks lock or the timer expires, the server resolves the round simultaneously.
- A draw awards no point and still exhausts both chosen emojis.
- If neither player reaches 3 wins before all live emojis are exhausted, the tiebreaker is:
  - higher round wins
  - if still tied, fewer unresolved side-status penalties
  - if still tied, sudden-death priority round using each player's ban-excluded wildcard slot from deck creation

### Status model

- Emoji pieces are one-shot match resources.
- Persistent effects are attached to `side state`, not to exhausted pieces.
- MVP persistent effect buckets:
  - `burn`
  - `poison`
  - `cleanse`
  - `growth`
  - `shield charge`
  - `stun/freeze carryovers` only if a matchup explicitly creates one
- Each persistent state must specify:
  - source emoji
  - trigger timing
  - expiry rule
  - cleanse interaction
  - WHY text fragment

### Player-facing fairness rules

- Opponent picks are never exposed before both sides lock.
- All authoritative resolution happens on the server.
- Every round returns:
  - winner or draw
  - effect log
  - reason code
  - short WHY line
  - optional expanded chain
  - Codex unlock payload
- Soft warning at `20s`, hard deadline at `30s`.
- One timeout loses the round.
- Two timeouts forfeit the match.

### Product framing

- Market the game as "fast tactics in one tap."
- Treat full-match elapsed time as short-session when both players remain active.
- Treat async continuation as a fallback convenience, not the fantasy.
- Social sharing is built around 4-6 second replay snippets plus caption templates.

## Core game systems

### 1. Gameplay and content

- Ship with the 12-emoji roster from the concept.
- Keep the five role buckets:
  - Elements
  - Tricks
  - Hazards
  - Guards/Support
  - Status/Ramp
- Author a full interaction sheet for each emoji:
  - role
  - primary verb
  - strengths
  - weaknesses
  - status interactions
  - arena hooks
  - reason-code templates
- Build the full 12 x 12 interaction matrix before live PvP.
- Reserve arena modifiers in code behind flags; do not enable them in ranked at launch.

### 2. Rules engine

- Build the rules engine as a standalone domain module with no Unity dependencies.
- Engine inputs:
  - `match_id`
  - `rules_version`
  - `round_number`
  - `arena_modifier`
  - `player_a_pick`
  - `player_b_pick`
  - `player_a_side_state`
  - `player_b_side_state`
- Engine outputs:
  - `round_result`
  - `winner`
  - `effect_log[]`
  - `reason_code`
  - `why_text`
  - `why_chain[]`
  - `updated_side_state`
  - `codex_events[]`
  - `replay_events[]`
- Resolution order:
  - arena modifier
  - passive protection
  - reflect/redirect
  - delete/nullify
  - pull/reposition
  - stun/freeze
  - direct damage/elemental counters
  - status application
  - growth/setup
  - final state check
  - WHY and Codex generation
- Every matchup result is versioned so historical replays survive balance changes.
- Required test coverage:
  - all 144 pairings
  - same-pick mirrors
  - ban edge cases
  - timeout outcomes
  - reconnect recovery
  - stale-client/rules-version handling

### 3. WHY and Codex

- WHY is a mandatory gameplay system, not optional UX.
- Keep the short WHY caption under 90 characters.
- Keep expanded WHY chains to 2-4 nodes.
- Reason codes are canonical and stable for analytics, Codex, QA, and replay captions.
- Codex unlock triggers:
  - first seen
  - first used
  - first lost against
- Each Codex entry stores:
  - emoji pair or interaction family
  - outcome summary
  - WHY text
  - counter tip
  - replay thumbnail key
  - first seen timestamp

### 4. Bots

- Practice Bot teaches readable counters and common safe mistakes.
- Chaos Bot models baiting and delayed support use.
- Bots must only use visible information plus configured behavior weights.
- Bot service should call the same rules engine and deck-state model as PvP.

## Client, backend, and live systems

### Client

- Unity 2D client owns:
  - authentication surfaces
  - deck builder
  - queue and ban flow
  - round pick UI
  - replay visualization
  - WHY/Codex presentation
  - leaderboard
  - pass/store shell
- Use a presentation model that is entirely driven by server state plus local animation timing.
- Start with portrait layout and optimize for thumb reach.

### Backend

- Supabase owns:
  - auth
  - profiles
  - decks
  - matches
  - turns
  - ratings
  - codex unlocks
  - seasons
  - pass progression
- Core Edge Functions:
  - `queue_or_join_match`
  - `submit_ban`
  - `submit_pick`
  - `resolve_round`
  - `finalize_match_and_update_elo`
  - `unlock_codex_entries`
  - `claim_rewarded_ad_reward`
- Apply RLS for:
  - profile ownership
  - deck ownership
  - match participation
  - turn submission by match participant only
- Realtime is used for:
  - queue status
  - turn updates
  - ban completion
  - match resolution notifications

### Data model

- `profiles`
- `decks`
- `deck_slots`
- `matches`
- `match_players`
- `match_bans`
- `rounds`
- `turns`
- `ratings`
- `rating_history`
- `codex_unlocks`
- `seasons`
- `season_progress`
- `cosmetics`
- `inventory`
- `ad_reward_claims`

### Analytics and notifications

- Firebase Analytics and Crashlytics are required before external testing.
- Minimum event set:
  - `deck_created`
  - `deck_updated`
  - `queue_started`
  - `match_found`
  - `ban_selected`
  - `pick_locked`
  - `round_resolved`
  - `match_won`
  - `match_lost`
  - `elo_changed`
  - `codex_entry_unlocked`
  - `share_clicked`
  - `share_completed`
  - `pass_viewed`
  - `pass_purchased`
  - `rewarded_ad_started`
  - `rewarded_ad_completed`
- FCM is used for:
  - your turn
  - match complete
  - pass ending soon
  - friend/rematch hooks in later phases

## Economy and progression

### Launch economy

- No gameplay power sales.
- Free players get full gameplay access, ranked eligibility, Codex, and basic cosmetics.
- Paid pass is `$4.99 / 28 days`.
- Rewarded ads are capped at `3-5 per day`.
- Reward types:
  - pass XP
  - cosmetic dust
  - replay frame
  - small cosmetic chest

### Ranked and seasons

- Elo with `K=32`.
- Ranked unlock requires `5 completed PvP matches`.
- Bot matches never affect Elo.
- Leaderboard views:
  - global top 100
  - my rank
  - nearest ranks
  - current season
- Reset cadence follows the 28-day season pass cadence.

## Build roadmap

### Phase 0: Design lock and simulation

- Finalize corrected match format, status model, and tiebreak rule.
- Write per-emoji spec sheets.
- Build the 12 x 12 interaction matrix with reason codes.
- Create a local simulation harness for matchup validation.
- Exit criteria:
  - all pairings authored
  - no blind-pick emoji
  - every emoji has at least two bad situations
  - WHY text exists for every canonical outcome

### Phase 1: Offline vertical slice

- Build local duel flow in Unity:
  - deck selection
  - ban screen
  - pick/lock
  - round resolution
  - WHY overlay
  - Codex unlock toast
  - bot opponent
- Exit criteria:
  - one full duel playable offline
  - replay timeline renders correctly
  - practice bot demonstrates all major counter families

### Phase 2: Online competitive MVP

- Implement Supabase schema, RLS, and Edge Functions.
- Add auth, profiles, deck persistence, queueing, async match state, and Elo.
- Add reconnect and timeout logic.
- Exit criteria:
  - real PvP duel completes end-to-end
  - no client authority over results
  - hidden picks cannot leak through subscriptions or payloads

### Phase 3: Retention and monetization

- Add Codex screens, leaderboard UI, push notifications, pass/store shell, rewarded ads, and replay-share templates.
- Add analytics dashboards and live tuning controls.
- Exit criteria:
  - share flow works with templated clips
  - pass rewards can be granted safely
  - core telemetry covers funnel, fairness, and churn points

### Phase 4: Closed alpha to launch prep

- Run structured playtests with fairness and comprehension prompts.
- Review confusing matchups and rebalance interaction matrix.
- Lock original battlemoji art pipeline and legal review.
- Prepare season 1 cosmetic content and moderation/support SOPs.
- Exit criteria:
  - players can explain why they lost
  - players do not describe the game as random
  - at least 1-2 emojis become repeat favorite mind-game tools
  - replay clips are understandable without extra tutorial context

## Test and validation plan

- Unit test every interaction rule and reason code.
- Snapshot test WHY text and replay event payloads.
- Simulate thousands of bot-vs-bot matches to flag dominant picks and dead emojis.
- Test matchmaking and timeout flows under reconnect and duplicate-submit conditions.
- Run playtests focused on:
  - fairness perception
  - clarity of WHY
  - whether bans feel strategic or annoying
  - whether support picks create clutch moments instead of boring stalls
- Launch only if the product is tracking toward:
  - `D1 >= 30%`
  - `D7 >= 8%`
  - `D30-like >= 2%`

## Defaults and assumptions used in this plan

- Competitive launch format uses `6` deck slots so bans and best-of-5 are mathematically consistent.
- Persistent statuses are side-state effects, not per-unit long-term objects.
- Arena modifiers are implemented behind flags but disabled for launch ranked.
- Hybrid async is the shipping posture: short when both players are present, async when needed.
- Art uses original battlemoji assets only.
- Public MVP excludes guilds, chat, live real-time PvP, complex maps, and multi-currency economy.
