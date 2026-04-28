import type { EmojiId, Formation, FormationPlacement, FormationSlot, Winner } from "./contracts.ts";
import { FORMATION_SLOT_ORDER } from "./contracts.ts";
import { buildDefaultFormation, resolveBattle, selectBattleTeamFromDeck } from "./battle-simulator.ts";
import { chooseBotBan } from "./bot-engine.ts";
import { EMOJI_DEFINITIONS, EMOJI_IDS } from "./emoji-definitions.ts";

type SquadStrategy = "random" | "heuristic";
type BanStrategy = "random" | "heuristic";
type FormationStrategy = "random" | "auto_fill" | "heuristic";
type Perspective = "player" | "opponent";

interface SimulationConfig {
  seed: number;
  randomSquadMatchCount: number;
  heuristicSquadMatchCount: number;
  heuristicBanMatchCount: number;
  autoFillRandomFormationMatchCount: number;
  autoFillHeuristicFormationMatchCount: number;
  heuristicMirrorMatchCount: number;
  sampledTopCoreCount: number;
  sampledTopCoreOpponentSamples: number;
  outputDirectory: string;
}

interface ScenarioDefinition {
  name: string;
  matchCount: number;
  player: StrategyBundle;
  opponent: StrategyBundle;
  trackBanLeverage: boolean;
}

interface StrategyBundle {
  squad: SquadStrategy;
  ban: BanStrategy;
  formation: FormationStrategy;
}

interface ScenarioSummary {
  scenario: string;
  matchCount: number;
  playerWins: number;
  opponentWins: number;
  draws: number;
  playerWinRate: number;
  opponentWinRate: number;
  drawRate: number;
  averageAliveMargin: number;
  averageHpMargin: number;
}

interface SquadStatsRow {
  scenario: string;
  perspective: Perspective;
  squadCore: string;
  appearances: number;
  wins: number;
  losses: number;
  draws: number;
  winRate: number;
  lossRate: number;
  drawRate: number;
  averageAliveMargin: number;
  averageHpMargin: number;
}

interface UnitPostBanStatsRow {
  scenario: string;
  perspective: Perspective;
  unit: EmojiId;
  appearances: number;
  wins: number;
  losses: number;
  draws: number;
  winRate: number;
  survivalRate: number;
  averageHpRemaining: number;
  averageTeamAliveAtEnd: number;
}

interface BanStatsRow {
  scenario: string;
  perspective: Perspective;
  bannedUnit: EmojiId;
  count: number;
  banRate: number;
  winRateAfterBan: number;
  averageAliveMargin: number;
  averageHpMargin: number;
  averageBanLeverage: number;
}

interface FormationSlotStatsRow {
  scenario: string;
  perspective: Perspective;
  slot: FormationSlot;
  unit: EmojiId;
  appearances: number;
  wins: number;
  losses: number;
  draws: number;
  winRate: number;
  survivalRate: number;
  averageHpRemaining: number;
}

interface FormationShapeStatsRow {
  scenario: string;
  perspective: Perspective;
  shape: string;
  appearances: number;
  wins: number;
  losses: number;
  draws: number;
  winRate: number;
  averageAliveMargin: number;
}

interface LeftRightSlotSymmetryRow {
  scenario: string;
  perspective: Perspective;
  laneGroup: "frontline" | "backline";
  leftAppearances: number;
  rightAppearances: number;
  leftWinRate: number;
  rightWinRate: number;
  leftSurvivalRate: number;
  rightSurvivalRate: number;
  leftAverageHpRemaining: number;
  rightAverageHpRemaining: number;
  survivalGap: number;
  winRateGap: number;
}

interface AutoFillStrategyRow {
  scenario: string;
  perspective: Perspective;
  strategy: FormationStrategy;
  matches: number;
  wins: number;
  losses: number;
  draws: number;
  winRate: number;
  averageAliveMargin: number;
  averageHpMargin: number;
}

interface CoreShellFrequencyRow {
  scenario: string;
  shellSize: number;
  shell: string;
  appearances: number;
  averageParentWinRate: number;
  bestParentCore: string;
}

interface MatchupCoreStatsRow {
  scenario: string;
  playerCore: string;
  opponentCore: string;
  matches: number;
  playerWins: number;
  opponentWins: number;
  draws: number;
  playerWinRate: number;
  drawRate: number;
  averageAliveMargin: number;
  averageHpMargin: number;
}

interface FlaggedFinding {
  severity: "Warning" | "Critical";
  category: string;
  finding: string;
  metric: string;
  threshold: string;
}

interface MatchRecord {
  scenario: string;
  playerDeck: EmojiId[];
  opponentDeck: EmojiId[];
  playerBan: EmojiId;
  opponentBan: EmojiId;
  playerTeam: EmojiId[];
  opponentTeam: EmojiId[];
  playerFormation: Formation;
  opponentFormation: Formation;
  playerFormationStrategy: FormationStrategy;
  opponentFormationStrategy: FormationStrategy;
  winner: Winner;
  playerAliveCount: number;
  opponentAliveCount: number;
  playerHpRemaining: number;
  opponentHpRemaining: number;
  playerBanLeverage?: number;
  opponentBanLeverage?: number;
}

interface CounterfactualSideContext {
  side: Perspective;
  actorTeam: EmojiId[];
  actorStrategy: StrategyBundle;
  targetDeck: EmojiId[];
  targetStrategy: StrategyBundle;
  battleSeedBase: string;
}

interface StatsAccumulator {
  appearances: number;
  wins: number;
  losses: number;
  draws: number;
  aliveMarginTotal: number;
  hpMarginTotal: number;
}

interface UnitAccumulator extends StatsAccumulator {
  survivors: number;
  hpRemainingTotal: number;
  teamAliveTotal: number;
}

interface BanAccumulator extends StatsAccumulator {
  leverageTotal: number;
  leverageSamples: number;
}

const DEFAULT_CONFIG: SimulationConfig = {
  seed: 12345,
  randomSquadMatchCount: 2000,
  heuristicSquadMatchCount: 2000,
  heuristicBanMatchCount: 2000,
  autoFillRandomFormationMatchCount: 2000,
  autoFillHeuristicFormationMatchCount: 2000,
  heuristicMirrorMatchCount: 2000,
  sampledTopCoreCount: 250,
  sampledTopCoreOpponentSamples: 40,
  outputDirectory: "",
};

const FRONT_SLOTS: FormationSlot[] = ["front_left", "front_center", "front_right"];
const SUPPORT_UNITS = new Set<EmojiId>(["soap", "heart", "shield"]);
const FRONTLINE_UNITS = new Set<EmojiId>(["shield", "chain", "ice", "water", "fire"]);
const BACKLINE_THREATS = new Set<EmojiId>(["ghost", "bomb", "snake", "lightning", "plant", "wind"]);

const TEAM_SYNERGY_BONUSES: Array<{ left: EmojiId; right: EmojiId; bonus: number }> = [
  { left: "shield", right: "plant", bonus: 14 },
  { left: "shield", right: "heart", bonus: 12 },
  { left: "shield", right: "soap", bonus: 12 },
  { left: "ice", right: "shield", bonus: 10 },
  { left: "soap", right: "heart", bonus: 8 },
  { left: "water", right: "ice", bonus: 8 },
  { left: "snake", right: "heart", bonus: 6 },
  { left: "magnet", right: "bomb", bonus: 18 },
  { left: "chain", right: "wind", bonus: 12 },
  { left: "chain", right: "ghost", bonus: 10 },
  { left: "ghost", right: "lightning", bonus: 8 },
  { left: "wind", right: "bomb", bonus: 10 },
];

class SeededRandom {
  private state: number;

  constructor(seed: number) {
    this.state = seed >>> 0;
    if (this.state === 0) {
      this.state = 0x6d2b79f5;
    }
  }

  next(): number {
    this.state = (Math.imul(this.state, 1664525) + 1013904223) >>> 0;
    return this.state / 0x100000000;
  }

  nextInt(maxExclusive: number): number {
    return Math.floor(this.next() * maxExclusive);
  }

  pick<T>(items: readonly T[]): T {
    return items[this.nextInt(items.length)];
  }

  shuffle<T>(items: readonly T[]): T[] {
    const copy = [...items];
    for (let index = copy.length - 1; index > 0; index -= 1) {
      const swapIndex = this.nextInt(index + 1);
      const temp = copy[index];
      copy[index] = copy[swapIndex];
      copy[swapIndex] = temp;
    }

    return copy;
  }
}

function hashParts(...parts: Array<string | number>): number {
  let hash = 2166136261;
  for (const part of parts) {
    const text = String(part);
    for (let index = 0; index < text.length; index += 1) {
      hash ^= text.charCodeAt(index);
      hash = Math.imul(hash, 16777619);
    }
  }

  return (hash >>> 0) || 1;
}

function squadSignature(deck: readonly EmojiId[]): string {
  return [...deck].sort().join(">");
}

function outcomeFromPerspective(winner: Winner, perspective: Perspective): number {
  if (winner === "draw") {
    return 0;
  }

  if (winner === "player_a") {
    return perspective === "player" ? 1 : -1;
  }

  return perspective === "player" ? -1 : 1;
}

