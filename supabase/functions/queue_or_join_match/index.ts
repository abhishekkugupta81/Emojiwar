import type {
  BattleState,
  EmojiId,
  Formation,
  MatchMode,
  QueueOrJoinMatchRequest,
  QueueOrJoinMatchResponse,
} from "../_shared/contracts.ts";
import { resolveBattle, selectBattleTeamFromDeck } from "../_shared/battle-simulator.ts";
import { EMOJI_DEFINITIONS } from "../_shared/emoji-definitions.ts";
import { jsonResponse, readJson } from "../_shared/http.ts";
import {
  computeFinalTeams,
  ensureFormation,
  isResumableActiveMatch,
  normalizeRankedPhase,
  phaseDeadlineAt,
  phaseTimeoutSecondsRemaining,
  QUEUE_TTL_SECONDS,
  RankedCurrentState,
  chooseAutomaticBan,
  removeBannedEmoji,
} from "../_shared/ranked-match.ts";
import { persistCodexUnlocks, persistRankedOutcome, winnerUserId } from "../_shared/ranked-persistence.ts";
import { insertRows, patchRows, resolveRequestUserId, selectRows } from "../_shared/supabase-admin.ts";
const RULES_VERSION = "launch-5v5-001";

function validateDeck(playerDeck: string[]): { isValid: boolean; error?: string } {
  if (playerDeck.length !== 6) {
    return { isValid: false, error: "Ranked queue requires exactly 6 unique emojis." };
  }

  const uniqueDeck = new Set(playerDeck);
  if (uniqueDeck.size !== playerDeck.length) {
    return { isValid: false, error: "Active deck emojis must be unique." };
  }

  const allowedEmojiIds = new Set(Object.keys(EMOJI_DEFINITIONS));
  if (playerDeck.some((emojiId) => !allowedEmojiIds.has(emojiId))) {
    return { isValid: false, error: "Active deck contains an unsupported emoji id." };
  }

  return { isValid: true };
}

interface MatchRow {
  id: string;
  rules_version: string | null;
  status: string;
  mode: MatchMode;
  updated_at: string;
  player_a: string;
  player_b: string | null;
  winner: string | null;
  deck_a: EmojiId[];
  deck_b: EmojiId[];
  bans?: {
    player_a?: EmojiId;
    player_b?: EmojiId;
  };
  current_state: RankedCurrentState & {
    battleState?: BattleState | null;
  };
}

async function expireStaleQueuedRows() {
  const cutoffIso = new Date(Date.now() - (QUEUE_TTL_SECONDS * 1000)).toISOString();
  await patchRows<Record<string, unknown>>(
    `matches?mode=eq.pvp_ranked&status=eq.queued&player_b=is.null&updated_at=lt.${encodeURIComponent(cutoffIso)}`,
    {
      status: "cancelled",
      current_state: {
        phase: "queue",
        expiredAt: new Date().toISOString(),
        reason: "queue_ttl_elapsed",
      },
    },
  );
}

