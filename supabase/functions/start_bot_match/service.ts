import type { EmojiId, MatchMode } from "../_shared/contracts.ts";
import { buildBotMatchPlan, chooseBotBan, getBotProfile } from "../_shared/bot-engine.ts";

export function getBotBan(mode: MatchMode, playerDeck: EmojiId[]): EmojiId {
  return chooseBotBan(mode, playerDeck);
}

export { buildBotMatchPlan, getBotProfile };
