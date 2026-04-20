import type { CodexUnlockEntry, GetCodexRequest, GetCodexResponse } from "../_shared/contracts.ts";
import { jsonResponse, readJson } from "../_shared/http.ts";
import { resolveRequestUserId, selectRows } from "../_shared/supabase-admin.ts";

interface CodexRow {
  interaction_key: string;
  reason_code: string;
  summary: string | null;
  tip: string | null;
  unlocked_at: string;
}

function mapEntry(row: CodexRow): CodexUnlockEntry {
  return {
    interaction_key: row.interaction_key,
    reason_code: row.reason_code,
    summary: row.summary?.trim() || "Unlocked interaction.",
    tip: row.tip?.trim() || "Experiment with counters and formation to find the answer.",
    unlocked_at: row.unlocked_at,
  };
}

Deno.serve(async (request) => {
  if (request.method !== "POST") {
    return jsonResponse({ error: "Method not allowed" }, 405);
  }

  try {
    const requestUserId = await resolveRequestUserId(request);
    const payload = await readJson<GetCodexRequest>(request).catch(() => ({ limit: 50 }));
    const requestedLimit = Number.isFinite(payload.limit) ? Math.trunc(payload.limit ?? 50) : 50;
    const limit = Math.max(1, Math.min(requestedLimit || 50, 100));

    const rows = await selectRows<CodexRow>(
      `codex_unlocks?select=interaction_key,reason_code,summary,tip,unlocked_at&user_id=eq.${requestUserId}&order=unlocked_at.desc`,
    );

    const entries = rows.slice(0, limit).map(mapEntry);
    const response: GetCodexResponse = {
      entries,
      totalUnlocked: rows.length,
      latestEntry: rows.length > 0 ? mapEntry(rows[0]) : null,
      note: rows.length > 0
        ? "Unlocked interactions, WHY summaries, and counter tips."
        : "Play bot or ranked battles to unlock interaction notes and counter tips.",
    };

    return jsonResponse(response);
  } catch (error) {
    return jsonResponse({ error: error instanceof Error ? error.message : "Codex lookup failed." }, 500);
  }
});
