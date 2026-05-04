import type { EmojiId, Winner } from "./contracts.ts";

export const EMOJI_CLASH_RULES_VERSION = "emoji-clash-pvp-001";
export const EMOJI_CLASH_TOTAL_TURNS = 5;
export const EMOJI_CLASH_TURN_VALUES = [1, 1, 2, 2, 3] as const;
export const EMOJI_CLASH_PICK_TTL_SECONDS = 15;
export const EMOJI_CLASH_QUEUE_TTL_SECONDS = 120;
export const EMOJI_CLASH_TIMEOUT_FORFEIT_STRIKES = 2;

export type EmojiClashTurnOutcome = "player_a" | "player_b" | "draw";

export interface EmojiClashProfile {
  unitKey: EmojiId;
  displayName: string;
  role: string;
  basePower: number;
  tags: string[];
  counterTags: string[];
  earlyBonus: number;
  lateBonus: number;
  finalTurnBonus: number;
  behindBonus: number;
}

export interface EmojiClashTurnRecord {
  turnNumber: number;
  turnValue: number;
  playerAUnitKey: EmojiId | "";
  playerBUnitKey: EmojiId | "";
  playerATimedOut?: boolean;
  playerBTimedOut?: boolean;
  playerATimeoutBurnUnitKey?: EmojiId | "";
  playerBTimeoutBurnUnitKey?: EmojiId | "";
  playerACombatPower: number;
  playerBCombatPower: number;
  outcome: EmojiClashTurnOutcome;
  playerAScoreAfter: number;
  playerBScoreAfter: number;
  reason: string;
}

export interface EmojiClashPublicState {
  phase: "queue" | "pick" | "finished" | "queue_cancelled";
  queueTicket?: string;
  opponentType?: "human" | "bot_fill";
  botProfileId?: string;
  botFillReason?: string;
  opponentDisplayName?: string;
  opponentAvatarKey?: string;
  currentTurnIndex: number;
  playerAScore: number;
  playerBScore: number;
  playerAUsedUnits: EmojiId[];
  playerBUsedUnits: EmojiId[];
  matchmakingRatingAtQueue?: number;
  queueStartedAt?: string;
  queueExpiresAt?: string;
  playerATimeoutStrikes?: number;
  playerBTimeoutStrikes?: number;
  turnHistory: EmojiClashTurnRecord[];
  turnDeadlineAt?: string;
  winner?: Winner | null;
  finishReason?: string;
  forfeitSides?: Array<"player_a" | "player_b">;
  systemNote?: string;
  cancelledAt?: string;
  cancelledBy?: string;
}

export const EMOJI_CLASH_ROSTER: EmojiId[] = [
  "fire",
  "water",
  "lightning",
  "ice",
  "magnet",
  "bomb",
  "mirror",
  "hole",
  "shield",
  "snake",
  "soap",
  "plant",
  "wind",
  "heart",
  "ghost",
  "chain",
];

