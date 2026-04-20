import type {
  BattleEvent,
  BattleState,
  BattleUnitState,
  CodexEvent,
  EmojiId,
  Formation,
  FormationPlacement,
  FormationSlot,
  InteractionEntry,
  MatchMode,
  Winner,
} from "./contracts.ts";
import { FORMATION_SLOT_ORDER } from "./contracts.ts";
import { EMOJI_DEFINITIONS } from "./emoji-definitions.ts";
import { getInteractionEntry } from "./interaction-matrix.ts";
import { REASON_CODES } from "./reason-codes.ts";

const FRONT_SLOTS: FormationSlot[] = ["front_left", "front_center", "front_right"];
const BACK_SLOTS: FormationSlot[] = ["back_left", "back_right"];
const MAX_CYCLES = 10;

export interface BattleResolveInput {
  mode: MatchMode;
  rulesVersion: string;
  battleSeed: string;
  teamA: EmojiId[];
  teamB: EmojiId[];
  formationA?: Formation | null;
  formationB?: Formation | null;
}

export interface BattleResolveResult {
  battleState: BattleState;
  winner: Winner;
  whySummary: string;
  whyChain: string[];
  codexEvents: CodexEvent[];
}

export interface SelectedBattleTeam {
  team: EmojiId[];
  benchedEmojiId: EmojiId | null;
}

function slotIndex(slot: FormationSlot): number {
  return FORMATION_SLOT_ORDER.indexOf(slot);
}

function isFrontSlot(slot: FormationSlot): boolean {
  return FRONT_SLOTS.includes(slot);
}

function slotLabel(slot: FormationSlot): string {
  switch (slot) {
    case "front_left":
      return "front left";
    case "front_center":
      return "front center";
    case "front_right":
      return "front right";
    case "back_left":
      return "back left";
    case "back_right":
      return "back right";
  }
}

function cloneUnit(unit: BattleUnitState): BattleUnitState {
  return { ...unit };
}

function cloneFormation(formation: Formation): Formation {
  return {
    placements: formation.placements.map((placement) => ({ ...placement })),
  };
}

function display(emojiId: EmojiId): string {
  return EMOJI_DEFINITIONS[emojiId].displayName;
}

export function selectBattleTeamFromDeck(deck: EmojiId[], desiredCount = 5): SelectedBattleTeam {
  if (deck.length <= desiredCount) {
    return { team: [...deck], benchedEmojiId: null };
  }

  return {
    team: deck.slice(0, desiredCount),
    benchedEmojiId: deck[desiredCount] ?? null,
  };
}

export function buildDefaultFormation(team: EmojiId[]): Formation {
  const sorted = [...team].sort((left, right) => {
    const leftStats = EMOJI_DEFINITIONS[left].stats;
    const rightStats = EMOJI_DEFINITIONS[right].stats;
    const leftFrontScore = (leftStats.preferredRow === "front" ? 3 : leftStats.preferredRow === "flex" ? 2 : 1) * 100 +
      (leftStats.hp * 10) + leftStats.attack;
    const rightFrontScore = (rightStats.preferredRow === "front" ? 3 : rightStats.preferredRow === "flex" ? 2 : 1) * 100 +
      (rightStats.hp * 10) + rightStats.attack;
    return rightFrontScore - leftFrontScore;
  });

  const front = sorted.slice(0, 3);
  const back = sorted.slice(3, 5).sort((left, right) => {
    const leftStats = EMOJI_DEFINITIONS[left].stats;
    const rightStats = EMOJI_DEFINITIONS[right].stats;
    const leftBackScore = (leftStats.preferredRow === "back" ? 3 : leftStats.preferredRow === "flex" ? 2 : 1) * 100 +
      (leftStats.speed * 10) + leftStats.attack;
    const rightBackScore = (rightStats.preferredRow === "back" ? 3 : rightStats.preferredRow === "flex" ? 2 : 1) * 100 +
      (rightStats.speed * 10) + rightStats.attack;
    return rightBackScore - leftBackScore;
  });

  const placements: FormationPlacement[] = [];
  if (front[1]) placements.push({ slot: "front_center", emojiId: front[1] });
  if (front[0]) placements.push({ slot: "front_left", emojiId: front[0] });
  if (front[2]) placements.push({ slot: "front_right", emojiId: front[2] });
  if (back[0]) placements.push({ slot: "back_left", emojiId: back[0] });
  if (back[1]) placements.push({ slot: "back_right", emojiId: back[1] });

  const placed = new Set(placements.map((placement) => placement.emojiId));
  for (const emojiId of team) {
    if (placed.has(emojiId)) {
      continue;
    }

    const nextSlot = FORMATION_SLOT_ORDER.find((slot) => !placements.some((placement) => placement.slot === slot));
    if (nextSlot) {
      placements.push({ slot: nextSlot, emojiId });
    }
  }

  placements.sort((left, right) => slotIndex(left.slot) - slotIndex(right.slot));
  return { placements };
}