function aliveCount(team: Array<{ alive: boolean }>): number {
  return team.filter((unit) => unit.alive).length;
}

function hpRemaining(team: Array<{ alive: boolean; hp: number }>): number {
  return team.filter((unit) => unit.alive).reduce((sum, unit) => sum + unit.hp, 0);
}

function battleScore(winner: Winner, aliveMargin: number, hpMargin: number): number {
  const outcome = winner === "player_a" ? 120 : winner === "player_b" ? -120 : 0;
  return outcome + (aliveMargin * 16) + hpMargin;
}

function createCsv(rows: Array<Record<string, unknown>>, columns: string[]): string {
  const escape = (value: unknown): string => {
    const text = value == null ? "" : String(value);
    if (text.includes(",") || text.includes("\"") || text.includes("\n")) {
      return `"${text.replaceAll("\"", "\"\"")}"`;
    }

    return text;
  };

  const lines = [columns.join(",")];
  for (const row of rows) {
    lines.push(columns.map((column) => escape(row[column])).join(","));
  }

  return `${lines.join("\n")}\n`;
}

function toRate(value: number, total: number): number {
  return total <= 0 ? 0 : value / total;
}

function formatPct(value: number): string {
  return `${(value * 100).toFixed(2)}%`;
}

function isSupport(emojiId: EmojiId): boolean {
  const tags = EMOJI_DEFINITIONS[emojiId].stats.tags;
  return SUPPORT_UNITS.has(emojiId) || tags.includes("support") || tags.includes("heal") || tags.includes("cleanse");
}

function isFrontliner(emojiId: EmojiId): boolean {
  const stats = EMOJI_DEFINITIONS[emojiId].stats;
  return FRONTLINE_UNITS.has(emojiId) || stats.preferredRow === "front" || stats.tags.includes("frontline") || stats.tags.includes("tank");
}

function isBacklineThreat(emojiId: EmojiId): boolean {
  const stats = EMOJI_DEFINITIONS[emojiId].stats;
  return BACKLINE_THREATS.has(emojiId) || stats.tags.includes("backline_reach") || stats.tags.includes("heavy");
}

function chooseCombinations(values: readonly EmojiId[], size: number): EmojiId[][] {
  const result: EmojiId[][] = [];
  const path: EmojiId[] = [];

  function visit(start: number): void {
    if (path.length === size) {
      result.push([...path]);
      return;
    }

    for (let index = start; index <= values.length - (size - path.length); index += 1) {
      path.push(values[index]);
      visit(index + 1);
      path.pop();
    }
  }

  visit(0);
  return result;
}

const ALL_SQUADS = chooseCombinations(EMOJI_IDS, 6);

function scoreSquad(deck: readonly EmojiId[]): number {
  let score = 0;
  let totalHp = 0;
  let totalAttack = 0;
  let totalSpeed = 0;
  let frontliners = 0;
  let supports = 0;
  let threats = 0;
  const coveredCounters = new Set<EmojiId>();

  for (const emojiId of deck) {
    const definition = EMOJI_DEFINITIONS[emojiId];
    totalHp += definition.stats.hp;
    totalAttack += definition.stats.attack;
    totalSpeed += definition.stats.speed;
    if (isFrontliner(emojiId)) {
      frontliners += 1;
    }

    if (isSupport(emojiId)) {
      supports += 1;
    }

    if (isBacklineThreat(emojiId) || definition.stats.attack >= 3) {
      threats += 1;
    }

    for (const counter of definition.counters) {
      coveredCounters.add(counter);
    }
  }

  score += totalHp * 1.1;
  score += totalAttack * 2.0;
  score += totalSpeed * 0.8;
  score += frontliners >= 2 ? 18 : -18;
  score += frontliners >= 3 ? 6 : 0;
  score += supports >= 1 ? 12 : -14;
  score += supports >= 2 ? 4 : 0;
  score += threats >= 2 ? 10 : -8;
  score += coveredCounters.size * 1.2;

  if (deck.includes("magnet") && deck.includes("bomb")) {
    score += 14;
  }

  if (deck.includes("shield") && deck.includes("heart")) {
    score += 10;
  }

  if (deck.includes("soap") && deck.includes("heart")) {
    score += 8;
  }

  if (deck.includes("chain") && deck.includes("wind")) {
    score += 8;
  }

  if (deck.includes("ghost") && deck.includes("lightning")) {
    score += 6;
  }

  for (const pair of TEAM_SYNERGY_BONUSES) {
    if (deck.includes(pair.left) && deck.includes(pair.right)) {
      score += pair.bonus * 0.35;
    }
  }

  return score;
}

const HEURISTIC_SQUAD_RANKING = ALL_SQUADS
  .map((deck) => ({ deck, score: scoreSquad(deck) }))
  .sort((left, right) => right.score - left.score || squadSignature(left.deck).localeCompare(squadSignature(right.deck)));

function pickRandomSquad(rng: SeededRandom): EmojiId[] {
  return [...ALL_SQUADS[rng.nextInt(ALL_SQUADS.length)]];
}

function pickHeuristicSquad(rng: SeededRandom): EmojiId[] {
  const window = Math.min(128, HEURISTIC_SQUAD_RANKING.length);
  const totalWeight = (window * (window + 1)) / 2;
  let needle = rng.next() * totalWeight;
  for (let index = 0; index < window; index += 1) {
    const weight = window - index;
    if (needle <= weight) {
      return [...HEURISTIC_SQUAD_RANKING[index].deck];
    }

    needle -= weight;
  }

  return [...HEURISTIC_SQUAD_RANKING[0].deck];
}

function removeBannedUnit(deck: readonly EmojiId[], bannedUnit: EmojiId): EmojiId[] {
  return selectBattleTeamFromDeck(deck.filter((emojiId) => emojiId !== bannedUnit), 5).team;
}

function buildFormationFromRanking(
  team: readonly EmojiId[],
  frontScore: (emojiId: EmojiId) => number,
  backScore: (emojiId: EmojiId) => number,
): Formation {
  const sortedFront = [...team].sort((left, right) => frontScore(right) - frontScore(left));
  const front = sortedFront.slice(0, 3);
  const remaining = team.filter((emojiId) => !front.includes(emojiId));
  const back = [...remaining].sort((left, right) => backScore(right) - backScore(left)).slice(0, 2);

  const placements: FormationPlacement[] = [];
  if (front[0]) placements.push({ slot: "front_center", emojiId: front[0] });
  if (front[1]) placements.push({ slot: "front_left", emojiId: front[1] });
  if (front[2]) placements.push({ slot: "front_right", emojiId: front[2] });
  if (back[0]) placements.push({ slot: "back_left", emojiId: back[0] });
  if (back[1]) placements.push({ slot: "back_right", emojiId: back[1] });

  const used = new Set(placements.map((placement) => placement.emojiId));
  for (const slot of FORMATION_SLOT_ORDER) {
    if (placements.some((placement) => placement.slot === slot)) {
      continue;
    }

    const nextEmoji = team.find((emojiId) => !used.has(emojiId));
    if (nextEmoji) {
      placements.push({ slot, emojiId: nextEmoji });
      used.add(nextEmoji);
    }
  }

  placements.sort((left, right) => FORMATION_SLOT_ORDER.indexOf(left.slot) - FORMATION_SLOT_ORDER.indexOf(right.slot));
  return { placements };
}

function randomFormation(team: readonly EmojiId[], seed: number): Formation {
  const rng = new SeededRandom(seed);
  const shuffled = rng.shuffle(team);
  return {
    placements: FORMATION_SLOT_ORDER.map((slot, index) => ({
      slot,
      emojiId: shuffled[index],
    })),
  };
}

function formationSignature(formation: Formation): string {
  return formation.placements
    .slice()
    .sort((left, right) => FORMATION_SLOT_ORDER.indexOf(left.slot) - FORMATION_SLOT_ORDER.indexOf(right.slot))
    .map((placement) => `${placement.slot}:${placement.emojiId}`)
    .join("|");
}

function uniqueFormations(candidates: Formation[]): Formation[] {
  const unique = new Map<string, Formation>();
  for (const formation of candidates) {
    unique.set(formationSignature(formation), formation);
  }

  return [...unique.values()];
}

