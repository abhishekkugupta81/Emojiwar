# EmojiWar

## Product

EmojiWar is a portrait mobile 5v5 squad battler for iPhone and Android. Players build a 6-emoji active deck, survive a blind ban, field the final 5 in formation, and win through composition, positioning, counters, and hidden-but-explainable interactions.

The launch direction is:

- `5v5 auto-battle`, not repeated duel rounds
- `16 launch emojis`
- blind ban in ranked
- deterministic server-authoritative battle resolution
- WHY summaries and short cause chains after every battle
- Codex unlocks tied to real interaction verbs

`README.md` is the top-level source of truth for launch scope, architecture, and exclusions.

## Launch Scope

Launch is intentionally gameplay-first.

Required launch features:

- `Battle Players`: ranked async PvP
- `Battle Bot`: Practice Bot and Smart Bot
- `Deck Builder`
- one persisted `active deck` of 6 unique emojis
- blind ban in ranked
- deterministic 5v5 auto-battle
- WHY summary and cause chain
- Codex
- Leaderboard
- guest-first account flow

Current implementation status:

- 16-emoji roster is wired through client and server ids/contracts
- active deck save and Supabase sync work
- Battle Bot now locks a final 5 and submits manual formation before the deterministic 5v5 battle resolves
- Ranked PvP supports queue, blind ban, manual formation, and server-resolved battle state
- Codex now reads real unlocks from Supabase
- Leaderboard now reads ranked standings from a dedicated Supabase Edge Function
- default auto-formation still exists as a fallback, but the launch flow now expects player-side formation locking

## Tech Stack

- `Unity Personal + C#`
- `Unity 2D + UGUI`
- `Supabase Auth/Postgres/Realtime/Storage/Edge Functions`
- `TypeScript/Deno` for Edge Functions, interaction matrix, and 5v5 battle simulator
- `Android-first` development order, with `iPhone + Android` as launch targets

## Core Rules

Launch rules are locked to:

- `16` total launch emojis
- `6-emoji active deck`
- `1 blind ban per side in ranked`
- `5-emoji final team after bans`
- `5-emoji direct squad selection in bot/casual flows`
- `5v5 auto-battle` decides the match
- last surviving team wins
- simultaneous wipe resolves as draw

Design law:

- no emoji is universally strongest
- every emoji must have at least `2` clear failure states
- support/control emojis must create visible swing moments
- positioning can beat stronger-looking raw teams
- synergy should beat individually flashy picks

## Launch Roster

Elements:

- `Fire`
- `Water`
- `Lightning`
- `Ice`

Tricks / Control:

- `Magnet`
- `Mirror`
- `Hole`
- `Wind`
- `Ghost`
- `Chain`

Hazards:

- `Bomb`

Guards / Support:

- `Shield`
- `Soap`
- `Heart`

Status / Ramp:

- `Snake`
- `Plant`

## Modes

### Battle Players

- ranked async PvP
- each player selects `6`
- each player blindly bans `1` from the opponent
- each player locks a formation for the final `5`
- final `5` auto-battle
- Elo and leaderboard eligible

### Battle Bot

- instant, always-available play
- uses the same 6-emoji active deck
- auto-benches or selects the final 5 for battle
- player locks formation for the final 5 before battle resolve
- Practice Bot teaches interactions
- Smart Bot pressures combo and positioning mistakes
- no Elo
- no leaderboard credit

## 5v5 Combat Model

Server combat uses `Clash Cycles`:

1. start-of-battle setup
2. frontline clash
3. ability / verb triggers
4. cleanup and status ticks
5. repeat until wipe or draw

The battle simulator is built in three layers:

- `Interaction Matrix`: ordered pairwise emoji interactions
- `Battle Orchestrator`: target selection, cycle order, and trigger timing
- `State Engine`: HP, shields, statuses, deaths, victory detection

The matrix is the interaction kernel, not the full match result table.

## Unity Client Architecture

Unity is the presentation and local interaction layer.

Client responsibilities:

