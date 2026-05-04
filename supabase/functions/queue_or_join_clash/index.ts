import type { EmojiId, MatchMode, Winner } from "../_shared/contracts.ts";
import type { EmojiClashPublicState } from "../_shared/emoji-clash-rules.ts";
import {
  createInitialClashState,
  EMOJI_CLASH_QUEUE_TTL_SECONDS,
  EMOJI_CLASH_RULES_VERSION,
  EMOJI_CLASH_TOTAL_TURNS,
  EMOJI_CLASH_TURN_VALUES,
  EmojiClashTurnRecord,
  startPickPhase,
  toDisplayName,
} from "../_shared/emoji-clash-rules.ts";
import {
  advanceExpiredClashTurnIfNeeded,
  fetchClashTurnRows,
  ensureClashState,
} from "../_shared/emoji-clash-pvp-runtime.ts";
import type { EmojiClashMatchRow } from "../_shared/emoji-clash-pvp-runtime.ts";
import { persistClashLadderOutcome } from "../_shared/clash-ladder-persistence.ts";
import {
  CLASH_INITIAL_MATCHMAKING_RATING,
  CLASH_RECENT_OPPONENT_COOLDOWN_SECONDS,
  selectEligibleClashOpponent,
  shouldCreateClashBotFill,
} from "../_shared/clash-matchmaking.ts";
import type { ClashQueueCandidate } from "../_shared/clash-matchmaking.ts";
import { getClashBotProfile, selectClashBotProfile } from "../_shared/clash-bot-profiles.ts";
import { ensureClashBotTurn, fetchClashBotTurn, isBotFillState } from "../_shared/clash-bot-fill-runtime.ts";
import { jsonResponse, readJson } from "../_shared/http.ts";
import { insertRows, patchRows, resolveRequestUserId, selectRows } from "../_shared/supabase-admin.ts";

interface QueueOrJoinClashRequest {
  userId: string;
  matchId?: string;
  forceFreshEntry?: boolean;
}

interface QueueOrJoinClashResponse {
  status: string;
  mode: "emoji_clash_pvp";
  serverNow: string;
  queueTicket: string;
  matchId: string;
  userId: string;
  opponentUserId: string;
  opponent_type: "human" | "bot_fill";
  bot_profile_id: string;
  bot_fill_reason: string;
  display_name_resolved: string;
  avatar_key_resolved: string;
  playerSide: "player_a" | "player_b";
  rulesVersion: string;
  phase: string;
  matchmaking_rating_at_queue: number;
  queueStartedAt: string;
  queueExpiresAt: string;
  currentTurnIndex: number;
  totalTurns: number;
  turnValues: readonly number[];
  turnDeadlineAt: string;
  phaseTimeoutSecondsRemaining: number;
  playerScore: number;
  opponentScore: number;
  playerUsedUnits: EmojiId[];
  opponentUsedUnits: EmojiId[];
  timeoutStrikesPlayer: number;
  timeoutStrikesOpponent: number;
  resolvedTurnHistory: PerspectiveTurnRecord[];
  playerPickLocked: boolean;
  opponentPickLocked: boolean;
  winner: Winner | "";
  finalOutcome: string;
  finishReason: string;
  note: string;
}

interface PerspectiveTurnRecord {
  turnNumber: number;
  turnValue: number;
  playerUnitKey: EmojiId | "";
  opponentUnitKey: EmojiId | "";
  playerTimedOut: boolean;
  opponentTimedOut: boolean;
  playerTimeoutBurnUnitKey: EmojiId | "";
  opponentTimeoutBurnUnitKey: EmojiId | "";
  playerCombatPower: number;
  opponentCombatPower: number;
  outcome: "player_win" | "opponent_win" | "draw";
  playerScoreAfter: number;
  opponentScoreAfter: number;
  reason: string;
}