function buildHeuristicFormation(team: readonly EmojiId[], opponents: readonly EmojiId[], seed: number): Formation {
  const candidates = uniqueFormations([
    buildDefaultFormation([...team]),
    buildFormationFromRanking(
      team,
      (emojiId) => {
        const stats = EMOJI_DEFINITIONS[emojiId].stats;
        return (isFrontliner(emojiId) ? 60 : 0) + (stats.hp * 10) + (stats.attack * 4) + (stats.preferredRow === "front" ? 18 : 0);
      },
      (emojiId) => {
        const stats = EMOJI_DEFINITIONS[emojiId].stats;
        return (isSupport(emojiId) ? 50 : 0) + (stats.speed * 8) + (stats.attack * 5) + (stats.preferredRow === "back" ? 18 : 0);
      },
    ),
    buildFormationFromRanking(
      team,
      (emojiId) => {
        const stats = EMOJI_DEFINITIONS[emojiId].stats;
        return (stats.attack * 10) + (stats.speed * 7) + (isBacklineThreat(emojiId) ? 20 : 0);
      },
      (emojiId) => {
        const stats = EMOJI_DEFINITIONS[emojiId].stats;
        return (isSupport(emojiId) ? 60 : 0) + (stats.speed * 9) + (stats.preferredRow === "back" ? 20 : 0);
      },
    ),
    buildFormationFromRanking(
      team,
      (emojiId) => {
        const stats = EMOJI_DEFINITIONS[emojiId].stats;
        return (["shield", "chain", "ice", "water"].includes(emojiId) ? 80 : 0) + (stats.hp * 9) + (stats.attack * 3);
      },
      (emojiId) => {
        const stats = EMOJI_DEFINITIONS[emojiId].stats;
        return (["heart", "soap", "plant", "mirror"].includes(emojiId) ? 80 : 0) + (stats.speed * 8);
      },
    ),
    ...Array.from({ length: 6 }, (_, index) => randomFormation(team, hashParts(seed, "candidate", index))),
  ]);

  const opponentDefault = buildDefaultFormation([...opponents]);
  const scored = candidates.map((candidate, index) => {
    const result = resolveBattle({
      mode: "pvp_ranked",
      rulesVersion: "launch-5v5-001",
      battleSeed: `heuristic-formation-${seed}-${index}`,
      teamA: [...team],
      teamB: [...opponents],
      formationA: candidate,
      formationB: opponentDefault,
    });
    const playerAlive = aliveCount(result.battleState.teamA);
    const opponentAlive = aliveCount(result.battleState.teamB);
    const playerHp = hpRemaining(result.battleState.teamA);
    const opponentHp = hpRemaining(result.battleState.teamB);
    return {
      candidate,
      score: battleScore(result.winner, playerAlive - opponentAlive, playerHp - opponentHp),
    };
  }).sort((left, right) => right.score - left.score);

  const top = scored.slice(0, Math.min(3, scored.length));
  const chooser = new SeededRandom(hashParts(seed, "formation-choice"));
  const totalWeight = top.reduce((sum, _, index) => sum + (top.length - index), 0);
  let needle = chooser.next() * totalWeight;
  for (let index = 0; index < top.length; index += 1) {
    const weight = top.length - index;
    if (needle <= weight) {
      return top[index].candidate;
    }

    needle -= weight;
  }

  return top[0].candidate;
}

function chooseDeck(strategy: SquadStrategy, seed: number): EmojiId[] {
  const rng = new SeededRandom(seed);
  return strategy === "heuristic" ? pickHeuristicSquad(rng) : pickRandomSquad(rng);
}

function chooseBan(strategy: BanStrategy, opponentDeck: readonly EmojiId[], seed: number): EmojiId {
  if (strategy === "heuristic") {
    return chooseBotBan("bot_smart", [...opponentDeck]);
  }

  const rng = new SeededRandom(seed);
  return rng.pick(opponentDeck);
}

function chooseFormation(
  strategy: FormationStrategy,
  team: readonly EmojiId[],
  opponents: readonly EmojiId[],
  seed: number,
): Formation {
  if (strategy === "auto_fill") {
    return buildDefaultFormation([...team]);
  }

  if (strategy === "heuristic") {
    return buildHeuristicFormation(team, opponents, seed);
  }

  return randomFormation(team, seed);
}

function shapeSignature(formation: Formation): string {
  const rowCode = (emojiId: EmojiId): string => {
    const preferred = EMOJI_DEFINITIONS[emojiId].stats.preferredRow;
    return preferred === "front" ? "F" : preferred === "back" ? "B" : "X";
  };

  const placements = FORMATION_SLOT_ORDER.map((slot) => formation.placements.find((placement) => placement.slot === slot));
  return `${rowCode(placements[0]!.emojiId)}${rowCode(placements[1]!.emojiId)}${rowCode(placements[2]!.emojiId)}-${rowCode(placements[3]!.emojiId)}${rowCode(placements[4]!.emojiId)}`;
}

function createScenarioDefinitions(config: SimulationConfig): ScenarioDefinition[] {
  return [
    {
      name: "RandomSquadVsRandomSquad",
      matchCount: config.randomSquadMatchCount,
      player: { squad: "random", ban: "random", formation: "random" },
      opponent: { squad: "random", ban: "random", formation: "random" },
      trackBanLeverage: true,
    },
    {
      name: "HeuristicSquadVsRandomSquad",
      matchCount: config.heuristicSquadMatchCount,
      player: { squad: "heuristic", ban: "random", formation: "auto_fill" },
      opponent: { squad: "random", ban: "random", formation: "auto_fill" },
      trackBanLeverage: false,
    },
    {
      name: "HeuristicBanVsRandomBan",
      matchCount: config.heuristicBanMatchCount,
      player: { squad: "random", ban: "heuristic", formation: "auto_fill" },
      opponent: { squad: "random", ban: "random", formation: "auto_fill" },
      trackBanLeverage: true,
    },
    {
      name: "AutoFillVsRandomFormation",
      matchCount: config.autoFillRandomFormationMatchCount,
      player: { squad: "random", ban: "random", formation: "auto_fill" },
      opponent: { squad: "random", ban: "random", formation: "random" },
      trackBanLeverage: false,
    },
    {
      name: "AutoFillVsHeuristicFormation",
      matchCount: config.autoFillHeuristicFormationMatchCount,
      player: { squad: "random", ban: "random", formation: "auto_fill" },
      opponent: { squad: "random", ban: "random", formation: "heuristic" },
      trackBanLeverage: false,
    },
    {
      name: "HeuristicSquadBanFormationMirror",
      matchCount: config.heuristicMirrorMatchCount,
      player: { squad: "heuristic", ban: "heuristic", formation: "heuristic" },
      opponent: { squad: "heuristic", ban: "heuristic", formation: "heuristic" },
      trackBanLeverage: true,
    },
  ];
}

function simulateBattle(
  scenario: ScenarioDefinition,
  matchIndex: number,
  baseSeed: number,
): MatchRecord {
  const playerDeck = chooseDeck(scenario.player.squad, hashParts(baseSeed, scenario.name, matchIndex, "player-deck"));
  const opponentDeck = chooseDeck(scenario.opponent.squad, hashParts(baseSeed, scenario.name, matchIndex, "opponent-deck"));

  const playerBan = chooseBan(scenario.player.ban, opponentDeck, hashParts(baseSeed, scenario.name, matchIndex, "player-ban"));
  const opponentBan = chooseBan(scenario.opponent.ban, playerDeck, hashParts(baseSeed, scenario.name, matchIndex, "opponent-ban"));

  const playerTeam = removeBannedUnit(playerDeck, opponentBan);
  const opponentTeam = removeBannedUnit(opponentDeck, playerBan);

  const playerFormation = chooseFormation(
    scenario.player.formation,
    playerTeam,
    opponentTeam,
    hashParts(baseSeed, scenario.name, matchIndex, "player-formation"),
  );
  const opponentFormation = chooseFormation(
    scenario.opponent.formation,
    opponentTeam,
    playerTeam,
    hashParts(baseSeed, scenario.name, matchIndex, "opponent-formation"),
  );

  const battleSeed = `${scenario.name}-${baseSeed}-${matchIndex}`;
  const result = resolveBattle({
    mode: "pvp_ranked",
    rulesVersion: "launch-5v5-001",
    battleSeed,
    teamA: playerTeam,
    teamB: opponentTeam,
    formationA: playerFormation,
    formationB: opponentFormation,
  });

  const playerAlive = aliveCount(result.battleState.teamA);
  const opponentAlive = aliveCount(result.battleState.teamB);
  const playerHp = hpRemaining(result.battleState.teamA);
  const opponentHp = hpRemaining(result.battleState.teamB);

  const record: MatchRecord = {
    scenario: scenario.name,
    playerDeck,
    opponentDeck,
    playerBan,
    opponentBan,
    playerTeam,
    opponentTeam,
    playerFormation,
    opponentFormation,
    playerFormationStrategy: scenario.player.formation,
    opponentFormationStrategy: scenario.opponent.formation,
    winner: result.winner,
    playerAliveCount: playerAlive,
    opponentAliveCount: opponentAlive,
    playerHpRemaining: playerHp,
    opponentHpRemaining: opponentHp,
  };

  if (scenario.trackBanLeverage) {
    record.playerBanLeverage = evaluateBanLeverage({
      side: "player",
      actorTeam: playerTeam,
      actorStrategy: scenario.player,
      targetDeck: opponentDeck,
      targetStrategy: scenario.opponent,
      battleSeedBase: `${battleSeed}-player-ban`,
    }, playerBan);

    record.opponentBanLeverage = evaluateBanLeverage({
      side: "opponent",
      actorTeam: opponentTeam,
      actorStrategy: scenario.opponent,
      targetDeck: playerDeck,
      targetStrategy: scenario.player,
      battleSeedBase: `${battleSeed}-opponent-ban`,
    }, opponentBan);
  }

  return record;
}

