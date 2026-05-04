export const CLASH_INITIAL_MATCHMAKING_RATING = 1000;
export const CLASH_QUEUE_BAND_ONE_SECONDS = 8;
export const CLASH_QUEUE_BAND_TWO_SECONDS = 10;
export const CLASH_QUEUE_BAND_ONE_RATING_DELTA = 150;
export const CLASH_QUEUE_BAND_TWO_RATING_DELTA = 300;
export const CLASH_RECENT_OPPONENT_COOLDOWN_SECONDS = 120;
export const CLASH_BOT_FILL_THRESHOLD_SECONDS = 10;

export interface ClashQueueCandidate {
  id: string;
  player_a: string;
  created_at?: string;
  current_state?: {
    matchmakingRatingAtQueue?: number;
    queueStartedAt?: string;
    queueExpiresAt?: string;
  } | null;
}

export function resolveClashMatchmakingBand(queueStartedAt: string, now = new Date()): number {
  const startedMs = Date.parse(queueStartedAt);
  if (!Number.isFinite(startedMs)) {
    return CLASH_QUEUE_BAND_ONE_RATING_DELTA;
  }

  const ageSeconds = Math.max(0, (now.getTime() - startedMs) / 1000);
  return ageSeconds < CLASH_QUEUE_BAND_ONE_SECONDS
    ? CLASH_QUEUE_BAND_ONE_RATING_DELTA
    : CLASH_QUEUE_BAND_TWO_RATING_DELTA;
}

export function shouldCreateClashBotFill(queueStartedAt: string, now = new Date()): boolean {
  const startedMs = Date.parse(queueStartedAt);
  if (!Number.isFinite(startedMs)) {
    return false;
  }

  return Math.max(0, (now.getTime() - startedMs) / 1000) >= CLASH_BOT_FILL_THRESHOLD_SECONDS;
}

export function isClashQueueExpired(candidate: ClashQueueCandidate, now = new Date()): boolean {
  const expiresAt = candidate.current_state?.queueExpiresAt;
  return !!expiresAt && Date.parse(expiresAt) <= now.getTime();
}

export function getClashQueueRating(candidate: ClashQueueCandidate): number {
  const rating = candidate.current_state?.matchmakingRatingAtQueue;
  return Number.isFinite(rating) ? Math.trunc(rating ?? CLASH_INITIAL_MATCHMAKING_RATING) : CLASH_INITIAL_MATCHMAKING_RATING;
}

export function selectEligibleClashOpponent(
  candidates: ClashQueueCandidate[],
  requestUserId: string,
  requestRating: number,
  requestQueueStartedAt = "",
  recentOpponentId = "",
  now = new Date(),
): ClashQueueCandidate | null {
  const requestBand = requestQueueStartedAt
    ? resolveClashMatchmakingBand(requestQueueStartedAt, now)
    : CLASH_QUEUE_BAND_ONE_RATING_DELTA;
  const eligible = candidates
    .filter((candidate) => candidate.player_a !== requestUserId)
    .filter((candidate) => !isClashQueueExpired(candidate, now))
    .filter((candidate) => {
      const queueStartedAt = candidate.current_state?.queueStartedAt ?? candidate.created_at ?? now.toISOString();
      const candidateBand = resolveClashMatchmakingBand(queueStartedAt, now);
      return Math.abs(getClashQueueRating(candidate) - requestRating) <= Math.max(requestBand, candidateBand);
    })
    .sort((left, right) => Date.parse(left.created_at ?? "") - Date.parse(right.created_at ?? ""));

  if (eligible.length <= 1 || !recentOpponentId) {
    return eligible[0] ?? null;
  }

  return eligible.find((candidate) => candidate.player_a !== recentOpponentId) ?? eligible[0] ?? null;
}
