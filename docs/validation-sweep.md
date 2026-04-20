# EmojiWar Validation Sweep

## Automated Checks

Run these from the repo root:

```powershell
npm run verify:resolver
npm run supabase:status
```

Expected:

- `verify:resolver` passes all checks
- local Supabase reports healthy APIs and database endpoints

## Unity Preflight

Before manual validation:

1. `Assets -> Refresh`
2. `EmojiWar -> Setup -> Generate All Starter Assets`
3. restart local functions runtime if backend code changed:

```powershell
npm run supabase:functions:serve
```

4. if local state is polluted, reset it:

```powershell
npm run supabase:db:reset
```

## Core Manual Sweep

### 1. Home / Session

- Home loads without overlap or cutoff in portrait preview
- `Player Profile` is visible
- two clients show different player identities
- `Active Squad` renders correctly

### 2. Deck Builder

- `Battle Players` starts with empty `6`-emoji selection
- `Battle Bot` starts with empty `5`-emoji selection
- all `16` emojis are selectable
- selection count updates correctly
- `Continue` stays disabled until exact count is reached
- `Edit Deck` still edits the persistent active squad

### 3. Ranked PvP Two-Client Flow

- client A chooses `6` and enters queue
- client B chooses `6` and enters queue
- both move to blind ban
- both can ban from opponent roster
- if both ban, both move to formation
- if one does nothing, blind-ban timeout auto-locks after about `30s`
- formation timeout auto-fills after about `45s`
- both clients receive the same final match result with correct opposite perspective:
  - one `You Win`
  - one `Opponent Wins`

### 4. Ranked Resume / Cancel

- queued player can `Return Home`
- queued row cancels cleanly
- no stale `Resume Ranked Match` CTA remains for cancelled queue
- in-progress ranked match shows `Resume Ranked Match` on Home
- resume returns to the correct current phase

### 5. Bot Flow

- choose `5` and continue
- formation screen loads
- Practice Bot completes a full match
- Smart Bot completes a full match
- Smart Bot feels meaningfully stronger than Practice Bot

### 6. Codex

- Codex opens without overlap
- latest unlock renders
- entries list scrolls
- title, summary, tip, and unlock timestamp appear

### 7. Leaderboard

- Leaderboard opens without overlap
- top players render
- nearby ranks render
- current user standing renders
- player names resolve from profiles when available

### 8. WHY / Battle Result Quality

- result screen is readable in portrait
- WHY summary is not dominated by repetitive generic clash text
- WHY chain contains distinct useful moments

## Device-Oriented Follow-Up

After the desktop sweep passes:

- Android build/export validation
- local-network backend testing from device using PC LAN IP
- app resume/background validation on device
- iPhone export/build validation on macOS/Xcode

## Current Acceptance Bar

The validation sweep is complete when:

- automated checks pass
- two-client ranked flow works end-to-end
- bot flow works end-to-end
- Codex and Leaderboard render live data correctly
- no blocking resume/timeout/cancel bugs remain
