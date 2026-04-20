import type { CancelRankedQueueRequest } from "../_shared/contracts.ts";
import { jsonResponse, readJson } from "../_shared/http.ts";
import { patchRows, resolveRequestUserId, selectRows } from "../_shared/supabase-admin.ts";

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
    const payload = await readJson<CancelRankedQueueRequest>(request);
    const requestUserId = await resolveRequestUserId(request);
    if (!payload.userId) {
      return jsonResponse({ error: "Cancel queue request is missing userId." }, 400);
    }

    if (payload.userId !== requestUserId) {
      return jsonResponse({ error: "Cancel queue request user does not match the authenticated session." }, 403);
    }

    const matchFilter = payload.matchId
      ? `id=eq.${payload.matchId}&player_a=eq.${requestUserId}`
      : `mode=eq.pvp_ranked&status=eq.queued&player_a=eq.${requestUserId}&player_b=is.null&order=created_at.desc&limit=1`;

    const rows = await selectRows<MatchRow>(
      `matches?select=id,status,player_a,player_b,current_state&${matchFilter}`,
    );
    const match = rows[0];
    if (!match) {
      return jsonResponse({
        cancelled: false,
        matchId: payload.matchId ?? "",
        status: "not_found",
        note: "No queued ranked match was found for this account.",
      });
    }

    if (match.status !== "queued" || match.player_b) {
      return jsonResponse({
        cancelled: false,
        matchId: match.id,
        status: "not_queued",
        note: "The ranked row is no longer queued, so there was nothing to cancel.",
      });
    }

    const currentState = {
      ...(match.current_state ?? {}),
      phase: "queue_cancelled",
      cancelledAt: new Date().toISOString(),
      cancelledBy: requestUserId,
    };

    await patchRows<MatchRow>(
      `matches?id=eq.${match.id}`,
      {
        status: "cancelled",
        current_state: currentState,
      },
    );

    return jsonResponse({
      cancelled: true,
      matchId: match.id,
      status: "cancelled",
      note: "Queued ranked matchmaking was cancelled.",
    });
  } catch (error) {
    return jsonResponse({ error: error instanceof Error ? error.message : "Cancel queue failed." }, 500);
  }
});