function evaluateBanLeverage(context: CounterfactualSideContext, actualBannedUnit: EmojiId): number {
  const scores: Array<{ bannedUnit: EmojiId; score: number }> = [];

  for (const bannedUnit of context.targetDeck) {
    const targetTeam = removeBannedUnit(context.targetDeck, bannedUnit);
    const actorFormation = chooseFormation(
      context.actorStrategy.formation,
      context.actorTeam,
      targetTeam,
      hashParts(context.battleSeedBase, context.side, bannedUnit, "actor"),
    );
    const targetFormation = chooseFormation(
      context.targetStrategy.formation,
      targetTeam,
      context.actorTeam,
      hashParts(context.battleSeedBase, context.side, bannedUnit, "target"),
    );

    const result = resolveBattle({
      mode: "pvp_ranked",
      rulesVersion: "launch-5v5-001",
      battleSeed: `${context.battleSeedBase}-${bannedUnit}`,
      teamA: context.side === "player" ? context.actorTeam : targetTeam,
      teamB: context.side === "player" ? targetTeam : context.actorTeam,
      formationA: context.side === "player" ? actorFormation : targetFormation,
      formationB: context.side === "player" ? targetFormation : actorFormation,
    });

    const teamAAlive = aliveCount(result.battleState.teamA);
    const teamBAlive = aliveCount(result.battleState.teamB);
    const teamAHp = hpRemaining(result.battleState.teamA);
    const teamBHp = hpRemaining(result.battleState.teamB);
    const rawScore = battleScore(result.winner, teamAAlive - teamBAlive, teamAHp - teamBHp);
    scores.push({
      bannedUnit,
      score: context.side === "player" ? rawScore : -rawScore,
    });
  }

  const actual = scores.find((entry) => entry.bannedUnit === actualBannedUnit)?.score ?? 0;
  const alternatives = scores.filter((entry) => entry.bannedUnit !== actualBannedUnit);
  const averageAlternative = alternatives.length === 0
    ? 0
    : alternatives.reduce((sum, entry) => sum + entry.score, 0) / alternatives.length;

  return actual - averageAlternative;
}

function createAccumulator<T extends StatsAccumulator>(factory: () => T): {
  map: Map<string, T>;
  get: (key: string) => T;
} {
  const map = new Map<string, T>();
  return {
    map,
    get(key: string): T {
      if (!map.has(key)) {
        map.set(key, factory());
      }

      return map.get(key)!;
    },
  };
}

function recordOutcome(accumulator: StatsAccumulator, outcome: number, aliveMargin: number, hpMargin: number): void {
  accumulator.appearances += 1;
  if (outcome > 0) {
    accumulator.wins += 1;
  } else if (outcome < 0) {
    accumulator.losses += 1;
  } else {
    accumulator.draws += 1;
  }

  accumulator.aliveMarginTotal += aliveMargin;
  accumulator.hpMarginTotal += hpMargin;
}

function runSampledTopCoreSearch(
  config: SimulationConfig,
  scenarioSummaries: ScenarioSummary[],
  squadStats: ReturnType<typeof createAccumulator<StatsAccumulator>>,
  matchupStats: ReturnType<typeof createAccumulator<StatsAccumulator>>,
): void {
  const scenarioName = "SampledTopCoreSearch";
  let playerWins = 0;
  let opponentWins = 0;
  let draws = 0;
  let aliveMarginTotal = 0;
  let hpMarginTotal = 0;
  let matches = 0;

  const sampledCores = HEURISTIC_SQUAD_RANKING.slice(0, Math.max(0, Math.min(config.sampledTopCoreCount, HEURISTIC_SQUAD_RANKING.length)));
  for (let coreIndex = 0; coreIndex < sampledCores.length; coreIndex += 1) {
    const playerDeck = [...sampledCores[coreIndex].deck];
    for (let opponentIndex = 0; opponentIndex < config.sampledTopCoreOpponentSamples; opponentIndex += 1) {
      const opponentDeck = chooseDeck("random", hashParts(config.seed, scenarioName, coreIndex, opponentIndex, "opponent"));
      const playerBan = chooseBan("random", opponentDeck, hashParts(config.seed, scenarioName, coreIndex, opponentIndex, "pban"));
      const opponentBan = chooseBan("random", playerDeck, hashParts(config.seed, scenarioName, coreIndex, opponentIndex, "oban"));
      const playerTeam = removeBannedUnit(playerDeck, opponentBan);
      const opponentTeam = removeBannedUnit(opponentDeck, playerBan);
      const playerFormation = buildDefaultFormation(playerTeam);
      const opponentFormation = buildDefaultFormation(opponentTeam);
      const result = resolveBattle({
        mode: "pvp_ranked",
        rulesVersion: "launch-5v5-001",
        battleSeed: `${scenarioName}-${coreIndex}-${opponentIndex}-${config.seed}`,
        teamA: playerTeam,
        teamB: opponentTeam,
        formationA: playerFormation,
        formationB: opponentFormation,
      });

      const playerAlive = aliveCount(result.battleState.teamA);
      const opponentAlive = aliveCount(result.battleState.teamB);
      const playerHp = hpRemaining(result.battleState.teamA);
      const opponentHp = hpRemaining(result.battleState.teamB);
      const aliveMargin = playerAlive - opponentAlive;
      const hpMargin = playerHp - opponentHp;
      const outcome = outcomeFromPerspective(result.winner, "player");

      if (outcome > 0) {
        playerWins += 1;
      } else if (outcome < 0) {
        opponentWins += 1;
      } else {
        draws += 1;
      }

      aliveMarginTotal += aliveMargin;
      hpMarginTotal += hpMargin;
      matches += 1;

      recordOutcome(
        squadStats.get(`${scenarioName}|player|${squadSignature(playerDeck)}`),
        outcome,
        aliveMargin,
        hpMargin,
      );
      recordOutcome(
        squadStats.get(`${scenarioName}|opponent|${squadSignature(opponentDeck)}`),
        -outcome,
        -aliveMargin,
        -hpMargin,
      );
      recordOutcome(
        matchupStats.get(`${scenarioName}|${squadSignature(playerDeck)}|${squadSignature(opponentDeck)}`),
        outcome,
        aliveMargin,
        hpMargin,
      );
    }
  }

  scenarioSummaries.push({
    scenario: scenarioName,
    matchCount: matches,
    playerWins,
    opponentWins,
    draws,
    playerWinRate: toRate(playerWins, matches),
    opponentWinRate: toRate(opponentWins, matches),
    drawRate: toRate(draws, matches),
    averageAliveMargin: matches === 0 ? 0 : aliveMarginTotal / matches,
    averageHpMargin: matches === 0 ? 0 : hpMarginTotal / matches,
  });
}

