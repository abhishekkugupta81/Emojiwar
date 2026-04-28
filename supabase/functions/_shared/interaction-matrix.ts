import type { EffectTag, EmojiId, InteractionEntry, InteractionOutcomeType } from "./contracts.ts";
import { EMOJI_DEFINITIONS, EMOJI_IDS } from "./emoji-definitions.ts";
import { REASON_CODES } from "./reason-codes.ts";

type Matrix = Record<EmojiId, Record<EmojiId, InteractionEntry>>;

const matrix = {} as Matrix;

function createEntry(
  attacker: EmojiId,
  defender: EmojiId,
  outcomeType: InteractionOutcomeType,
  reasonCode: string,
  whyText: string,
  whyChain: string[],
  effectTags: EffectTag[],
): InteractionEntry {
  return {
    attacker,
    defender,
    outcomeType,
    reasonCode,
    whyText,
    whyChain,
    effectTags,
  };
}

function display(emojiId: EmojiId): string {
  return EMOJI_DEFINITIONS[emojiId].displayName;
}

function buildGenericWhyText(attacker: EmojiId, defender: EmojiId, outcomeType: InteractionOutcomeType): string {
  switch (outcomeType) {
    case "attacker_advantage":
      return `${display(attacker)} found the better lane against ${display(defender)}.`;
    case "defender_advantage":
      return `${display(defender)} turned ${display(attacker)} back in the lane.`;
    default:
      return `${display(attacker)} and ${display(defender)} traded without a clear edge.`;
  }
}

function buildGenericWhyChain(attacker: EmojiId, defender: EmojiId, outcomeType: InteractionOutcomeType): string[] {
  const attackerDef = EMOJI_DEFINITIONS[attacker];
  const defenderDef = EMOJI_DEFINITIONS[defender];

  switch (outcomeType) {
    case "attacker_advantage":
      return [
        `${display(attacker)} pressed with ${attackerDef.primaryVerb}.`,
        `${display(defender)} could not fully answer with ${defenderDef.primaryVerb}.`,
      ];
    case "defender_advantage":
      return [
        `${display(attacker)} opened with ${attackerDef.primaryVerb}.`,
        `${display(defender)} held the lane with ${defenderDef.primaryVerb}.`,
      ];
    default:
      return [
        `${display(attacker)} entered with ${attackerDef.primaryVerb}.`,
        `${display(defender)} matched it with ${defenderDef.primaryVerb}.`,
      ];
  }
}

function setEntry(
  attacker: EmojiId,
  defender: EmojiId,
  outcomeType: InteractionOutcomeType,
  reasonCode: string,
  whyText: string,
  whyChain: string[],
  effectTags: EffectTag[],
) {
  matrix[attacker][defender] = createEntry(attacker, defender, outcomeType, reasonCode, whyText, whyChain, effectTags);
}

function setCounter(
  attacker: EmojiId,
  defender: EmojiId,
  reasonCode: string,
  whyText: string,
  whyChain: string[],
  effectTags: EffectTag[],
) {
  setEntry(attacker, defender, "attacker_advantage", reasonCode, whyText, whyChain, effectTags);
  setEntry(defender, attacker, "defender_advantage", reasonCode, whyText, [...whyChain].reverse(), effectTags);
}

for (const attacker of EMOJI_IDS) {
  matrix[attacker] = {} as Record<EmojiId, InteractionEntry>;
  for (const defender of EMOJI_IDS) {
    if (attacker === defender) {
      setEntry(
        attacker,
        defender,
        "neutral",
        REASON_CODES.MIRROR_MATCH_DRAW,
        `${display(attacker)} mirrored itself and neither side broke formation.`,
        [`${display(attacker)} met its own answer`, "Neither side found a clear edge"],
        ["damage"],
      );
      continue;
    }

    const attackerDef = EMOJI_DEFINITIONS[attacker];
    const attackerFavored = attackerDef.counters.includes(defender);
    const defenderFavored = attackerDef.failsAgainst.includes(defender);
    const outcomeType: InteractionOutcomeType = attackerFavored
      ? "attacker_advantage"
      : defenderFavored
        ? "defender_advantage"
        : "neutral";

    setEntry(
      attacker,
      defender,
      outcomeType,
      REASON_CODES.DIRECT_CLASH,
      buildGenericWhyText(attacker, defender, outcomeType),
      buildGenericWhyChain(attacker, defender, outcomeType),
      ["damage"],
    );
  }
}