function createUnits(team: "player_a" | "player_b", formation: Formation): BattleUnitState[] {
  return formation.placements.map((placement) => {
    const definition = EMOJI_DEFINITIONS[placement.emojiId];
    return {
      unitId: `${team}:${placement.slot}`,
      team,
      emojiId: placement.emojiId,
      slot: placement.slot,
      hp: definition.stats.hp,
      maxHp: definition.stats.hp,
      attack: definition.stats.attack,
      speed: definition.stats.speed,
      alive: true,
      shield: 0,
      growth: 0,
      burn: 0,
      poison: 0,
      stun: 0,
      freeze: 0,
      bind: 0,
      disrupt: 0,
      firstHitAvailable: definition.stats.tags.includes("phase"),
      mirrorArmed: definition.stats.tags.includes("reflect"),
    };
  });
}

function aliveUnits(units: BattleUnitState[]): BattleUnitState[] {
  return units.filter((unit) => unit.alive && unit.hp > 0);
}

function slotPriority(slot: FormationSlot): number {
  switch (slot) {
    case "front_center":
      return 0;
    case "front_left":
      return 1;
    case "front_right":
      return 2;
    case "back_left":
      return 3;
    case "back_right":
      return 4;
  }
}

function sortActionOrder(units: BattleUnitState[]): BattleUnitState[] {
  return [...aliveUnits(units)].sort((left, right) => {
    const speedDelta = effectiveSpeed(right) - effectiveSpeed(left);
    if (speedDelta != 0) {
      return speedDelta;
    }

    const rowDelta = Number(isFrontSlot(left.slot)) - Number(isFrontSlot(right.slot));
    if (rowDelta != 0) {
      return -rowDelta;
    }

    return slotPriority(left.slot) - slotPriority(right.slot);
  });
}

function findMostInjuredAlly(units: BattleUnitState[]): BattleUnitState | null {
  const injured = aliveUnits(units).filter((unit) => unit.hp < unit.maxHp);
  if (injured.length === 0) {
    return null;
  }

  injured.sort((left, right) => {
    const leftMissing = left.maxHp - left.hp;
    const rightMissing = right.maxHp - right.hp;
    if (rightMissing != leftMissing) {
      return rightMissing - leftMissing;
    }

    return slotPriority(left.slot) - slotPriority(right.slot);
  });

  return injured[0];
}

function effectiveSpeed(unit: BattleUnitState): number {
  return Math.max(1, unit.speed - (unit.disrupt * 4));
}

function hasMovementAbility(unit: BattleUnitState): boolean {
  const tags = EMOJI_DEFINITIONS[unit.emojiId].stats.tags;
  return tags.includes("pull") || tags.includes("push") || tags.includes("phase");
}