function buildRows(config: SimulationConfig) {
  const scenarioSummaries: ScenarioSummary[] = [];
  const squadStats = createAccumulator<StatsAccumulator>(() => ({
    appearances: 0,
    wins: 0,
    losses: 0,
    draws: 0,
    aliveMarginTotal: 0,
    hpMarginTotal: 0,
  }));
  const unitStats = createAccumulator<UnitAccumulator>(() => ({
    appearances: 0,
    wins: 0,
    losses: 0,
    draws: 0,
    aliveMarginTotal: 0,
    hpMarginTotal: 0,
    survivors: 0,
    hpRemainingTotal: 0,
    teamAliveTotal: 0,
  }));
  const banStats = createAccumulator<BanAccumulator>(() => ({
    appearances: 0,
    wins: 0,
    losses: 0,
    draws: 0,
    aliveMarginTotal: 0,
    hpMarginTotal: 0,
    leverageTotal: 0,
    leverageSamples: 0,
  }));
  const slotStats = createAccumulator<UnitAccumulator>(() => ({
    appearances: 0,
    wins: 0,
    losses: 0,
    draws: 0,
    aliveMarginTotal: 0,
    hpMarginTotal: 0,
    survivors: 0,
    hpRemainingTotal: 0,
    teamAliveTotal: 0,
  }));
  const shapeStats = createAccumulator<StatsAccumulator>(() => ({
    appearances: 0,
    wins: 0,
    losses: 0,
    draws: 0,
    aliveMarginTotal: 0,
    hpMarginTotal: 0,
  }));
  const autoFillStats = createAccumulator<StatsAccumulator>(() => ({
    appearances: 0,
    wins: 0,
    losses: 0,
    draws: 0,
    aliveMarginTotal: 0,
    hpMarginTotal: 0,
  }));
  const matchupStats = createAccumulator<StatsAccumulator>(() => ({
    appearances: 0,
    wins: 0,
    losses: 0,
    draws: 0,
    aliveMarginTotal: 0,
    hpMarginTotal: 0,
  }));

  for (const scenario of createScenarioDefinitions(config)) {
    let playerWins = 0;
    let opponentWins = 0;
    let draws = 0;
    let aliveMarginTotal = 0;
    let hpMarginTotal = 0;

    for (let matchIndex = 0; matchIndex < scenario.matchCount; matchIndex += 1) {
      const match = simulateBattle(scenario, matchIndex, config.seed);
      const playerAliveMargin = match.playerAliveCount - match.opponentAliveCount;
      const playerHpMargin = match.playerHpRemaining - match.opponentHpRemaining;
      const playerOutcome = outcomeFromPerspective(match.winner, "player");
      const opponentOutcome = -playerOutcome;

      if (playerOutcome > 0) {
        playerWins += 1;
      } else if (playerOutcome < 0) {
        opponentWins += 1;
      } else {
        draws += 1;
      }

      aliveMarginTotal += playerAliveMargin;
      hpMarginTotal += playerHpMargin;

      recordOutcome(squadStats.get(`${scenario.name}|player|${squadSignature(match.playerDeck)}`), playerOutcome, playerAliveMargin, playerHpMargin);
      recordOutcome(squadStats.get(`${scenario.name}|opponent|${squadSignature(match.opponentDeck)}`), opponentOutcome, -playerAliveMargin, -playerHpMargin);
      recordOutcome(matchupStats.get(`${scenario.name}|${squadSignature(match.playerDeck)}|${squadSignature(match.opponentDeck)}`), playerOutcome, playerAliveMargin, playerHpMargin);

      const playerBattleUnits = new Map(match.playerTeam.map((emojiId) => [emojiId, emojiId]));
      const opponentBattleUnits = new Map(match.opponentTeam.map((emojiId) => [emojiId, emojiId]));
      void playerBattleUnits;
      void opponentBattleUnits;

      const resolved = resolveBattle({
        mode: "pvp_ranked",
        rulesVersion: "launch-5v5-001",
        battleSeed: `${scenario.name}-record-${config.seed}-${matchIndex}`,
        teamA: match.playerTeam,
        teamB: match.opponentTeam,
        formationA: match.playerFormation,
        formationB: match.opponentFormation,
      });

      const playerUnitStates = new Map(resolved.battleState.teamA.map((unit) => [unit.emojiId, unit]));
      const opponentUnitStates = new Map(resolved.battleState.teamB.map((unit) => [unit.emojiId, unit]));

      for (const emojiId of match.playerTeam) {
        const unitState = playerUnitStates.get(emojiId);
        const accumulator = unitStats.get(`${scenario.name}|player|${emojiId}`);
        recordOutcome(accumulator, playerOutcome, playerAliveMargin, playerHpMargin);
        if (unitState?.alive) {
          accumulator.survivors += 1;
          accumulator.hpRemainingTotal += unitState.hp;
        }

        accumulator.teamAliveTotal += match.playerAliveCount;
      }

      for (const emojiId of match.opponentTeam) {
        const unitState = opponentUnitStates.get(emojiId);
        const accumulator = unitStats.get(`${scenario.name}|opponent|${emojiId}`);
        recordOutcome(accumulator, opponentOutcome, -playerAliveMargin, -playerHpMargin);
        if (unitState?.alive) {
          accumulator.survivors += 1;
          accumulator.hpRemainingTotal += unitState.hp;
        }

        accumulator.teamAliveTotal += match.opponentAliveCount;
      }

      for (const placement of match.playerFormation.placements) {
        const unitState = playerUnitStates.get(placement.emojiId);
        const accumulator = slotStats.get(`${scenario.name}|player|${placement.slot}|${placement.emojiId}`);
        recordOutcome(accumulator, playerOutcome, playerAliveMargin, playerHpMargin);
        if (unitState?.alive) {
          accumulator.survivors += 1;
          accumulator.hpRemainingTotal += unitState.hp;
        }

        accumulator.teamAliveTotal += match.playerAliveCount;
      }

      for (const placement of match.opponentFormation.placements) {
        const unitState = opponentUnitStates.get(placement.emojiId);
        const accumulator = slotStats.get(`${scenario.name}|opponent|${placement.slot}|${placement.emojiId}`);
        recordOutcome(accumulator, opponentOutcome, -playerAliveMargin, -playerHpMargin);
        if (unitState?.alive) {
          accumulator.survivors += 1;
          accumulator.hpRemainingTotal += unitState.hp;
        }

        accumulator.teamAliveTotal += match.opponentAliveCount;
      }

      recordOutcome(shapeStats.get(`${scenario.name}|player|${shapeSignature(match.playerFormation)}`), playerOutcome, playerAliveMargin, playerHpMargin);
      recordOutcome(shapeStats.get(`${scenario.name}|opponent|${shapeSignature(match.opponentFormation)}`), opponentOutcome, -playerAliveMargin, -playerHpMargin);

      recordOutcome(banStats.get(`${scenario.name}|player|${match.playerBan}`), playerOutcome, playerAliveMargin, playerHpMargin);
      recordOutcome(banStats.get(`${scenario.name}|opponent|${match.opponentBan}`), opponentOutcome, -playerAliveMargin, -playerHpMargin);

      if (typeof match.playerBanLeverage === "number") {
        const accumulator = banStats.get(`${scenario.name}|player|${match.playerBan}`);
        accumulator.leverageTotal += match.playerBanLeverage;
        accumulator.leverageSamples += 1;
      }

      if (typeof match.opponentBanLeverage === "number") {
        const accumulator = banStats.get(`${scenario.name}|opponent|${match.opponentBan}`);
        accumulator.leverageTotal += match.opponentBanLeverage;
        accumulator.leverageSamples += 1;
      }

      recordOutcome(
        autoFillStats.get(`${scenario.name}|player|${match.playerFormationStrategy}`),
        playerOutcome,
        playerAliveMargin,
        playerHpMargin,
      );
      recordOutcome(
        autoFillStats.get(`${scenario.name}|opponent|${match.opponentFormationStrategy}`),
        opponentOutcome,
        -playerAliveMargin,
        -playerHpMargin,
      );
    }

    scenarioSummaries.push({
      scenario: scenario.name,
      matchCount: scenario.matchCount,
      playerWins,
      opponentWins,
      draws,
      playerWinRate: toRate(playerWins, scenario.matchCount),
      opponentWinRate: toRate(opponentWins, scenario.matchCount),
      drawRate: toRate(draws, scenario.matchCount),
      averageAliveMargin: scenario.matchCount === 0 ? 0 : aliveMarginTotal / scenario.matchCount,
      averageHpMargin: scenario.matchCount === 0 ? 0 : hpMarginTotal / scenario.matchCount,
    });
  }

  runSampledTopCoreSearch(config, scenarioSummaries, squadStats, matchupStats);

  const squadRows: SquadStatsRow[] = [...squadStats.map.entries()].map(([key, value]) => {
    const [scenario, perspective, squadCore] = key.split("|");
    return {
      scenario,
      perspective: perspective as Perspective,
      squadCore,
      appearances: value.appearances,
      wins: value.wins,
      losses: value.losses,
      draws: value.draws,
      winRate: toRate(value.wins, value.appearances),
      lossRate: toRate(value.losses, value.appearances),
      drawRate: toRate(value.draws, value.appearances),
      averageAliveMargin: value.appearances === 0 ? 0 : value.aliveMarginTotal / value.appearances,
      averageHpMargin: value.appearances === 0 ? 0 : value.hpMarginTotal / value.appearances,
    };
  }).sort((left, right) => right.winRate - left.winRate || right.appearances - left.appearances || left.squadCore.localeCompare(right.squadCore));

  const unitRows: UnitPostBanStatsRow[] = [...unitStats.map.entries()].map(([key, value]) => {
    const [scenario, perspective, unit] = key.split("|");
    return {
      scenario,
      perspective: perspective as Perspective,
      unit: unit as EmojiId,
      appearances: value.appearances,
      wins: value.wins,
      losses: value.losses,
      draws: value.draws,
      winRate: toRate(value.wins, value.appearances),
      survivalRate: toRate(value.survivors, value.appearances),
      averageHpRemaining: value.appearances === 0 ? 0 : value.hpRemainingTotal / value.appearances,
      averageTeamAliveAtEnd: value.appearances === 0 ? 0 : value.teamAliveTotal / value.appearances,
    };
  }).sort((left, right) => right.winRate - left.winRate || left.unit.localeCompare(right.unit));

  const banRows: BanStatsRow[] = [...banStats.map.entries()].map(([key, value]) => {
    const [scenario, perspective, bannedUnit] = key.split("|");
    const scenarioMatchCount = scenarioSummaries.find((summary) => summary.scenario === scenario)?.matchCount ?? value.appearances;
    return {
      scenario,
      perspective: perspective as Perspective,
      bannedUnit: bannedUnit as EmojiId,
      count: value.appearances,
      banRate: toRate(value.appearances, scenarioMatchCount),
      winRateAfterBan: toRate(value.wins, value.appearances),
      averageAliveMargin: value.appearances === 0 ? 0 : value.aliveMarginTotal / value.appearances,
      averageHpMargin: value.appearances === 0 ? 0 : value.hpMarginTotal / value.appearances,
      averageBanLeverage: value.leverageSamples === 0 ? 0 : value.leverageTotal / value.leverageSamples,
    };
  }).sort((left, right) => right.banRate - left.banRate || right.averageBanLeverage - left.averageBanLeverage);

  const slotRows: FormationSlotStatsRow[] = [...slotStats.map.entries()].map(([key, value]) => {
    const [scenario, perspective, slot, unit] = key.split("|");
    return {
      scenario,
      perspective: perspective as Perspective,
      slot: slot as FormationSlot,
      unit: unit as EmojiId,
      appearances: value.appearances,
      wins: value.wins,
      losses: value.losses,
      draws: value.draws,
      winRate: toRate(value.wins, value.appearances),
      survivalRate: toRate(value.survivors, value.appearances),
      averageHpRemaining: value.appearances === 0 ? 0 : value.hpRemainingTotal / value.appearances,
    };
  }).sort((left, right) => right.winRate - left.winRate || right.survivalRate - left.survivalRate);

  const shapeRows: FormationShapeStatsRow[] = [...shapeStats.map.entries()].map(([key, value]) => {
    const [scenario, perspective, shape] = key.split("|");
    return {
      scenario,
      perspective: perspective as Perspective,
      shape,
      appearances: value.appearances,
      wins: value.wins,
      losses: value.losses,
      draws: value.draws,
      winRate: toRate(value.wins, value.appearances),
      averageAliveMargin: value.appearances === 0 ? 0 : value.aliveMarginTotal / value.appearances,
    };
  }).sort((left, right) => right.winRate - left.winRate || right.appearances - left.appearances);

  const autoFillRows: AutoFillStrategyRow[] = [...autoFillStats.map.entries()]
    .map(([key, value]) => {
      const [scenario, perspective, strategy] = key.split("|");
      return {
        scenario,
        perspective: perspective as Perspective,
        strategy: strategy as FormationStrategy,
        matches: value.appearances,
        wins: value.wins,
        losses: value.losses,
        draws: value.draws,
        winRate: toRate(value.wins, value.appearances),
        averageAliveMargin: value.appearances === 0 ? 0 : value.aliveMarginTotal / value.appearances,
        averageHpMargin: value.appearances === 0 ? 0 : value.hpMarginTotal / value.appearances,
      };
    })
    .filter((row) => row.strategy === "auto_fill" || row.strategy === "heuristic" || row.strategy === "random")
    .sort((left, right) => left.scenario.localeCompare(right.scenario) || left.perspective.localeCompare(right.perspective) || left.strategy.localeCompare(right.strategy));

  const matchupRows: MatchupCoreStatsRow[] = [...matchupStats.map.entries()].map(([key, value]) => {
    const [scenario, playerCore, opponentCore] = key.split("|");
    return {
      scenario,
      playerCore,
      opponentCore,
      matches: value.appearances,
      playerWins: value.wins,
      opponentWins: value.losses,
      draws: value.draws,
      playerWinRate: toRate(value.wins, value.appearances),
      drawRate: toRate(value.draws, value.appearances),
      averageAliveMargin: value.appearances === 0 ? 0 : value.aliveMarginTotal / value.appearances,
      averageHpMargin: value.appearances === 0 ? 0 : value.hpMarginTotal / value.appearances,
    };
  }).sort((left, right) => right.playerWinRate - left.playerWinRate || right.matches - left.matches);

  const symmetryAccumulator = new Map<string, {
    leftAppearances: number;
    rightAppearances: number;
    leftWinWeighted: number;
    rightWinWeighted: number;
    leftSurvivalWeighted: number;
    rightSurvivalWeighted: number;
    leftHpWeighted: number;
    rightHpWeighted: number;
  }>();
  for (const row of slotRows) {
    let laneGroup: "frontline" | "backline" | null = null;
    let side: "left" | "right" | null = null;
    if (row.slot === "front_left") {
      laneGroup = "frontline";
      side = "left";
    } else if (row.slot === "front_right") {
      laneGroup = "frontline";
      side = "right";
    } else if (row.slot === "back_left") {
      laneGroup = "backline";
      side = "left";
    } else if (row.slot === "back_right") {
      laneGroup = "backline";
      side = "right";
    }

    if (!laneGroup || !side) {
      continue;
    }

    const key = `${row.scenario}|${row.perspective}|${laneGroup}`;
    const current = symmetryAccumulator.get(key) ?? {
      leftAppearances: 0,
      rightAppearances: 0,
      leftWinWeighted: 0,
      rightWinWeighted: 0,
      leftSurvivalWeighted: 0,
      rightSurvivalWeighted: 0,
      leftHpWeighted: 0,
      rightHpWeighted: 0,
    };

    if (side === "left") {
      current.leftAppearances += row.appearances;
      current.leftWinWeighted += row.winRate * row.appearances;
      current.leftSurvivalWeighted += row.survivalRate * row.appearances;
      current.leftHpWeighted += row.averageHpRemaining * row.appearances;
    } else {
      current.rightAppearances += row.appearances;
      current.rightWinWeighted += row.winRate * row.appearances;
      current.rightSurvivalWeighted += row.survivalRate * row.appearances;
      current.rightHpWeighted += row.averageHpRemaining * row.appearances;
    }

    symmetryAccumulator.set(key, current);
  }

  const symmetryRows: LeftRightSlotSymmetryRow[] = [...symmetryAccumulator.entries()].map(([key, value]) => {
    const [scenario, perspective, laneGroup] = key.split("|");
    const leftSurvivalRate = value.leftAppearances === 0 ? 0 : value.leftSurvivalWeighted / value.leftAppearances;
    const rightSurvivalRate = value.rightAppearances === 0 ? 0 : value.rightSurvivalWeighted / value.rightAppearances;
    const leftWinRate = value.leftAppearances === 0 ? 0 : value.leftWinWeighted / value.leftAppearances;
    const rightWinRate = value.rightAppearances === 0 ? 0 : value.rightWinWeighted / value.rightAppearances;
    const leftAverageHpRemaining = value.leftAppearances === 0 ? 0 : value.leftHpWeighted / value.leftAppearances;
    const rightAverageHpRemaining = value.rightAppearances === 0 ? 0 : value.rightHpWeighted / value.rightAppearances;
    return {
      scenario,
      perspective: perspective as Perspective,
      laneGroup: laneGroup as "frontline" | "backline",
      leftAppearances: value.leftAppearances,
      rightAppearances: value.rightAppearances,
      leftWinRate,
      rightWinRate,
      leftSurvivalRate,
      rightSurvivalRate,
      leftAverageHpRemaining,
      rightAverageHpRemaining,
      survivalGap: rightSurvivalRate - leftSurvivalRate,
      winRateGap: rightWinRate - leftWinRate,
    };
  }).sort((left, right) => Math.abs(right.survivalGap) - Math.abs(left.survivalGap));

  const topSampledSquads = squadRows
    .filter((row) => row.scenario === "SampledTopCoreSearch" && row.perspective === "player" && row.appearances >= 5)
    .slice(0, 50);
  const shellAccumulator = new Map<string, { appearances: number; parentWinRateTotal: number; bestParentCore: string; bestParentWinRate: number }>();
  for (const row of topSampledSquads) {
    const units = row.squadCore.split(">") as EmojiId[];
    for (const shellSize of [4, 5]) {
      for (const shell of chooseCombinations(units, shellSize)) {
        const shellSignature = shell.join(">");
        const key = `${row.scenario}|${shellSize}|${shellSignature}`;
        const current = shellAccumulator.get(key) ?? {
          appearances: 0,
          parentWinRateTotal: 0,
          bestParentCore: row.squadCore,
          bestParentWinRate: row.winRate,
        };
        current.appearances += 1;
        current.parentWinRateTotal += row.winRate;
        if (row.winRate > current.bestParentWinRate) {
          current.bestParentWinRate = row.winRate;
          current.bestParentCore = row.squadCore;
        }
        shellAccumulator.set(key, current);
      }
    }
  }

  const shellRows: CoreShellFrequencyRow[] = [...shellAccumulator.entries()].map(([key, value]) => {
    const [scenario, shellSize, shell] = key.split("|");
    return {
      scenario,
      shellSize: Number(shellSize),
      shell,
      appearances: value.appearances,
      averageParentWinRate: value.parentWinRateTotal / Math.max(1, value.appearances),
      bestParentCore: value.bestParentCore,
    };
  }).sort((left, right) => right.appearances - left.appearances || right.averageParentWinRate - left.averageParentWinRate || left.shell.localeCompare(right.shell));

  const findings = buildFindings(scenarioSummaries, banRows, slotRows, symmetryRows, shapeRows, autoFillRows, squadRows, shellRows);

  return {
    scenarioSummaries,
    squadRows,
    unitRows,
    banRows,
    slotRows,
    symmetryRows,
    shapeRows,
    autoFillRows,
    matchupRows,
    shellRows,
    findings,
  };
}

