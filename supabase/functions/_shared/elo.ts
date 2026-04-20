export function expectedScore(playerRating: number, opponentRating: number): number {
  return 1 / (1 + 10 ** ((opponentRating - playerRating) / 400));
}

export function calculateEloRating(
  playerRating: number,
  opponentRating: number,
  actualScore: number,
  kFactor = 32,
): number {
  const next = playerRating + kFactor * (actualScore - expectedScore(playerRating, opponentRating));
  return Math.round(next);
}