function buildMatchedResponse(
  match: MatchRow,
  requestUserId: string,
  deckId: string,
  note: string,
): QueueOrJoinMatchResponse {
  const playerSide = match.player_a === requestUserId ? "player_a" : "player_b";
  const isPlayerA = playerSide === "player_a";
  const phase = normalizeRankedPhase(match);
  const playerDeck = isPlayerA ? match.deck_a : match.deck_b;
  const opponentDeck = isPlayerA ? match.deck_b : match.deck_a;
  const playerBannedEmojiId = isPlayerA ? match.bans?.player_b ?? null : match.bans?.player_a ?? null;
  const opponentBannedEmojiId = isPlayerA ? match.bans?.player_a ?? null : match.bans?.player_b ?? null;
  const playerFinalTeam = isPlayerA ? match.current_state?.finalTeamA ?? [] : match.current_state?.finalTeamB ?? [];
  const opponentFinalTeam = isPlayerA ? match.current_state?.finalTeamB ?? [] : match.current_state?.finalTeamA ?? [];
  const playerFormation = isPlayerA ? match.current_state?.formationA : match.current_state?.formationB;
  const opponentFormation = isPlayerA ? match.current_state?.formationB : match.current_state?.formationA;
  const battleState = match.current_state?.battleState ?? null;

  const winner = battleState?.winner;

  return {
    status: "matched",
    queueTicket: match.current_state?.queueTicket ?? match.id,
    userId: requestUserId,
    deckId,
    estimatedWaitSeconds: 0,
    phaseDeadlineAt: phaseDeadlineAt(match),
    phaseTimeoutSecondsRemaining: phaseTimeoutSecondsRemaining(match),
    note,
    matchId: match.id,
    opponentUserId: isPlayerA ? match.player_b ?? "" : match.player_a,
    rulesVersion: match.rules_version ?? RULES_VERSION,
    playerSide,
    phase,
    playerDeck,
    opponentDeck,
    playerBannedEmojiId,
    opponentBannedEmojiId,
    playerFinalTeam,
    opponentFinalTeam,
    playerFormation,
    opponentFormation,
    battleState,
    winner,
    whySummary: match.current_state?.whySummary ?? battleState?.whySummary ?? "",
    whyChain: match.current_state?.whyChain ?? battleState?.whyChain ?? [],
  };
}

function buildQueuedResponse(
  match: MatchRow,
  requestUserId: string,
  deckId: string,
  note: string,
): QueueOrJoinMatchResponse {
  return {
    status: "queued",
    queueTicket: match.current_state?.queueTicket ?? match.id,
    userId: requestUserId,
    deckId,
    estimatedWaitSeconds: 15,
    phaseDeadlineAt: phaseDeadlineAt(match),
    phaseTimeoutSecondsRemaining: phaseTimeoutSecondsRemaining(match),
    note,
    matchId: match.id,
    phase: "queue",
    playerDeck: match.current_state?.playerDeckA ?? match.deck_a,
  };
}

async function advanceTimedOutMatchIfNeeded(match: MatchRow): Promise<{ match: MatchRow; note: string }> {
  const phase = normalizeRankedPhase(match);
  const remaining = phaseTimeoutSecondsRemaining(match);
  const baseNote = match.current_state?.systemNote ?? "";

  if (phase === "ban" && remaining <= 0) {
    const nextBans = {
      ...(match.bans ?? {}),
    };

    if (!nextBans.player_a) {
      const autoBan = chooseAutomaticBan(match.deck_b);
      if (autoBan) {
        nextBans.player_a = autoBan;
      }
    }

    if (!nextBans.player_b) {
      const autoBan = chooseAutomaticBan(match.deck_a);
      if (autoBan) {
        nextBans.player_b = autoBan;
      }
    }

    const finalTeamA = selectBattleTeamFromDeck(removeBannedEmoji(match.deck_a, nextBans.player_b), 5).team;
    const finalTeamB = selectBattleTeamFromDeck(removeBannedEmoji(match.deck_b, nextBans.player_a), 5).team;
    const note = "Blind ban timer expired. Missing bans were locked automatically.";

    const patched = await patchRows<MatchRow>(
      `matches?id=eq.${match.id}`,
      {
        status: "formation",
        bans: nextBans,
        current_state: {
          ...(match.current_state ?? {}),
          phase: "formation",
          playerDeckA: match.deck_a,
          playerDeckB: match.deck_b,
          finalTeamA,
          finalTeamB,
          battleSeed: match.current_state?.battleSeed ?? crypto.randomUUID(),
          systemNote: note,
          timedOutAt: new Date().toISOString(),
        },
      },
    );

    return {
      match: patched[0],
      note,
    };
  }

  if (phase === "formation" && remaining <= 0) {
    const { finalTeamA, finalTeamB } = match.current_state?.finalTeamA?.length === 5 && match.current_state?.finalTeamB?.length === 5
      ? {
        finalTeamA: match.current_state.finalTeamA,
        finalTeamB: match.current_state.finalTeamB,
      }
      : computeFinalTeams(match);

    const formationA = ensureFormation(match.current_state?.formationA, finalTeamA);
    const formationB = ensureFormation(match.current_state?.formationB, finalTeamB);
    const battle = resolveBattle({
      mode: match.mode,
      rulesVersion: match.rules_version ?? RULES_VERSION,
      battleSeed: match.current_state?.battleSeed ?? crypto.randomUUID(),
      teamA: finalTeamA,
      teamB: finalTeamB,
      formationA,
      formationB,
    });
    const note = "Formation timer expired. Missing formations were auto-filled and the battle resolved.";

    const patched = await patchRows<MatchRow>(
      `matches?id=eq.${match.id}`,
      {
        status: "finished",
        winner: winnerUserId(match, battle.winner),
        current_state: {
          ...(match.current_state ?? {}),
          phase: "finished",
          finalTeamA,
          finalTeamB,
          formationA,
          formationB,
          battleSeed: match.current_state?.battleSeed ?? crypto.randomUUID(),
          battleState: battle.battleState,
          whySummary: battle.whySummary,
          whyChain: battle.whyChain,
          systemNote: note,
          timedOutAt: new Date().toISOString(),
        },
      },
    );

    await persistRankedOutcome(match, battle.winner);
    await persistCodexUnlocks(match, battle.codexEvents);

    return {
      match: patched[0],
      note,
    };
  }

  return {
    match,
    note: baseNote,
  };
}