function buildFindings(
  scenarioSummaries: ScenarioSummary[],
  banRows: BanStatsRow[],
  slotRows: FormationSlotStatsRow[],
  symmetryRows: LeftRightSlotSymmetryRow[],
  shapeRows: FormationShapeStatsRow[],
  autoFillRows: AutoFillStrategyRow[],
  squadRows: SquadStatsRow[],
  shellRows: CoreShellFrequencyRow[],
): FlaggedFinding[] {
  const findings: FlaggedFinding[] = [];

  for (const row of banRows.filter((entry) => entry.count >= 20)) {
    if (row.banRate >= 0.20) {
      findings.push({
        severity: row.banRate >= 0.30 ? "Critical" : "Warning",
        category: "Ban Rate",
        finding: `${row.bannedUnit} is banned at ${formatPct(row.banRate)} in ${row.scenario} (${row.perspective}).`,
        metric: row.banRate.toFixed(4),
        threshold: ">= 0.20",
      });
    }

    if (Math.abs(row.averageBanLeverage) >= 25) {
      findings.push({
        severity: Math.abs(row.averageBanLeverage) >= 40 ? "Critical" : "Warning",
        category: "Ban Leverage",
        finding: `${row.bannedUnit} swings outcomes strongly when removed in ${row.scenario} (${row.perspective}).`,
        metric: row.averageBanLeverage.toFixed(2),
        threshold: "|leverage| >= 25",
      });
    }
  }

  for (const row of symmetryRows.filter((entry) => entry.leftAppearances >= 100 && entry.rightAppearances >= 100)) {
    if (Math.abs(row.survivalGap) >= 0.15) {
      findings.push({
        severity: Math.abs(row.survivalGap) >= 0.25 ? "Critical" : "Warning",
        category: "Slot Symmetry",
        finding: `${row.laneGroup} ${row.scenario} (${row.perspective}) has a ${formatPct(Math.abs(row.survivalGap))} left/right survival gap.`,
        metric: row.survivalGap.toFixed(4),
        threshold: "|survival gap| >= 0.15",
      });
    }
  }

  for (const row of autoFillRows.filter((entry) => entry.scenario === "AutoFillVsHeuristicFormation" && entry.strategy === "auto_fill")) {
    if (row.winRate <= 0.35) {
      findings.push({
        severity: "Warning",
        category: "Auto Fill",
        finding: `Auto-fill underperforms simple heuristic formation in ${row.scenario} (${row.perspective}).`,
        metric: row.winRate.toFixed(4),
        threshold: "<= 0.35",
      });
    }
  }

  for (const row of shapeRows.filter((entry) => entry.appearances >= 25)) {
    if (row.winRate >= 0.65) {
      findings.push({
        severity: row.winRate >= 0.72 ? "Critical" : "Warning",
        category: "Formation Shape",
        finding: `Formation shape ${row.shape} is outperforming in ${row.scenario} (${row.perspective}).`,
        metric: row.winRate.toFixed(4),
        threshold: ">= 0.65",
      });
    }
  }

  for (const row of squadRows.filter((entry) => entry.appearances >= 8)) {
    if (row.winRate >= 0.75) {
      findings.push({
        severity: row.winRate >= 0.85 ? "Critical" : "Warning",
        category: "Squad Core",
        finding: `Squad core ${row.squadCore} is dramatically outperforming in ${row.scenario} (${row.perspective}).`,
        metric: row.winRate.toFixed(4),
        threshold: ">= 0.75",
      });
    }
  }

  for (const row of shellRows.filter((entry) => entry.appearances >= 3)) {
    if (row.averageParentWinRate >= 0.75) {
      findings.push({
        severity: row.averageParentWinRate >= 0.85 ? "Critical" : "Warning",
        category: "Core Shell",
        finding: `${row.shellSize}-unit shell ${row.shell} repeats across strong sampled cores in ${row.scenario}.`,
        metric: row.averageParentWinRate.toFixed(4),
        threshold: "avg parent win rate >= 0.75",
      });
    }
  }

  const mirrorSummary = scenarioSummaries.find((row) => row.scenario === "HeuristicSquadBanFormationMirror");
  if (mirrorSummary && mirrorSummary.drawRate >= 0.25) {
    findings.push({
      severity: "Warning",
      category: "Mirror Resolution",
      finding: `Heuristic mirror scenario draws too often at ${formatPct(mirrorSummary.drawRate)}.`,
      metric: mirrorSummary.drawRate.toFixed(4),
      threshold: ">= 0.25",
    });
  }

  return findings;
}

