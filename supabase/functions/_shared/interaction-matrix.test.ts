import { EMOJI_DEFINITIONS, EMOJI_IDS } from "./emoji-definitions.ts";
import { getInteractionEntry } from "./interaction-matrix.ts";
import { REASON_CODES } from "./reason-codes.ts";

function assert(condition: unknown, message: string): asserts condition {
  if (!condition) {
    throw new Error(message);
  }
}

Deno.test("interaction matrix covers all 256 ordered emoji pairs", () => {
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
});

Deno.test("every launch emoji has at least two counters and two failure states", () => {
  for (const emojiId of EMOJI_IDS) {
    const definition = EMOJI_DEFINITIONS[emojiId];
    assert(definition != null, `Missing definition for ${emojiId}`);
    assert(definition.counters.length >= 2, `${emojiId} needs at least 2 counters`);
    assert(definition.failsAgainst.length >= 2, `${emojiId} needs at least 2 failure states`);
    assert(definition.stats.hp > 0, `${emojiId} must have hp`);
    assert(definition.stats.attack > 0, `${emojiId} must have attack`);
    assert(definition.stats.speed > 0, `${emojiId} must have speed`);
  }
});

Deno.test("new interaction verbs are represented in the matrix", () => {
  const windVsHeart = getInteractionEntry("wind", "heart");
  assert(windVsHeart.effectTags.includes("push"), "Wind vs Heart should include push");

  const heartVsShield = getInteractionEntry("heart", "shield");
  assert(heartVsShield.effectTags.includes("heal"), "Heart vs Shield should include heal");

  const ghostVsHeart = getInteractionEntry("ghost", "heart");
  assert(ghostVsHeart.effectTags.includes("phase"), "Ghost vs Heart should include phase");

  const chainVsWind = getInteractionEntry("chain", "wind");
  assert(chainVsWind.effectTags.includes("bind"), "Chain vs Wind should include bind");
});
