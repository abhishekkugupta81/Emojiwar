# EmojiWar Mobile Build Hardening

## Goal

Prepare the current Unity + Supabase project for repeatable Android and iPhone test builds after gameplay implementation is stable.

## Current hardening decisions

- Product name: `EmojiWar`
- Company name: `EmojiWar`
- `PlayerSettings.runInBackground = true`
- Unity analytics submission disabled
- Portrait-first play validation is required for all UI checks

## Unity editor tools

Use these menu items before device testing:

- `EmojiWar -> Mobile -> Apply Launch Mobile Defaults`
- `EmojiWar -> Mobile -> Use Local Supabase (127.0.0.1)`
- `EmojiWar -> Mobile -> Use This PC LAN Supabase`
- `EmojiWar -> Mobile -> Set Suggested Bundle Identifiers`
- `EmojiWar -> Mobile -> Validate Android Readiness`
- `EmojiWar -> Mobile -> Validate iPhone Readiness`
- `EmojiWar -> Mobile -> Build Android APK (Development)`
- `EmojiWar -> Mobile -> Build Android App Bundle (Release)`
- `EmojiWar -> Mobile -> Export iPhone Xcode Project`
- `EmojiWar -> Mobile -> Open Build Output Folder`

Important:

- `Generate All Starter Assets` now preserves an already-configured Supabase URL instead of always resetting it to `127.0.0.1`
- for Android/iPhone testing on Wi-Fi, use `Use This PC LAN Supabase` after your PC is connected to the same network as the device

## Local backend assumptions

- Local Supabase URL: `http://127.0.0.1:54321`
- This works for:
  - Unity Editor
  - Windows standalone on the same machine
- This does **not** work for real mobile devices on Wi-Fi.

For device testing, replace the project URL in the Unity config asset with your PC's LAN IP:

- Example: `http://192.168.1.25:54321`

The new `EmojiWar -> Mobile -> Use This PC LAN Supabase` menu item does this automatically.

## Android hardening checklist

1. Unity modules installed
- Android Build Support
- Android SDK & NDK Tools
- OpenJDK

2. Player settings to confirm
- portrait behavior only
- package name set intentionally
- min SDK reviewed
- app icon and splash placeholders assigned before public test distribution
- run `EmojiWar -> Mobile -> Validate Android Readiness`

3. Device validation
- app launches cleanly
- guest auth persists across app restart
- deck builder is scrollable and readable
- ranked queue works against the intended backend URL
- bot flow completes end-to-end
- app resume does not lose ranked match state

4. Network validation
- local Wi-Fi device can reach the Supabase URL
- function calls succeed for:
  - `queue_or_join_match`
  - `submit_ban`
  - `submit_formation`
  - `get_codex`
  - `get_leaderboard`

## Android build workflow

1. `Assets -> Refresh`
2. `EmojiWar -> Setup -> Generate All Starter Assets`
3. `EmojiWar -> Mobile -> Apply Launch Mobile Defaults`
4. `EmojiWar -> Mobile -> Set Suggested Bundle Identifiers`
5. `EmojiWar -> Mobile -> Use This PC LAN Supabase`
6. keep local backend running:

```powershell
npm run supabase:functions:serve
```

7. `EmojiWar -> Mobile -> Validate Android Readiness`
8. Build an installable package:
- `EmojiWar -> Mobile -> Build Android APK (Development)` for device testing
- `EmojiWar -> Mobile -> Build Android App Bundle (Release)` for distribution packaging
9. `EmojiWar -> Mobile -> Open Build Output Folder`
10. Install APK on device and validate the gameplay checklist below

## iPhone hardening checklist

1. Unity modules installed
- iOS Build Support

2. Required environment
- macOS machine
- Xcode
- Apple signing setup

3. Player settings to confirm
- portrait behavior only
- bundle identifier set intentionally
- target iOS version reviewed
- launch/splash placeholders assigned
- run `EmojiWar -> Mobile -> Validate iPhone Readiness`

4. Validation
- generated Xcode project builds
- guest auth persists across relaunch
- local-network backend URL is reachable if testing against local Supabase
- ranked resume path survives background/foreground transitions

## iPhone export workflow

1. `Assets -> Refresh`
2. `EmojiWar -> Setup -> Generate All Starter Assets`
3. `EmojiWar -> Mobile -> Apply Launch Mobile Defaults`
4. `EmojiWar -> Mobile -> Set Suggested Bundle Identifiers`
5. `EmojiWar -> Mobile -> Use This PC LAN Supabase`
6. `EmojiWar -> Mobile -> Validate iPhone Readiness`
7. On macOS Unity editor, run:
- `EmojiWar -> Mobile -> Export iPhone Xcode Project`
8. Open the exported Xcode project, configure signing/team, then build/install on iPhone

## Device validation checklist

Run this on Android first, then iPhone:

1. App launch
- app starts from cold launch without error
- Home shows profile and active squad

2. Guest auth persistence
- close app completely and reopen
- same guest identity/profile should remain

3. Deck Builder
- all 16 emojis selectable
- no overlap/cutoff
- save/edit flows work

4. Bot flow
- choose 5, lock formation, resolve battle
- result screen renders WHY and chain text

5. Ranked flow
- queue two players
- blind ban
- formation lock
- battle resolve on both clients

6. Resume and timeout
- background/foreground the app during queue, blind ban, and formation
- verify resume CTA/state restoration
- verify blind-ban auto-lock timeout and formation auto-fill timeout

7. Codex and Leaderboard
- Codex unlock list loads
- Leaderboard standings load
- no blocking screen overlap on both platforms

## Pre-device validation in Unity

Before pushing to a device, validate:

1. `Assets -> Refresh`
2. `EmojiWar -> Setup -> Generate All Starter Assets`
3. `npm run supabase:functions:serve`
4. `npm run verify:resolver`
5. One bot match
6. One two-client ranked match
7. Codex and Leaderboard load without layout overlap

## Remaining deliberate decisions

These should be chosen intentionally before store submission or external testing:

- Android package identifier
- iOS bundle identifier
- versioning scheme
- app icons / splash assets
- production Supabase project URL and publishable key
