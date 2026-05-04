import type { Winner } from "./contracts.ts";
import type { EmojiClashPublicState } from "./emoji-clash-rules.ts";
import { calculateEloRating } from "./elo.ts";
import { insertRows, patchRows, selectRows } from "./supabase-admin.ts";
import {
  CLASH_INITIAL_HIDDEN_MMR,
  CLASH_INITIAL_VISIBLE_POINTS,
  type ClashLadderOpponentType,
  type ClashLadderResult,
  visibleDeltaForClashResult,
} from "./clash-ladder-policy.ts";

const CLASH_LADDER_MODE = "emoji_clash_pvp";
const CLASH_K_FACTOR = 32;

interface ClashRatingMatch {
  id: string;
  mode: string;
  player_a: string;
  player_b: string | null;
  bot_profile_id?: string | null;
  winner: string | null;
  current_state: EmojiClashPublicState | null;
  ladder_applied_at?: string | null;
}

interface ModeRatingRow {
  user_id: string;
  ladder_mode: string;
  rating: number;
  hidden_mmr?: number;
  visible_points?: number;
  last_visible_points_change_at?: string | null;
  games_played: number;
  wins: number;
  losses: number;
  draws: number;
  timeout_forfeits: number;
  matches_vs_humans?: number;
  matches_vs_bots?: number;
  bot_fill_wins?: number;
  bot_fill_losses?: number;
  bot_fill_points_earned?: number;
}

export async function persistClashLadderOutcome(match: ClashRatingMatch): Promise<boolean> {
  if (match.mode !== CLASH_LADDER_MODE || !match.current_state || match.current_state.phase !== "finished") {
    return false;
  }

  const opponentType: ClashLadderOpponentType = isBotFillMatch(match) ? "bot_fill" : "human";
  if (opponentType === "human" && !match.player_b) {
    return false;
  }

  const guardRows = await patchRows<ClashRatingMatch>(
    `matches?id=eq.${match.id}&ladder_applied_at=is.null`,
    { ladder_applied_at: new Date().toISOString() },
  );
  if (guardRows.length === 0) {
    return false;
  }

  const winner = resolveWinner(match);
  const selectColumns = "user_id,ladder_mode,rating,hidden_mmr,visible_points,last_visible_points_change_at,games_played,wins,losses,draws,timeout_forfeits,matches_vs_humans,matches_vs_bots,bot_fill_wins,bot_fill_losses,bot_fill_points_earned";
  const userIds = opponentType === "human" ? `${match.player_a},${match.player_b}` : match.player_a;
  const ratingRows = await selectRows<ModeRatingRow>(
    `mode_ratings?select=${selectColumns}&ladder_mode=eq.${CLASH_LADDER_MODE}&user_id=in.(${userIds})`,
  );
  const byUser = new Map(ratingRows.map((row) => [row.user_id, row]));
  const rowA = byUser.get(match.player_a) ?? null;
  const scoreA = winner === "player_a" ? 1 : winner === "draw" ? 0.5 : 0;

  const forfeitSides = new Set(match.current_state.forfeitSides ?? []);
  const resultA = resultForSide("player_a", winner, forfeitSides);
  if (opponentType === "bot_fill") {
    await upsertModeRating(match.player_a, rowA, {
      matchId: match.id,
      result: resultA,
      opponentType,
      hiddenMmr: getHiddenMmr(rowA),
      visibleDelta: visibleDeltaForClashResult(resultA, opponentType),
      timeoutForfeited: forfeitSides.has("player_a"),
      botProfileId: match.bot_profile_id ?? match.current_state.botProfileId ?? "",
    });
    return true;
  }

  const rowB = byUser.get(match.player_b ?? "") ?? null;
  const ratingA = getHiddenMmr(rowA);
  const ratingB = getHiddenMmr(rowB);
  const scoreB = winner === "player_b" ? 1 : winner === "draw" ? 0.5 : 0;
  const nextA = calculateEloRating(ratingA, ratingB, scoreA, CLASH_K_FACTOR);
  const nextB = calculateEloRating(ratingB, ratingA, scoreB, CLASH_K_FACTOR);
  const resultB = resultForSide("player_b", winner, forfeitSides);

  await upsertModeRating(match.player_a, rowA, {
    matchId: match.id,
    result: resultA,
    opponentType,
    hiddenMmr: nextA,
    visibleDelta: visibleDeltaForClashResult(resultA, opponentType),
    timeoutForfeited: forfeitSides.has("player_a"),
  });
  await upsertModeRating(match.player_b ?? "", rowB, {
    matchId: match.id,
    result: resultB,
    opponentType,
    hiddenMmr: nextB,
    visibleDelta: visibleDeltaForClashResult(resultB, opponentType),
    timeoutForfeited: forfeitSides.has("player_b"),
  });

  return true;
}

