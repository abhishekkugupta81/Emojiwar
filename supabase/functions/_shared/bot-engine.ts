import type { BotProfile, EmojiId, Formation, FormationPlacement, FormationSlot, MatchMode } from "./contracts.ts";
import { FORMATION_SLOT_ORDER } from "./contracts.ts";
import { buildDefaultFormation, resolveBattle, selectBattleTeamFromDeck } from "./battle-simulator.ts";
import { BOT_PROFILES, EMOJI_DEFINITIONS, PRACTICE_BOT_DECK, SMART_BOT_DECK } from "./emoji-definitions.ts";

const FRONT_SLOTS: FormationSlot[] = ["front_left", "front_center", "front_right"];
const BACK_SLOTS: FormationSlot[] = ["back_left", "back_right"];

const PRACTICE_ARCHETYPES: Array<{
  label: string;
  deck: EmojiId[];
  note: string;
  specialty: "status" | "combo" | "tempo" | "stability";
}> = [
  {
    label: "Sustain Shell",
    deck: ["shield", "water", "ice", "soap", "heart", "plant"],
    note: "Practice Bot is running a protected sustain shell and will keep its support pieces behind a stable frontline.",
    specialty: "status",
  },
  {
    label: "Status Anchor",
    deck: ["shield", "ice", "snake", "soap", "heart", "chain"],
    note: "Practice Bot is using a slower status shell with cleanse cover and a clear frontline anchor.",
    specialty: "status",
  },
  {
    label: "Tempo Guard",
    deck: ["water", "ice", "wind", "chain", "shield", "soap"],
    note: "Practice Bot is leaning on obvious disruption tools and a straightforward defensive setup.",
    specialty: "tempo",
  },
  {
    label: "Counter Pressure",
    deck: ["shield", "water", "lightning", "ice", "soap", "heart"],
    note: "Practice Bot is showing a reactive counter shell built to answer explosive openings without tricky pivots.",
    specialty: "combo",
  },
];

const FRAGILE_BACKLINERS = new Set<EmojiId>(["heart", "soap", "bomb", "plant", "mirror", "hole"]);
const SUPPORT_EMOJIS = new Set<EmojiId>(["soap", "heart", "shield"]);
const PROTECTIVE_FRONTLINERS = new Set<EmojiId>(["shield", "chain", "ice", "water", "fire"]);
const MOVEMENT_UNITS = new Set<EmojiId>(["magnet", "wind", "ghost"]);
const BACKLINE_THREATS = new Set<EmojiId>(["ghost", "bomb", "snake", "fire", "wind"]);

const TEAM_SYNERGY_BONUSES: Array<{ left: EmojiId; right: EmojiId; bonus: number }> = [
  { left: "shield", right: "plant", bonus: 14 },
  { left: "shield", right: "heart", bonus: 12 },
  { left: "shield", right: "soap", bonus: 12 },
  { left: "ice", right: "shield", bonus: 10 },
  { left: "soap", right: "heart", bonus: 8 },
  { left: "water", right: "ice", bonus: 8 },
  { left: "snake", right: "heart", bonus: 6 },
  { left: "magnet", right: "bomb", bonus: 18 },
  { left: "chain", right: "wind", bonus: 12 },
  { left: "chain", right: "ghost", bonus: 10 },
  { left: "ghost", right: "lightning", bonus: 8 },
  { left: "wind", right: "bomb", bonus: 10 },
];

export interface BotMatchPlan {
  profile: BotProfile;
  deck: EmojiId[];
  team: EmojiId[];
  benchEmojiId: EmojiId | null;
  formation: Formation;
  strategyLabel: string;
  note: string;
}

interface SimulatedFormationChoice {
  formation: Formation;
  score: number;
  whySummary: string;
}

interface OpponentProfile {
  hasBombCombo: boolean;
  hasStatusPressure: boolean;
  hasFragileBackline: boolean;
  hasSustain: boolean;
  hasRamp: boolean;
  hasDive: boolean;
  hasMovementPressure: boolean;
}