const profiles: Record<EmojiId, EmojiClashProfile> = {
  fire: createProfile("fire", "Tempo", 5, 2, 0, 0, 0, ["aggressive", "flame", "projectile", "tempo"], ["nature", "frozen", "passive", "cleanse"]),
  water: createProfile("water", "Control", 5, 0, 1, 0, 0, ["control", "liquid", "cleanse"], ["flame", "explosive", "tech"]),
  lightning: createProfile("lightning", "Tempo", 5, 2, 0, 0, 0, ["aggressive", "electric", "projectile", "tempo"], ["liquid", "flexible"]),
  ice: createProfile("ice", "Control", 5, 0, 0, 0, 0, ["control", "frozen", "defense"], ["aggressive", "explosive", "elusive"]),
  magnet: createProfile("magnet", "Disrupt", 5, 0, 0, 0, 0, ["disrupt", "tech", "control"], ["projectile", "electric", "guard"]),
  bomb: createProfile("bomb", "Burst", 6, 1, 0, 0, 0, ["aggressive", "explosive", "projectile"], ["scaling", "guard", "reactive"]),
  mirror: createProfile("mirror", "Trick", 5, 0, 0, 1, 0, ["reactive", "trick", "flexible"], ["tempo", "elusive"]),
  hole: createProfile("hole", "Trap", 5, 0, 1, 0, 0, ["disrupt", "void", "trap"], ["projectile", "tech", "electric"]),
  shield: createProfile("shield", "Defense", 5, 0, 0, 0, 1, ["defense", "guard", "safe"], ["aggressive", "explosive", "poison"]),
  snake: createProfile("snake", "Pressure", 5, 1, 1, 0, 0, ["poison", "trick", "scaling"], ["support", "passive", "nature", "reactive"]),
  soap: createProfile("soap", "Cleanse", 5, 0, 0, 0, 1, ["cleanse", "liquid", "control"], ["poison", "flame", "tech"]),
  plant: createProfile("plant", "Scaling", 5, 0, 1, 1, 0, ["scaling", "nature", "passive"], ["liquid", "guard", "void"]),
  wind: createProfile("wind", "Flex", 5, 1, 0, 0, 0, ["flexible", "tempo", "control"], ["explosive", "disrupt", "void"]),
  heart: createProfile("heart", "Comeback", 4, 0, 1, 1, 3, ["support", "comeback", "flexible"], ["aggressive", "tempo", "late", "elusive"]),
  ghost: createProfile("ghost", "Late", 5, 0, 1, 1, 0, ["late", "elusive", "trick"], ["guard", "passive"]),
  chain: createProfile("chain", "Bind", 5, 0, 0, 0, 1, ["defense", "bind", "disrupt"], ["explosive", "poison"]),
};

const headToHead: Partial<Record<EmojiId, Partial<Record<EmojiId, number>>>> = {
  water: bonus([["fire", 2], ["bomb", 2], ["magnet", 1]]),
  fire: bonus([["plant", 2], ["ice", 1], ["snake", 1]]),
  lightning: bonus([["water", 2], ["wind", 1], ["soap", 1]]),
  ice: bonus([["bomb", 2], ["lightning", 1], ["ghost", 1]]),
  magnet: bonus([["lightning", 2], ["fire", 1], ["bomb", 1], ["shield", 1], ["mirror", 1], ["chain", 1]]),
  bomb: bonus([["plant", 2], ["shield", 1], ["mirror", 1], ["chain", 1]]),
  mirror: bonus([["lightning", 1], ["ghost", 2], ["bomb", 1], ["snake", 1], ["chain", 1]]),
  hole: bonus([["lightning", 2], ["magnet", 2], ["bomb", 1], ["mirror", 1]]),
  shield: bonus([["fire", 2], ["bomb", 1], ["snake", 2], ["lightning", 1]]),
  snake: bonus([["plant", 2], ["heart", 2], ["water", 1], ["mirror", 1]]),
  soap: bonus([["snake", 2], ["fire", 1], ["magnet", 2], ["ghost", 1]]),
  plant: bonus([["water", 2], ["shield", 1], ["hole", 2]]),
  wind: bonus([["bomb", 2], ["magnet", 2], ["hole", 2], ["ghost", 2]]),
  heart: bonus([["ghost", 3], ["lightning", 1], ["bomb", 1], ["fire", 1], ["ice", 1]]),
  ghost: bonus([["shield", 2], ["plant", 1], ["chain", 1]]),
  chain: bonus([["bomb", 2], ["fire", 1], ["wind", 1], ["snake", 1]]),
};

function createProfile(
  unitKey: EmojiId,
  role: string,
  basePower: number,
  earlyBonus: number,
  lateBonus: number,
  finalTurnBonus: number,
  behindBonus: number,
  tags: string[],
  counterTags: string[],
): EmojiClashProfile {
  return {
    unitKey,
    displayName: toDisplayName(unitKey),
    role,
    basePower,
    tags,
    counterTags,
    earlyBonus,
    lateBonus,
    finalTurnBonus,
    behindBonus,
  };
}

function bonus(entries: Array<[EmojiId, number]>): Partial<Record<EmojiId, number>> {
  return Object.fromEntries(entries) as Partial<Record<EmojiId, number>>;
}

export function isEmojiClashUnit(value: string): value is EmojiId {
  return EMOJI_CLASH_ROSTER.includes(value as EmojiId);
}