function buildSummaryMarkdown(
  config: SimulationConfig,
  scenarioSummaries: ScenarioSummary[],
  squadRows: SquadStatsRow[],
  unitRows: UnitPostBanStatsRow[],
  banRows: BanStatsRow[],
  slotRows: FormationSlotStatsRow[],
  symmetryRows: LeftRightSlotSymmetryRow[],
  autoFillRows: AutoFillStrategyRow[],
  shellRows: CoreShellFrequencyRow[],
  findings: FlaggedFinding[],
): string {
  const strongestUnits = unitRows
    .filter((row) => row.scenario === "RandomSquadVsRandomSquad" && row.perspective === "player")
    .sort((left, right) => right.winRate - left.winRate)
    .slice(0, 5);

  const weakestUnits = unitRows
    .filter((row) => row.scenario === "RandomSquadVsRandomSquad" && row.perspective === "player")
    .sort((left, right) => left.winRate - right.winRate)
    .slice(0, 5);

  const topBans = banRows.slice(0, 5);
  const filteredTopSlots = slotRows
    .filter((row) => row.appearances >= 10)
    .sort((left, right) => right.survivalRate - left.survivalRate || right.winRate - left.winRate)
    .slice(0, 5);
  const topCores = squadRows
    .filter((row) => row.scenario === "SampledTopCoreSearch" && row.perspective === "player")
    .slice(0, 10);
  const structuralRows = symmetryRows.slice(0, 4);
  const topShellRows = shellRows.slice(0, 8);

  const lines: string[] = [];
  lines.push("# Secondary Mode Balance Summary", "");
  lines.push("## 1. Simulation Settings");
  lines.push(`- Seed: ${config.seed}`);
  lines.push(`- Random squad sample matches: ${config.randomSquadMatchCount}`);
  lines.push(`- Heuristic squad sample matches: ${config.heuristicSquadMatchCount}`);
  lines.push(`- Heuristic ban sample matches: ${config.heuristicBanMatchCount}`);
  lines.push(`- Auto-fill vs random formation matches: ${config.autoFillRandomFormationMatchCount}`);
  lines.push(`- Auto-fill vs heuristic formation matches: ${config.autoFillHeuristicFormationMatchCount}`);
  lines.push(`- Heuristic mirror matches: ${config.heuristicMirrorMatchCount}`);
  lines.push(`- Sampled top-core search size: ${config.sampledTopCoreCount} x ${config.sampledTopCoreOpponentSamples}`);
  lines.push("- Ranked mode flow covered: squad -> blind ban -> five-unit formation -> battle -> result");
  lines.push(`- Batch command: \`Unity -batchmode -projectPath <project> -executeMethod SecondaryModeBalanceSimulationRunner.RunDefaultBatchAndExit -quit\``);
  lines.push("");
  lines.push("## 2. Scenario Results");
  for (const summary of scenarioSummaries) {
    lines.push(`- ${summary.scenario}: player ${formatPct(summary.playerWinRate)}, opponent ${formatPct(summary.opponentWinRate)}, draw ${formatPct(summary.drawRate)}, avg alive margin ${summary.averageAliveMargin.toFixed(2)}, avg HP margin ${summary.averageHpMargin.toFixed(2)}`);
  }
  lines.push("");
  lines.push("## 3. Ban Pressure");
  for (const row of topBans) {
    lines.push(`- ${row.scenario} ${row.perspective}: ${row.bannedUnit} banned ${formatPct(row.banRate)}, win rate after ban ${formatPct(row.winRateAfterBan)}, leverage ${row.averageBanLeverage.toFixed(2)}`);
  }
  lines.push("");
  lines.push("## 4. Post-Ban Unit Performance");
  lines.push("- Strongest post-ban units:");
  for (const row of strongestUnits) {
    lines.push(`  - ${row.unit}: win ${formatPct(row.winRate)}, survive ${formatPct(row.survivalRate)}, avg team alive ${row.averageTeamAliveAtEnd.toFixed(2)}`);
  }
  lines.push("- Weakest post-ban units:");
  for (const row of weakestUnits) {
    lines.push(`  - ${row.unit}: win ${formatPct(row.winRate)}, survive ${formatPct(row.survivalRate)}, avg team alive ${row.averageTeamAliveAtEnd.toFixed(2)}`);
  }
  lines.push("");
  lines.push("## 5. Structural Findings");
  for (const row of structuralRows) {
    lines.push(`- ${row.scenario} ${row.perspective} ${row.laneGroup}: left survive ${formatPct(row.leftSurvivalRate)}, right survive ${formatPct(row.rightSurvivalRate)}, gap ${formatPct(row.survivalGap)}`);
  }
  if (topShellRows.length > 0) {
    lines.push("- Repeated high-performing shells:");
    for (const row of topShellRows) {
      lines.push(`  - ${row.shellSize}-unit ${row.shell}: seen ${row.appearances} times, avg parent win ${formatPct(row.averageParentWinRate)}`);
    }
  }
  lines.push("");
  lines.push("## 5. Formation Health");
  for (const row of filteredTopSlots) {
    lines.push(`- ${row.scenario} ${row.perspective} ${row.slot} ${row.unit}: win ${formatPct(row.winRate)}, survive ${formatPct(row.survivalRate)}`);
  }
  const autoFillFocus = autoFillRows.filter((row) => row.scenario === "AutoFillVsHeuristicFormation" || row.scenario === "AutoFillVsRandomFormation");
  lines.push("");
  lines.push("## 6. Auto-Fill Competitiveness");
  for (const row of autoFillFocus) {
    lines.push(`- ${row.scenario} ${row.perspective} ${row.strategy}: win ${formatPct(row.winRate)}, avg alive margin ${row.averageAliveMargin.toFixed(2)}`);
  }
  lines.push("");
  lines.push("## 7. Top Sampled Squad Cores");
  for (const row of topCores) {
    lines.push(`- ${row.squadCore}: win ${formatPct(row.winRate)} over ${row.appearances} sampled matches`);
  }
  lines.push("");
  lines.push("## 8. Flagged Findings");
  if (findings.length === 0) {
    lines.push("- No flagged findings in this sample.");
  } else {
    for (const finding of findings) {
      lines.push(`- [${finding.severity}] ${finding.category}: ${finding.finding}`);
    }
  }
  lines.push("");
  lines.push("## 9. Suggested Next Steps");
  lines.push("- Review units with both high ban rate and high ban leverage before changing balance.");
  lines.push("- Compare auto-fill rows against heuristic formation rows before adjusting the ranked default layout.");
  lines.push("- Use squad core rows and matchup core rows to inspect whether one six-unit prep shell is warping bans or frontline slot value.");
  return `${lines.join("\n")}\n`;
}

