import type { EmojiId } from "./contracts.ts";
import {
  EMOJI_CLASH_ROSTER,
  type EmojiClashPublicState,
} from "./emoji-clash-rules.ts";
import { getClashBotProfile, type ClashBotProfile } from "./clash-bot-profiles.ts";
import { insertRows, selectRows } from "./supabase-admin.ts";

export interface ClashBotTurnRow {
  match_id: string;
  round_number: number;
  bot_profile_id: string;
  emoji_id: EmojiId;
}

const personaUnitBias: Record<ClashBotProfile["persona"], Partial<Record<EmojiId, number>>> = {
  tempo: { fire: 16, lightning: 16, bomb: 12, wind: 10, chain: 6 },
  trick: { mirror: 16, hole: 14, ghost: 12, snake: 10, magnet: 8 },
  control: { water: 14, ice: 14, shield: 12, soap: 10, magnet: 8 },
  comeback: { heart: 16, plant: 12, ghost: 10, soap: 8, shield: 8 },
};

export function isBotFillState(state: EmojiClashPublicState): boolean {
  return state.opponentType === "bot_fill" && !!state.botProfileId;
}

export async function fetchClashBotTurn(matchId: string, turnNumber: number): Promise<ClashBotTurnRow | null> {
  const rows = await selectRows<ClashBotTurnRow>(
    `clash_bot_turns?select=match_id,round_number,bot_profile_id,emoji_id&match_id=eq.${matchId}&round_number=eq.${turnNumber}&limit=1`,
  );
  return rows[0] ?? null;
}

export async function ensureClashBotTurn(
  matchId: string,
  state: EmojiClashPublicState,
): Promise<ClashBotTurnRow | null> {
  if (!isBotFillState(state) || state.phase !== "pick") {
    return null;
  }

  const turnNumber = state.currentTurnIndex + 1;
  const existing = await fetchClashBotTurn(matchId, turnNumber);
  if (existing) {
    return existing;
  }

  const profile = getClashBotProfile(state.botProfileId ?? "");
  if (!profile) {
    return null;
  }

  const emojiId = chooseClashBotUnit(matchId, state, profile);
  try {
    const inserted = await insertRows<ClashBotTurnRow>("clash_bot_turns", {
      match_id: matchId,
      round_number: turnNumber,
      bot_profile_id: profile.bot_profile_id,
      emoji_id: emojiId,
    });
    return inserted[0] ?? await fetchClashBotTurn(matchId, turnNumber);
  } catch (_error) {
    return await fetchClashBotTurn(matchId, turnNumber);
  }
}

export function chooseClashBotUnit(
  matchId: string,
  state: EmojiClashPublicState,
  profile: ClashBotProfile,
): EmojiId {
  const used = new Set(state.playerBUsedUnits);
  const available = EMOJI_CLASH_ROSTER.filter((unit) => !used.has(unit));
  const turnNumber = state.currentTurnIndex + 1;
  const seed = stableHash(`${matchId}|${profile.bot_profile_id}|${turnNumber}|${profile.persona}`);
  let best = available[0];
  let bestScore = Number.NEGATIVE_INFINITY;
  for (const unit of available) {
    const bias = personaUnitBias[profile.persona][unit] ?? 0;
    const timing = timingBias(unit, turnNumber);
    const jitter = stableHash(`${seed}|${unit}`) % 13;
    const score = bias + timing + jitter;
    if (score > bestScore || (score === bestScore && unit < best)) {
      best = unit;
      bestScore = score;
    }
  }

  return best;
}

function timingBias(unit: EmojiId, turnNumber: number): number {
  if (turnNumber <= 2 && (unit === "fire" || unit === "lightning" || unit === "wind")) return 5;
  if (turnNumber >= 4 && (unit === "heart" || unit === "ghost" || unit === "mirror")) return 5;
  if (turnNumber === 5 && (unit === "heart" || unit === "bomb" || unit === "hole")) return 4;
  return 0;
}

function stableHash(value: string): number {
  let hash = 2166136261;
  for (let index = 0; index < value.length; index++) {
    hash ^= value.charCodeAt(index);
    hash = Math.imul(hash, 16777619);
  }

  return hash >>> 0;
}
