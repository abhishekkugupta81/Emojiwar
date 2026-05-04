import type { MatchMode } from "../_shared/contracts.ts";
import {
  EMOJI_CLASH_RULES_VERSION,
  EMOJI_CLASH_TOTAL_TURNS,
  isEmojiClashUnit,
  resolvePickedTurn,
} from "../_shared/emoji-clash-rules.ts";
import type { EmojiClashPublicState } from "../_shared/emoji-clash-rules.ts";
import {
  advanceExpiredClashTurnIfNeeded,
  fetchClashTurnRows,
  ensureClashState,
  winnerUserId,
} from "../_shared/emoji-clash-pvp-runtime.ts";
import type { EmojiClashMatchRow, EmojiClashTurnRow } from "../_shared/emoji-clash-pvp-runtime.ts";
import { ensureClashBotTurn, fetchClashBotTurn, isBotFillState } from "../_shared/clash-bot-fill-runtime.ts";
import { persistClashLadderOutcome } from "../_shared/clash-ladder-persistence.ts";
import { jsonResponse, readJson } from "../_shared/http.ts";
import { insertRows, patchRows, resolveRequestUserId, selectRows } from "../_shared/supabase-admin.ts";

interface SubmitClashPickRequest {
  matchId: string;
  playerId: string;
  turnNumber: number;
  emojiId: string;
}

interface MatchRow {
  id: string;
  mode: MatchMode | "emoji_clash_pvp";
  status: string;
  rules_version: string | null;
  player_a: string;
  player_b: string | null;
  bot_profile_id?: string | null;
  winner: string | null;
  current_state: EmojiClashPublicState | null;
}