interface MatchRow {
  id: string;
  mode: MatchMode | "emoji_clash_pvp";
  status: string;
  rules_version: string | null;
  updated_at: string;
  created_at?: string;
  player_a: string;
  player_b: string | null;
  bot_profile_id?: string | null;
  winner: string | null;
  current_state: EmojiClashPublicState | null;
}

interface ModeRatingRow {
  user_id: string;
  ladder_mode: string;
  rating: number;
  hidden_mmr?: number;
}

function activeOnlineFilter(userId: string): string {
  return `mode=in.(pvp_ranked,emoji_clash_pvp)&status=in.(queued,banning,formation,pick,resolving)&or=(player_a.eq.${userId},player_b.eq.${userId})&order=updated_at.desc&limit=5`;
}

async function expireStaleQueuedRows() {
  const now = new Date();
  const queuedRows = await selectRows<MatchRow>(
    "matches?select=id,mode,status,rules_version,updated_at,created_at,player_a,player_b,bot_profile_id,winner,current_state&mode=eq.emoji_clash_pvp&status=eq.queued&player_b=is.null&limit=100",
  );

  for (const row of queuedRows) {
    const state = ensureClashState(row as EmojiClashMatchRow);
    const expiresAt = state.queueExpiresAt || row.updated_at;
    if (Date.parse(expiresAt) > now.getTime()) {
      continue;
    }

    await patchRows<Record<string, unknown>>(
      `matches?id=eq.${row.id}`,
      {
        status: "cancelled",
        current_state: {
          ...state,
          phase: "queue_cancelled",
          expiredAt: now.toISOString(),
          reason: "queue_ttl_elapsed",
        },
      },
    );
  }
}

async function getClashMatchmakingRating(userId: string): Promise<number> {
  const rows = await selectRows<ModeRatingRow>(
    `mode_ratings?select=user_id,ladder_mode,rating,hidden_mmr&ladder_mode=eq.emoji_clash_pvp&user_id=eq.${userId}&limit=1`,
  );
  return rows[0]?.hidden_mmr ?? rows[0]?.rating ?? CLASH_INITIAL_MATCHMAKING_RATING;
}

function refreshQueueState(match: MatchRow, now = new Date()): EmojiClashPublicState {
  const state = ensureClashState(match as EmojiClashMatchRow);
  const queueStartedAt = state.queueStartedAt || match.created_at || match.updated_at || now.toISOString();
  return {
    ...state,
    phase: "queue",
    queueStartedAt,
    queueExpiresAt: new Date(now.getTime() + EMOJI_CLASH_QUEUE_TTL_SECONDS * 1000).toISOString(),
    matchmakingRatingAtQueue: state.matchmakingRatingAtQueue ?? CLASH_INITIAL_MATCHMAKING_RATING,
  };
}

async function refreshQueuedMatch(match: MatchRow): Promise<MatchRow> {
  if (match.status !== "queued" || match.player_b) {
    return match;
  }

  const patched = await patchRows<MatchRow>(
    `matches?id=eq.${match.id}`,
    { current_state: refreshQueueState(match) },
  );
  return patched[0] ?? match;
}

async function getMostRecentClashOpponent(userId: string): Promise<string> {
  const cutoff = new Date(Date.now() - CLASH_RECENT_OPPONENT_COOLDOWN_SECONDS * 1000).toISOString();
  const rows = await selectRows<MatchRow>(
    `matches?select=id,mode,status,rules_version,updated_at,created_at,player_a,player_b,bot_profile_id,winner,current_state&mode=eq.emoji_clash_pvp&status=eq.finished&updated_at=gte.${encodeURIComponent(cutoff)}&or=(player_a.eq.${userId},player_b.eq.${userId})&order=updated_at.desc&limit=1`,
  );
  const match = rows[0];
  if (!match?.player_b) {
    return "";
  }

  return match.player_a === userId ? match.player_b : match.player_a;
}