setCounter(
  "water",
  "fire",
  REASON_CODES.WATER_EXTINGUISHED_FIRE,
  "Water extinguished Fire before the burn could spread.",
  ["Water reached the clash first", "Fire lost its pressure line"],
  ["damage", "cleanse"],
);

setCounter(
  "water",
  "lightning",
  REASON_CODES.WATER_SHORTED_LIGHTNING,
  "Water grounded Lightning and blunted the tempo swing.",
  ["Lightning charged a stun line", "Water shorted the strike"],
  ["damage", "disrupt"],
);

setCounter(
  "water",
  "hole",
  REASON_CODES.WATER_FILLED_HOLE,
  "Water filled Hole and removed the void threat.",
  ["Hole opened a delete line", "Water collapsed the opening"],
  ["damage", "disrupt"],
);

setCounter(
  "fire",
  "ice",
  REASON_CODES.FIRE_MELTED_ICE,
  "Fire melted Ice and reopened the frontline.",
  ["Ice tried to freeze the lane", "Fire broke through the freeze"],
  ["damage", "burn"],
);

setCounter(
  "fire",
  "plant",
  REASON_CODES.FIRE_SCORCHED_PLANT,
  "Fire scorched Plant before it could scale.",
  ["Plant needed time to grow", "Fire removed the ramp piece early"],
  ["damage", "burn"],
);

setCounter(
  "lightning",
  "magnet",
  REASON_CODES.LIGHTNING_STUNNED_MAGNET,
  "Lightning stunned Magnet before the pull resolved.",
  ["Magnet lined up a combo pull", "Lightning interrupted it first"],
  ["damage", "stun"],
);

setCounter(
  "lightning",
  "ghost",
  REASON_CODES.LIGHTNING_SNAPPED_GHOST,
  "Lightning snapped Ghost out of its dive line.",
  ["Ghost tried to phase past the frontline", "Lightning pinned it in place"],
  ["damage", "stun"],
);

setCounter(
  "ice",
  "bomb",
  REASON_CODES.ICE_DEFUSED_BOMB,
  "Ice defused Bomb before it could blow up the team fight.",
  ["Bomb armed for a team blast", "Ice froze the fuse in time"],
  ["damage", "freeze"],
);

setCounter(
  "ice",
  "snake",
  REASON_CODES.ICE_DELAYED_POISON,
  "Ice slowed Snake's poison line long enough to stabilize.",
  ["Snake looked for a long fight", "Ice delayed the pressure"],
  ["damage", "freeze"],
);

setCounter(
  "magnet",
  "bomb",
  REASON_CODES.MAGNET_PULLED_BOMB,
  "Magnet dragged Bomb into the wrong lane and swung the blast.",
  ["Bomb waited behind the line", "Magnet repositioned the hazard"],
  ["pull", "splash", "heavy_damage"],
);

setCounter(
  "magnet",
  "shield",
  REASON_CODES.MAGNET_STOLE_SHIELD,
  "Magnet stripped Shield before it could protect the lane.",
  ["Shield set up the frontline block", "Magnet removed the protection"],
  ["pull", "shield_break"],
);

setCounter(
  "mirror",
  "lightning",
  REASON_CODES.MIRROR_REFLECTED_TARGETED_EFFECT,
  "Mirror reflected Lightning back through the line.",
  ["Lightning committed to a targeted stun", "Mirror sent it back"],
  ["reflect", "damage"],
);

setCounter(
  "mirror",
  "magnet",
  REASON_CODES.MIRROR_REFLECTED_TARGETED_EFFECT,
  "Mirror turned Magnet's pull back on the combo player.",
  ["Magnet tried to set the combo", "Mirror reversed the target"],
  ["reflect", "pull"],
);

setCounter(
  "hole",
  "bomb",
  REASON_CODES.HOLE_DELETED_HAZARD,
  "Hole deleted Bomb before the splash hit the formation.",
  ["Bomb armed in the backline", "Hole removed the hazard entirely"],
  ["delete"],
);

