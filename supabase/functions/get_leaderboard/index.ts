import type { GetLeaderboardRequest, GetLeaderboardResponse, LeaderboardEntry } from "../_shared/contracts.ts";
import { jsonResponse, readJson } from "../_shared/http.ts";
import { resolveRequestUserId, selectRows } from "../_shared/supabase-admin.ts";

interface RatingRow {
  user_id: string;
  current_elo: number;
  wins: number;
  losses: number;
  updated_at: string;
}

interface ProfileRow {
  user_id: string;
  display_name: string | null;
}

function buildFallbackName(userId: string): string {
  return `Player-${userId.slice(0, 8)}`;
}

function buildEntry(row: RatingRow, rank: number, requestUserId: string, namesByUser: Map<string, string>): LeaderboardEntry {
  return {
    rank,
    userId: row.user_id,
    displayName: namesByUser.get(row.user_id) ?? buildFallbackName(row.user_id),
    currentElo: row.current_elo,
    wins: row.wins,
    losses: row.losses,
    isCurrentUser: row.user_id === requestUserId,
  };
}

Deno.serve(async (request) => {
  if (request.method !== "POST") {
    return jsonResponse({ error: "Method not allowed" }, 405);
  }

  try {
    const payload = await readJson<GetLeaderboardRequest>(request).catch(() => ({ limit: 25 }));
    const requestUserId = await resolveRequestUserId(request);
    const requestedLimit = Number.isFinite(payload.limit) ? Math.trunc(payload.limit ?? 25) : 25;
    const limit = Math.max(1, Math.min(requestedLimit || 25, 100));

    const ratings = await selectRows<RatingRow>(
      "ratings?select=user_id,current_elo,wins,losses,updated_at&order=current_elo.desc,updated_at.asc",
    );

    if (ratings.length === 0) {
      const emptyResponse: GetLeaderboardResponse = {
        entries: [],
        myEntry: null,
        totalRatedPlayers: 0,
        note: "No ranked results yet. Finish ranked battles to appear on the leaderboard.",
      };
      return jsonResponse(emptyResponse);
    }

    const topRows = ratings.slice(0, limit);
    const myRow = ratings.find((row) => row.user_id === requestUserId) ?? null;
    const myRank = myRow ? ratings.findIndex((row) => row.user_id === myRow.user_id) + 1 : -1;
    const nearbyRows = myEntryWindow(ratings, myRank);
    const requestedProfileIds = [...new Set([
      ...topRows.map((row) => row.user_id),
      ...nearbyRows.map((row) => row.user_id),
      ...(myRow ? [myRow.user_id] : []),
    ])];

    const namesByUser = new Map<string, string>();
    if (requestedProfileIds.length > 0) {
      const profileRows = await selectRows<ProfileRow>(
        `profiles?select=user_id,display_name&user_id=in.(${requestedProfileIds.join(",")})`,
      );

      for (const row of profileRows) {
        if (row.display_name && row.display_name.trim().length > 0) {
          namesByUser.set(row.user_id, row.display_name.trim());
        }
      }
    }

    const entries = topRows.map((row, index) => buildEntry(row, index + 1, requestUserId, namesByUser));
    const myEntry = myRow
      ? buildEntry(myRow, myRank, requestUserId, namesByUser)
      : null;
    const nearbyEntries = myEntry
      ? nearbyRows
        .map((row, index) => buildEntry(row, Math.max(0, myRank - 3) + index + 1, requestUserId, namesByUser))
      : [];

    const response: GetLeaderboardResponse = {
      entries,
      nearbyEntries,
      myEntry,
      totalRatedPlayers: ratings.length,
      note: myEntry
        ? "Top players plus your current ranked standing."
        : "Top players shown. Finish a ranked match to add your own standing.",
    };

    return jsonResponse(response);
  } catch (error) {
    return jsonResponse({ error: error instanceof Error ? error.message : "Leaderboard lookup failed." }, 500);
  }
});

function myEntryWindow(ratings: RatingRow[], myRank: number): RatingRow[] {
  if (myRank <= 0) {
    return [];
  }

  return ratings.slice(Math.max(0, myRank - 3), Math.min(ratings.length, myRank + 2));
}