function hasTag(emojiId: EmojiId, tag: string): boolean {
  return EMOJI_DEFINITIONS[emojiId].stats.tags.includes(tag);
}

function isSupportEmoji(emojiId: EmojiId): boolean {
  return SUPPORT_EMOJIS.has(emojiId) || hasTag(emojiId, "support") || hasTag(emojiId, "heal") || hasTag(emojiId, "cleanse");
}

function isFrontliner(emojiId: EmojiId): boolean {
  const definition = EMOJI_DEFINITIONS[emojiId];
  return definition.stats.preferredRow === "front" ||
    definition.stats.preferredRow === "flex" ||
    definition.stats.tags.includes("frontline") ||
    definition.stats.tags.includes("tank");
}

function isFragileBackliner(emojiId: EmojiId): boolean {
  return FRAGILE_BACKLINERS.has(emojiId) ||
    (EMOJI_DEFINITIONS[emojiId].stats.preferredRow === "back" && EMOJI_DEFINITIONS[emojiId].stats.hp <= 6);
}

function formatEmojiList(emojiIds: EmojiId[]): string {
  return emojiIds.map((emojiId) => EMOJI_DEFINITIONS[emojiId].displayName).join(", ");
}

function describeOpponentTeam(opponents: EmojiId[]): OpponentProfile {
  return {
    hasBombCombo: opponents.includes("bomb") && (opponents.includes("magnet") || opponents.includes("wind")),
    hasStatusPressure: opponents.some((emojiId) => ["snake", "fire", "lightning", "chain", "ice"].includes(emojiId)),
    hasFragileBackline: opponents.some((emojiId) => isFragileBackliner(emojiId)),
    hasSustain: opponents.some((emojiId) => ["heart", "soap", "shield"].includes(emojiId)),
    hasRamp: opponents.includes("plant") || opponents.includes("snake"),
    hasDive: opponents.includes("ghost"),
    hasMovementPressure: opponents.some((emojiId) => MOVEMENT_UNITS.has(emojiId)),
  };
}

function scoreEmojiAgainstOpponent(emojiId: EmojiId, opponents: EmojiId[], profile: OpponentProfile, mode: MatchMode): number {
  const definition = EMOJI_DEFINITIONS[emojiId];
  let score = definition.stats.hp * 1.5 + definition.stats.attack * 2 + definition.stats.speed;

  for (const opponent of opponents) {
    if (definition.counters.includes(opponent)) {
      score += 16;
    }

    if (definition.failsAgainst.includes(opponent)) {
      score -= 12;
    }
  }

  if (profile.hasStatusPressure && ["soap", "heart", "water", "ice"].includes(emojiId)) {
    score += 14;
  }

  if (profile.hasBombCombo && ["hole", "ice", "shield", "chain", "lightning", "mirror"].includes(emojiId)) {
    score += 16;
  }

  if (profile.hasDive && ["shield", "chain", "lightning", "ice"].includes(emojiId)) {
    score += 12;
  }

  if (profile.hasFragileBackline && ["ghost", "bomb", "snake", "wind", "fire"].includes(emojiId)) {
    score += 10;
  }

  if (profile.hasSustain && ["ghost", "bomb", "snake", "fire"].includes(emojiId)) {
    score += 10;
  }

  if (profile.hasRamp && ["fire", "bomb", "ghost", "wind"].includes(emojiId)) {
    score += 8;
  }

  if (mode === "bot_practice") {
    if (isFragileBackliner(emojiId) && !isSupportEmoji(emojiId) && emojiId !== "plant") {
      score -= 6;
    }

    if (emojiId === "magnet" || emojiId === "ghost" || emojiId === "bomb") {
      score -= 4;
    }
  } else {
    if (emojiId === "magnet" || emojiId === "ghost" || emojiId === "bomb" || emojiId === "chain") {
      score += 6;
    }
  }

  return score;
}

function scoreTeamSynergy(team: EmojiId[]): number {
  let score = 0;
  for (const pair of TEAM_SYNERGY_BONUSES) {
    if (team.includes(pair.left) && team.includes(pair.right)) {
      score += pair.bonus;
    }
  }

  return score;
}