setCounter(
  "hole",
  "lightning",
  REASON_CODES.HOLE_DELETED_HAZARD,
  "Hole swallowed Lightning before it could stun the line.",
  ["Lightning lined up tempo pressure", "Hole removed the effect path"],
  ["delete"],
);

setCounter(
  "hole",
  "ghost",
  REASON_CODES.HOLE_DELETED_HAZARD,
  "Hole caught Ghost's dive path and deleted it before the phase landed.",
  ["Ghost committed to the backline lane", "Hole removed the dive before it connected"],
  ["delete"],
);

setCounter(
  "shield",
  "fire",
  REASON_CODES.SHIELD_BLOCKED_IMPACT,
  "Shield absorbed Fire's first meaningful hit.",
  ["Fire tried to force the lane", "Shield blocked the impact"],
  ["shield"],
);

setCounter(
  "shield",
  "lightning",
  REASON_CODES.SHIELD_BLOCKED_IMPACT,
  "Shield caught Lightning's opener and held the line.",
  ["Lightning looked for an opening stun", "Shield denied the first impact"],
  ["shield"],
);

setCounter(
  "shield",
  "snake",
  REASON_CODES.SHIELD_BLOCKED_IMPACT,
  "Shield blocked Snake's opening pressure and bought time.",
  ["Snake wanted a clean poison line", "Shield forced it to waste the opener"],
  ["shield"],
);

setCounter(
  "soap",
  "ghost",
  REASON_CODES.SOAP_CLEANSED_STATUS,
  "Soap scrubbed Ghost's dive timing away and left it exposed in the lane.",
  ["Ghost looked for a clean phase angle", "Soap disrupted the timing before the dive landed"],
  ["damage", "disrupt"],
);

setCounter(
  "soap",
  "fire",
  REASON_CODES.SOAP_CLEANSED_STATUS,
  "Soap washed away Fire's burn pressure.",
  ["Fire tried to start attrition", "Soap reset the line"],
  ["cleanse"],
);

setCounter(
  "soap",
  "snake",
  REASON_CODES.SOAP_CLEANSED_STATUS,
  "Soap cleansed Snake's poison before it could decide the fight.",
  ["Snake committed to poison pressure", "Soap erased the status swing"],
  ["cleanse"],
);

setCounter(
  "soap",
  "chain",
  REASON_CODES.SOAP_CLEANSED_STATUS,
  "Soap broke Chain's bind and restored movement.",
  ["Chain locked the lane", "Soap restored the formation"],
  ["cleanse"],
);

setCounter(
  "snake",
  "heart",
  REASON_CODES.SNAKE_APPLIED_POISON,
  "Snake poisoned through Heart's sustain plan.",
  ["Heart tried to prolong the fight", "Snake made healing inefficient"],
  ["poison", "damage"],
);

setCounter(
  "snake",
  "plant",
  REASON_CODES.SNAKE_APPLIED_POISON,
  "Snake poisoned Plant before it could finish scaling.",
  ["Plant wanted a protected growth line", "Snake forced it to decay early"],
  ["poison", "damage"],
);

setCounter(
  "plant",
  "shield",
  REASON_CODES.PLANT_GREW_UNCHECKED,
  "Plant grew unchecked behind a passive shield line.",
  ["Shield held but did not threaten back", "Plant converted time into power"],
  ["grow", "damage"],
);

setCounter(
  "plant",
  "mirror",
  REASON_CODES.PLANT_GREW_UNCHECKED,
  "Plant outlasted Mirror and turned time into damage.",
  ["Mirror waited for a target", "Plant kept growing anyway"],
  ["grow", "damage"],
);

setCounter(
  "plant",
  "soap",
  REASON_CODES.PLANT_GREW_UNCHECKED,
  "Plant ignored Soap's low pressure and kept growing.",
  ["Soap reset statuses", "Plant still won the tempo race"],
  ["grow", "damage"],
);

setCounter(
  "wind",
  "plant",
  REASON_CODES.WIND_SCATTERED_FORMATION,
  "Wind scattered Plant before the protected growth could matter.",
  ["Plant relied on a stable lane", "Wind displaced the setup"],
  ["push", "damage", "disrupt"],
);