export function toDisplayName(unitKey: string): string {
  return unitKey.length === 0 ? "No pick" : unitKey.charAt(0).toUpperCase() + unitKey.slice(1);
}

export function getTurnValue(turnIndex: number): number {
  if (turnIndex < 0 || turnIndex >= EMOJI_CLASH_TURN_VALUES.length) {
    throw new Error("Emoji Clash has exactly five turns.");
  }

  return EMOJI_CLASH_TURN_VALUES[turnIndex];
}

export function createInitialClashState(queueTicket: string): EmojiClashPublicState {
  return {
    phase: "queue",
    queueTicket,
    currentTurnIndex: 0,
    playerAScore: 0,
    playerBScore: 0,
    playerAUsedUnits: [],
    playerBUsedUnits: [],
    playerATimeoutStrikes: 0,
    playerBTimeoutStrikes: 0,
    turnHistory: [],
  };
}

export function startPickPhase(state: EmojiClashPublicState, now = new Date()): EmojiClashPublicState {
  return {
    ...state,
    phase: "pick",
    turnDeadlineAt: new Date(now.getTime() + EMOJI_CLASH_PICK_TTL_SECONDS * 1000).toISOString(),
  };
}

export function resolvePickedTurn(
  state: EmojiClashPublicState,
  playerAUnitKey: EmojiId,
  playerBUnitKey: EmojiId,
): EmojiClashPublicState {
  const turnIndex = Math.max(0, Math.min(EMOJI_CLASH_TOTAL_TURNS - 1, state.currentTurnIndex));
  const turnNumber = turnIndex + 1;
  const turnValue = getTurnValue(turnIndex);
  const playerAPower = computePower(playerAUnitKey, playerBUnitKey, turnNumber, state.playerAScore, state.playerBScore);
  const playerBPower = computePower(playerBUnitKey, playerAUnitKey, turnNumber, state.playerBScore, state.playerAScore);
  const outcome: EmojiClashTurnOutcome = playerAPower > playerBPower ? "player_a" : playerBPower > playerAPower ? "player_b" : "draw";
  return appendResolvedTurn(state, {
    turnNumber,
    turnValue,
    playerAUnitKey,
    playerBUnitKey,
    playerACombatPower: playerAPower,
    playerBCombatPower: playerBPower,
    outcome,
    playerAScoreAfter: state.playerAScore + (outcome === "player_a" ? turnValue : 0),
    playerBScoreAfter: state.playerBScore + (outcome === "player_b" ? turnValue : 0),
    reason: buildReason(playerAUnitKey, playerBUnitKey, playerAPower, playerBPower, outcome),
  }, playerAUnitKey, playerBUnitKey);
}