function scoreTeamShape(team: EmojiId[], profile: OpponentProfile, mode: MatchMode): number {
  const frontliners = team.filter(isFrontliner).length;
  const supports = team.filter(isSupportEmoji).length;
  const fragileBackliners = team.filter(isFragileBackliner).length;
  const antiStatus = team.filter((emojiId) => ["soap", "heart", "water", "ice"].includes(emojiId)).length;
  const backlinePressure = team.filter((emojiId) => BACKLINE_THREATS.has(emojiId)).length;
  const comboEngines = team.filter((emojiId) => ["magnet", "bomb", "ghost", "wind", "chain", "plant"].includes(emojiId)).length;

  let score = 0;

  score += frontliners >= 2 ? 18 : -24;
  score += supports >= 1 ? 10 : -10;
  score += supports >= 2 ? 4 : 0;

  if (profile.hasStatusPressure) {
    score += antiStatus >= 1 ? 12 : -12;
  }

  if (profile.hasFragileBackline) {
    score += backlinePressure >= 1 ? 8 : 0;
  }

  if (mode === "bot_practice") {
    score -= Math.max(0, fragileBackliners - 2) * 8;
    score -= Math.max(0, comboEngines - 2) * 6;
  } else {
    score += comboEngines >= 2 ? 8 : 0;
    score += frontliners >= 3 ? 4 : 0;
  }

  return score;
}

function scoreTeam(team: EmojiId[], opponents: EmojiId[], mode: MatchMode, profile: OpponentProfile): number {
  let score = 0;

  for (const emojiId of team) {
    score += scoreEmojiAgainstOpponent(emojiId, opponents, profile, mode);
  }

  score += scoreTeamShape(team, profile, mode);
  score += scoreTeamSynergy(team);

  return score;
}

function countAlive(team: Array<{ alive: boolean }>): number {
  return team.filter((unit) => unit.alive).length;
}

function sumLivingHp(team: Array<{ alive: boolean; hp: number }>): number {
  return team.filter((unit) => unit.alive).reduce((sum, unit) => sum + unit.hp, 0);
}

function countMeaningfulEvents(captions: string[]): number {
  return captions.filter((caption) =>
    !caption.includes("standard engagement") &&
    !caption.includes("entered battle")
  ).length;
}

function formationSignature(formation: Formation): string {
  return formation.placements
    .map((placement) => `${placement.slot}:${placement.emojiId}`)
    .join("|");
}

function uniqueFormations(candidates: Formation[]): Formation[] {
  const unique = new Map<string, Formation>();
  for (const formation of candidates) {
    unique.set(formationSignature(formation), formation);
  }

  return [...unique.values()];
}

function scoreSimulatedBattle(team: EmojiId[], formation: Formation, opponents: EmojiId[], battleSeed: string): {
  score: number;
  whySummary: string;
} {
  const result = resolveBattle({
    mode: "bot_smart",
    rulesVersion: "launch-5v5-001",
    battleSeed,
    teamA: team,
    teamB: opponents,
    formationA: formation,
    formationB: buildDefaultFormation(opponents),
  });

  const aliveA = countAlive(result.battleState.teamA);
  const aliveB = countAlive(result.battleState.teamB);
  const hpA = sumLivingHp(result.battleState.teamA);
  const hpB = sumLivingHp(result.battleState.teamB);
  const meaningfulEvents = countMeaningfulEvents(result.whyChain);

  let score = 0;
  if (result.winner === "player_a") {
    score += 180;
  } else if (result.winner === "draw") {
    score += 30;
  } else {
    score -= 140;
  }

  score += (aliveA - aliveB) * 18;
  score += (hpA - hpB) * 3;
  score += Math.min(meaningfulEvents, 5) * 2;

  return {
    score,
    whySummary: result.whySummary,
  };
}

