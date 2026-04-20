import type { EmojiId } from "./contracts.ts";
import { getInteractionEntry } from "./interaction-matrix.ts";

export interface SideState {
  burn: number;
  poison: number;
  growth: number;
  cleanse: boolean;
  shieldCharge: number;
  delayedEffects: string[];
}

export interface RoundResolveInput {
  matchId: string;
  rulesVersion: string;
  roundNumber: number;
  playerAPick: EmojiId;
  playerBPick: EmojiId;
  sideStateA?: SideState;
  sideStateB?: SideState;
}

export interface EffectLogEntry {
  actor: string;
  target: string;
  effectType: string;
  detail: string;
}

export interface ReplayEvent {
  eventType: string;
  source: string;
  target?: string;
  caption: string;
}

export interface RoundResolveResult {
  winner: "player_a" | "player_b" | "draw";
  isDraw: boolean;
  reasonCode: string;
  whyText: string;
  whyChain: string[];
  effectLog: EffectLogEntry[];
  replayEvents: ReplayEvent[];
  nextSideStateA: SideState;
  nextSideStateB: SideState;
}

export function emptySideState(): SideState {
  return {
    burn: 0,
    poison: 0,
    growth: 0,
    cleanse: false,
    shieldCharge: 0,
    delayedEffects: [],
  };
}

export function resolveRound(input: RoundResolveInput): RoundResolveResult {
  const entry = getInteractionEntry(input.playerAPick, input.playerBPick);
  const winner = entry.outcomeType === "attacker_advantage"
    ? "player_a"
    : entry.outcomeType === "defender_advantage"
      ? "player_b"
      : "draw";

  return {
    winner,
    isDraw: winner === "draw",
    reasonCode: entry.reasonCode,
    whyText: entry.whyText,
    whyChain: entry.whyChain,
    effectLog: [{
      actor: input.playerAPick,
      target: input.playerBPick,
      effectType: entry.effectTags[0] ?? "resolve",
      detail: entry.whyText,
    }],
    replayEvents: [{
      eventType: "resolve",
      source: input.playerAPick,
      target: input.playerBPick,
      caption: entry.whyText,
    }],
    nextSideStateA: input.sideStateA ?? emptySideState(),
    nextSideStateB: input.sideStateB ?? emptySideState(),
  };
}