function findDirtyAlly(units: BattleUnitState[]): BattleUnitState | null {
  return aliveUnits(units).find((unit) => unit.burn > 0 || unit.poison > 0 || unit.bind > 0 || unit.stun > 0 || unit.freeze > 0) ?? null;
}

function firstAliveInSlots(units: BattleUnitState[], slots: FormationSlot[]): BattleUnitState[] {
  return slots
    .map((slot) => units.find((unit) => unit.alive && unit.slot === slot) ?? null)
    .filter((unit): unit is BattleUnitState => unit !== null);
}

function findPreferredBacklineTarget(units: BattleUnitState[]): BattleUnitState | null {
  const alive = aliveUnits(units).filter((unit) => !isFrontSlot(unit.slot));
  if (alive.length === 0) {
    return null;
  }

  const targetOrder: EmojiId[] = ["heart", "soap", "bomb", "plant", "mirror", "hole"];
  alive.sort((left, right) => {
    const leftIndex = targetOrder.indexOf(left.emojiId);
    const rightIndex = targetOrder.indexOf(right.emojiId);
    if (leftIndex != rightIndex) {
      return (leftIndex === -1 ? 99 : leftIndex) - (rightIndex === -1 ? 99 : rightIndex);
    }

    return left.hp - right.hp;
  });

  return alive[0];
}

function findControlPriorityTarget(units: BattleUnitState[]): BattleUnitState | null {
  const alive = aliveUnits(units);
  const targetOrder: EmojiId[] = ["magnet", "ghost", "wind", "bomb", "heart", "plant"];
  alive.sort((left, right) => {
    const leftIndex = targetOrder.indexOf(left.emojiId);
    const rightIndex = targetOrder.indexOf(right.emojiId);
    if (leftIndex != rightIndex) {
      return (leftIndex === -1 ? 99 : leftIndex) - (rightIndex === -1 ? 99 : rightIndex);
    }

    return left.hp - right.hp;
  });
  return alive[0] ?? null;
}

function findTarget(
  actor: BattleUnitState,
  enemies: BattleUnitState[],
  options?: { disablePhaseDive?: boolean },
): BattleUnitState | null {
  const frontline = firstAliveInSlots(enemies, FRONT_SLOTS);
  const backline = firstAliveInSlots(enemies, BACK_SLOTS);

  if (actor.emojiId === "ghost" && !options?.disablePhaseDive) {
    return findPreferredBacklineTarget(enemies) ?? frontline[0] ?? backline[0] ?? null;
  }

  if (actor.emojiId === "lightning" || actor.emojiId === "chain") {
    return findControlPriorityTarget(frontline.length > 0 ? frontline : enemies) ?? null;
  }

  if (actor.emojiId === "bomb") {
    return frontline[0] ?? backline[0] ?? null;
  }

  if (frontline.length > 0) {
    return frontline[0];
  }

  return backline[0] ?? null;
}

function markDead(unit: BattleUnitState) {
  if (!unit.alive || unit.hp > 0) {
    return;
  }

  unit.alive = false;
  unit.hp = 0;
  unit.shield = 0;
}

function addEvent(events: BattleEvent[], cycle: number, type: string, actor: BattleUnitState | string, target: BattleUnitState | string | null, reasonCode: string, caption: string) {
  events.push({
    cycle,
    type,
    actor: typeof actor === "string" ? actor : actor.unitId,
    target: target == null ? undefined : (typeof target === "string" ? target : target.unitId),
    reasonCode,
    caption,
  });
}