setCounter(
  "wind",
  "heart",
  REASON_CODES.WIND_SCATTERED_FORMATION,
  "Wind disrupted Heart's support line and exposed the backline.",
  ["Heart wanted a calm sustain cycle", "Wind broke the formation"],
  ["push", "damage", "disrupt"],
);

setCounter(
  "wind",
  "bomb",
  REASON_CODES.WIND_SCATTERED_FORMATION,
  "Wind scattered the hazard setup before Bomb could land cleanly.",
  ["Bomb relied on a clustered target", "Wind broke the team shape"],
  ["push", "disrupt"],
);

setCounter(
  "heart",
  "shield",
  REASON_CODES.HEART_STABILIZED_ALLY,
  "Heart outlasted Shield by keeping allies topped up through the cycle.",
  ["Shield bought time", "Heart converted that time into sustain"],
  ["heal"],
);

setCounter(
  "heart",
  "plant",
  REASON_CODES.HEART_STABILIZED_ALLY,
  "Heart kept the team healthy long enough to deny Plant's payoff.",
  ["Plant needed incremental advantage", "Heart erased the attrition edge"],
  ["heal"],
);

setCounter(
  "ghost",
  "heart",
  REASON_CODES.GHOST_PHASED_PAST_FRONT,
  "Ghost phased past the frontline and deleted Heart's support lane.",
  ["Heart hid behind the frontline", "Ghost reached the backline anyway"],
  ["phase", "damage", "disrupt"],
);

setCounter(
  "ghost",
  "bomb",
  REASON_CODES.GHOST_PHASED_PAST_FRONT,
  "Ghost reached Bomb before the hazard could safely detonate.",
  ["Bomb waited for protection", "Ghost ignored the frontline and got there"],
  ["phase", "damage"],
);

setCounter(
  "chain",
  "magnet",
  REASON_CODES.CHAIN_BOUND_TARGET,
  "Chain bound Magnet and stopped the pull combo.",
  ["Magnet relied on repositioning", "Chain locked the movement verb"],
  ["bind", "damage"],
);

setCounter(
  "chain",
  "wind",
  REASON_CODES.CHAIN_BOUND_TARGET,
  "Chain anchored Wind and denied the scatter effect.",
  ["Wind tried to reshape the formation", "Chain locked the lane in place"],
  ["bind", "damage"],
);

setCounter(
  "chain",
  "ghost",
  REASON_CODES.CHAIN_BOUND_TARGET,
  "Chain caught Ghost before it could phase into the backline.",
  ["Ghost looked for a dive window", "Chain bound it in the frontline lane"],
  ["bind", "damage"],
);

setCounter(
  "chain",
  "bomb",
  REASON_CODES.CHAIN_BOUND_TARGET,
  "Chain pinned Bomb in place and denied the explosive setup.",
  ["Bomb depended on space and timing", "Chain removed both"],
  ["bind", "disrupt"],
);

setCounter(
  "bomb",
  "heart",
  REASON_CODES.BOMB_OVERWHELMED_TARGET,
  "Bomb overwhelmed Heart's sustain line with a single blast.",
  ["Heart tried to outheal attrition", "Bomb ended the fight too quickly"],
  ["splash", "heavy_damage"],
);

setCounter(
  "bomb",
  "soap",
  REASON_CODES.BOMB_OVERWHELMED_TARGET,
  "Bomb ignored Soap's utility and blasted through the backline.",
  ["Soap prepared utility value", "Bomb created immediate lethal pressure"],
  ["splash", "heavy_damage"],
);

setCounter(
  "bomb",
  "ghost",
  REASON_CODES.BOMB_OVERWHELMED_TARGET,
  "Bomb caught Ghost in the blast before it could finish the dive.",
  ["Ghost threatened the backline", "Bomb punished the commitment with splash"],
  ["splash", "heavy_damage"],
);

export const INTERACTION_MATRIX: Matrix = Object.freeze(matrix);

export function getInteractionEntry(attacker: EmojiId, defender: EmojiId): InteractionEntry {
  return INTERACTION_MATRIX[attacker][defender];
}