function combinations(source: EmojiId[], count: number): EmojiId[][] {
  const result: EmojiId[][] = [];

  function visit(index: number, current: EmojiId[]) {
    if (current.length === count) {
      result.push([...current]);
      return;
    }

    for (let cursor = index; cursor < source.length; cursor += 1) {
      current.push(source[cursor]);
      visit(cursor + 1, current);
      current.pop();
    }
  }

  if (count <= 0 || source.length < count) {
    return result;
  }

  visit(0, []);
  return result;
}

function permutations(source: EmojiId[]): EmojiId[][] {
  const result: EmojiId[][] = [];

  function visit(remaining: EmojiId[], current: EmojiId[]) {
    if (remaining.length === 0) {
      result.push([...current]);
      return;
    }

    for (let index = 0; index < remaining.length; index += 1) {
      const next = remaining[index];
      current.push(next);
      visit([...remaining.slice(0, index), ...remaining.slice(index + 1)], current);
      current.pop();
    }
  }

  visit(source, []);
  return result;
}

function buildFormationFromOrderedTeam(order: EmojiId[]): Formation {
  const placements: FormationPlacement[] = [];
  for (let index = 0; index < order.length && index < FORMATION_SLOT_ORDER.length; index += 1) {
    placements.push({
      slot: FORMATION_SLOT_ORDER[index],
      emojiId: order[index],
    });
  }

  return { placements };
}

function frontlinePriority(emojiId: EmojiId, profile: OpponentProfile): number {
  const definition = EMOJI_DEFINITIONS[emojiId];
  let score = definition.stats.hp * 10 + definition.stats.attack * 3;

  if (PROTECTIVE_FRONTLINERS.has(emojiId)) {
    score += 25;
  }

  if (definition.stats.preferredRow === "front") {
    score += 20;
  } else if (definition.stats.preferredRow === "flex") {
    score += 10;
  } else {
    score -= 18;
  }

  if (profile.hasDive && ["shield", "chain", "ice", "lightning"].includes(emojiId)) {
    score += 10;
  }

  if (isFragileBackliner(emojiId)) {
    score -= 20;
  }

  return score;
}

function backlinePriority(emojiId: EmojiId): number {
  const definition = EMOJI_DEFINITIONS[emojiId];
  let score = definition.stats.speed * 10 + definition.stats.attack * 4;

  if (definition.stats.preferredRow === "back") {
    score += 20;
  } else if (definition.stats.preferredRow === "flex") {
    score += 10;
  }

  if (isSupportEmoji(emojiId) || isFragileBackliner(emojiId)) {
    score += 18;
  }

  if (PROTECTIVE_FRONTLINERS.has(emojiId)) {
    score -= 14;
  }

  return score;
}

function scorePlacement(emojiId: EmojiId, slot: FormationSlot, opponents: EmojiId[], profile: OpponentProfile, mode: MatchMode): number {
  const definition = EMOJI_DEFINITIONS[emojiId];
  let score = 0;

  if (FRONT_SLOTS.includes(slot)) {
    score += frontlinePriority(emojiId, profile);

    if (slot === "front_center") {
      if (emojiId === "shield") score += 18;
      if (emojiId === "chain") score += 15;
      if (emojiId === "ice") score += 12;
      if (emojiId === "water") score += 9;
      if (emojiId === "fire") score += 6;
    }
  } else {
    score += backlinePriority(emojiId);
    if (profile.hasDive && isFragileBackliner(emojiId)) {
      score -= 8;
    }
  }

  if (mode === "bot_smart") {
    score += scoreEmojiAgainstOpponent(emojiId, opponents, profile, mode) * 0.35;
  }

  if (definition.stats.tags.includes("backline_reach") && BACK_SLOTS.includes(slot)) {
    score += 5;
  }

  return score;
}