function applyStatusTicks(cycle: number, teamA: BattleUnitState[], teamB: BattleUnitState[], events: BattleEvent[]) {
  for (const unit of [...teamA, ...teamB]) {
    if (!unit.alive) {
      continue;
    }

    if (unit.burn > 0) {
      unit.hp -= 1;
      unit.burn -= 1;
      addEvent(events, cycle, "status_tick", unit, unit, REASON_CODES.DIRECT_CLASH, `${display(unit.emojiId)} burned for 1.`);
    }

    if (unit.poison > 0) {
      unit.hp -= 1;
      unit.poison -= 1;
      addEvent(events, cycle, "status_tick", unit, unit, REASON_CODES.SNAKE_APPLIED_POISON, `${display(unit.emojiId)} took poison damage.`);
    }

    unit.bind = Math.max(0, unit.bind - 1);
    unit.disrupt = Math.max(0, unit.disrupt - 1);
    markDead(unit);
  }
}

function advanceFrontline(cycle: number, team: BattleUnitState[], events: BattleEvent[]) {
  const openFrontSlots = FRONT_SLOTS.filter((slot) => !team.some((unit) => unit.alive && unit.slot === slot));
  if (openFrontSlots.length === 0) {
    return;
  }

  for (const openSlot of openFrontSlots) {
    const candidates = aliveUnits(team)
      .filter((unit) => !isFrontSlot(unit.slot))
      .sort((left, right) => {
        const leftPreference = EMOJI_DEFINITIONS[left.emojiId].stats.preferredRow === "front"
          ? 0
          : EMOJI_DEFINITIONS[left.emojiId].stats.preferredRow === "flex"
          ? 1
          : 2;
        const rightPreference = EMOJI_DEFINITIONS[right.emojiId].stats.preferredRow === "front"
          ? 0
          : EMOJI_DEFINITIONS[right.emojiId].stats.preferredRow === "flex"
          ? 1
          : 2;

        if (leftPreference !== rightPreference) {
          return leftPreference - rightPreference;
        }

        const hpDelta = right.hp - left.hp;
        if (hpDelta !== 0) {
          return hpDelta;
        }

        return slotPriority(left.slot) - slotPriority(right.slot);
      });

    const candidate = candidates[0];
    if (!candidate) {
      break;
    }

    const previousSlot = candidate.slot;
    candidate.slot = openSlot;
    addEvent(
      events,
      cycle,
      "advance",
      candidate,
      candidate,
      REASON_CODES.FRONTLINE_COLLAPSE_ADVANCED,
      `${display(candidate.emojiId)} advanced from ${slotLabel(previousSlot)} to ${slotLabel(openSlot)} after the frontline collapsed.`,
    );
  }
}

function applySplashDamage(cycle: number, actor: BattleUnitState, enemies: BattleUnitState[], primaryTarget: BattleUnitState, events: BattleEvent[]) {
  for (const unit of aliveUnits(enemies)) {
    if (unit.unitId === primaryTarget.unitId) {
      continue;
    }

    unit.hp -= 1;
    addEvent(events, cycle, "splash", actor, unit, REASON_CODES.BOMB_OVERWHELMED_TARGET, `${display(actor.emojiId)} clipped ${display(unit.emojiId)} with splash damage.`);
    markDead(unit);
  }
}

function tryMoveToBack(target: BattleUnitState, team: BattleUnitState[]): boolean {
  if (!isFrontSlot(target.slot)) {
    return false;
  }

  const openSlot = BACK_SLOTS.find((slot) => !team.some((unit) => unit.alive && unit.slot === slot));
  if (!openSlot) {
    return false;
  }

  target.slot = openSlot;
  return true;
}

function tryMoveToFront(target: BattleUnitState, team: BattleUnitState[]): boolean {
  if (isFrontSlot(target.slot)) {
    return false;
  }

  const openSlot = FRONT_SLOTS.find((slot) => !team.some((unit) => unit.alive && unit.slot === slot));
  if (!openSlot) {
    return false;
  }

  target.slot = openSlot;
  return true;
}