Deno.serve(async (request) => {
  if (request.method !== "POST") {
    return jsonResponse({ error: "Method not allowed" }, 405);
  }

  try {
    const payload = await readJson<QueueOrJoinMatchRequest>(request);
    const requestUserId = await resolveRequestUserId(request);
    if (!payload.userId || !payload.deckId) {
      return jsonResponse({ error: "Queue request is missing userId or deckId." }, 400);
    }

    if (payload.userId !== requestUserId) {
      return jsonResponse({ error: "Queue request user does not match the authenticated session." }, 403);
    }

    const playerDeck = Array.isArray(payload.playerDeck) ? payload.playerDeck as EmojiId[] : [];
    const forceFreshEntry = payload.forceFreshEntry === true;
    const validation = validateDeck(playerDeck);
    if (!validation.isValid) {
      return jsonResponse({ error: validation.error }, 400);
    }

    await expireStaleQueuedRows();

    if (payload.matchId) {
      const exactMatchRows = await selectRows<MatchRow>(
        `matches?select=id,mode,updated_at,rules_version,status,player_a,player_b,winner,deck_a,deck_b,bans,current_state&id=eq.${payload.matchId}&or=(player_a.eq.${requestUserId},player_b.eq.${requestUserId})&limit=1`,
      );
      if (exactMatchRows[0] && exactMatchRows[0].status !== "cancelled") {
        const exactMatch = await advanceTimedOutMatchIfNeeded(exactMatchRows[0]);
        if (exactMatch.match.status === "queued" && !exactMatch.match.player_b) {
          return jsonResponse(buildQueuedResponse(exactMatch.match, requestUserId, payload.deckId, exactMatch.note || "Still searching for an opponent."));
        }

        return jsonResponse(buildMatchedResponse(exactMatch.match, requestUserId, payload.deckId, exactMatch.note || "Ranked match resumed from persisted server state."));
      }
    }

    const activeOnlineRows = await selectRows<MatchRow>(
      `matches?select=id,mode,updated_at,rules_version,status,player_a,player_b,winner,deck_a,deck_b,bans,current_state&mode=in.(pvp_ranked,emoji_clash_pvp)&status=in.(queued,banning,formation,pick,resolving)&or=(player_a.eq.${requestUserId},player_b.eq.${requestUserId})&order=updated_at.desc&limit=5`,
    );
    const activeClash = activeOnlineRows.find((match) => match.mode === "emoji_clash_pvp");
    if (activeClash) {
      return jsonResponse({ error: "You already have an active Quick Clash PvP match or queue." }, 409);
    }

    if (!forceFreshEntry) {
      const existingActiveMatches = await selectRows<MatchRow>(
        `matches?select=id,mode,updated_at,rules_version,status,player_a,player_b,winner,deck_a,deck_b,bans,current_state&mode=eq.pvp_ranked&status=in.(banning,formation,resolving)&or=(player_a.eq.${requestUserId},player_b.eq.${requestUserId})&order=created_at.desc&limit=5`,
      );
      for (const activeMatch of existingActiveMatches) {
        const refreshed = await advanceTimedOutMatchIfNeeded(activeMatch);
        if (isResumableActiveMatch(refreshed.match)) {
          return jsonResponse(buildMatchedResponse(refreshed.match, requestUserId, payload.deckId, refreshed.note || "Ranked match resumed from persisted server state."));
        }
      }
    }

    if (!forceFreshEntry) {
      const existingOwnQueue = await selectRows<MatchRow>(
        `matches?select=id,mode,updated_at,rules_version,status,player_a,player_b,winner,deck_a,deck_b,bans,current_state&mode=eq.pvp_ranked&status=eq.queued&player_a=eq.${requestUserId}&player_b=is.null&order=created_at.asc&limit=1`,
      );
      const ownQueuedMatch = existingOwnQueue[0];
      if (ownQueuedMatch) {
        return jsonResponse(buildQueuedResponse(ownQueuedMatch, requestUserId, payload.deckId, "You are already in the ranked queue with this account."));
      }
    }

    const openQueue = await selectRows<MatchRow>(
      `matches?select=id,mode,updated_at,rules_version,status,player_a,player_b,winner,deck_a,deck_b,bans,current_state&mode=eq.pvp_ranked&status=eq.queued&player_b=is.null&player_a=neq.${requestUserId}&order=created_at.asc&limit=1`,
    );
    const queuedOpponent = openQueue[0];

    if (queuedOpponent) {
      const battleSeed = crypto.randomUUID();
      const matched = await patchRows<MatchRow>(
        `matches?id=eq.${queuedOpponent.id}`,
        {
          player_b: requestUserId,
          deck_b: playerDeck,
          status: "banning",
          bans: {},
          current_state: {
            phase: "ban",
            queueTicket: queuedOpponent.current_state?.queueTicket ?? queuedOpponent.id,
            battleSeed,
            playerDeckA: queuedOpponent.deck_a,
            playerDeckB: playerDeck,
          },
          rules_version: RULES_VERSION,
        },
      );
      return jsonResponse(
        buildMatchedResponse(
          matched[0],
          requestUserId,
          payload.deckId,
          "Opponent found. Blind ban the enemy 6-emoji deck to set the final 5v5 battle.",
        ),
      );
    }

    const queueTicket = crypto.randomUUID();
    const createdRows = await insertRows<MatchRow>("matches", {
      mode: "pvp_ranked",
      status: "queued",
      player_a: requestUserId,
      deck_a: playerDeck,
      deck_b: [],
      current_state: {
        phase: "queue",
        queueTicket,
        playerDeckA: playerDeck,
        playerDeckB: [],
      },
      rules_version: RULES_VERSION,
    });

    return jsonResponse({
      ...buildQueuedResponse(createdRows[0], requestUserId, payload.deckId, "Ranked queue request accepted. Waiting for another 6-emoji squad."),
    });
  } catch (error) {
    return jsonResponse({ error: error instanceof Error ? error.message : "Queue request failed." }, 500);
  }
});
