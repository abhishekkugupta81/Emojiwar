import { calculateEloRating } from "../_shared/elo.ts";
import { jsonResponse, readJson } from "../_shared/http.ts";
import { insertRows, patchRows, selectRows } from "../_shared/supabase-admin.ts";

interface FinalizeMatchRequest {
  matchId?: string;
  playerAUserId?: string;
  playerBUserId?: string;
  playerARating?: number;
  playerBRating?: number;
  winner: "player_a" | "player_b" | "draw";
}

interface RatingRow {
  user_id: string;
  current_elo: number;
  wins: number;
  losses: number;
}

Deno.serve(async (request) => {
  if (request.method !== "POST") {
    return jsonResponse({ error: "Method not allowed" }, 405);
  }

  try {
    const payload = await readJson<FinalizeMatchRequest>(request);
    const scoreA = payload.winner === "player_a" ? 1 : payload.winner === "draw" ? 0.5 : 0;
    const scoreB = payload.winner === "player_b" ? 1 : payload.winner === "draw" ? 0.5 : 0;

    let playerARating = payload.playerARating ?? 1200;
    let playerBRating = payload.playerBRating ?? 1200;

    let playerARow: RatingRow | null = null;
    let playerBRow: RatingRow | null = null;

    if (payload.playerAUserId) {
      playerARow = (await selectRows<RatingRow>(
        `ratings?select=user_id,current_elo,wins,losses&user_id=eq.${payload.playerAUserId}&limit=1`,
      ))[0] ?? null;
      playerARating = playerARow?.current_elo ?? playerARating;
    }

    if (payload.playerBUserId) {
      playerBRow = (await selectRows<RatingRow>(
        `ratings?select=user_id,current_elo,wins,losses&user_id=eq.${payload.playerBUserId}&limit=1`,
      ))[0] ?? null;
      playerBRating = playerBRow?.current_elo ?? playerBRating;
    }

    const playerANextRating = calculateEloRating(playerARating, playerBRating, scoreA);
    const playerBNextRating = calculateEloRating(playerBRating, playerARating, scoreB);

    if (payload.playerAUserId) {
      const playerAWins = (playerARow?.wins ?? 0) + (payload.winner === "player_a" ? 1 : 0);
      const playerALosses = (playerARow?.losses ?? 0) + (payload.winner === "player_b" ? 1 : 0);
      if (playerARow) {
        await patchRows(`ratings?user_id=eq.${payload.playerAUserId}`, {
          current_elo: playerANextRating,
          wins: playerAWins,
          losses: playerALosses,
        });
      } else {
        await insertRows("ratings", {
          user_id: payload.playerAUserId,
          current_elo: playerANextRating,
          wins: playerAWins,
          losses: playerALosses,
        });
      }
    }

    if (payload.playerBUserId) {
      const playerBWins = (playerBRow?.wins ?? 0) + (payload.winner === "player_b" ? 1 : 0);
      const playerBLosses = (playerBRow?.losses ?? 0) + (payload.winner === "player_a" ? 1 : 0);
      if (playerBRow) {
        await patchRows(`ratings?user_id=eq.${payload.playerBUserId}`, {
          current_elo: playerBNextRating,
          wins: playerBWins,
          losses: playerBLosses,
        });
      } else {
        await insertRows("ratings", {
          user_id: payload.playerBUserId,
          current_elo: playerBNextRating,
          wins: playerBWins,
          losses: playerBLosses,
        });
      }
    }

    return jsonResponse({
      playerANextRating,
      playerBNextRating,
      persisted: !!payload.playerAUserId || !!payload.playerBUserId,
      matchId: payload.matchId ?? null,
    });
  } catch (error) {
    return jsonResponse({ error: error instanceof Error ? error.message : "Finalize match failed." }, 500);
  }
});