Deno.serve(async (request) => {
  if (request.method !== "POST") {
    return jsonResponse({ error: "Method not allowed" }, 405);
  }

  try {
    const payload = await readJson<SubmitClashPickRequest>(request);
    const requestUserId = await resolveRequestUserId(request);
    if (!payload.matchId || !payload.playerId || !payload.emojiId || payload.turnNumber <= 0) {
      return jsonResponse({ error: "Quick Clash pick request is missing matchId, playerId, turnNumber, or emojiId." }, 400);
    }

    if (payload.playerId !== requestUserId) {
      return jsonResponse({ error: "Quick Clash pick player does not match the authenticated session." }, 403);
    }

    const unitKey = payload.emojiId.trim().toLowerCase();
    if (!isEmojiClashUnit(unitKey)) {
      return jsonResponse({ error: "Unsupported Emoji Clash unit." }, 400);
    }

    const rows = await selectRows<MatchRow>(
      `matches?select=id,mode,status,rules_version,player_a,player_b,bot_profile_id,winner,current_state&id=eq.${payload.matchId}&mode=eq.emoji_clash_pvp&limit=1`,
    );
    const match = rows[0];
    if (!match) {
      return jsonResponse({ error: "Quick Clash match not found." }, 404);
    }

    const initialState = ensureClashState(match as EmojiClashMatchRow);
    const isBotFill = isBotFillState(initialState);
    const actorSide = match.player_a === requestUserId
      ? "player_a"
      : match.player_b === requestUserId
        ? "player_b"
        : null;
    if (!actorSide) {
      return jsonResponse({ error: "Authenticated user is not part of this Quick Clash match." }, 403);
    }

    if (match.status === "finished") {
      return jsonResponse({ accepted: true, matchId: match.id, status: "finished", phase: "finished", note: "This Quick Clash match is already finished." });
    }

    if (match.status !== "pick" || (!match.player_b && !isBotFill)) {
      return jsonResponse({ error: `Quick Clash is not accepting picks in status '${match.status}'.` }, 409);
    }

    const deadlineCheck = await advanceExpiredClashTurnIfNeeded(match as EmojiClashMatchRow);
    if (deadlineCheck.advanced) {
      if (deadlineCheck.match.status === "finished") {
        await persistClashLadderOutcome(deadlineCheck.match);
      }

      return jsonResponse({
        accepted: false,
        matchId: deadlineCheck.match.id,
        playerId: requestUserId,
        turnNumber: payload.turnNumber,
        emojiId: "",
        status: deadlineCheck.match.status,
        phase: deadlineCheck.match.current_state?.phase ?? deadlineCheck.match.status,
        opponentReady: false,
        note: deadlineCheck.note || "Turn timer expired before this pick arrived.",
      });
    }

    const state = ensureClashState(deadlineCheck.match);
    if (isBotFillState(state)) {
      await ensureClashBotTurn(match.id, state);
    }
    const expectedTurnNumber = state.currentTurnIndex + 1;
    if (payload.turnNumber !== expectedTurnNumber || expectedTurnNumber > EMOJI_CLASH_TOTAL_TURNS) {
      return jsonResponse({ error: "Pick was submitted for a stale turn." }, 409);
    }

    const usedUnits = actorSide === "player_a" ? state.playerAUsedUnits : state.playerBUsedUnits;
    if (usedUnits.includes(unitKey)) {
      return jsonResponse({ error: "That emoji has already been used in this Quick Clash match." }, 400);
    }

    const turnRowsBefore = await fetchClashTurnRows(match.id, expectedTurnNumber);
    const existing = turnRowsBefore.find((turn) => turn.player_id === requestUserId);
    if (existing) {
      if (existing.emoji_id !== unitKey) {
        return jsonResponse({ error: "A different emoji is already locked for this turn." }, 409);
      }
    } else {
      try {
        await insertRows<EmojiClashTurnRow>("turns", {
          match_id: match.id,
          round_number: expectedTurnNumber,
          player_id: requestUserId,
          emoji_id: unitKey,
        });
      } catch (_error) {
        const afterConflict = await fetchClashTurnRows(match.id, expectedTurnNumber);
        const conflictRow = afterConflict.find((turn) => turn.player_id === requestUserId);
        if (!conflictRow || conflictRow.emoji_id !== unitKey) {
          return jsonResponse({ error: "A different emoji is already locked for this turn." }, 409);
        }
      }
    }

    const turnRows = await fetchClashTurnRows(match.id, expectedTurnNumber);
    const pickA = turnRows.find((turn) => turn.player_id === match.player_a)?.emoji_id ?? "";
    const botTurn = isBotFillState(state) ? await fetchClashBotTurn(match.id, expectedTurnNumber) : null;
    const pickB = match.player_b ? turnRows.find((turn) => turn.player_id === match.player_b)?.emoji_id ?? "" : botTurn?.emoji_id ?? "";
    if (pickA && pickB) {
      const nextState = resolvePickedTurn(state, pickA, pickB);
      const finished = nextState.phase === "finished";
      const patched = await patchRows<MatchRow>(
        `matches?id=eq.${match.id}`,
        {
          status: finished ? "finished" : "pick",
          winner: finished ? winnerUserId(match, nextState.winner ?? "draw") : null,
          current_state: nextState,
          rules_version: EMOJI_CLASH_RULES_VERSION,
        },
      );
      if (finished) {
        await persistClashLadderOutcome(patched[0]);
      } else if (isBotFillState(nextState)) {
        await ensureClashBotTurn(match.id, nextState);
      }
      return jsonResponse({
        accepted: true,
        matchId: match.id,
        playerId: requestUserId,
        turnNumber: expectedTurnNumber,
        emojiId: unitKey,
        status: finished ? "finished" : "resolved",
        phase: nextState.phase,
        opponentReady: true,
        note: finished ? "Final clash resolved." : "Both picks are locked. Turn resolved.",
      });
    }

    return jsonResponse({
      accepted: true,
      matchId: match.id,
      playerId: requestUserId,
      turnNumber: expectedTurnNumber,
      emojiId: unitKey,
      status: "awaiting_other_pick",
      phase: "pick",
      opponentReady: false,
      note: "Your pick is locked. Waiting for the other player.",
    });
  } catch (error) {
    return jsonResponse({ error: error instanceof Error ? error.message : "Submit Quick Clash pick failed." }, 500);
  }
});
