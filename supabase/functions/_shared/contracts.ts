export type EmojiId =
  | "fire"
  | "water"
  | "lightning"
  | "ice"
  | "magnet"
  | "bomb"
  | "mirror"
  | "hole"
  | "shield"
  | "snake"
  | "soap"
  | "plant"
  | "wind"
  | "heart"
  | "ghost"
  | "chain";

export type EmojiRole =
  | "element"
  | "trick"
  | "hazard"
  | "guard_support"
  | "status_ramp";

export type MatchMode = "pvp_ranked" | "bot_practice" | "bot_smart" | "emoji_clash_pvp";
export type MatchStatus = "queued" | "banning" | "formation" | "pick" | "resolving" | "finished" | "cancelled";
export type Winner = "player_a" | "player_b" | "draw";
export type FormationSlot = "front_left" | "front_center" | "front_right" | "back_left" | "back_right";
export type PreferredRow = "front" | "back" | "flex";

export type EffectTag =
  | "damage"
  | "heavy_damage"
  | "burn"
  | "poison"
  | "stun"
  | "freeze"
  | "pull"
  | "reflect"
  | "delete"
  | "shield"
  | "shield_break"
  | "cleanse"
  | "grow"
  | "heal"
  | "push"
  | "bind"
  | "phase"
  | "splash"
  | "disrupt";

export type InteractionOutcomeType = "attacker_advantage" | "defender_advantage" | "neutral";

export interface UnitStats {
  hp: number;
  attack: number;
  speed: number;
  tags: string[];
  preferredRow: PreferredRow;
}

export interface EmojiDefinition {
  id: EmojiId;
  displayName: string;
  role: EmojiRole;
  primaryVerb: string;
  whySummary: string;
  counters: EmojiId[];
  failsAgainst: EmojiId[];
  stats: UnitStats;
}

export interface FormationPlacement {
  slot: FormationSlot;
  emojiId: EmojiId;
}

export interface Formation {
  placements: FormationPlacement[];
}

export interface BattleUnitState {
  unitId: string;
  team: "player_a" | "player_b";
  emojiId: EmojiId;
  slot: FormationSlot;
  hp: number;
  maxHp: number;
  attack: number;
  speed: number;
  alive: boolean;
  shield: number;
  growth: number;
  burn: number;
  poison: number;
  stun: number;
  freeze: number;
  bind: number;
  disrupt: number;
  firstHitAvailable: boolean;
  mirrorArmed: boolean;
}

export interface BattleEvent {
  cycle: number;
  type: string;
  actor: string;
  target?: string;
  reasonCode: string;
  caption: string;
}

export interface InteractionEntry {
  attacker: EmojiId;
  defender: EmojiId;
  outcomeType: InteractionOutcomeType;
  reasonCode: string;
  whyText: string;
  whyChain: string[];
  effectTags: EffectTag[];
}

export interface BattleState {
  cycle: number;
  teamA: BattleUnitState[];
  teamB: BattleUnitState[];
  eventLog: BattleEvent[];
  winner: Winner;
  whySummary: string;
  whyChain: string[];
}

export interface DeckRecord {
  deckId: string;
  userId: string;
  emojiIds: EmojiId[];
  isActive: boolean;
  updatedAt: string;
}

export interface CodexEvent {
  interactionKey: string;
  reasonCode: string;
  summary: string;
  tip: string;
}

export interface CodexUnlockEntry {
  interaction_key: string;
  reason_code: string;
  summary: string;
  tip: string;
  unlocked_at: string;
}

export interface GetCodexRequest {
  limit?: number;
}

export interface GetCodexResponse {
  entries: CodexUnlockEntry[];
  totalUnlocked: number;
  latestEntry: CodexUnlockEntry | null;
  note: string;
}

export interface MatchState {
  matchId: string;
  mode: MatchMode;
  rulesVersion: string;
  status: MatchStatus;
  battleSeed: string;
  phase: "queue" | "ban" | "formation" | "finished";
  playerDeckA: EmojiId[];
  playerDeckB: EmojiId[];
  finalTeamA: EmojiId[];
  finalTeamB: EmojiId[];
  bannedByA?: EmojiId | null;
  bannedByB?: EmojiId | null;
  formationA: Formation;
  formationB: Formation;
  battleState?: BattleState | null;
  winnerId?: string | null;
}

