import type {
  BattleState,
  EmojiId,
  Formation,
  FormationPlacement,
  FormationSlot,
  MatchMode,
  SubmitFormationRequest,
  Winner,
} from "../_shared/contracts.ts";
import { FORMATION_SLOT_ORDER } from "../_shared/contracts.ts";
import { resolveBattle } from "../_shared/battle-simulator.ts";
import { jsonResponse, readJson } from "../_shared/http.ts";
import { patchRows, resolveRequestUserId, selectRows } from "../_shared/supabase-admin.ts";
import { persistCodexUnlocks, persistRankedOutcome, winnerUserId } from "../_shared/ranked-persistence.ts";

const RULES_VERSION = "launch-5v5-001";

interface MatchRow {
  id: string;
  mode: MatchMode;
  status: string;
  rules_version: string | null;
  player_a: string;
  player_b: string | null;
  winner: string | null;
  deck_a: EmojiId[];
  deck_b: EmojiId[];
  bans: {
    player_a?: EmojiId;
    player_b?: EmojiId;
  };
  current_state: {
    phase?: "queue" | "ban" | "formation" | "finished";
    battleSeed?: string;
    finalTeamA?: EmojiId[];
    finalTeamB?: EmojiId[];
    formationA?: Formation;
    formationB?: Formation;
    battleState?: BattleState | null;
    whySummary?: string;
    whyChain?: string[];
  };
}

function canonicalizeFormation(formation: Formation): Formation {
  const placementBySlot = new Map<FormationSlot, FormationPlacement>();
  for (const placement of formation.placements ?? []) {
    placementBySlot.set(placement.slot, placement);
  }

  return {
    placements: FORMATION_SLOT_ORDER
      .map((slot) => placementBySlot.get(slot) ?? null)
      .filter((placement): placement is FormationPlacement => placement !== null),
  };
}

function isFormationComplete(formation?: Formation | null): boolean {
  return !!formation && Array.isArray(formation.placements) && formation.placements.length === 5;
}

function validateFormation(formation: Formation | null | undefined, expectedTeam: EmojiId[]): string | null {
  if (!formation || !Array.isArray(formation.placements)) {
    return "Formation is missing placements.";
  }

  if (formation.placements.length !== 5) {
    return "Formation must place exactly 5 emojis.";
  }

  const slotSet = new Set<string>();
  const emojiSet = new Set<string>();
  for (const placement of formation.placements) {
    if (!FORMATION_SLOT_ORDER.includes(placement.slot)) {
      return `Unsupported formation slot '${placement.slot}'.`;
    }

    if (slotSet.has(placement.slot)) {
      return "Formation slots must be unique.";
    }

    if (emojiSet.has(placement.emojiId)) {
      return "Formation emojis must be unique.";
    }

    slotSet.add(placement.slot);
    emojiSet.add(placement.emojiId);
  }

  if (slotSet.size !== 5) {
    return "Formation must assign all 5 slots.";
  }

  const expected = new Set(expectedTeam);
  if (expected.size !== 5) {
    return "Final team is invalid for formation locking.";
  }

  for (const emojiId of emojiSet) {
    if (!expected.has(emojiId as EmojiId)) {
      return "Formation includes an emoji that is not part of the final 5.";
    }
  }

  return null;
}

