import { assert, assertEquals } from "https://deno.land/std@0.224.0/assert/mod.ts";
import { CLASH_BOT_PROFILES, selectClashBotProfile } from "./clash-bot-profiles.ts";

Deno.test("Quick Clash bot profiles use stable player-facing handles", () => {
  assertEquals(CLASH_BOT_PROFILES.length, 20);
  for (const profile of CLASH_BOT_PROFILES) {
    assert(profile.display_name.length >= 6 && profile.display_name.length <= 14);
    assert(!/bot|ai|cpu/i.test(profile.display_name));
    assert(profile.active);
  }
});

Deno.test("Quick Clash bot profile selection avoids immediate repeat when possible", () => {
  const first = selectClashBotProfile(1000, "match-seed");
  const next = selectClashBotProfile(1000, "match-seed", first.bot_profile_id);
  assert(first.bot_profile_id !== next.bot_profile_id);
});
