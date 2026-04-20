import { buildDefaultFormation, resolveBattle } from "./battle-simulator.ts";
import { buildBotMatchPlan, chooseBotBan } from "./bot-engine.ts";
import { EMOJI_DEFINITIONS, EMOJI_IDS } from "./emoji-definitions.ts";
import { getInteractionEntry } from "./interaction-matrix.ts";
import { REASON_CODES } from "./reason-codes.ts";
import type { EmojiId } from "./contracts.ts";

function assert(condition: unknown, message: string): asserts condition {
  if (!condition) {
    throw new Error(message);
  }
}

function serialize(value: unknown): string {
  return JSON.stringify(value, null, 2);
}

function resolve(teamA: EmojiId[], teamB: EmojiId[], seed = "verify-seed") {
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
  seed = "verify-seed",
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

type Check = {
  name: string;
  run: () => void;
};

const checks: Check[] = [
  {
    name: "practice bot returns a valid readable plan",
    run: () => {
      const plan = buildBotMatchPlan("bot_practice", ["bomb", "magnet", "snake", "plant", "ghost"]);
      assert(plan.deck.length === 6, `Practice bot deck should contain 6 emojis: ${serialize(plan.deck)}`);
      assert(new Set(plan.deck).size === 6, "Practice bot deck should use unique emojis.");
      assert(plan.team.length === 5, `Practice bot team should contain 5 emojis: ${serialize(plan.team)}`);
      assert(plan.formation.placements.length === 5, "Practice bot formation should place all 5 units.");
      assert(
        plan.team.some((emojiId) => ["shield", "water", "ice", "soap", "heart", "chain"].includes(emojiId)),
        `Practice bot should bring at least one clear counter/support answer: ${serialize(plan.team)}`,
      );
      assert(plan.note.includes("Selected team:"), "Practice bot note should explain the selected team.");
    },
  },
  {
    name: "smart bot drafts real answers into explosive combo pressure",
    run: () => {
      const plan = buildBotMatchPlan("bot_smart", ["magnet", "bomb", "ghost", "plant", "heart"]);
      assert(plan.deck.length === 6, "Smart bot deck should contain 6 emojis.");
      assert(plan.team.length === 5, "Smart bot team should contain 5 emojis.");
      assert(plan.formation.placements.length === 5, "Smart bot formation should place all 5 units.");
      assert(
        plan.deck.some((emojiId) => ["hole", "ice", "lightning", "chain", "mirror", "shield"].includes(emojiId)),
        `Smart bot should draft answers into combo pressure: ${serialize(plan.deck)}`,
      );
      assert(
        plan.note.includes("Expected swing:"),
        `Smart bot note should surface the simulated battle swing: ${plan.note}`,
      );
    },
  },
  {
    name: "smart bot ban targets a credible player centerpiece",
    run: () => {
      const ban = chooseBotBan("bot_smart", ["magnet", "bomb", "shield", "heart", "plant", "soap"]);
      assert(
        ["magnet", "bomb", "shield"].includes(ban),
        `Smart bot should ban a meaningful centerpiece, got ${ban}`,
      );
    },
  },
  {
    name: "interaction matrix covers all 256 ordered emoji pairs",
    run: () => {
      let count = 0;
      const validReasonCodes = new Set<string>(Object.values(REASON_CODES).map((value) => String(value)));

      for (const attacker of EMOJI_IDS) {
        for (const defender of EMOJI_IDS) {
          const entry = getInteractionEntry(attacker, defender);
          assert(entry != null, `Missing matrix entry for ${attacker} -> ${defender}`);
          assert(entry.attacker === attacker, `Matrix attacker mismatch for ${attacker} -> ${defender}`);
          assert(entry.defender === defender, `Matrix defender mismatch for ${attacker} -> ${defender}`);
          assert(validReasonCodes.has(entry.reasonCode), `Invalid reason code for ${attacker} -> ${defender}`);
          assert(entry.whyText.trim().length > 0, `Missing whyText for ${attacker} -> ${defender}`);
          assert(entry.whyChain.length > 0, `Missing whyChain for ${attacker} -> ${defender}`);
          count += 1;
        }
      }

      assert(count === 256, `Expected 256 ordered pairs, got ${count}`);
    },
  },
  {
    name: "every launch emoji has at least two counters and two failure states",
    run: () => {
      for (const emojiId of EMOJI_IDS) {
        const definition = EMOJI_DEFINITIONS[emojiId];
        assert(definition != null, `Missing definition for ${emojiId}`);
        assert(definition.counters.length >= 2, `${emojiId} needs at least 2 counters`);
        assert(definition.failsAgainst.length >= 2, `${emojiId} needs at least 2 failure states`);
        assert(definition.stats.hp > 0, `${emojiId} must have hp`);
        assert(definition.stats.attack > 0, `${emojiId} must have attack`);
        assert(definition.stats.speed > 0, `${emojiId} must have speed`);
      }
    },
  },
  {
    name: "new interaction verbs are represented in the matrix",
    run: () => {
      const windVsHeart = getInteractionEntry("wind", "heart");
      assert(windVsHeart.effectTags.includes("push"), "Wind vs Heart should include push");

      const heartVsShield = getInteractionEntry("heart", "shield");
      assert(heartVsShield.effectTags.includes("heal"), "Heart vs Shield should include heal");

      const ghostVsHeart = getInteractionEntry("ghost", "heart");
      assert(ghostVsHeart.effectTags.includes("phase"), "Ghost vs Heart should include phase");

      const chainVsWind = getInteractionEntry("chain", "wind");
      assert(chainVsWind.effectTags.includes("bind"), "Chain vs Wind should include bind");
    },
  },
  {
    name: "battle simulator is deterministic for the same seed and formations",
    run: () => {
      const first = resolve(
        ["shield", "heart", "wind", "soap", "plant"],
        ["lightning", "ghost", "chain", "bomb", "fire"],
        "fixed-seed",
      );
      const second = resolve(
        ["shield", "heart", "wind", "soap", "plant"],
        ["lightning", "ghost", "chain", "bomb", "fire"],
        "fixed-seed",
      );

      assert(serialize(first) === serialize(second), "Battle resolution should be deterministic for the same input");
    },
  },
  {
    name: "wind can generate a push event against a support target",
    run: () => {
      const result = resolve(["wind"], ["heart"], "wind-heart");
      assert(
        result.battleState.eventLog.some((event) => event.reasonCode === REASON_CODES.WIND_SCATTERED_FORMATION),
        "Expected a wind push/disruption event",
      );
    },
  },
  {
    name: "heart can generate a heal event during the battle",
    run: () => {
      const result = resolve(["heart", "plant"], ["lightning"], "heart-heal");
      assert(
        result.battleState.eventLog.some((event) =>
          event.type === "heal" && event.reasonCode === REASON_CODES.HEART_STABILIZED_ALLY
        ),
        "Expected Heart to heal an ally",
      );
    },
  },
  {
    name: "ghost can reach a protected backline target",
    run: () => {
      const result = resolve(["ghost"], ["shield", "heart"], "ghost-dive");
      assert(
        result.battleState.eventLog.some((event) => event.reasonCode === REASON_CODES.GHOST_PHASED_PAST_FRONT),
        "Expected Ghost to produce a backline dive event",
      );
    },
  },
  {
    name: "chain can bind a movement-based target",
    run: () => {
      const result = resolve(["chain"], ["wind"], "chain-bind");
      assert(
        result.battleState.eventLog.some((event) => event.reasonCode === REASON_CODES.CHAIN_BOUND_TARGET),
        "Expected Chain to bind Wind",
      );
    },
  },
  {
    name: "frontline protection changes the opening clash lane",
    run: () => {
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
    },
  },
  {
    name: "frontline collapse advances exposed backliners forward",
    run: () => {
      const result = resolveWithFormations(
        ["shield"],
        ["heart"],
        { placements: [{ slot: "front_center", emojiId: "shield" }] },
        { placements: [{ slot: "back_right", emojiId: "heart" }] },
        "frontline-collapse",
      );

      assert(
        result.battleState.eventLog.some((event) => event.reasonCode === REASON_CODES.FRONTLINE_COLLAPSE_ADVANCED),
        "Expected a frontline collapse advancement event",
      );
    },
  },
  {
    name: "different formations can change the overall battle log",
    run: () => {
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
    },
  },
  {
    name: "battle result always resolves to a valid final winner state",
    run: () => {
      const result = resolve(["bomb"], ["bomb"], "bomb-draw");
      assert(["player_a", "player_b", "draw"].includes(result.winner), "Winner must be a valid final state");
      assert(result.battleState.winner === result.winner, "Battle state winner must match top-level winner");
    },
  },
  {
    name: "why summary prefers meaningful interaction highlights over generic clashes",
    run: () => {
      const result = resolve(["magnet"], ["mirror"], "highlight-priority");
      assert(
        result.whySummary.includes("Mirror turned Magnet's pull back"),
        `Expected a reflected combo summary, got: ${result.whySummary}`,
      );
      assert(
        !result.whySummary.includes("standard engagement"),
        "Summary should not fall back to a generic clash when a meaningful event exists.",
      );
    },
  },
  {
    name: "generic-only battles fall back to final-state summaries instead of standard engagement text",
    run: () => {
      const result = resolve(["ice"], ["mirror"], "generic-fallback");
      assert(
        !result.whySummary.includes("standard engagement"),
        `Generic-only battles should use a final-state fallback summary, got: ${result.whySummary}`,
      );
      assert(result.whyChain.length > 0, "Fallback WHY chain should still explain the battle outcome.");
    },
  },
  {
    name: "why chain removes duplicate captions from repetitive battle logs",
    run: () => {
      const result = resolve(
        ["ice", "bomb", "hole", "snake", "plant"],
        ["fire", "lightning", "magnet", "mirror", "shield"],
        "dedupe-chain",
      );
      const uniqueCaptions = new Set(result.whyChain);
      assert(
        uniqueCaptions.size === result.whyChain.length,
        `WHY chain should not repeat captions: ${serialize(result.whyChain)}`,
      );
    },
  },
];

let passed = 0;

for (const check of checks) {
  try {
    check.run();
    passed += 1;
    console.log(`PASS ${check.name}`);
  } catch (error) {
    console.error(`FAIL ${check.name}`);
    if (error instanceof Error) {
      console.error(error.message);
      if (error.stack) {
        console.error(error.stack);
      }
    } else {
      console.error(String(error));
    }
    Deno.exit(1);
  }
}

console.log(`Verified ${passed}/${checks.length} resolver checks.`);