Deno.serve(async (request) => {
  if (request.method !== "POST") {
    return jsonResponse({ error: "Method not allowed" }, 405);
  }

  try {
    const payload = await readJson<SubmitFormationRequest>(request);
    const requestUserId = await resolveRequestUserId(request);

    if (!payload.matchId || !payload.playerId || !payload.formation) {
      return jsonResponse({ error: "Formation request is missing matchId, playerId, or formation." }, 400);
    }

    if (payload.playerId !== requestUserId) {
      return jsonResponse({ error: "Formation request player does not match the authenticated session." }, 403);
    }

    const rows = await selectRows<MatchRow>(
      `matches?select=id,mode,status,rules_version,player_a,player_b,winner,deck_a,deck_b,bans,current_state&id=eq.${payload.matchId}&limit=1`,
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
        status: "finished",
        phase: "finished",
        note: "This match is already finished.",
      });
    }

    if (match.status !== "formation") {
      return jsonResponse({ error: `Match is not accepting formations in status '${match.status}'.` }, 409);
    }

    const expectedTeam = actorSide === "player_a"
      ? match.current_state?.finalTeamA ?? []
      : match.current_state?.finalTeamB ?? [];
    const validationError = validateFormation(payload.formation, expectedTeam);
    if (validationError) {
      return jsonResponse({ error: validationError }, 400);
    }

    const normalizedFormation = canonicalizeFormation(payload.formation);
    const updatedCurrentState = {
      ...(match.current_state ?? {}),
      phase: "formation" as const,
      formationA: actorSide === "player_a" ? normalizedFormation : match.current_state?.formationA,
      formationB: actorSide === "player_b" ? normalizedFormation : match.current_state?.formationB,
      battleSeed: match.current_state?.battleSeed ?? crypto.randomUUID(),
    };

    const hasBothFormations = isFormationComplete(updatedCurrentState.formationA) && isFormationComplete(updatedCurrentState.formationB);
    if (!hasBothFormations) {
      await patchRows<MatchRow>(
        `matches?id=eq.${match.id}`,
        {
          status: "formation",
          current_state: updatedCurrentState,
        },
      );

      return jsonResponse({
        accepted: true,
        matchId: match.id,
        playerId: requestUserId,
        status: "awaiting_other_formation",
        phase: "formation",
        note: "Your formation is locked. Waiting for the other side to finish setup.",
        playerFinalTeam: actorSide === "player_a" ? updatedCurrentState.finalTeamA : updatedCurrentState.finalTeamB,
        opponentFinalTeam: actorSide === "player_a" ? updatedCurrentState.finalTeamB : updatedCurrentState.finalTeamA,
        playerFormation: actorSide === "player_a" ? updatedCurrentState.formationA : updatedCurrentState.formationB,
        opponentFormation: actorSide === "player_a" ? updatedCurrentState.formationB : updatedCurrentState.formationA,
      });
    }

    const battle = resolveBattle({
      mode: match.mode,
      rulesVersion: match.rules_version ?? RULES_VERSION,
      battleSeed: updatedCurrentState.battleSeed ?? crypto.randomUUID(),
      teamA: updatedCurrentState.finalTeamA ?? [],
      teamB: updatedCurrentState.finalTeamB ?? [],
      formationA: updatedCurrentState.formationA,
      formationB: updatedCurrentState.formationB,
    });

    await patchRows<MatchRow>(
      `matches?id=eq.${match.id}`,
      {
        status: "finished",
        winner: winnerUserId(match, battle.winner),
        current_state: {
          ...updatedCurrentState,
          phase: "finished",
          battleState: battle.battleState,
          whySummary: battle.whySummary,
          whyChain: battle.whyChain,
        },
      },
    );

    await persistRankedOutcome(match, battle.winner);
    await persistCodexUnlocks(match, battle.codexEvents);

    return jsonResponse({
      accepted: true,
      matchId: match.id,
      playerId: requestUserId,
      status: "finished",
      phase: "finished",
      note: "Both formations are locked. The 5v5 battle has been resolved.",
      playerFinalTeam: actorSide === "player_a" ? updatedCurrentState.finalTeamA : updatedCurrentState.finalTeamB,
      opponentFinalTeam: actorSide === "player_a" ? updatedCurrentState.finalTeamB : updatedCurrentState.finalTeamA,
      playerFormation: actorSide === "player_a" ? updatedCurrentState.formationA : updatedCurrentState.formationB,
      opponentFormation: actorSide === "player_a" ? updatedCurrentState.formationB : updatedCurrentState.formationA,
      battleState: battle.battleState,
      winner: battle.winner,
      whySummary: battle.whySummary,
      whyChain: battle.whyChain,
    });
  } catch (error) {
    return jsonResponse({ error: error instanceof Error ? error.message : "Submit formation failed." }, 500);
  }
});