export interface BotProfile {
  id: "practice" | "smart";
  difficulty: MatchMode;
  aggression: number;
  defenseBias: number;
  comboBias: number;
}

export interface StartBotMatchRequest {
  mode: MatchMode;
  activeDeckId?: string;
  playerDeck: EmojiId[];
}

export interface StartBotMatchResponse {
  matchId: string;
  mode: MatchMode;
  playerDeckId?: string;
  botProfile: BotProfile;
  playerDeck: EmojiId[];
  botDeck: EmojiId[];
  playerTeam: EmojiId[];
  botTeam: EmojiId[];
  benchEmojiId: EmojiId | null;
  playerBannedEmojiId?: EmojiId | null;
  opponentBannedEmojiId?: EmojiId | null;
  playerFinalTeam?: EmojiId[];
  opponentFinalTeam?: EmojiId[];
  rulesVersion: string;
  status: MatchStatus;
  phase: "ban" | "formation" | "finished";
  playerFormation?: Formation;
  botFormation?: Formation;
  battleState?: BattleState | null;
  winner?: Winner;
  whySummary?: string;
  whyChain?: string[];
  note: string;
}

export interface SubmitFormationRequest {
  matchId: string;
  playerId: string;
  formation: Formation;
}

export interface SubmitFormationResponse {
  accepted: boolean;
  matchId: string;
  playerId: string;
  status: MatchStatus | "awaiting_other_formation";
  phase: "formation" | "finished";
  note: string;
  playerFinalTeam?: EmojiId[];
  opponentFinalTeam?: EmojiId[];
  playerFormation?: Formation;
  opponentFormation?: Formation;
  battleState?: BattleState | null;
  winner?: Winner;
  whySummary?: string;
  whyChain?: string[];
}

export interface QueueOrJoinMatchRequest {
  userId: string;
  deckId: string;
  playerDeck: EmojiId[];
  matchId?: string;
  forceFreshEntry?: boolean;
}

export interface QueueOrJoinMatchResponse {
  status: "queued" | "matched";
  queueTicket: string;
  userId: string;
  deckId: string;
  estimatedWaitSeconds: number;
  phaseDeadlineAt?: string;
  phaseTimeoutSecondsRemaining?: number;
  note: string;
  matchId?: string;
  opponentUserId?: string;
  rulesVersion?: string;
  playerSide?: "player_a" | "player_b";
  phase?: "queue" | "ban" | "formation" | "finished";
  playerDeck?: EmojiId[];
  opponentDeck?: EmojiId[];
  playerBannedEmojiId?: EmojiId | null;
  opponentBannedEmojiId?: EmojiId | null;
  playerFinalTeam?: EmojiId[];
  opponentFinalTeam?: EmojiId[];
  playerFormation?: Formation;
  opponentFormation?: Formation;
  battleState?: BattleState | null;
  winner?: Winner;
  whySummary?: string;
  whyChain?: string[];
}

export interface CancelRankedQueueRequest {
  userId: string;
  matchId?: string;
}

export interface CancelRankedQueueResponse {
  cancelled: boolean;
  matchId: string;
  status: "cancelled" | "not_found" | "not_queued";
  note: string;
}

export interface LeaderboardEntry {
  rank: number;
  userId: string;
  displayName: string;
  currentElo: number;
  wins: number;
  losses: number;
  isCurrentUser: boolean;
}

export interface GetLeaderboardRequest {
  limit?: number;
}

export interface GetLeaderboardResponse {
  entries: LeaderboardEntry[];
  nearbyEntries?: LeaderboardEntry[];
  myEntry: LeaderboardEntry | null;
  totalRatedPlayers: number;
  note: string;
}

export const FORMATION_SLOT_ORDER: FormationSlot[] = [
  "front_left",
  "front_center",
  "front_right",
  "back_left",
  "back_right",
];
