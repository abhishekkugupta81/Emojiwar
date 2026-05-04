import { jsonResponse, readJson } from "../_shared/http.ts";
import { resolveRequestUserId, selectRows } from "../_shared/supabase-admin.ts";

const CLASH_LADDER_MODE = "emoji_clash_pvp";

interface GetClashLeaderboardRequest {
  limit?: number;
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
  updated_at: string;
}

interface ProfileRow {
  user_id: string;
  display_name: string | null;
}

function fallbackHandle(rank: number): string {
  return `Challenger #${rank}`;
}

Deno.serve(async (request) => {
  if (request.method !== "POST") {
    return jsonResponse({ error: "Method not allowed" }, 405);
  }

  try {
    const payload = await readJson<GetClashLeaderboardRequest>(request).catch(() => ({ limit: 25 }));
    const requestUserId = await resolveRequestUserId(request);
    const requestedLimit = Number.isFinite(payload.limit) ? Math.trunc(payload.limit ?? 25) : 25;
    const limit = Math.max(1, Math.min(requestedLimit || 25, 100));
    const ratings = await selectRows<ModeRatingRow>(
      `mode_ratings?select=user_id,ladder_mode,rating,hidden_mmr,visible_points,last_visible_points_change_at,games_played,wins,losses,draws,timeout_forfeits,matches_vs_humans,matches_vs_bots,bot_fill_wins,bot_fill_losses,bot_fill_points_earned,updated_at&ladder_mode=eq.${CLASH_LADDER_MODE}&order=visible_points.desc,last_visible_points_change_at.asc,user_id.asc`,
    );

    const topRows = ratings.slice(0, limit);
    const myRow = ratings.find((row) => row.user_id === requestUserId) ?? null;
    const myRank = myRow ? ratings.findIndex((row) => row.user_id === requestUserId) + 1 : -1;
    const nearbyRows = myRank > 0 ? ratings.slice(Math.max(0, myRank - 3), Math.min(ratings.length, myRank + 2)) : [];
    const profileIds = [...new Set([
      ...topRows.map((row) => row.user_id),
      ...nearbyRows.map((row) => row.user_id),
      ...(myRow ? [myRow.user_id] : []),
    ])];
    const namesByUser = new Map<string, string>();
    if (profileIds.length > 0) {
      const profileRows = await selectRows<ProfileRow>(
        `profiles?select=user_id,display_name&user_id=in.(${profileIds.join(",")})`,
      );
      for (const row of profileRows) {
        if (row.display_name && row.display_name.trim()) {
          namesByUser.set(row.user_id, row.display_name.trim());
        }
      }
    }

    const buildEntry = (row: ModeRatingRow, rank: number) => ({
      rank,
      ladder_mode: row.ladder_mode,
      user_id: row.user_id,
      userId: row.user_id,
      display_handle: namesByUser.get(row.user_id) ?? fallbackHandle(rank),
      displayName: namesByUser.get(row.user_id) ?? fallbackHandle(rank),
      rating: row.rating,
      hidden_mmr: row.hidden_mmr ?? row.rating,
      hiddenMmr: row.hidden_mmr ?? row.rating,
      visible_points: row.visible_points ?? 0,
      visiblePoints: row.visible_points ?? 0,
      last_visible_points_change_at: row.last_visible_points_change_at ?? "",
      lastVisiblePointsChangeAt: row.last_visible_points_change_at ?? "",
      currentElo: row.hidden_mmr ?? row.rating,
      games_played: row.games_played,
      wins: row.wins,
      losses: row.losses,
      draws: row.draws,
      timeout_forfeits: row.timeout_forfeits,
      matches_vs_humans: row.matches_vs_humans ?? 0,
      matches_vs_bots: row.matches_vs_bots ?? 0,
      bot_fill_wins: row.bot_fill_wins ?? 0,
      bot_fill_losses: row.bot_fill_losses ?? 0,
      bot_fill_points_earned: row.bot_fill_points_earned ?? 0,
      updated_at: row.updated_at,
      isCurrentUser: row.user_id === requestUserId,
    });

    return jsonResponse({
      ladder_mode: CLASH_LADDER_MODE,
      entries: topRows.map((row, index) => buildEntry(row, index + 1)),
      nearbyEntries: myRank > 0
        ? nearbyRows.map((row, index) => buildEntry(row, Math.max(0, myRank - 3) + index + 1))
        : [],
      myEntry: myRow ? buildEntry(myRow, myRank) : null,
      totalRatedPlayers: ratings.length,
      note: ratings.length === 0
        ? "No Quick Clash PvP results yet."
        : "Quick Clash PvP standings.",
    });
  } catch (error) {
    return jsonResponse({ error: error instanceof Error ? error.message : "Quick Clash leaderboard lookup failed." }, 500);
  }
});