- bootstrap guest session state
- create or sync the active deck
- render Home, Deck Builder, Match, Codex, and Leaderboard screens
- send queue, ban, and match requests to Supabase Edge Functions
- render server battle outcomes, WHY text, and Codex-ready explanations

Scene structure:

- `Bootstrap`
- `Home`
- `DeckBuilder`
- `Match`
- `Codex`
- `Leaderboard`

Implementation modules:

- `Core`
- `Gameplay`
- `UI`
- `Content`

Unity is not authoritative. It never decides the battle winner, WHY text, Codex reason codes, or Elo changes.

## Supabase Backend Architecture

Supabase is the only launch backend platform.

Services in use:

- `Auth` for guest-first identity
- `Postgres` for profiles, decks, matches, ratings, Codex unlocks, and analytics
- `Realtime` for queue and match-state driven UI
- `Storage` reserved for future replay thumbnails / shared assets
- `Edge Functions` for queueing, bot battles, bans, battle resolution, Elo, and Codex writes

Launch server functions:

- `queue_or_join_match`
- `cancel_ranked_queue`
- `start_bot_match`
- `submit_ban`
- `submit_formation`
- `get_codex`
- `get_leaderboard`
- `finalize_match_and_update_elo`
- `unlock_codex_entries`

Legacy duel endpoints remain in the repo only as compatibility stubs during the pivot. They are not part of the launch architecture.

## Repo Structure

```text
client/
  Assets/
    Scenes/
    Scripts/
  Packages/
  ProjectSettings/
supabase/
  functions/
  migrations/
docs/
README.md
```

## Implementation Phases

### Phase 1: Roster + Contracts

- expand all ids/contracts from `12` to `16`
- lock stats, role tags, and preferred rows
- wire content generation and deck builder to the 16-roster

### Phase 2: Interaction Matrix

- author the `16x16` ordered interaction-kernel matrix
- keep the matrix authoritative for pairwise verb outcomes
- lock reason codes and WHY text

### Phase 3: 5v5 Simulator

- resolve clash cycles deterministically
- apply positioning and target rules
- apply burn, poison, stun, freeze, bind, push, pull, heal, and growth
- generate battle event logs and WHY output

### Phase 4: Match Lifecycle

- ranked queue
- blind ban
- formation locking
- server-resolved auto-battle
- final result and Codex generation

### Phase 5: Client Tactical Layer

- richer battle replay presentation
- live leaderboard / Codex surfaces

## Testing

The launch test plan must cover:

- all `256` ordered interaction entries
- deterministic battle resolution for the same seed / teams / formations
- new verb behavior for `Wind`, `Heart`, `Ghost`, and `Chain`
- blind ban correctness in ranked
- no infinite battle loops
- simultaneous final elimination as draw
- zero Elo / leaderboard impact from bot matches
- guest auth persistence across restarts

The first automated resolver tests live under:

- `supabase/functions/_shared/interaction-matrix.test.ts`
- `supabase/functions/_shared/battle-simulator.test.ts`

Local verification commands:

- `npm run test:resolver`
- `npm run verify:resolver`

Use `verify:resolver` as the fallback on Windows if `deno test` crashes in the Deno runtime.

## After Launch Only

These are explicitly excluded from launch implementation:

- Firebase
- RevenueCat
- push notifications
- ads SDKs
- season pass/store
- rewarded ads
- third-party analytics/crash SDKs
- casual PvP live-ops expansion
- ranked arena modifiers

Do not add these systems during launch implementation unless the plan is explicitly revised.

## Supporting Docs

- [Research analysis](c:\Users\coron\Projects\EmojiWar\docs\research-analysis.md)
- [Complete game plan](c:\Users\coron\Projects\EmojiWar\docs\complete-game-plan.md)
- [Validation sweep](c:\Users\coron\Projects\EmojiWar\docs\validation-sweep.md)
- [Mobile build hardening](c:\Users\coron\Projects\EmojiWar\docs\mobile-build-hardening.md)
