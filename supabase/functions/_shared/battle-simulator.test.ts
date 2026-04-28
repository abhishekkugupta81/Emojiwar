import { buildDefaultFormation, resolveBattle } from "./battle-simulator.ts";
import type { EmojiId } from "./contracts.ts";
import { REASON_CODES } from "./reason-codes.ts";

function assert(condition: unknown, message: string): asserts condition {
  if (!condition) {
    throw new Error(message);
  }
}

function serialize(value: unknown): string {
  return JSON.stringify(value, null, 2);
}

function resolve(teamA: EmojiId[], teamB: EmojiId[], seed = "test-seed") {
  return resolveBattle({
    mode: "pvp_ranked",
    rulesVersion: "launch-5v5-001",
    battleSeed: seed,
    teamA,
    teamB,
    formationA: buildDefaultFormation(teamA),
    formationB: buildDefaultFormation(teamB),
  });
}

function resolveWithFormations(
  teamA: EmojiId[],
  teamB: EmojiId[],
  formationA: ReturnType<typeof buildDefaultFormation>,
  formationB: ReturnType<typeof buildDefaultFormation>,
  seed = "test-seed",
) {
  return resolveBattle({
    mode: "pvp_ranked",
    rulesVersion: "launch-5v5-001",
    battleSeed: seed,
    teamA,
    teamB,
    formationA,
    formationB,
  });
}

Deno.test("default formation creates unique ordered slots", () => {
  const formation = buildDefaultFormation(["shield", "heart", "wind", "bomb", "soap"]);
  assert(formation.placements.length === 5, "Formation should place 5 units");

  const slotSet = new Set(formation.placements.map((placement) => placement.slot));
  const emojiSet = new Set(formation.placements.map((placement) => placement.emojiId));
  assert(slotSet.size === 5, "Formation slots must be unique");
  assert(emojiSet.size === 5, "Formation emojis must be unique");
});

Deno.test("battle simulator is deterministic for the same seed and formations", () => {
  const first = resolve(["shield", "heart", "wind", "soap", "plant"], ["lightning", "ghost", "chain", "bomb", "fire"], "fixed-seed");
  const second = resolve(["shield", "heart", "wind", "soap", "plant"], ["lightning", "ghost", "chain", "bomb", "fire"], "fixed-seed");

  assert(serialize(first) === serialize(second), "Battle resolution should be deterministic for the same input");
});

Deno.test("wind can generate a push event against a support target", () => {
  const result = resolve(["wind"], ["heart"], "wind-heart");
  assert(
    result.battleState.eventLog.some((event) => event.reasonCode === REASON_CODES.WIND_SCATTERED_FORMATION),
    "Expected a wind push/disruption event",
  );
});

Deno.test("heart can generate a heal event during the battle", () => {
  const result = resolve(["heart", "plant"], ["lightning"], "heart-heal");
  assert(
    result.battleState.eventLog.some((event) => event.type === "heal" && event.reasonCode === REASON_CODES.HEART_STABILIZED_ALLY),
    "Expected Heart to heal an ally",
  );
});

Deno.test("ghost can reach a protected backline target", () => {
  const result = resolve(["ghost"], ["shield", "heart"], "ghost-dive");
  assert(
    result.battleState.eventLog.some((event) => event.reasonCode === REASON_CODES.GHOST_PHASED_PAST_FRONT),
    "Expected Ghost to produce a backline dive event",
  );
});

Deno.test("chain can bind a movement-based target", () => {
  const result = resolve(["chain"], ["wind"], "chain-bind");
  assert(
    result.battleState.eventLog.some((event) => event.reasonCode === REASON_CODES.CHAIN_BOUND_TARGET),
    "Expected Chain to bind Wind",
  );
});

Deno.test("frontline protection changes the opening clash lane", () => {
  const teamA: EmojiId[] = ["fire"];
  const teamB: EmojiId[] = ["shield", "heart"];

  const protectedHeart = resolveWithFormations(
    teamA,
    teamB,
    { placements: [{ slot: "front_center", emojiId: "fire" }] },
    {
      placements: [
        { slot: "front_center", emojiId: "shield" },
        { slot: "back_right", emojiId: "heart" },
      ],
    },
    "frontline-protection",
  );

  const exposedHeart = resolveWithFormations(
    teamA,
    teamB,
    { placements: [{ slot: "front_center", emojiId: "fire" }] },
    {
      placements: [
        { slot: "front_center", emojiId: "heart" },
        { slot: "back_right", emojiId: "shield" },
      ],
    },
    "frontline-exposure",
  );

  assert(
    protectedHeart.battleState.eventLog.some((event) => event.caption === "Shield blocked Fire's impact."),
    "Protected formation should route the opener into Shield.",
  );
  assert(
    exposedHeart.battleState.eventLog.some((event) => event.caption === "Fire found the better lane against Heart."),
    "Exposed formation should leave Heart taking the opening clash.",
  );
});