function writeReportFiles(config: SimulationConfig): void {
  const {
    scenarioSummaries,
    squadRows,
    unitRows,
    banRows,
    slotRows,
    symmetryRows,
    shapeRows,
    autoFillRows,
    matchupRows,
    shellRows,
    findings,
  } = buildRows(config);

  Deno.mkdirSync(config.outputDirectory, { recursive: true });

  Deno.writeTextFileSync(
    `${config.outputDirectory}/secondary_balance_summary.md`,
    buildSummaryMarkdown(config, scenarioSummaries, squadRows, unitRows, banRows, slotRows, symmetryRows, autoFillRows, shellRows, findings),
  );
  Deno.writeTextFileSync(
    `${config.outputDirectory}/squad_win_rates.csv`,
    createCsv(squadRows as unknown as Array<Record<string, unknown>>, [
      "scenario", "perspective", "squadCore", "appearances", "wins", "losses", "draws", "winRate", "lossRate", "drawRate", "averageAliveMargin", "averageHpMargin",
    ]),
  );
  Deno.writeTextFileSync(
    `${config.outputDirectory}/unit_post_ban_stats.csv`,
    createCsv(unitRows as unknown as Array<Record<string, unknown>>, [
      "scenario", "perspective", "unit", "appearances", "wins", "losses", "draws", "winRate", "survivalRate", "averageHpRemaining", "averageTeamAliveAtEnd",
    ]),
  );
  Deno.writeTextFileSync(
    `${config.outputDirectory}/ban_stats.csv`,
    createCsv(banRows as unknown as Array<Record<string, unknown>>, [
      "scenario", "perspective", "bannedUnit", "count", "banRate", "winRateAfterBan", "averageAliveMargin", "averageHpMargin", "averageBanLeverage",
    ]),
  );
  Deno.writeTextFileSync(
    `${config.outputDirectory}/formation_slot_stats.csv`,
    createCsv(slotRows as unknown as Array<Record<string, unknown>>, [
      "scenario", "perspective", "slot", "unit", "appearances", "wins", "losses", "draws", "winRate", "survivalRate", "averageHpRemaining",
    ]),
  );
  Deno.writeTextFileSync(
    `${config.outputDirectory}/left_right_slot_symmetry.csv`,
    createCsv(symmetryRows as unknown as Array<Record<string, unknown>>, [
      "scenario", "perspective", "laneGroup", "leftAppearances", "rightAppearances", "leftWinRate", "rightWinRate", "leftSurvivalRate", "rightSurvivalRate", "leftAverageHpRemaining", "rightAverageHpRemaining", "survivalGap", "winRateGap",
    ]),
  );
  Deno.writeTextFileSync(
    `${config.outputDirectory}/formation_shape_stats.csv`,
    createCsv(shapeRows as unknown as Array<Record<string, unknown>>, [
      "scenario", "perspective", "shape", "appearances", "wins", "losses", "draws", "winRate", "averageAliveMargin",
    ]),
  );
  Deno.writeTextFileSync(
    `${config.outputDirectory}/auto_fill_vs_manual_heuristic.csv`,
    createCsv(autoFillRows as unknown as Array<Record<string, unknown>>, [
      "scenario", "perspective", "strategy", "matches", "wins", "losses", "draws", "winRate", "averageAliveMargin", "averageHpMargin",
    ]),
  );
  Deno.writeTextFileSync(
    `${config.outputDirectory}/matchup_core_stats.csv`,
    createCsv(matchupRows as unknown as Array<Record<string, unknown>>, [
      "scenario", "playerCore", "opponentCore", "matches", "playerWins", "opponentWins", "draws", "playerWinRate", "drawRate", "averageAliveMargin", "averageHpMargin",
    ]),
  );
  Deno.writeTextFileSync(
    `${config.outputDirectory}/core_shell_frequency.csv`,
    createCsv(shellRows as unknown as Array<Record<string, unknown>>, [
      "scenario", "shellSize", "shell", "appearances", "averageParentWinRate", "bestParentCore",
    ]),
  );
  Deno.writeTextFileSync(
    `${config.outputDirectory}/flagged_findings.csv`,
    createCsv(findings as unknown as Array<Record<string, unknown>>, [
      "severity", "category", "finding", "metric", "threshold",
    ]),
  );

  const manifest = {
    outputDirectory: config.outputDirectory,
    seed: config.seed,
    generatedFiles: [
      "secondary_balance_summary.md",
      "squad_win_rates.csv",
      "unit_post_ban_stats.csv",
      "ban_stats.csv",
      "formation_slot_stats.csv",
      "left_right_slot_symmetry.csv",
      "formation_shape_stats.csv",
      "auto_fill_vs_manual_heuristic.csv",
      "matchup_core_stats.csv",
      "core_shell_frequency.csv",
      "flagged_findings.csv",
    ],
    scenarioSummaries: scenarioSummaries.map((row) =>
      `${row.scenario}: player ${formatPct(row.playerWinRate)}, opponent ${formatPct(row.opponentWinRate)}, draw ${formatPct(row.drawRate)}`
    ),
  };

  Deno.writeTextFileSync(`${config.outputDirectory}/secondary_mode_balance_manifest.json`, JSON.stringify(manifest, null, 2));
  console.log(`Secondary mode balance reports written to ${config.outputDirectory}`);
}

function parseConfig(configPath: string): SimulationConfig {
  const parsed = JSON.parse(Deno.readTextFileSync(configPath)) as Record<string, unknown>;
  return {
    seed: Number(parsed.Seed ?? DEFAULT_CONFIG.seed),
    randomSquadMatchCount: Number(parsed.RandomSquadMatchCount ?? DEFAULT_CONFIG.randomSquadMatchCount),
    heuristicSquadMatchCount: Number(parsed.HeuristicSquadMatchCount ?? DEFAULT_CONFIG.heuristicSquadMatchCount),
    heuristicBanMatchCount: Number(parsed.HeuristicBanMatchCount ?? DEFAULT_CONFIG.heuristicBanMatchCount),
    autoFillRandomFormationMatchCount: Number(parsed.AutoFillRandomFormationMatchCount ?? DEFAULT_CONFIG.autoFillRandomFormationMatchCount),
    autoFillHeuristicFormationMatchCount: Number(parsed.AutoFillHeuristicFormationMatchCount ?? DEFAULT_CONFIG.autoFillHeuristicFormationMatchCount),
    heuristicMirrorMatchCount: Number(parsed.HeuristicMirrorMatchCount ?? DEFAULT_CONFIG.heuristicMirrorMatchCount),
    sampledTopCoreCount: Number(parsed.SampledTopCoreCount ?? DEFAULT_CONFIG.sampledTopCoreCount),
    sampledTopCoreOpponentSamples: Number(parsed.SampledTopCoreOpponentSamples ?? DEFAULT_CONFIG.sampledTopCoreOpponentSamples),
    outputDirectory: String(parsed.OutputDirectory ?? DEFAULT_CONFIG.outputDirectory),
  };
}

if (import.meta.main) {
  if (Deno.args.length < 1) {
    throw new Error("Usage: deno run secondary-mode-balance-sim.ts <config-path>");
  }

  const config = parseConfig(Deno.args[0]);
  writeReportFiles(config);
}
