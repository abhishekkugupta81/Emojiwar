import { jsonResponse, readJson } from "../_shared/http.ts";
import { patchRows, resolveRequestUserId, selectRows } from "../_shared/supabase-admin.ts";

interface CancelClashQueueRequest {
  userId: string;
  matchId?: string;
}

interface MatchRow {
  id: string;
  status: string;
  player_a: string;
  player_b: string | null;
  current_state: Record<string, unknown> | null;
}

Deno.serve(async (request) => {
  if (request.method !== "POST") {
    return jsonResponse({ error: "Method not allowed" }, 405);
  }

  try {
    const payload = await readJson<CancelClashQueueRequest>(request);
    const requestUserId = await resolveRequestUserId(request);
    if (!payload.userId) {
      return jsonResponse({ error: "Cancel Quick Clash queue request is missing userId." }, 400);
    }

    if (payload.userId !== requestUserId) {
      return jsonResponse({ error: "Cancel Quick Clash queue user does not match the authenticated session." }, 403);
    }

    const filter = payload.matchId
      ? `id=eq.${payload.matchId}&mode=eq.emoji_clash_pvp&player_a=eq.${requestUserId}`
      : `mode=eq.emoji_clash_pvp&status=eq.queued&player_a=eq.${requestUserId}&player_b=is.null&order=created_at.desc&limit=1`;
    const rows = await selectRows<MatchRow>(`matches?select=id,status,player_a,player_b,current_state&${filter}`);
    const match = rows[0];
    if (!match) {
      return jsonResponse({ cancelled: false, matchId: payload.matchId ?? "", status: "not_found", note: "No queued Quick Clash match was found." });
    }

    if (match.status !== "queued" || match.player_b) {
      return jsonResponse({ cancelled: false, matchId: match.id, status: "not_queued", note: "The Quick Clash row is no longer queued." });
    }

    await patchRows<MatchRow>(
      `matches?id=eq.${match.id}`,
      {
        status: "cancelled",
        current_state: {
          ...(match.current_state ?? {}),
          phase: "queue_cancelled",
          cancelledAt: new Date().toISOString(),
          cancelledBy: requestUserId,
        },
      },
    );

    return jsonResponse({ cancelled: true, matchId: match.id, status: "cancelled", note: "Queued Quick Clash matchmaking was cancelled." });
  } catch (error) {
    return jsonResponse({ error: error instanceof Error ? error.message : "Cancel Quick Clash queue failed." }, 500);
  }
});
