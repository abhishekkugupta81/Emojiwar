import type { BattleState, EmojiId, Formation, MatchMode } from "../_shared/contracts.ts";
import { selectBattleTeamFromDeck } from "../_shared/battle-simulator.ts";
import { EMOJI_DEFINITIONS } from "../_shared/emoji-definitions.ts";
import { jsonResponse, readJson } from "../_shared/http.ts";
import { patchRows, resolveRequestUserId, selectRows } from "../_shared/supabase-admin.ts";

interface SubmitBanRequest {
  matchId: string;
  playerId: string;
  bannedEmojiId: string;
}

interface MatchRow {
  id: string;
  mode: MatchMode;
  status: string;
  rules_version: string | null;
  player_a: string;
  player_b: string | null;
  deck_a: EmojiId[];
  deck_b: EmojiId[];
  bans: {
    player_a?: EmojiId;
    player_b?: EmojiId;
  };
  current_state: {
    phase?: "queue" | "ban" | "formation" | "finished";
    queueTicket?: string;
    battleSeed?: string;
    playerDeckA?: EmojiId[];
    playerDeckB?: EmojiId[];
    finalTeamA?: EmojiId[];
    finalTeamB?: EmojiId[];
    formationA?: Formation;
    formationB?: Formation;
    battleState?: BattleState | null;
    whySummary?: string;
    whyChain?: string[];
  };
}

function removeBannedEmoji(deck: EmojiId[], bannedEmojiId?: EmojiId | null): EmojiId[] {
  return bannedEmojiId == null ? [...deck] : deck.filter((emojiId) => emojiId !== bannedEmojiId);
}

Deno.serve(async (request) => {
  if (request.method !== "POST") {
    return jsonResponse({ error: "Method not allowed" }, 405);
  }

  try {
    const payload = await readJson<SubmitBanRequest>(request);
    const requestUserId = await resolveRequestUserId(request);

    if (!payload.matchId || !payload.playerId || !payload.bannedEmojiId) {
      return jsonResponse({ error: "Ban request is missing matchId, playerId, or bannedEmojiId." }, 400);
    }

    if (payload.playerId !== requestUserId) {
      return jsonResponse({ error: "Ban request player does not match the authenticated session." }, 403);
    }

    const rows = await selectRows<MatchRow>(
      `matches?select=id,mode,status,rules_version,player_a,player_b,deck_a,deck_b,bans,current_state&id=eq.${payload.matchId}&limit=1`,
    );
    const match = rows[0];
    if (!match) {
      return jsonResponse({ error: "Match not found." }, 404);
    }

    const actorSide = match.player_a === requestUserId
      ? "player_a"
      : match.player_b === requestUserId
        ? "player_b"
        : null;
    if (!actorSide) {
      return jsonResponse({ error: "Authenticated user is not part of this match." }, 403);
    }

    if (match.status === "finished") {
      return jsonResponse({
        accepted: true,
        matchId: match.id,
        playerId: requestUserId,
        bannedEmojiId: payload.bannedEmojiId,
        status: "finished",
        phase: "finished",
        note: "This ranked match is already finished.",
      });
    }

    if (match.status !== "banning") {
      return jsonResponse({ error: `Match is not accepting bans in status '${match.status}'.` }, 409);
    }

    const targetDeck = actorSide === "player_a" ? match.deck_b : match.deck_a;
    if (!targetDeck.includes(payload.bannedEmojiId as EmojiId)) {
      return jsonResponse({ error: "Selected emoji is not available to ban on the opponent deck." }, 400);
    }

    const updatedBans = {
      ...(match.bans ?? {}),
      [actorSide]: payload.bannedEmojiId as EmojiId,
    };
    const hasBothBans = !!updatedBans.player_a && !!updatedBans.player_b;

    if (!hasBothBans) {
      await patchRows<MatchRow>(
        `matches?id=eq.${match.id}`,
        {
          status: "banning",
          bans: updatedBans,
          current_state: {
            ...(match.current_state ?? {}),
            phase: "ban",
            playerDeckA: match.deck_a,
            playerDeckB: match.deck_b,
          },
        },
      );

      return jsonResponse({
        accepted: true,
        matchId: payload.matchId,
        playerId: requestUserId,
        bannedEmojiId: payload.bannedEmojiId,
        status: "awaiting_other_ban",
        phase: "ban",
        note: "Your blind ban is locked. Waiting for the other player.",
      });
    }

    await patchRows<MatchRow>(
      `matches?id=eq.${match.id}`,
      {
        status: "formation",
        bans: updatedBans,
        current_state: {
          ...(match.current_state ?? {}),
          phase: "formation",
          playerDeckA: match.deck_a,
          playerDeckB: match.deck_b,
          finalTeamA: selectBattleTeamFromDeck(removeBannedEmoji(match.deck_a, updatedBans.player_b), 5).team,
          finalTeamB: selectBattleTeamFromDeck(removeBannedEmoji(match.deck_b, updatedBans.player_a), 5).team,
          battleSeed: match.current_state?.battleSeed ?? crypto.randomUUID(),
        },
      },
    );

    return jsonResponse({
      accepted: true,
      matchId: payload.matchId,
      playerId: requestUserId,
      bannedEmojiId: payload.bannedEmojiId,
      status: "formation",
      phase: "formation",
      note: "Both bans are locked. Set your 5v5 formation before the battle resolves.",
    });
  } catch (error) {
    return jsonResponse({ error: error instanceof Error ? error.message : "Submit ban failed." }, 500);
  }
});
