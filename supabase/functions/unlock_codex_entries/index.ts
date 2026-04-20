import { jsonResponse, readJson } from "../_shared/http.ts";
import { insertRows, resolveRequestUserId } from "../_shared/supabase-admin.ts";

interface UnlockCodexEntriesRequest {
  userId: string;
  events: Array<{
    interactionKey: string;
    reasonCode: string;
    summary: string;
    tip: string;
  }>;
}

Deno.serve(async (request) => {
  if (request.method !== "POST") {
    return jsonResponse({ error: "Method not allowed" }, 405);
  }

  try {
    const payload = await readJson<UnlockCodexEntriesRequest>(request);
    const requestUserId = await resolveRequestUserId(request);
    if (payload.userId !== requestUserId) {
      return jsonResponse({ error: "Codex unlock request user does not match the authenticated session." }, 403);
    }

    const uniqueByKey = new Map<string, UnlockCodexEntriesRequest["events"][number]>();
    for (const event of payload.events) {
      uniqueByKey.set(`${event.interactionKey}:${event.reasonCode}`, event);
    }

    const unlocked: UnlockCodexEntriesRequest["events"] = [];

    for (const event of uniqueByKey.values()) {
      try {
        await insertRows("codex_unlocks", {
          user_id: requestUserId,
          interaction_key: event.interactionKey,
          reason_code: event.reasonCode,
          summary: event.summary,
          tip: event.tip,
        });
        unlocked.push(event);
      } catch {
        // Unique conflict means the Codex entry was already unlocked.
      }
    }

    return jsonResponse({
      userId: requestUserId,
      unlocked,
    });
  } catch (error) {
    return jsonResponse({ error: error instanceof Error ? error.message : "Unlock Codex entries failed." }, 500);
  }
});
