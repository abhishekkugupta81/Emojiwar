import { assertEquals } from "https://deno.land/std@0.224.0/assert/mod.ts";
import { visibleDeltaForClashResult } from "./clash-ladder-policy.ts";

Deno.test("Quick Clash human ladder points use locked visible values", () => {
  assertEquals(visibleDeltaForClashResult("win", "human"), 100);
  assertEquals(visibleDeltaForClashResult("draw", "human"), 40);
  assertEquals(visibleDeltaForClashResult("loss", "human"), -35);
  assertEquals(visibleDeltaForClashResult("timeout_forfeit", "human"), -35);
});

Deno.test("Quick Clash bot-fill visible points use same locked values without touching hidden MMR policy", () => {
  assertEquals(visibleDeltaForClashResult("win", "bot_fill", 0), 100);
  assertEquals(visibleDeltaForClashResult("draw", "bot_fill", 0), 40);
  assertEquals(visibleDeltaForClashResult("loss", "bot_fill", 0), -35);
  assertEquals(visibleDeltaForClashResult("timeout_forfeit", "bot_fill", 0), -35);
});

Deno.test("Quick Clash timeout-forfeit remains a normal visible loss", () => {
  assertEquals(visibleDeltaForClashResult("loss", "human"), visibleDeltaForClashResult("timeout_forfeit", "human"));
  assertEquals(visibleDeltaForClashResult("loss", "bot_fill"), visibleDeltaForClashResult("timeout_forfeit", "bot_fill"));
});
