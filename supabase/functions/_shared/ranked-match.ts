import type { EmojiId, Formation, MatchMode } from "./contracts.ts";
import { buildDefaultFormation, selectBattleTeamFromDeck } from "./battle-simulator.ts";

export const QUEUE_TTL_SECONDS = 120;
export const BAN_TTL_SECONDS = 30;
export const FORMATION_TTL_SECONDS = 45;

export type RankedPhase = "queue" | "ban" | "formation" | "finished";

export interface RankedCurrentState {
  phase?: RankedPhase | "resolving";
  queueTicket?: string;
  battleSeed?: string;
  playerDeckA?: EmojiId[];
  playerDeckB?: EmojiId[];
  finalTeamA?: EmojiId[];
  finalTeamB?: EmojiId[];
  formationA?: Formation;
  formationB?: Formation;
  battleState?: unknown;
  whySummary?: string;
  whyChain?: string[];
  systemNote?: string;
}

export interface RankedLifecycleRow {
  id: string;
  mode: MatchMode;
  status: string;
  updated_at: string;
  rules_version: string | null;
  player_a: string;
  player_b: string | null;
  winner: string | null;
  deck_a: EmojiId[];
  deck_b: EmojiId[];
  bans?: {
    player_a?: EmojiId;
    player_b?: EmojiId;
  };
  current_state: RankedCurrentState;
}

const AUTO_BAN_PRIORITY: EmojiId[] = [
  "bomb",
  "magnet",
  "ghost",
  "heart",
  "plant",
  "mirror",
  "chain",
  "wind",
  "snake",
  "lightning",
  "ice",
  "shield",
  "hole",
  "soap",
  "water",
  "fire",
];

export function normalizeRankedPhase(match: RankedLifecycleRow): RankedPhase {
  const rawPhase = match.current_state?.phase;
  if (rawPhase === "queue" || rawPhase === "ban" || rawPhase === "formation" || rawPhase === "finished") {
    return rawPhase;
  }

  if (match.status === "finished" || match.status === "resolving" || !!match.winner || !!match.current_state?.battleState) {
    return "finished";
  }

  if (match.status === "formation") {
    return "formation";
  }

  if (match.status === "banning") {
    return "ban";
  }

  return "queue";
}

export function isResumableActiveMatch(match: RankedLifecycleRow | null | undefined): boolean {
  if (!match || match.status === "cancelled") {
    return false;
  }

  if (normalizeRankedPhase(match) === "queue" && match.status !== "queued") {
    return false;
  }

  if ((match.status === "banning" || match.status === "formation") && !match.player_b) {
    return false;
  }

  return true;
}

export function removeBannedEmoji(deck: EmojiId[], bannedEmojiId?: EmojiId | null): EmojiId[] {
  return bannedEmojiId == null ? [...deck] : deck.filter((emojiId) => emojiId !== bannedEmojiId);
}

export function chooseAutomaticBan(targetDeck: EmojiId[]): EmojiId | null {
  for (const emojiId of AUTO_BAN_PRIORITY) {
    if (targetDeck.includes(emojiId)) {
      return emojiId;
    }
  }

  return targetDeck[0] ?? null;
}

export function computeFinalTeams(match: RankedLifecycleRow): { finalTeamA: EmojiId[]; finalTeamB: EmojiId[] } {
  const finalTeamA = selectBattleTeamFromDeck(removeBannedEmoji(match.deck_a, match.bans?.player_b), 5).team;
  const finalTeamB = selectBattleTeamFromDeck(removeBannedEmoji(match.deck_b, match.bans?.player_a), 5).team;
  return { finalTeamA, finalTeamB };
}

export function ensureFormation(formation: Formation | null | undefined, finalTeam: EmojiId[]): Formation {
  if (isFormationComplete(formation, finalTeam)) {
    return formation as Formation;
  }

  return buildDefaultFormation(finalTeam);
}

export function isFormationComplete(formation: Formation | null | undefined, expectedTeam?: EmojiId[]): boolean {
  if (!formation || !Array.isArray(formation.placements) || formation.placements.length !== 5) {
    return false;
  }

  if (!expectedTeam || expectedTeam.length !== 5) {
    return true;
  }

  const expected = new Set(expectedTeam);
  return formation.placements.every((placement) => expected.has(placement.emojiId));
}

export function phaseTimeoutSeconds(match: RankedLifecycleRow): number {
  switch (normalizeRankedPhase(match)) {
    case "queue":
      return QUEUE_TTL_SECONDS;
    case "ban":
      return BAN_TTL_SECONDS;
    case "formation":
      return FORMATION_TTL_SECONDS;
    default:
      return 0;
  }
}

export function phaseDeadlineAt(match: RankedLifecycleRow): string {
  const timeoutSeconds = phaseTimeoutSeconds(match);
  if (timeoutSeconds <= 0) {
    return "";
  }

  const updatedAt = Date.parse(match.updated_at);
  if (Number.isNaN(updatedAt)) {
    return "";
  }

  return new Date(updatedAt + (timeoutSeconds * 1000)).toISOString();
}

export function phaseTimeoutSecondsRemaining(match: RankedLifecycleRow): number {
  const deadline = phaseDeadlineAt(match);
  if (!deadline || deadline.trim().length == 0) {
    return 0;
  }

  const remainingMs = Date.parse(deadline) - Date.now();
  return remainingMs <= 0 ? 0 : Math.ceil(remainingMs / 1000);
}