function scoreFormationStructure(order: EmojiId[], profile: OpponentProfile): number {
  const front = order.slice(0, 3);
  const back = order.slice(3, 5);
  let score = 0;

  if (front.includes("shield")) {
    score += 12;
  }

  if (front.includes("chain")) {
    score += 10;
  }

  if (back.includes("heart") && (front.includes("shield") || front.includes("chain") || front.includes("ice"))) {
    score += 10;
  }

  if (back.includes("soap") && (front.includes("shield") || front.includes("water") || front.includes("ice"))) {
    score += 8;
  }

  if (back.includes("plant") && front.some((emojiId) => ["shield", "heart", "soap"].includes(emojiId))) {
    score += 8;
  }

  if (back.includes("bomb") && order.includes("magnet")) {
    score += 10;
  }

  if (order.includes("chain") && order.includes("wind")) {
    score += 8;
  }

  if (order.includes("ghost") && back.includes("ghost")) {
    score += 5;
  }

  if (profile.hasDive && front.some((emojiId) => ["shield", "chain", "ice"].includes(emojiId))) {
    score += 6;
  }

  return score;
}

function buildStableFormation(team: EmojiId[], opponents: EmojiId[]): Formation {
  const profile = describeOpponentTeam(opponents);
  const frontSorted = [...team].sort((left, right) => frontlinePriority(right, profile) - frontlinePriority(left, profile));
  const front = frontSorted.slice(0, Math.min(3, team.length));
  const placedFront = new Set(front);
  const back = team
    .filter((emojiId) => !placedFront.has(emojiId))
    .sort((left, right) => backlinePriority(right) - backlinePriority(left));

  const ordered: EmojiId[] = [];
  const frontCenter = front.find((emojiId) => ["shield", "chain", "ice", "water", "fire"].includes(emojiId)) ?? front[0];
  if (frontCenter) {
    ordered[1] = frontCenter;
  }

  const remainingFront = front.filter((emojiId) => emojiId !== frontCenter);
  if (remainingFront[0]) {
    ordered[0] = remainingFront[0];
  }
  if (remainingFront[1]) {
    ordered[2] = remainingFront[1];
  }
  if (back[0]) {
    ordered[3] = back[0];
  }
  if (back[1]) {
    ordered[4] = back[1];
  }

  for (const emojiId of team) {
    if (ordered.includes(emojiId)) {
      continue;
    }

    const nextIndex = FORMATION_SLOT_ORDER.findIndex((_, index) => ordered[index] == null);
    if (nextIndex >= 0) {
      ordered[nextIndex] = emojiId;
    }
  }

  return buildFormationFromOrderedTeam(ordered.filter((emojiId): emojiId is EmojiId => Boolean(emojiId)));
}

function buildBestFormation(team: EmojiId[], opponents: EmojiId[], mode: MatchMode): Formation {
  const profile = describeOpponentTeam(opponents);
  let bestFormation = buildStableFormation(team, opponents);
  let bestScore = Number.NEGATIVE_INFINITY;

  for (const permutation of permutations(team)) {
    if (permutation.length !== team.length) {
      continue;
    }

    let score = 0;
    for (let index = 0; index < permutation.length; index += 1) {
      score += scorePlacement(permutation[index], FORMATION_SLOT_ORDER[index], opponents, profile, mode);
    }

    score += scoreFormationStructure(permutation, profile);
    if (score > bestScore) {
      bestScore = score;
      bestFormation = buildFormationFromOrderedTeam(permutation);
    }
  }

  return bestFormation;
}

function selectSmartFormation(team: EmojiId[], opponents: EmojiId[]): SimulatedFormationChoice {
  const candidates = uniqueFormations([
    buildBestFormation(team, opponents, "bot_smart"),
    buildStableFormation(team, opponents),
    buildDefaultFormation(team),
  ]);

  let bestChoice: SimulatedFormationChoice | null = null;

  for (let index = 0; index < candidates.length; index += 1) {
    const formation = candidates[index];
    const simulation = scoreSimulatedBattle(team, formation, opponents, `smart-formation-${index}-${team.join("-")}`);

    if (bestChoice == null || simulation.score > bestChoice.score) {
      bestChoice = {
        formation,
        score: simulation.score,
        whySummary: simulation.whySummary,
      };
    }
  }

  return bestChoice ?? {
    formation: buildDefaultFormation(team),
    score: 0,
    whySummary: "Smart Bot defaulted to a standard formation.",
  };
}