function resolveAttack(
  cycle: number,
  actor: BattleUnitState,
  allies: BattleUnitState[],
  enemies: BattleUnitState[],
  events: BattleEvent[],
) {
  if (!actor.alive) {
    return;
  }

  if (actor.stun > 0 || actor.freeze > 0) {
    const reasonCode = actor.freeze > 0 ? REASON_CODES.ICE_DEFUSED_BOMB : REASON_CODES.LIGHTNING_STUNNED_MAGNET;
    addEvent(events, cycle, "skip", actor, null, reasonCode, `${display(actor.emojiId)} lost its action this cycle.`);
    actor.stun = 0;
    actor.freeze = 0;
    return;
  }

  if (actor.emojiId === "heart") {
    const ally = findMostInjuredAlly(allies);
    if (ally) {
      ally.hp = Math.min(ally.maxHp, ally.hp + 2);
      addEvent(events, cycle, "heal", actor, ally, REASON_CODES.HEART_STABILIZED_ALLY, `Heart healed ${display(ally.emojiId)} and stabilized the team.`);
      return;
    }
  }

  if (actor.emojiId === "soap") {
    const ally = findDirtyAlly(allies);
    if (ally) {
      ally.burn = 0;
      ally.poison = 0;
      ally.bind = 0;
      ally.stun = 0;
      ally.freeze = 0;
      addEvent(events, cycle, "cleanse", actor, ally, REASON_CODES.SOAP_CLEANSED_STATUS, `Soap cleansed ${display(ally.emojiId)} before collapse.`);
      return;
    }
  }

  if (actor.emojiId === "shield") {
    const ally = aliveUnits(allies)
      .filter((unit) => isFrontSlot(unit.slot) && unit.shield <= 0)
      .sort((left, right) => left.hp - right.hp)[0];
    if (ally) {
      ally.shield = 1;
      addEvent(events, cycle, "shield", actor, ally, REASON_CODES.SHIELD_BLOCKED_IMPACT, `Shield reinforced ${display(ally.emojiId)} for the next clash.`);
      return;
    }
  }

  if (actor.emojiId === "plant") {
    actor.growth += 1;
  }

  const movementSuppressed = actor.bind > 0 && hasMovementAbility(actor);
  const target = findTarget(actor, enemies, { disablePhaseDive: movementSuppressed });
  if (!target) {
    return;
  }

  const baseEntry = getInteractionEntry(actor.emojiId, target.emojiId);
  const entry: InteractionEntry = movementSuppressed
    ? {
      attacker: actor.emojiId,
      defender: target.emojiId,
      outcomeType: "neutral",
      reasonCode: REASON_CODES.CHAIN_BOUND_TARGET,
      whyText: `Chain bound ${display(actor.emojiId)} and forced a plain clash.`,
      whyChain: [
        `${display(actor.emojiId)} lost access to its movement play.`,
        `${display(target.emojiId)} got a normal lane fight instead of a combo swing.`,
      ],
      effectTags: ["damage"],
    }
    : baseEntry;

  if (movementSuppressed) {
    addEvent(events, cycle, "bind_lock", actor, target, REASON_CODES.CHAIN_BOUND_TARGET, entry.whyText);
  }

  if (target.firstHitAvailable && actor.emojiId !== "lightning" && actor.emojiId !== "chain") {
    target.firstHitAvailable = false;
    addEvent(events, cycle, "phase", actor, target, REASON_CODES.GHOST_PHASED_PAST_FRONT, `Ghost phased through ${display(target.emojiId)}'s opener.`);
    return;
  }

  if (target.mirrorArmed && entry.effectTags.includes("reflect")) {
    target.mirrorArmed = false;
    actor.hp -= Math.max(1, actor.attack + 1);
    addEvent(events, cycle, "reflect", target, actor, REASON_CODES.MIRROR_REFLECTED_TARGETED_EFFECT, entry.whyText);
    markDead(actor);
    return;
  }

  if (target.shield > 0 && !entry.effectTags.includes("delete") && !entry.effectTags.includes("shield_break")) {
    target.shield -= 1;
    addEvent(events, cycle, "block", target, actor, REASON_CODES.SHIELD_BLOCKED_IMPACT, `${display(target.emojiId)} blocked ${display(actor.emojiId)}'s impact.`);
    return;
  }

  if (entry.effectTags.includes("shield_break")) {
    target.shield = 0;
  }

  if (entry.effectTags.includes("push")) {
    if (tryMoveToBack(target, enemies)) {
      addEvent(events, cycle, "push", actor, target, REASON_CODES.WIND_SCATTERED_FORMATION, entry.whyText);
    } else {
      target.disrupt = Math.max(target.disrupt, 1);
      addEvent(events, cycle, "disrupt", actor, target, REASON_CODES.WIND_SCATTERED_FORMATION, `${display(target.emojiId)} was disrupted when Wind found no legal push space.`);
    }
  }

  if (entry.effectTags.includes("pull")) {
    if (tryMoveToFront(target, enemies)) {
      addEvent(events, cycle, "pull", actor, target, REASON_CODES.MAGNET_PULLED_BOMB, entry.whyText);
    }
  }

  if (entry.effectTags.includes("delete")) {
    target.hp = 0;
  } else {
    let damage = actor.attack + actor.growth + (entry.effectTags.includes("heavy_damage") ? 2 : 0);
    if (entry.outcomeType === "attacker_advantage") {
      damage += 1;
    } else if (entry.outcomeType === "defender_advantage") {
      damage = Math.max(1, damage - 1);
    }

    if (actor.disrupt > 0) {
      damage = Math.max(1, damage - actor.disrupt);
    }

    target.hp -= Math.max(1, damage);
  }

  if (entry.effectTags.includes("burn")) {
    target.burn = Math.max(target.burn, 2);
  }

  if (entry.effectTags.includes("poison")) {
    target.poison = Math.max(target.poison, 2);
  }

  if (entry.effectTags.includes("stun")) {
    target.stun = Math.max(target.stun, 1);
  }

  if (entry.effectTags.includes("freeze")) {
    target.freeze = Math.max(target.freeze, 1);
  }

  if (entry.effectTags.includes("bind")) {
    target.bind = Math.max(target.bind, 1);
  }

  if (entry.effectTags.includes("disrupt")) {
    target.disrupt = Math.max(target.disrupt, 1);
  }

  addEvent(events, cycle, "attack", actor, target, entry.reasonCode, entry.whyText);

  if (entry.effectTags.includes("splash")) {
    applySplashDamage(cycle, actor, enemies, target, events);
  }

  markDead(target);
}