Deno.test("same-side flank targeting no longer defaults to front left", () => {
  const rightLane = resolveWithFormations(
    ["fire"],
    ["heart", "soap"],
    { placements: [{ slot: "front_right", emojiId: "fire" }] },
    {
      placements: [
        { slot: "front_left", emojiId: "heart" },
        { slot: "front_right", emojiId: "soap" },
      ],
    },
    "same-side-right-lane",
  );

  const firstAttack = rightLane.battleState.eventLog.find((event) => event.type === "attack");
  assert(firstAttack?.target === "player_b:front_right", `Expected the right-lane target to be hit first, got ${serialize(firstAttack)}`);
});

Deno.test("hole can delete ghost before the dive resolves", () => {
  const result = resolve(["hole"], ["ghost"], "hole-ghost");
  assert(
    result.battleState.eventLog.some((event) => event.caption.includes("Hole caught Ghost's dive path")),
    "Expected Hole to counter Ghost with a delete event.",
  );
  assert(result.winner === "player_a", `Hole should beat Ghost in the direct lane, got ${result.winner}`);
});

Deno.test("soap can disrupt ghost instead of folding to the dive", () => {
  const result = resolve(["soap"], ["ghost"], "soap-ghost");
  assert(
    result.battleState.eventLog.some((event) => event.caption.includes("Soap scrubbed Ghost's dive timing away")),
    "Expected Soap to produce the anti-Ghost counter event.",
  );
});

Deno.test("backline units advance when the frontline collapses", () => {
  const result = resolveWithFormations(
    ["shield"],
    ["heart"],
    { placements: [{ slot: "front_center", emojiId: "shield" }] },
    { placements: [{ slot: "back_right", emojiId: "heart" }] },
    "frontline-collapse",
  );

  assert(
    result.battleState.eventLog.some((event) => event.reasonCode === REASON_CODES.FRONTLINE_COLLAPSE_ADVANCED),
    "Expected a frontline collapse advancement event when a backline-only team has open front slots.",
  );
});

Deno.test("different formations can change the overall battle winner", () => {
  const teamA: EmojiId[] = ["shield", "heart", "plant", "soap", "fire"];
  const teamB: EmojiId[] = ["ghost", "snake", "bomb", "lightning", "chain"];

  const protectedResult = resolveWithFormations(
    teamA,
    teamB,
    {
      placements: [
        { slot: "front_left", emojiId: "shield" },
        { slot: "front_center", emojiId: "fire" },
        { slot: "front_right", emojiId: "plant" },
        { slot: "back_left", emojiId: "soap" },
        { slot: "back_right", emojiId: "heart" },
      ],
    },
    buildDefaultFormation(teamB),
    "formation-matters-protected",
  );

  const exposedResult = resolveWithFormations(
    teamA,
    teamB,
    {
      placements: [
        { slot: "front_left", emojiId: "heart" },
        { slot: "front_center", emojiId: "fire" },
        { slot: "front_right", emojiId: "plant" },
        { slot: "back_left", emojiId: "shield" },
        { slot: "back_right", emojiId: "soap" },
      ],
    },
    buildDefaultFormation(teamB),
    "formation-matters-exposed",
  );

  assert(
    serialize(protectedResult.battleState.eventLog) !== serialize(exposedResult.battleState.eventLog),
    "Different formations should produce different battle logs for the same teams.",
  );
});

Deno.test("battle result always resolves to a valid final winner state", () => {
  const result = resolve(["bomb"], ["bomb"], "bomb-draw");
  assert(["player_a", "player_b", "draw"].includes(result.winner), "Winner must be a valid final state");
  assert(result.battleState.winner === result.winner, "Battle state winner must match top-level winner");
});

Deno.test("why summary prefers meaningful interaction highlights over generic clashes", () => {
  const result = resolve(["magnet"], ["mirror"], "highlight-priority");
  assert(
    result.whySummary.includes("Mirror turned Magnet's pull back"),
    `Expected a reflected combo summary, got: ${result.whySummary}`,
  );
  assert(!result.whySummary.includes("standard engagement"), "Summary should not fall back to a generic clash when a meaningful event exists.");
});

Deno.test("generic-only battles fall back to final-state summaries instead of standard engagement text", () => {
  const result = resolve(["ice"], ["mirror"], "generic-fallback");
  assert(
    !result.whySummary.includes("standard engagement"),
    `Generic-only battles should use a final-state fallback summary, got: ${result.whySummary}`,
  );
  assert(result.whyChain.length > 0, "Fallback WHY chain should still explain the battle outcome.");
});

Deno.test("why chain removes duplicate captions from repetitive battle logs", () => {
  const result = resolve(
    ["ice", "bomb", "hole", "snake", "plant"],
    ["fire", "lightning", "magnet", "mirror", "shield"],
    "dedupe-chain",
  );
  const uniqueCaptions = new Set(result.whyChain);
  assert(uniqueCaptions.size === result.whyChain.length, `WHY chain should not repeat captions: ${serialize(result.whyChain)}`);
});