function resolveWinner(match: ClashRatingMatch): Winner {
  if (match.winner === match.player_a) return "player_a";
  if (match.winner === match.player_b) return "player_b";
  return match.current_state?.winner ?? "draw";
}

function resultForSide(side: "player_a" | "player_b", winner: Winner, forfeitSides: Set<string>): ClashLadderResult {
  if (winner === "draw") {
    return "draw";
  }

  if (winner === side) {
    return "win";
  }

  return forfeitSides.has(side) ? "timeout_forfeit" : "loss";
}

interface RatingPatchOptions {
  matchId: string;
  result: ClashLadderResult;
  opponentType: ClashLadderOpponentType;
  hiddenMmr: number;
  visibleDelta: number;
  timeoutForfeited: boolean;
  botProfileId?: string;
}

async function upsertModeRating(
  userId: string,
  existing: ModeRatingRow | null,
  options: RatingPatchOptions,
) {
  const oldHiddenMmr = getHiddenMmr(existing);
  const oldVisiblePoints = getVisiblePoints(existing);
  const nextHiddenMmr = options.hiddenMmr;
  const nextVisiblePoints = oldVisiblePoints + options.visibleDelta;
  const botWin = options.opponentType === "bot_fill" && options.result === "win";
  const botLoss = options.opponentType === "bot_fill" && (options.result === "loss" || options.result === "timeout_forfeit");
  const next = {
    user_id: userId,
    ladder_mode: CLASH_LADDER_MODE,
    rating: nextHiddenMmr,
    hidden_mmr: nextHiddenMmr,
    visible_points: nextVisiblePoints,
    last_visible_points_change_at: options.visibleDelta !== 0
      ? new Date().toISOString()
      : existing?.last_visible_points_change_at ?? null,
    games_played: (existing?.games_played ?? 0) + 1,
    wins: (existing?.wins ?? 0) + (options.result === "win" ? 1 : 0),
    losses: (existing?.losses ?? 0) + (options.result === "loss" || options.result === "timeout_forfeit" ? 1 : 0),
    draws: (existing?.draws ?? 0) + (options.result === "draw" ? 1 : 0),
    timeout_forfeits: (existing?.timeout_forfeits ?? 0) + (options.timeoutForfeited ? 1 : 0),
    matches_vs_humans: (existing?.matches_vs_humans ?? 0) + (options.opponentType === "human" ? 1 : 0),
    matches_vs_bots: (existing?.matches_vs_bots ?? 0) + (options.opponentType === "bot_fill" ? 1 : 0),
    bot_fill_wins: (existing?.bot_fill_wins ?? 0) + (botWin ? 1 : 0),
    bot_fill_losses: (existing?.bot_fill_losses ?? 0) + (botLoss ? 1 : 0),
    bot_fill_points_earned: (existing?.bot_fill_points_earned ?? 0) + (options.opponentType === "bot_fill" ? options.visibleDelta : 0),
  };

  if (existing) {
    await patchRows(
      `mode_ratings?user_id=eq.${userId}&ladder_mode=eq.${CLASH_LADDER_MODE}`,
      next,
    );
  } else {
    await insertRows("mode_ratings", next);
  }

  await insertRows("mode_rating_events", {
    match_id: options.matchId,
    user_id: userId,
    ladder_mode: CLASH_LADDER_MODE,
    result: options.result,
    opponent_type: options.opponentType,
    old_rating: oldHiddenMmr,
    new_rating: nextHiddenMmr,
    delta: nextHiddenMmr - oldHiddenMmr,
    old_hidden_mmr: oldHiddenMmr,
    new_hidden_mmr: nextHiddenMmr,
    hidden_mmr_delta: nextHiddenMmr - oldHiddenMmr,
    old_visible_points: oldVisiblePoints,
    new_visible_points: nextVisiblePoints,
    visible_delta: options.visibleDelta,
    bot_profile_id: options.botProfileId ?? null,
  });
}

function isBotFillMatch(match: ClashRatingMatch): boolean {
  return match.current_state?.opponentType === "bot_fill" || !!match.bot_profile_id || !!match.current_state?.botProfileId;
}

function getHiddenMmr(row: ModeRatingRow | null): number {
  return row?.hidden_mmr ?? row?.rating ?? CLASH_INITIAL_HIDDEN_MMR;
}

function getVisiblePoints(row: ModeRatingRow | null): number {
  return row?.visible_points ?? CLASH_INITIAL_VISIBLE_POINTS;
}