function determineWinner(teamA: BattleUnitState[], teamB: BattleUnitState[]): Winner | null {
  const aliveA = aliveUnits(teamA);
  const aliveB = aliveUnits(teamB);
  if (aliveA.length === 0 && aliveB.length === 0) {
    return "draw";
  }
  if (aliveA.length === 0) {
    return "player_b";
  }
  if (aliveB.length === 0) {
    return "player_a";
  }
  return null;
}

function formatEmojiList(emojiIds: EmojiId[], limit = 2): string {
  const names = emojiIds.slice(0, limit).map(display);
  if (names.length === 0) {
    return "The surviving team";
  }
  if (names.length === 1) {
    return names[0];
  }
  if (emojiIds.length > limit) {
    return `${names.slice(0, -1).join(", ")}, ${names[names.length - 1]}, and allies`;
  }
  if (names.length === 2) {
    return `${names[0]} and ${names[1]}`;
  }

  return `${names.slice(0, -1).join(", ")}, and ${names[names.length - 1]}`;
}

function isGenericClashEvent(event: BattleEvent): boolean {
  return event.reasonCode === REASON_CODES.DIRECT_CLASH || event.caption.includes("standard engagement");
}

function scoreWhyEvent(event: BattleEvent): number {
  let score = event.cycle * 10;

  switch (event.type) {
    case "reflect":
      score += 140;
      break;
    case "pull":
      score += 135;
      break;
    case "push":
    case "disrupt":
      score += 130;
      break;
    case "phase":
      score += 125;
      break;
    case "heal":
    case "cleanse":
      score += 120;
      break;
    case "splash":
      score += 118;
      break;
    case "block":
    case "shield":
      score += 112;
      break;
    case "status_tick":
      score += 104;
      break;
    case "timeout":
      score += 100;
      break;
    case "skip":
      score += 90;
      break;
    case "attack":
      score += 80;
      break;
    default:
      score += 60;
      break;
  }

  if (event.reasonCode !== REASON_CODES.DIRECT_CLASH) {
    score += 18;
  }

  if (isGenericClashEvent(event)) {
    score -= 40;
  }

  if (event.reasonCode === REASON_CODES.DRAW_BY_SIMULTANEOUS_ELIMINATION || event.reasonCode === REASON_CODES.DRAW_BY_TIMEOUT) {
    score += 24;
  }

  return score;
}

