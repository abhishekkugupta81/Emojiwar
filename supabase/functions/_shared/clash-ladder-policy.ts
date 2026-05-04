export type ClashLadderResult = "win" | "loss" | "draw" | "timeout_forfeit";
export type ClashLadderOpponentType = "human" | "bot_fill";

export const CLASH_INITIAL_HIDDEN_MMR = 1000;
export const CLASH_INITIAL_VISIBLE_POINTS = 0;
export const CLASH_WIN_POINTS = 100;
export const CLASH_DRAW_POINTS = 40;
export const CLASH_LOSS_POINTS = -35;

export function visibleDeltaForClashResult(
  result: ClashLadderResult,
  _opponentType: ClashLadderOpponentType,
  _positiveBotFillWinsToday = 0,
): number {
  if (result === "win") return CLASH_WIN_POINTS;
  if (result === "draw") return CLASH_DRAW_POINTS;
  return CLASH_LOSS_POINTS;
}