function chooseBestFive(deck: EmojiId[], opponents: EmojiId[], mode: MatchMode): { team: EmojiId[]; benchEmojiId: EmojiId | null; score: number } {
  const profile = describeOpponentTeam(opponents);
  const variants = deck.length > 5 ? combinations(deck, 5) : [deck];
  let bestTeam = variants[0];
  let bestScore = Number.NEGATIVE_INFINITY;

  for (const variant of variants) {
    const score = scoreTeam(variant, opponents, mode, profile);
    if (score > bestScore) {
      bestScore = score;
      bestTeam = variant;
    }
  }

  const benchEmojiId = deck.find((emojiId) => !bestTeam.includes(emojiId)) ?? null;
  return {
    team: [...bestTeam],
    benchEmojiId,
    score: bestScore,
  };
}

function choosePracticeArchetype(opponents: EmojiId[]): typeof PRACTICE_ARCHETYPES[number] {
  const profile = describeOpponentTeam(opponents);
  let best = PRACTICE_ARCHETYPES[0];
  let bestScore = Number.NEGATIVE_INFINITY;

  for (const archetype of PRACTICE_ARCHETYPES) {
    const bestFive = chooseBestFive(archetype.deck, opponents, "bot_practice");
    let score = bestFive.score;

    if (profile.hasStatusPressure && archetype.specialty === "status") {
      score += 10;
    }

    if (profile.hasBombCombo && ["combo", "tempo"].includes(archetype.specialty)) {
      score += 12;
    }

    if (profile.hasSustain && archetype.specialty === "combo") {
      score += 8;
    }

    if (profile.hasMovementPressure && archetype.specialty === "stability") {
      score += 6;
    }

    if (score > bestScore) {
      bestScore = score;
      best = archetype;
    }
  }

  return best;
}

function uniqueEmojiIds(source: EmojiId[]): EmojiId[] {
  return [...new Set(source)];
}

function buildSmartCandidatePool(opponents: EmojiId[]): EmojiId[] {
  const candidates: EmojiId[] = ["shield", "chain", "ice", "water", "soap", "heart"];

  for (const opponent of opponents) {
    candidates.push(...EMOJI_DEFINITIONS[opponent].failsAgainst);

    switch (opponent) {
      case "bomb":
        candidates.push("hole", "ice", "shield", "water", "chain");
        break;
      case "magnet":
        candidates.push("lightning", "mirror", "chain", "ghost");
        break;
      case "ghost":
        candidates.push("chain", "lightning", "shield", "ice");
        break;
      case "snake":
      case "fire":
        candidates.push("soap", "heart", "ice", "water");
        break;
      case "heart":
      case "plant":
      case "soap":
        candidates.push("ghost", "bomb", "wind", "fire");
        break;
      case "wind":
        candidates.push("chain", "shield", "ice");
        break;
      default:
        break;
    }
  }

  if (opponents.includes("bomb") || opponents.includes("shield")) {
    candidates.push("magnet");
  }

  if (opponents.some((emojiId) => ["heart", "plant", "soap", "mirror"].includes(emojiId))) {
    candidates.push("ghost");
  }

  if (opponents.some((emojiId) => ["shield", "chain", "ice"].includes(emojiId))) {
    candidates.push("bomb");
  }

  const unique = uniqueEmojiIds(candidates);
  const profile = describeOpponentTeam(opponents);
  const ranked = unique
    .sort((left, right) =>
      scoreEmojiAgainstOpponent(right, opponents, profile, "bot_smart") -
      scoreEmojiAgainstOpponent(left, opponents, profile, "bot_smart")
    );

  const prioritized = uniqueEmojiIds([
    ...ranked.slice(0, 10),
    ...SMART_BOT_DECK,
  ]);

  return prioritized.slice(0, Math.max(6, Math.min(10, prioritized.length)));
}