function buildFinalStateSummary(winner: Winner, teamA: BattleUnitState[], teamB: BattleUnitState[]): string {
  if (winner === "draw") {
    const aliveA = aliveUnits(teamA).length;
    const aliveB = aliveUnits(teamB).length;
    if (aliveA === 0 && aliveB === 0) {
      return "Both teams collapsed in the same final exchange.";
    }

    return "Neither formation found a decisive edge before the battle timed out.";
  }

  const survivingTeam = winner === "player_a" ? teamA : teamB;
  const survivors = aliveUnits(survivingTeam).map((unit) => unit.emojiId);

  if (survivors.length === 1) {
    return `${display(survivors[0])} anchored the last surviving formation.`;
  }

  return `${formatEmojiList(survivors)} were the last team standing.`;
}

function buildFallbackChain(winner: Winner, teamA: BattleUnitState[], teamB: BattleUnitState[]): string[] {
  if (winner === "draw") {
    return [
      "Both teams traded through the final clash cycle.",
      "The last exchange removed every remaining unit.",
    ];
  }

  const survivingTeam = winner === "player_a" ? teamA : teamB;
  const losingTeam = winner === "player_a" ? teamB : teamA;
  const survivors = aliveUnits(survivingTeam).map((unit) => unit.emojiId);
  const defeatedCount = losingTeam.filter((unit) => !unit.alive).length;

  return [
    "The frontline traded without a clean combo swing.",
    `${formatEmojiList(survivors)} survived the final clash cycle.`,
    `The opposing formation lost ${defeatedCount} unit${defeatedCount === 1 ? "" : "s"} and could not recover.`,
  ];
}

function buildWhySummary(
  events: BattleEvent[],
  winner: Winner,
  teamA: BattleUnitState[],
  teamB: BattleUnitState[],
): { whySummary: string; whyChain: string[] } {
  const rankedEvents = events
    .filter((event) => event.type !== "setup")
    .sort((left, right) => {
      const scoreDelta = scoreWhyEvent(right) - scoreWhyEvent(left);
      if (scoreDelta !== 0) {
        return scoreDelta;
      }

      return right.cycle - left.cycle;
    });

  const uniqueHighlights: BattleEvent[] = [];
  const seenCaptions = new Set<string>();
  for (const event of rankedEvents) {
    if (seenCaptions.has(event.caption)) {
      continue;
    }

    seenCaptions.add(event.caption);
    uniqueHighlights.push(event);
  }

  const topHighlight = uniqueHighlights.find((event) => !isGenericClashEvent(event));
  const whySummary = topHighlight?.caption ?? buildFinalStateSummary(winner, teamA, teamB);

  const chainCandidates = uniqueHighlights
    .filter((event) => event.caption !== whySummary && scoreWhyEvent(event) >= 95)
    .slice(0, 3)
    .sort((left, right) => left.cycle - right.cycle);

  const whyChain = chainCandidates.length > 0
    ? chainCandidates.map((event) => event.caption)
    : buildFallbackChain(winner, teamA, teamB);

  return { whySummary, whyChain };
}

