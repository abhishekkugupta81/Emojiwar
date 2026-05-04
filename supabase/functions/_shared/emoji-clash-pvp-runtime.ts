import type { EmojiId, MatchMode, Winner } from "./contracts.ts";
import {
  createInitialClashState,
  EMOJI_CLASH_RULES_VERSION,
  EMOJI_CLASH_TOTAL_TURNS,
  resolveTimeoutTurn,
} from "./emoji-clash-rules.ts";
import type { EmojiClashPublicState } from "./emoji-clash-rules.ts";
import { ensureClashBotTurn, fetchClashBotTurn, isBotFillState } from "./clash-bot-fill-runtime.ts";
import { patchRows, selectRows } from "./supabase-admin.ts";

export interface EmojiClashMatchRow {
  id: string;
  mode: MatchMode | "emoji_clash_pvp";
  status: string;
  rules_version: string | null;
  updated_at?: string;
  player_a: string;
  player_b: string | null;
  bot_profile_id?: string | null;
  winner: string | null;
  current_state: EmojiClashPublicState | null;
  ladder_applied_at?: string | null;
}

export interface EmojiClashTurnRow {
  match_id: string;
  round_number: number;
  player_id: string;
  emoji_id: EmojiId;
}

export function ensureClashState(match: EmojiClashMatchRow): EmojiClashPublicState {
  const state = match.current_state ?? createInitialClashState(match.id);
  return {
    phase: state.phase ?? "queue",
    queueTicket: state.queueTicket ?? match.id,
    opponentType: state.opponentType ?? "human",
    botProfileId: state.botProfileId,
    botFillReason: state.botFillReason,
    opponentDisplayName: state.opponentDisplayName,
    opponentAvatarKey: state.opponentAvatarKey,
    currentTurnIndex: state.currentTurnIndex ?? 0,
    playerAScore: state.playerAScore ?? 0,
    playerBScore: state.playerBScore ?? 0,
    playerAUsedUnits: state.playerAUsedUnits ?? [],
    playerBUsedUnits: state.playerBUsedUnits ?? [],
    matchmakingRatingAtQueue: state.matchmakingRatingAtQueue,
    queueStartedAt: state.queueStartedAt,
    queueExpiresAt: state.queueExpiresAt,
    playerATimeoutStrikes: state.playerATimeoutStrikes ?? 0,
    playerBTimeoutStrikes: state.playerBTimeoutStrikes ?? 0,
    turnHistory: state.turnHistory ?? [],
    turnDeadlineAt: state.turnDeadlineAt ?? "",
    winner: state.winner ?? null,
    finishReason: state.finishReason ?? "",
    forfeitSides: state.forfeitSides ?? [],
    systemNote: state.systemNote ?? "",
    cancelledAt: state.cancelledAt,
    cancelledBy: state.cancelledBy,
  };
}

export async function fetchClashTurnRows(matchId: string, turnNumber: number): Promise<EmojiClashTurnRow[]> {
  return await selectRows<EmojiClashTurnRow>(
    `turns?select=match_id,round_number,player_id,emoji_id&match_id=eq.${matchId}&round_number=eq.${turnNumber}`,
  );
}

export function winnerUserId(match: EmojiClashMatchRow, winner: Winner): string | null {
  if (winner === "player_a") return match.player_a;
  if (winner === "player_b") return match.player_b ?? null;
  return null;
}

export async function advanceExpiredClashTurnIfNeeded(
  match: EmojiClashMatchRow,
): Promise<{ match: EmojiClashMatchRow; advanced: boolean; note: string }> {
  if (match.status !== "pick") {
    return { match, advanced: false, note: match.current_state?.systemNote ?? "" };
  }

  const state = ensureClashState(match);
  if (state.phase !== "pick" || state.currentTurnIndex >= EMOJI_CLASH_TOTAL_TURNS || !state.turnDeadlineAt) {
    return { match, advanced: false, note: state.systemNote ?? "" };
  }

  if (Date.parse(state.turnDeadlineAt) > Date.now()) {
    return { match, advanced: false, note: state.systemNote ?? "" };
  }

  const turnNumber = state.currentTurnIndex + 1;
  if (isBotFillState(state)) {
    await ensureClashBotTurn(match.id, state);
  }

  const turnRows = await fetchClashTurnRows(match.id, turnNumber);
  const pickA = turnRows.find((turn) => turn.player_id === match.player_a)?.emoji_id ?? "";
  const botTurn = isBotFillState(state) ? await fetchClashBotTurn(match.id, turnNumber) : null;
  const pickB = match.player_b ? turnRows.find((turn) => turn.player_id === match.player_b)?.emoji_id ?? "" : botTurn?.emoji_id ?? "";
  const nextState = resolveTimeoutTurn(state, pickA, pickB, match.id, match.player_a, match.player_b ?? state.botProfileId ?? "player_b");
  nextState.systemNote = buildTimeoutSystemNote(nextState);
  const finished = nextState.phase === "finished";
  const patched = await patchRows<EmojiClashMatchRow>(
    `matches?id=eq.${match.id}`,
    {
      status: finished ? "finished" : "pick",
      winner: finished ? winnerUserId(match, nextState.winner ?? "draw") : null,
      current_state: nextState,
      rules_version: EMOJI_CLASH_RULES_VERSION,
    },
  );

  return { match: patched[0], advanced: true, note: nextState.systemNote ?? "" };
}

function buildTimeoutSystemNote(state: EmojiClashPublicState): string {
  const latest = state.turnHistory[state.turnHistory.length - 1];
  if (!latest) {
    return "Turn timer expired.";
  }

  if (state.finishReason && state.finishReason.includes("timeout_forfeit")) {
    return "Timeout forfeit resolved the match.";
  }

  return latest.reason || "Turn timer expired.";
}