function buildPracticePlan(opponents: EmojiId[]): BotMatchPlan {
  const archetype = choosePracticeArchetype(opponents);
  const bestFive = chooseBestFive(archetype.deck, opponents, "bot_practice");
  const formation = buildStableFormation(bestFive.team, opponents);
  const opponentProfile = describeOpponentTeam(opponents);
  const teachingNote = opponentProfile.hasBombCombo
    ? " It will keep obvious bomb answers in the frontline and avoid fragile combo pivots."
    : opponentProfile.hasStatusPressure
    ? " It is favoring simple anti-status cover so the battle plan stays readable."
    : " It is using a stable formation so you can read the frontline and backline roles clearly.";

  return {
    profile: BOT_PROFILES.practice,
    deck: [...archetype.deck],
    team: bestFive.team,
    benchEmojiId: bestFive.benchEmojiId,
    formation,
    strategyLabel: archetype.label,
    note: `${archetype.note}${teachingNote} Selected team: ${formatEmojiList(bestFive.team)}.`,
  };
}

function summarizeSmartPlan(
  team: EmojiId[],
  deck: EmojiId[],
  benchEmojiId: EmojiId | null,
  opponents: EmojiId[],
  simulatedWhySummary: string,
): { label: string; note: string } {
  const pressureCombo = deck.includes("magnet") && deck.includes("bomb");
  const controlCombo = deck.includes("chain") && deck.includes("wind");
  const sustainShell = deck.includes("shield") && deck.includes("heart");
  const divePressure = team.includes("ghost");
  const profile = describeOpponentTeam(opponents);

  let label = "Counter Draft";
  let plan = "Smart Bot drafted directly into your current squad's weak points.";

  if (pressureCombo) {
    label = "Pull-Bomb Pivot";
    plan = "Smart Bot drafted Magnet + Bomb pressure and is looking for forced splash swings.";
  } else if (controlCombo) {
    label = "Chain Tempo Lock";
    plan = "Smart Bot drafted Chain + Wind disruption to punish rigid formation and movement lines.";
  } else if (sustainShell) {
    label = "Protected Sustain";
    plan = "Smart Bot drafted a tighter sustain shell and will protect Heart behind a stable frontline.";
  } else if (divePressure) {
    label = "Backline Dive";
    plan = "Smart Bot drafted Ghost pressure to reach your fragile backline before it stabilizes.";
  } else if (profile.hasStatusPressure) {
    label = "Status Answer";
    plan = "Smart Bot drafted anti-status answers first and will win through cleaner late cycles.";
  }

  const benchNote = benchEmojiId == null
    ? ""
    : ` It benched ${EMOJI_DEFINITIONS[benchEmojiId].displayName} from its 6-card prep deck.`;

  return {
    label,
    note: `${plan}${benchNote} Final team: ${formatEmojiList(team)}. Expected swing: ${simulatedWhySummary}`,
  };
}

function buildSmartPlan(opponents: EmojiId[]): BotMatchPlan {
  const candidatePool = buildSmartCandidatePool(opponents);
  const deckVariants = combinations(candidatePool, 6);
  const pool = deckVariants.length > 0 ? deckVariants : [candidatePool.slice(0, 6)];
  let bestPlan: BotMatchPlan | null = null;
  let bestScore = Number.NEGATIVE_INFINITY;
  const profile = describeOpponentTeam(opponents);

  for (const deck of pool) {
    const bestFive = chooseBestFive(deck, opponents, "bot_smart");
    const simulatedFormation = selectSmartFormation(bestFive.team, opponents);
    let score = bestFive.score + simulatedFormation.score;

    for (const placement of simulatedFormation.formation.placements) {
      score += scorePlacement(placement.emojiId, placement.slot, opponents, profile, "bot_smart");
    }

    if (bestFive.benchEmojiId != null) {
      score += scoreEmojiAgainstOpponent(bestFive.benchEmojiId, opponents, profile, "bot_smart") * 0.2;
    }

    if (score > bestScore) {
      const summary = summarizeSmartPlan(
        bestFive.team,
        deck,
        bestFive.benchEmojiId,
        opponents,
        simulatedFormation.whySummary,
      );
      bestScore = score;
      bestPlan = {
        profile: BOT_PROFILES.smart,
        deck: [...deck],
        team: bestFive.team,
        benchEmojiId: bestFive.benchEmojiId,
        formation: simulatedFormation.formation,
        strategyLabel: summary.label,
        note: summary.note,
      };
    }
  }

  if (bestPlan) {
    return bestPlan;
  }

  const fallbackDeck = SMART_BOT_DECK;
  const fallbackTeam = chooseBestFive(fallbackDeck, opponents, "bot_smart");
  return {
    profile: BOT_PROFILES.smart,
    deck: [...fallbackDeck],
    team: fallbackTeam.team,
    benchEmojiId: fallbackTeam.benchEmojiId,
    formation: selectSmartFormation(fallbackTeam.team, opponents).formation,
    strategyLabel: "Counter Draft",
    note: "Smart Bot fell back to its default adaptive draft shell.",
  };
}

