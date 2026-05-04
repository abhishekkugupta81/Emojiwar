import { assertEquals } from "https://deno.land/std@0.224.0/assert/mod.ts";
import {
  resolveClashMatchmakingBand,
  selectEligibleClashOpponent,
  shouldCreateClashBotFill,
} from "./clash-matchmaking.ts";

const now = new Date("2026-05-02T12:00:00.000Z");

Deno.test("Quick Clash matchmaking uses locked rating bands", () => {
  assertEquals(resolveClashMatchmakingBand("2026-05-02T11:59:55.000Z", now), 150);
  assertEquals(resolveClashMatchmakingBand("2026-05-02T11:59:50.000Z", now), 300);
  assertEquals(resolveClashMatchmakingBand("2026-05-02T11:59:40.000Z", now), 300);
});

Deno.test("Quick Clash matchmaking picks oldest eligible opponent inside rating band", () => {
  const selected = selectEligibleClashOpponent([
    candidate("newer", "user-newer", 1000, "2026-05-02T11:59:58.000Z"),
    candidate("oldest", "user-oldest", 1100, "2026-05-02T11:59:50.000Z"),
    candidate("too-high", "user-high", 1400, "2026-05-02T11:59:45.000Z"),
  ], "request-user", 1000, "", "", now);

  assertEquals(selected?.id, "oldest");
});

Deno.test("Quick Clash matchmaking does not match outside first band before 8 seconds", () => {
  const selected = selectEligibleClashOpponent([
    candidate("too-far", "user-far", 1200, "2026-05-02T11:59:55.000Z"),
  ], "request-user", 1000, "", "", now);

  assertEquals(selected, null);
});

Deno.test("Quick Clash matchmaking widens to second band after 8 seconds", () => {
  const selected = selectEligibleClashOpponent([
    candidate("wide", "user-wide", 1250, "2026-05-02T11:59:50.000Z"),
  ], "request-user", 1000, "", "", now);

  assertEquals(selected?.id, "wide");
});

Deno.test("Quick Clash matchmaking can widen from actively waiting request queue", () => {
  const selected = selectEligibleClashOpponent([
    candidate("new-candidate", "candidate", 1250, "2026-05-02T11:59:58.000Z"),
  ], "request-user", 1000, "2026-05-02T11:59:50.000Z", "", now);

  assertEquals(selected?.id, "new-candidate");
});

Deno.test("Quick Clash matchmaking does not widen beyond second band after 10 seconds", () => {
  const selected = selectEligibleClashOpponent([
    candidate("too-wide", "user-wide", 1400, "2026-05-02T11:59:40.000Z"),
  ], "request-user", 1000, "", "", now);

  assertEquals(selected, null);
});

Deno.test("Quick Clash bot fill threshold uses original queueStartedAt", () => {
  assertEquals(shouldCreateClashBotFill("2026-05-02T11:59:51.000Z", now), false);
  assertEquals(shouldCreateClashBotFill("2026-05-02T11:59:50.000Z", now), true);
  assertEquals(shouldCreateClashBotFill("2026-05-02T11:59:30.000Z", now), true);
});


Deno.test("Quick Clash matchmaking avoids immediate rematch when another eligible opponent exists", () => {
  const selected = selectEligibleClashOpponent([
    candidate("recent", "recent-opponent", 1000, "2026-05-02T11:59:40.000Z"),
    candidate("other", "other-opponent", 1000, "2026-05-02T11:59:45.000Z"),
  ], "request-user", 1000, "", "recent-opponent", now);

  assertEquals(selected?.id, "other");
});

Deno.test("Quick Clash matchmaking allows recent opponent if no alternative exists", () => {
  const selected = selectEligibleClashOpponent([
    candidate("recent", "recent-opponent", 1000, "2026-05-02T11:59:40.000Z"),
  ], "request-user", 1000, "", "recent-opponent", now);

  assertEquals(selected?.id, "recent");
});

function candidate(id: string, player: string, rating: number, createdAt: string) {
  return {
    id,
    player_a: player,
    created_at: createdAt,
    current_state: {
      matchmakingRatingAtQueue: rating,
      queueStartedAt: createdAt,
      queueExpiresAt: "2026-05-02T12:02:00.000Z",
    },
  };
}