export function resolveTimeoutTurn(
  state: EmojiClashPublicState,
  playerAUnitKey: EmojiId | "",
  playerBUnitKey: EmojiId | "",
  matchId = state.queueTicket ?? "",
  playerAUserId = "player_a",
  playerBUserId = "player_b",
): EmojiClashPublicState {
  const turnIndex = Math.max(0, Math.min(EMOJI_CLASH_TOTAL_TURNS - 1, state.currentTurnIndex));
  const turnNumber = turnIndex + 1;
  const turnValue = getTurnValue(turnIndex);
  const playerATimedOut = !playerAUnitKey;
  const playerBTimedOut = !playerBUnitKey;
  const playerATimeoutBurn = playerATimedOut
    ? selectTimeoutBurnUnit(state.playerAUsedUnits, matchId, turnNumber, "player_a", playerAUserId)
    : "";
  const playerBTimeoutBurn = playerBTimedOut
    ? selectTimeoutBurnUnit(state.playerBUsedUnits, matchId, turnNumber, "player_b", playerBUserId)
    : "";
  const consumedA = playerAUnitKey || playerATimeoutBurn;
  const consumedB = playerBUnitKey || playerBTimeoutBurn;
  const outcome: EmojiClashTurnOutcome = playerAUnitKey && !playerBUnitKey
    ? "player_a"
    : playerBUnitKey && !playerAUnitKey
      ? "player_b"
      : "draw";
  const nextTimeoutStrikesA = (state.playerATimeoutStrikes ?? 0) + (playerATimedOut ? 1 : 0);
  const nextTimeoutStrikesB = (state.playerBTimeoutStrikes ?? 0) + (playerBTimedOut ? 1 : 0);
  const reason = buildTimeoutReason(playerAUnitKey, playerBUnitKey, playerATimeoutBurn, playerBTimeoutBurn, outcome, turnValue);
  return appendResolvedTurn(state, {
    turnNumber,
    turnValue,
    playerAUnitKey: playerAUnitKey || playerATimeoutBurn,
    playerBUnitKey: playerBUnitKey || playerBTimeoutBurn,
    playerATimedOut,
    playerBTimedOut,
    playerATimeoutBurnUnitKey: playerATimeoutBurn,
    playerBTimeoutBurnUnitKey: playerBTimeoutBurn,
    playerACombatPower: playerAUnitKey ? profiles[playerAUnitKey].basePower : 0,
    playerBCombatPower: playerBUnitKey ? profiles[playerBUnitKey].basePower : 0,
    outcome,
    playerAScoreAfter: state.playerAScore + (outcome === "player_a" ? turnValue : 0),
    playerBScoreAfter: state.playerBScore + (outcome === "player_b" ? turnValue : 0),
    reason,
  }, consumedA || null, consumedB || null, {
    playerATimeoutStrikes: nextTimeoutStrikesA,
    playerBTimeoutStrikes: nextTimeoutStrikesB,
  });
}

function appendResolvedTurn(
  state: EmojiClashPublicState,
  record: EmojiClashTurnRecord,
  consumedA: EmojiId | null,
  consumedB: EmojiId | null,
  strikeUpdate?: { playerATimeoutStrikes: number; playerBTimeoutStrikes: number },
): EmojiClashPublicState {
  const nextTurnIndex = record.turnNumber >= EMOJI_CLASH_TOTAL_TURNS ? EMOJI_CLASH_TOTAL_TURNS : record.turnNumber;
  const playerATimeoutStrikes = strikeUpdate?.playerATimeoutStrikes ?? state.playerATimeoutStrikes ?? 0;
  const playerBTimeoutStrikes = strikeUpdate?.playerBTimeoutStrikes ?? state.playerBTimeoutStrikes ?? 0;
  const forfeitSides: Array<"player_a" | "player_b"> = [];
  if (playerATimeoutStrikes >= EMOJI_CLASH_TIMEOUT_FORFEIT_STRIKES) forfeitSides.push("player_a");
  if (playerBTimeoutStrikes >= EMOJI_CLASH_TIMEOUT_FORFEIT_STRIKES) forfeitSides.push("player_b");
  const isForfeit = forfeitSides.length > 0;
  const isFinished = isForfeit || nextTurnIndex >= EMOJI_CLASH_TOTAL_TURNS;
  const nextState: EmojiClashPublicState = {
    ...state,
    currentTurnIndex: nextTurnIndex,
    playerAScore: record.playerAScoreAfter,
    playerBScore: record.playerBScoreAfter,
    playerAUsedUnits: consumedA ? [...state.playerAUsedUnits, consumedA] : [...state.playerAUsedUnits],
    playerBUsedUnits: consumedB ? [...state.playerBUsedUnits, consumedB] : [...state.playerBUsedUnits],
    playerATimeoutStrikes,
    playerBTimeoutStrikes,
    turnHistory: [...state.turnHistory, record],
    phase: isFinished ? "finished" : "pick",
    turnDeadlineAt: isFinished ? "" : new Date(Date.now() + EMOJI_CLASH_PICK_TTL_SECONDS * 1000).toISOString(),
  };
  if (isForfeit) {
    nextState.forfeitSides = forfeitSides;
    nextState.finishReason = forfeitSides.length === 2
      ? "both_timeout_forfeit"
      : `${forfeitSides[0]}_timeout_forfeit`;
    nextState.winner = resolveForfeitWinner(nextState, forfeitSides);
  } else if (isFinished) {
    nextState.finishReason = "completed";
    nextState.winner = nextState.playerAScore > nextState.playerBScore
      ? "player_a"
      : nextState.playerBScore > nextState.playerAScore
        ? "player_b"
        : "draw";
  }

  return nextState;
}