function chooseSmartBan(playerDeck: EmojiId[]): EmojiId {
  let best = playerDeck[0];
  let bestScore = Number.NEGATIVE_INFINITY;

  for (const emojiId of playerDeck) {
    const reducedDeck = playerDeck.filter((candidate) => candidate !== emojiId);
    const remainingTeam = selectBattleTeamFromDeck(reducedDeck, 5).team;
    const smartPlan = buildSmartPlan(remainingTeam);
    const simulation = scoreSimulatedBattle(
      smartPlan.team,
      smartPlan.formation,
      remainingTeam,
      `smart-ban-${emojiId}-${remainingTeam.join("-")}`,
    );
    const score = (scoreBanThreat(emojiId, playerDeck, "bot_smart") * 4) + simulation.score;

    if (score > bestScore) {
      bestScore = score;
      best = emojiId;
    }
  }

  return best;
}

function scoreBanThreat(emojiId: EmojiId, playerDeck: EmojiId[], mode: MatchMode): number {
  const definition = EMOJI_DEFINITIONS[emojiId];
  let score = definition.stats.attack * 4 + definition.stats.speed * 2 + definition.stats.hp;

  if (playerDeck.includes("magnet") && playerDeck.includes("bomb")) {
    if (emojiId === "magnet") score += mode === "bot_smart" ? 26 : 18;
    if (emojiId === "bomb") score += mode === "bot_smart" ? 22 : 16;
  }

  if (playerDeck.includes("heart") || playerDeck.includes("soap")) {
    if (emojiId === "ghost") score += mode === "bot_smart" ? 18 : 10;
  }

  if (playerDeck.includes("plant") || playerDeck.includes("heart")) {
    if (emojiId === "fire") score += 10;
  }

  if (playerDeck.includes("snake")) {
    if (emojiId === "snake") score += mode === "bot_smart" ? 14 : 10;
  }

  if (playerDeck.includes("shield") && (playerDeck.includes("plant") || playerDeck.includes("bomb"))) {
    if (emojiId === "shield") score += mode === "bot_smart" ? 14 : 8;
  }

  if (definition.counters.some((counter) => playerDeck.includes(counter))) {
    score += 6;
  }

  return score;
}

export function getBotProfile(mode: MatchMode): BotProfile {
  return mode === "bot_smart" ? BOT_PROFILES.smart : BOT_PROFILES.practice;
}

export function getBotDeck(mode: MatchMode, opponents: EmojiId[] = []): EmojiId[] {
  return buildBotMatchPlan(mode, opponents).deck;
}

export function buildBotMatchPlan(mode: MatchMode, opponents: EmojiId[]): BotMatchPlan {
  if (mode === "bot_smart") {
    return buildSmartPlan(opponents);
  }

  return buildPracticePlan(opponents);
}

export function chooseBotBan(mode: MatchMode, playerDeck: EmojiId[]): EmojiId {
  if (mode === "bot_smart" && playerDeck.length > 1) {
    return chooseSmartBan(playerDeck);
  }

  let best = playerDeck[0];
  let bestScore = Number.NEGATIVE_INFINITY;

  for (const emojiId of playerDeck) {
    const score = scoreBanThreat(emojiId, playerDeck, mode);
    if (score > bestScore) {
      bestScore = score;
      best = emojiId;
    }
  }

  return best;
}
