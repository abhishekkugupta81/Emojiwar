import { assertEquals } from "https://deno.land/std@0.224.0/assert/mod.ts";
import {
  createInitialClashState,
  EMOJI_CLASH_PICK_TTL_SECONDS,
  EMOJI_CLASH_TURN_VALUES,
  resolvePickedTurn,
  resolveTimeoutTurn,
  selectTimeoutBurnUnit,
  startPickPhase,
} from "./emoji-clash-rules.ts";

Deno.test("Emoji Clash PvP keeps launch turn scoring", () => {
  assertEquals([...EMOJI_CLASH_TURN_VALUES], [1, 1, 2, 2, 3]);
});

Deno.test("Emoji Clash PvP uses 15 second turn timer", () => {
  assertEquals(EMOJI_CLASH_PICK_TTL_SECONDS, 15);
});

Deno.test("Emoji Clash PvP resolves both picks and consumes both units", () => {
  const state = startPickPhase(createInitialClashState("queue-test"));
  const resolved = resolvePickedTurn(state, "wind", "bomb");
  assertEquals(resolved.turnHistory.length, 1);
  assertEquals(resolved.playerAUsedUnits, ["wind"]);
  assertEquals(resolved.playerBUsedUnits, ["bomb"]);
  assertEquals(resolved.currentTurnIndex, 1);
});

Deno.test("Emoji Clash PvP one-player timeout awards turn and consumes one unit per side", () => {
  const state = startPickPhase(createInitialClashState("queue-test"));
  const resolved = resolveTimeoutTurn(state, "fire", "", "match-one", "user-a", "user-b");
  assertEquals(resolved.playerAScore, 1);
  assertEquals(resolved.playerBScore, 0);
  assertEquals(resolved.playerAUsedUnits, ["fire"]);
  assertEquals(resolved.playerBUsedUnits.length, 1);
  assertEquals(resolved.playerBTimeoutStrikes, 1);
  assertEquals(resolved.turnHistory[0].playerBTimedOut, true);
  assertEquals(resolved.turnHistory[0].outcome, "player_a");
});

Deno.test("Emoji Clash PvP double timeout draws and consumes one unit per side", () => {
  const state = startPickPhase(createInitialClashState("queue-test"));
  const resolved = resolveTimeoutTurn(state, "", "", "match-double", "user-a", "user-b");
  assertEquals(resolved.playerAScore, 0);
  assertEquals(resolved.playerBScore, 0);
  assertEquals(resolved.playerAUsedUnits.length, 1);
  assertEquals(resolved.playerBUsedUnits.length, 1);
  assertEquals(resolved.playerATimeoutStrikes, 1);
  assertEquals(resolved.playerBTimeoutStrikes, 1);
  assertEquals(resolved.turnHistory[0].outcome, "draw");
});

Deno.test("Emoji Clash PvP deterministic timeout burn avoids used units", () => {
  const burn = selectTimeoutBurnUnit(["bomb", "fire", "wind"], "match-burn", 2, "player_a", "user-a");
  assertEquals(["bomb", "fire", "wind"].includes(burn), false);
  assertEquals(burn, selectTimeoutBurnUnit(["bomb", "fire", "wind"], "match-burn", 2, "player_a", "user-a"));
});

Deno.test("Emoji Clash PvP second timeout strike forfeits match", () => {
  const state = startPickPhase(createInitialClashState("queue-test"));
  const first = resolveTimeoutTurn(state, "", "fire", "match-forfeit", "user-a", "user-b");
  const second = resolveTimeoutTurn(first, "", "water", "match-forfeit", "user-a", "user-b");
  assertEquals(second.phase, "finished");
  assertEquals(second.playerATimeoutStrikes, 2);
  assertEquals(second.winner, "player_b");
  assertEquals(second.finishReason, "player_a_timeout_forfeit");
});

Deno.test("Emoji Clash PvP simultaneous forfeit uses current score then draw fallback", () => {
  const state = startPickPhase(createInitialClashState("queue-test"));
  const first = resolveTimeoutTurn(state, "", "", "match-both-forfeit", "user-a", "user-b");
  const second = resolveTimeoutTurn(first, "", "", "match-both-forfeit", "user-a", "user-b");
  assertEquals(second.phase, "finished");
  assertEquals(second.playerATimeoutStrikes, 2);
  assertEquals(second.playerBTimeoutStrikes, 2);
  assertEquals(second.winner, "draw");
  assertEquals(second.finishReason, "both_timeout_forfeit");
});