async function getMostRecentClashBotProfile(userId: string): Promise<string> {
  const cutoff = new Date(Date.now() - CLASH_RECENT_OPPONENT_COOLDOWN_SECONDS * 1000).toISOString();
  const rows = await selectRows<MatchRow>(
    `matches?select=id,mode,status,rules_version,updated_at,created_at,player_a,player_b,bot_profile_id,winner,current_state&mode=eq.emoji_clash_pvp&status=eq.finished&updated_at=gte.${encodeURIComponent(cutoff)}&player_a=eq.${userId}&bot_profile_id=not.is.null&order=updated_at.desc&limit=1`,
  );
  return rows[0]?.bot_profile_id ?? "";
}

async function candidateHasOtherActiveOnlineMatch(candidate: MatchRow): Promise<boolean> {
  const rows = await selectRows<MatchRow>(
    `matches?select=id,mode,status,rules_version,updated_at,created_at,player_a,player_b,bot_profile_id,winner,current_state&id=neq.${candidate.id}&mode=in.(pvp_ranked,emoji_clash_pvp)&status=in.(queued,banning,formation,pick,resolving)&or=(player_a.eq.${candidate.player_a},player_b.eq.${candidate.player_a})&limit=1`,
  );
  return rows.length > 0;
}

async function findEligibleOpenQueue(requestUserId: string, requestRating: number, requestQueueStartedAt = ""): Promise<MatchRow | null> {
  const now = new Date();
  const candidateRows = await selectRows<MatchRow>(
    "matches?select=id,mode,status,rules_version,updated_at,created_at,player_a,player_b,bot_profile_id,winner,current_state&mode=eq.emoji_clash_pvp&status=eq.queued&player_b=is.null&order=created_at.asc&limit=50",
  );
  const activeEligible: MatchRow[] = [];
  for (const candidate of candidateRows) {
    if (candidate.player_a === requestUserId) {
      continue;
    }

    if (await candidateHasOtherActiveOnlineMatch(candidate)) {
      continue;
    }

    activeEligible.push(candidate);
  }

  const recentOpponent = await getMostRecentClashOpponent(requestUserId);
  return selectEligibleClashOpponent(
    activeEligible as ClashQueueCandidate[],
    requestUserId,
    requestRating,
    requestQueueStartedAt,
    recentOpponent,
    now,
  ) as MatchRow | null;
}

async function cancelSupersededQueue(match: MatchRow, reason: string) {
  const state = ensureClashState(match as EmojiClashMatchRow);
  await patchRows<MatchRow>(
    `matches?id=eq.${match.id}&status=eq.queued&player_b=is.null`,
    {
      status: "cancelled",
      current_state: {
        ...state,
        phase: "queue_cancelled",
        cancelledAt: new Date().toISOString(),
        reason,
      },
    },
  );
}

async function matchQueuedRowsForRequester(requesterQueue: MatchRow, opponentQueue: MatchRow, requestUserId: string): Promise<MatchRow | null> {
  const requesterCreated = Date.parse(requesterQueue.created_at ?? requesterQueue.updated_at);
  const opponentCreated = Date.parse(opponentQueue.created_at ?? opponentQueue.updated_at);
  if (requesterCreated <= opponentCreated) {
    const startedState = startPickPhase(ensureClashState(requesterQueue as EmojiClashMatchRow));
    const matched = await patchRows<MatchRow>(
      `matches?id=eq.${requesterQueue.id}&status=eq.queued&player_b=is.null`,
      {
        player_b: opponentQueue.player_a,
        status: "pick",
        current_state: startedState,
        rules_version: EMOJI_CLASH_RULES_VERSION,
      },
    );
    if (matched.length === 0) {
      return null;
    }

    await cancelSupersededQueue(opponentQueue, "matched_elsewhere");
    return matched[0];
  }

  const startedState = startPickPhase(ensureClashState(opponentQueue as EmojiClashMatchRow));
  const matched = await patchRows<MatchRow>(
    `matches?id=eq.${opponentQueue.id}&status=eq.queued&player_b=is.null`,
    {
      player_b: requestUserId,
      status: "pick",
      current_state: startedState,
      rules_version: EMOJI_CLASH_RULES_VERSION,
    },
  );
  if (matched.length === 0) {
    return null;
  }

  await cancelSupersededQueue(requesterQueue, "matched_elsewhere");
  return matched[0];
}

