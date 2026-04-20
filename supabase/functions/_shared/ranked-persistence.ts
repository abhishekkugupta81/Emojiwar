import type { CodexEvent, MatchMode, Winner } from "./contracts.ts";
import { calculateEloRating } from "./elo.ts";
import { insertRows, patchRows, selectRows } from "./supabase-admin.ts";

interface RatingRow {
  user_id: string;
  current_elo: number;
  wins: number;
  losses: number;
}

export interface PersistableRankedMatch {
  mode: MatchMode;
  player_a: string;
  player_b: string | null;
}

export function winnerUserId(match: PersistableRankedMatch, winner: Winner): string | null {
  if (winner === "player_a") {
    return match.player_a;
  }

  if (winner === "player_b") {
    return match.player_b;
  }

  return null;
}

async function upsertRating(userId: string, nextElo: number, wins: number, losses: number) {
  const existing = (await selectRows<RatingRow>(
    `ratings?select=user_id,current_elo,wins,losses&user_id=eq.${userId}&limit=1`,
  ))[0] ?? null;

  if (existing) {
    await patchRows(`ratings?user_id=eq.${userId}`, {
      current_elo: nextElo,
      wins,
      losses,
    });
    return;
  }

  await insertRows("ratings", {
    user_id: userId,
    current_elo: nextElo,
    wins,
    losses,
  });
}

export async function persistRankedOutcome(match: PersistableRankedMatch, winner: Winner) {
  if (match.mode !== "pvp_ranked" || !match.player_b) {
    return;
  }

  const ratingRows = await selectRows<RatingRow>(
    `ratings?select=user_id,current_elo,wins,losses&user_id=in.(${match.player_a},${match.player_b})`,
  );
  const ratingByUser = new Map(ratingRows.map((row) => [row.user_id, row]));
  const playerARow = ratingByUser.get(match.player_a) ?? null;
  const playerBRow = ratingByUser.get(match.player_b) ?? null;

  const scoreA = winner === "player_a" ? 1 : winner === "draw" ? 0.5 : 0;
  const scoreB = winner === "player_b" ? 1 : winner === "draw" ? 0.5 : 0;

  const playerARating = playerARow?.current_elo ?? 1200;
  const playerBRating = playerBRow?.current_elo ?? 1200;
  const playerANextRating = calculateEloRating(playerARating, playerBRating, scoreA);
  const playerBNextRating = calculateEloRating(playerBRating, playerARating, scoreB);

  await upsertRating(
    match.player_a,
    playerANextRating,
    (playerARow?.wins ?? 0) + (winner === "player_a" ? 1 : 0),
    (playerARow?.losses ?? 0) + (winner === "player_b" ? 1 : 0),
  );
  await upsertRating(
    match.player_b,
    playerBNextRating,
    (playerBRow?.wins ?? 0) + (winner === "player_b" ? 1 : 0),
    (playerBRow?.losses ?? 0) + (winner === "player_a" ? 1 : 0),
  );
}

export async function persistCodexUnlocks(match: PersistableRankedMatch, codexEvents: CodexEvent[]) {
  const recipients = [match.player_a, match.player_b].filter((value): value is string => !!value);

  for (const userId of recipients) {
    for (const event of codexEvents) {
      try {
        await insertRows("codex_unlocks", {
          user_id: userId,
          interaction_key: event.interactionKey,
          reason_code: event.reasonCode,
          summary: event.summary,
          tip: event.tip,
        });
      } catch {
        // Duplicate Codex unlocks are expected across repeated matchups.
      }
    }
  }
}