function buildCodexEvents(events: BattleEvent[]): CodexEvent[] {
  const unique = new Map<string, CodexEvent>();
  for (const event of events) {
    if (!event.reasonCode) {
      continue;
    }
    unique.set(event.reasonCode, {
      interactionKey: event.reasonCode,
      reasonCode: event.reasonCode,
      summary: event.caption,
      tip: "Review the interaction in the Codex to understand the counterplay.",
    });
  }
  return [...unique.values()];
}

export function resolveBattle(input: BattleResolveInput): BattleResolveResult {
  const formationA = cloneFormation(input.formationA ?? buildDefaultFormation(input.teamA));
  const formationB = cloneFormation(input.formationB ?? buildDefaultFormation(input.teamB));

  const teamA = createUnits("player_a", formationA);
  const teamB = createUnits("player_b", formationB);
  const events: BattleEvent[] = [];

  for (const unit of [...teamA, ...teamB]) {
    if (unit.emojiId === "shield") {
      unit.shield = 1;
      addEvent(events, 0, "setup", unit, unit, REASON_CODES.SHIELD_BLOCKED_IMPACT, `${display(unit.emojiId)} entered battle with a shield ready.`);
    }
    if (unit.emojiId === "plant") {
      unit.growth = 1;
      addEvent(events, 0, "setup", unit, unit, REASON_CODES.PLANT_GREW_UNCHECKED, "Plant entered battle marked for growth.");
    }
  }

  let winner: Winner | null = determineWinner(teamA, teamB);
  let cycle = 0;

  while (!winner && cycle < MAX_CYCLES) {
    cycle += 1;
    const order = sortActionOrder([...teamA, ...teamB]);
    for (const actorRef of order) {
      const actor = [...teamA, ...teamB].find((unit) => unit.unitId === actorRef.unitId);
      if (!actor || !actor.alive) {
        continue;
      }

      const allies = actor.team === "player_a" ? teamA : teamB;
      const enemies = actor.team === "player_a" ? teamB : teamA;
      resolveAttack(cycle, actor, allies, enemies, events);
      winner = determineWinner(teamA, teamB);
      if (winner) {
        break;
      }
    }

    if (!winner) {
      applyStatusTicks(cycle, teamA, teamB, events);
      advanceFrontline(cycle, teamA, events);
      advanceFrontline(cycle, teamB, events);
      winner = determineWinner(teamA, teamB);
    }
  }

  if (!winner) {
    const hpA = aliveUnits(teamA).reduce((sum, unit) => sum + unit.hp, 0);
    const hpB = aliveUnits(teamB).reduce((sum, unit) => sum + unit.hp, 0);
    winner = hpA === hpB ? "draw" : hpA > hpB ? "player_a" : "player_b";
    addEvent(
      events,
      cycle,
      "timeout",
      "system",
      null,
      winner === "draw" ? REASON_CODES.DRAW_BY_TIMEOUT : REASON_CODES.TEAM_NEUTRALIZATION,
      winner === "draw"
        ? "Both teams timed out in a dead even fight."
        : "The battle ended on surviving team health after the final clash cycle.",
    );
  }

  const finalWinner = determineWinner(teamA, teamB) ?? winner;
  const { whySummary, whyChain } = buildWhySummary(events, finalWinner, teamA, teamB);
  const battleState: BattleState = {
    cycle,
    teamA: teamA.map(cloneUnit),
    teamB: teamB.map(cloneUnit),
    eventLog: events,
    winner: finalWinner,
    whySummary,
    whyChain,
  };

  return {
    battleState,
    winner: finalWinner,
    whySummary,
    whyChain,
    codexEvents: buildCodexEvents(events),
  };
}