async function maybeCreateBotFillMatch(match: MatchRow, requestUserId: string): Promise<MatchRow | null> {
  if (match.status !== "queued" || match.player_b || match.player_a !== requestUserId) {
    return null;
  }

  const state = ensureClashState(match as EmojiClashMatchRow);
  const queueStartedAt = state.queueStartedAt || match.created_at || match.updated_at || "";
  if (!shouldCreateClashBotFill(queueStartedAt)) {
    return null;
  }

  const rating = state.matchmakingRatingAtQueue ?? await getClashMatchmakingRating(requestUserId);
  const recentProfileId = await getMostRecentClashBotProfile(requestUserId);
  const profile = selectClashBotProfile(rating, `${match.id}|${requestUserId}|${queueStartedAt}`, recentProfileId);
  const startedState = startPickPhase({
    ...state,
    opponentType: "bot_fill",
    botProfileId: profile.bot_profile_id,
    botFillReason: "queue_timeout",
    opponentDisplayName: profile.display_name,
    opponentAvatarKey: profile.avatar_key,
  });
  const patched = await patchRows<MatchRow>(
    `matches?id=eq.${match.id}&status=eq.queued&player_b=is.null`,
    {
      status: "pick",
      bot_profile_id: profile.bot_profile_id,
      current_state: startedState,
      rules_version: EMOJI_CLASH_RULES_VERSION,
    },
  );

  if (patched.length === 0) {
    return null;
  }

  await ensureClashBotTurn(patched[0].id, ensureClashState(patched[0] as EmojiClashMatchRow));
  return patched[0];
}


function secondsRemaining(deadline: string): number {
  if (!deadline) return 0;
  const remaining = Date.parse(deadline) - Date.now();
  return remaining <= 0 ? 0 : Math.ceil(remaining / 1000);
}

function mapHistory(records: EmojiClashTurnRecord[], isPlayerA: boolean): PerspectiveTurnRecord[] {
  return records.map((record) => ({
    turnNumber: record.turnNumber,
    turnValue: record.turnValue,
    playerUnitKey: isPlayerA ? record.playerAUnitKey : record.playerBUnitKey,
    opponentUnitKey: isPlayerA ? record.playerBUnitKey : record.playerAUnitKey,
    playerTimedOut: isPlayerA ? record.playerATimedOut ?? false : record.playerBTimedOut ?? false,
    opponentTimedOut: isPlayerA ? record.playerBTimedOut ?? false : record.playerATimedOut ?? false,
    playerTimeoutBurnUnitKey: isPlayerA ? record.playerATimeoutBurnUnitKey ?? "" : record.playerBTimeoutBurnUnitKey ?? "",
    opponentTimeoutBurnUnitKey: isPlayerA ? record.playerBTimeoutBurnUnitKey ?? "" : record.playerATimeoutBurnUnitKey ?? "",
    playerCombatPower: isPlayerA ? record.playerACombatPower : record.playerBCombatPower,
    opponentCombatPower: isPlayerA ? record.playerBCombatPower : record.playerACombatPower,
    outcome: record.outcome === "draw" ? "draw" : record.outcome === (isPlayerA ? "player_a" : "player_b") ? "player_win" : "opponent_win",
    playerScoreAfter: isPlayerA ? record.playerAScoreAfter : record.playerBScoreAfter,
    opponentScoreAfter: isPlayerA ? record.playerBScoreAfter : record.playerAScoreAfter,
    reason: mapReason(record, isPlayerA),
  }));
}