function resolveForfeitWinner(state: EmojiClashPublicState, forfeitSides: Array<"player_a" | "player_b">): Winner {
  if (forfeitSides.length === 1) {
    return forfeitSides[0] === "player_a" ? "player_b" : "player_a";
  }

  if (state.playerAScore > state.playerBScore) return "player_a";
  if (state.playerBScore > state.playerAScore) return "player_b";
  return "draw";
}

export function selectTimeoutBurnUnit(
  usedUnits: EmojiId[],
  matchId: string,
  turnNumber: number,
  playerSide: "player_a" | "player_b",
  userId: string,
): EmojiId | "" {
  const used = new Set(usedUnits);
  const remaining = [...EMOJI_CLASH_ROSTER].filter((unit) => !used.has(unit)).sort();
  if (remaining.length === 0) {
    return "";
  }

  const seed = stableHash(`${matchId}|${turnNumber}|${playerSide}|${userId}`);
  return remaining[seed % remaining.length];
}

function stableHash(value: string): number {
  let hash = 2166136261;
  for (let index = 0; index < value.length; index++) {
    hash ^= value.charCodeAt(index);
    hash = Math.imul(hash, 16777619);
  }

  return hash >>> 0;
}

function buildTimeoutReason(
  playerAUnitKey: EmojiId | "",
  playerBUnitKey: EmojiId | "",
  playerABurn: EmojiId | "",
  playerBBurn: EmojiId | "",
  outcome: EmojiClashTurnOutcome,
  turnValue: number,
): string {
  if (!playerAUnitKey && !playerBUnitKey) {
    return `Both players missed the turn. No points awarded. Player A lost ${toDisplayName(playerABurn)}. Player B lost ${toDisplayName(playerBBurn)}.`;
  }

  if (outcome === "player_a") {
    return `Player B missed the turn. Player A gained +${turnValue}. Player B lost ${toDisplayName(playerBBurn)}.`;
  }

  if (outcome === "player_b") {
    return `Player A missed the turn. Player B gained +${turnValue}. Player A lost ${toDisplayName(playerABurn)}.`;
  }

  return "Turn timer expired.";
}

function computePower(self: EmojiId, opponent: EmojiId, turnNumber: number, ownScore: number, opponentScore: number): number {
  const profile = profiles[self];
  return profile.basePower +
    computeMatchupBonus(profile, profiles[opponent]) +
    computeTimingBonus(profile, turnNumber) +
    (ownScore < opponentScore ? profile.behindBonus : 0);
}

function computeMatchupBonus(profile: EmojiClashProfile, opponent: EmojiClashProfile): number {
  const direct = headToHead[profile.unitKey]?.[opponent.unitKey] ?? 0;
  const tagBonus = opponent.tags.some((tag) => profile.counterTags.includes(tag)) ? 1 : 0;
  return direct + tagBonus;
}

function computeTimingBonus(profile: EmojiClashProfile, turnNumber: number): number {
  let total = 0;
  if (turnNumber <= 2) total += profile.earlyBonus;
  if (turnNumber >= 4) total += profile.lateBonus;
  if (turnNumber === EMOJI_CLASH_TOTAL_TURNS) total += profile.finalTurnBonus;
  return total;
}

function buildReason(playerAUnitKey: EmojiId, playerBUnitKey: EmojiId, powerA: number, powerB: number, outcome: EmojiClashTurnOutcome): string {
  if (outcome === "draw") {
    return `${toDisplayName(playerAUnitKey)} and ${toDisplayName(playerBUnitKey)} fought to a draw.`;
  }

  const winner = outcome === "player_a" ? playerAUnitKey : playerBUnitKey;
  const loser = outcome === "player_a" ? playerBUnitKey : playerAUnitKey;
  const margin = Math.abs(powerA - powerB);
  return margin >= 2
    ? `${toDisplayName(winner)} countered ${toDisplayName(loser).toLowerCase()}.`
    : `${toDisplayName(winner)} won the cleaner clash.`;
}