function mapReason(record: EmojiClashTurnRecord, isPlayerA: boolean): string {
  const playerTimedOut = isPlayerA ? record.playerATimedOut : record.playerBTimedOut;
  const opponentTimedOut = isPlayerA ? record.playerBTimedOut : record.playerATimedOut;
  const playerBurn = isPlayerA ? record.playerATimeoutBurnUnitKey : record.playerBTimeoutBurnUnitKey;
  const opponentBurn = isPlayerA ? record.playerBTimeoutBurnUnitKey : record.playerATimeoutBurnUnitKey;

  if (playerTimedOut && opponentTimedOut) {
    return "Both players missed the turn. No points awarded.";
  }

  if (opponentTimedOut) {
    return `Opponent missed the turn. You gained +${record.turnValue}. Opponent lost ${toDisplayName(opponentBurn ?? "")}.`;
  }

  if (playerTimedOut) {
    return `You missed the turn. Rival gained +${record.turnValue}. You lost ${toDisplayName(playerBurn ?? "")}.`;
  }

  return record.reason;
}

async function buildResponse(match: MatchRow, requestUserId: string, note: string): Promise<QueueOrJoinClashResponse> {
  if (match.status === "finished") {
    await persistClashLadderOutcome(match);
  }

  const state = ensureClashState(match as EmojiClashMatchRow);
  if (match.status === "pick" && isBotFillState(state)) {
    await ensureClashBotTurn(match.id, state);
  }
  const isPlayerA = match.player_a === requestUserId;
  const turnRows = match.status === "pick" && state.currentTurnIndex < EMOJI_CLASH_TOTAL_TURNS
    ? await fetchClashTurnRows(match.id, state.currentTurnIndex + 1)
    : [];
  const playerPickLocked = turnRows.some((turn) => turn.player_id === requestUserId);
  const opponentId = isPlayerA ? match.player_b ?? "" : match.player_a;
  const botTurn = isBotFillState(state) && match.status === "pick" && state.currentTurnIndex < EMOJI_CLASH_TOTAL_TURNS
    ? await fetchClashBotTurn(match.id, state.currentTurnIndex + 1)
    : null;
  const opponentPickLocked = opponentId ? turnRows.some((turn) => turn.player_id === opponentId) : !!botTurn;
  const winner = state.winner ?? resolveSnapshotWinner(match);
  const botProfile = state.botProfileId ? getClashBotProfile(state.botProfileId) : null;

  return {
    status: match.status,
    mode: "emoji_clash_pvp",
    serverNow: new Date().toISOString(),
    queueTicket: state.queueTicket ?? match.id,
    matchId: match.id,
    userId: requestUserId,
    opponentUserId: opponentId,
    opponent_type: isBotFillState(state) ? "bot_fill" : "human",
    bot_profile_id: state.botProfileId ?? "",
    bot_fill_reason: state.botFillReason ?? "",
    display_name_resolved: state.opponentDisplayName ?? botProfile?.display_name ?? "Rival",
    avatar_key_resolved: state.opponentAvatarKey ?? botProfile?.avatar_key ?? "",
    playerSide: isPlayerA ? "player_a" : "player_b",
    rulesVersion: match.rules_version ?? EMOJI_CLASH_RULES_VERSION,
    phase: state.phase,
    matchmaking_rating_at_queue: state.matchmakingRatingAtQueue ?? CLASH_INITIAL_MATCHMAKING_RATING,
    queueStartedAt: state.queueStartedAt ?? "",
    queueExpiresAt: state.queueExpiresAt ?? "",
    currentTurnIndex: state.currentTurnIndex,
    totalTurns: EMOJI_CLASH_TOTAL_TURNS,
    turnValues: EMOJI_CLASH_TURN_VALUES,
    turnDeadlineAt: state.turnDeadlineAt ?? "",
    phaseTimeoutSecondsRemaining: secondsRemaining(state.phase === "queue" ? state.queueExpiresAt ?? "" : state.turnDeadlineAt ?? ""),
    playerScore: isPlayerA ? state.playerAScore : state.playerBScore,
    opponentScore: isPlayerA ? state.playerBScore : state.playerAScore,
    playerUsedUnits: isPlayerA ? state.playerAUsedUnits : state.playerBUsedUnits,
    opponentUsedUnits: isPlayerA ? state.playerBUsedUnits : state.playerAUsedUnits,
    timeoutStrikesPlayer: isPlayerA ? state.playerATimeoutStrikes ?? 0 : state.playerBTimeoutStrikes ?? 0,
    timeoutStrikesOpponent: isPlayerA ? state.playerBTimeoutStrikes ?? 0 : state.playerATimeoutStrikes ?? 0,
    resolvedTurnHistory: mapHistory(state.turnHistory, isPlayerA),
    playerPickLocked,
    opponentPickLocked,
    winner,
    finalOutcome: state.finishReason ?? "",
    finishReason: state.finishReason ?? "",
    note: note || state.systemNote || "",
  };
}

function resolveSnapshotWinner(match: MatchRow): Winner | "" {
  if (!match.winner) {
    return match.status === "finished" ? "draw" : "";
  }

  if (match.winner === match.player_a) {
    return "player_a";
  }

  if (match.player_b && match.winner === match.player_b) {
    return "player_b";
  }

  return match.status === "finished" ? "draw" : "";
}

Deno.serve(async (request) => {
  if (request.method !== "POST") {
    return jsonResponse({ error: "Method not allowed" }, 405);
  }

  try {
    const payload = await readJson<QueueOrJoinClashRequest>(request);
    const requestUserId = await resolveRequestUserId(request);
    if (!payload.userId) {
      return jsonResponse({ error: "Quick Clash queue request is missing userId." }, 400);
    }

    if (payload.userId !== requestUserId) {
      return jsonResponse({ error: "Quick Clash queue user does not match the authenticated session." }, 403);
    }

    await expireStaleQueuedRows();

    if (payload.matchId) {
      const rows = await selectRows<MatchRow>(
        `matches?select=id,mode,status,rules_version,updated_at,player_a,player_b,bot_profile_id,winner,current_state&id=eq.${payload.matchId}&mode=eq.emoji_clash_pvp&or=(player_a.eq.${requestUserId},player_b.eq.${requestUserId})&limit=1`,
      );
      if (rows[0] && rows[0].status !== "cancelled") {
        if (rows[0].status === "queued" && !rows[0].player_b) {
          const refreshedQueue = await refreshQueuedMatch(rows[0]);
          const state = ensureClashState(refreshedQueue as EmojiClashMatchRow);
          const opponentQueue = await findEligibleOpenQueue(
            requestUserId,
            state.matchmakingRatingAtQueue ?? await getClashMatchmakingRating(requestUserId),
            state.queueStartedAt ?? refreshedQueue.created_at ?? refreshedQueue.updated_at,
          );
          if (opponentQueue) {
            const matched = await matchQueuedRowsForRequester(refreshedQueue, opponentQueue, requestUserId);
            if (matched) {
              return jsonResponse(await buildResponse(matched, requestUserId, "Opponent found inside your rating band. Pick your first fighter."));
            }
          }

          const botFill = await maybeCreateBotFillMatch(refreshedQueue, requestUserId);
          if (botFill) {
            return jsonResponse(await buildResponse(botFill, requestUserId, "Rival found. Pick your first fighter."));
          }

          return jsonResponse(await buildResponse(refreshedQueue, requestUserId, "Still searching inside your rating band."));
        }

        const refreshed = await advanceExpiredClashTurnIfNeeded(rows[0] as EmojiClashMatchRow);
        return jsonResponse(await buildResponse(refreshed.match as MatchRow, requestUserId, refreshed.note || "Quick Clash resumed."));
      }
    }

    const activeRows = await selectRows<MatchRow>(
      `matches?select=id,mode,status,rules_version,updated_at,player_a,player_b,bot_profile_id,winner,current_state&${activeOnlineFilter(requestUserId)}`,
    );
    const activeClash = activeRows.find((row) => row.mode === "emoji_clash_pvp" && row.status !== "cancelled");
    if (activeClash && !payload.forceFreshEntry) {
      if (activeClash.status === "queued" && !activeClash.player_b) {
        const refreshedQueue = await refreshQueuedMatch(activeClash);
        const state = ensureClashState(refreshedQueue as EmojiClashMatchRow);
        const opponentQueue = await findEligibleOpenQueue(
          requestUserId,
          state.matchmakingRatingAtQueue ?? await getClashMatchmakingRating(requestUserId),
          state.queueStartedAt ?? refreshedQueue.created_at ?? refreshedQueue.updated_at,
        );
        if (opponentQueue) {
          const matched = await matchQueuedRowsForRequester(refreshedQueue, opponentQueue, requestUserId);
          if (matched) {
            return jsonResponse(await buildResponse(matched, requestUserId, "Opponent found inside your rating band. Pick your first fighter."));
          }
        }

        const botFill = await maybeCreateBotFillMatch(refreshedQueue, requestUserId);
        if (botFill) {
          return jsonResponse(await buildResponse(botFill, requestUserId, "Rival found. Pick your first fighter."));
        }

        return jsonResponse(await buildResponse(refreshedQueue, requestUserId, "Still searching inside your rating band."));
      }

      const refreshed = await advanceExpiredClashTurnIfNeeded(activeClash as EmojiClashMatchRow);
      return jsonResponse(await buildResponse(refreshed.match as MatchRow, requestUserId, refreshed.note || "Quick Clash resumed from server state."));
    }

    const activeOther = activeRows.find((row) => row.mode === "pvp_ranked" || row.mode === "emoji_clash_pvp");
    if (activeOther) {
      return jsonResponse({ error: "You already have an active online PvP match or queue." }, 409);
    }

    const requestRating = await getClashMatchmakingRating(requestUserId);
    const openQueue = await findEligibleOpenQueue(requestUserId, requestRating);
    if (openQueue) {
      const startedState = startPickPhase(ensureClashState(openQueue as EmojiClashMatchRow));
      const matched = await patchRows<MatchRow>(
        `matches?id=eq.${openQueue.id}&status=eq.queued&player_b=is.null`,
        {
          player_b: requestUserId,
          status: "pick",
          current_state: startedState,
          rules_version: EMOJI_CLASH_RULES_VERSION,
        },
      );
      if (matched.length > 0) {
        return jsonResponse(await buildResponse(matched[0], requestUserId, "Opponent found. Pick your first fighter."));
      }
    }

    const queueTicket = crypto.randomUUID();
    const now = new Date();
    const queueStartedAt = now.toISOString();
    const createdRows = await insertRows<MatchRow>("matches", {
      mode: "emoji_clash_pvp",
      status: "queued",
      player_a: requestUserId,
      deck_a: [],
      deck_b: [],
      bans: {},
      current_state: {
        ...createInitialClashState(queueTicket),
        matchmakingRatingAtQueue: requestRating,
        queueStartedAt,
        queueExpiresAt: new Date(now.getTime() + EMOJI_CLASH_QUEUE_TTL_SECONDS * 1000).toISOString(),
      },
      rules_version: EMOJI_CLASH_RULES_VERSION,
    });

    return jsonResponse(await buildResponse(createdRows[0], requestUserId, "Quick Clash PvP queue accepted. Waiting for another player."));
  } catch (error) {
    return jsonResponse({ error: error instanceof Error ? error.message : "Quick Clash queue failed." }, 500);
  }
});
