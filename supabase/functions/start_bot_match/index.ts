import type { EmojiId, MatchMode, StartBotMatchRequest } from "../_shared/contracts.ts";
import { buildDefaultFormation, selectBattleTeamFromDeck } from "../_shared/battle-simulator.ts";
import { EMOJI_DEFINITIONS } from "../_shared/emoji-definitions.ts";
import { insertRows, resolveRequestUserId } from "../_shared/supabase-admin.ts";
import { buildBotMatchPlan, getBotBan } from "./service.ts";

const RULES_VERSION = "launch-5v5-001";

function validateDeck(mode: MatchMode, playerDeck: string[]): { isValid: boolean; error?: string } {
  if (mode === "bot_practice" && playerDeck.length !== 6) {
    return { isValid: false, error: "Battle Practice requires exactly 6 unique emojis for blind ban." };
  }

  if (mode === "bot_smart" && (playerDeck.length < 5 || playerDeck.length > 6)) {
    return { isValid: false, error: "Battle Bot accepts a 5-emoji squad or a 6-emoji active deck." };
  }

  const uniqueDeck = new Set(playerDeck);
  if (uniqueDeck.size !== playerDeck.length) {
    return { isValid: false, error: "Battle Bot team selection must use unique emojis." };
  }

  const allowedEmojiIds = new Set(Object.keys(EMOJI_DEFINITIONS));
  if (playerDeck.some((emojiId) => !allowedEmojiIds.has(emojiId))) {
    return { isValid: false, error: "Battle Bot team contains an unsupported emoji id." };
  }

  return { isValid: true };
}

Deno.serve(async (request) => {
  if (request.method !== "POST") {
    return new Response(JSON.stringify({ error: "Method not allowed" }), { status: 405 });
  }

  try {
    const payload = await request.json() as StartBotMatchRequest;
    const requestUserId = await resolveRequestUserId(request);
    const mode: MatchMode = payload.mode === "bot_smart" ? "bot_smart" : "bot_practice";
    const playerDeck = Array.isArray(payload.playerDeck) ? payload.playerDeck as EmojiId[] : [];
    const validation = validateDeck(mode, playerDeck);
    if (!validation.isValid) {
      return new Response(JSON.stringify({ error: validation.error }), {
        status: 400,
        headers: { "Content-Type": "application/json" },
      });
    }

    const playerPlanTeam = mode === "bot_practice"
      ? [...playerDeck]
      : selectBattleTeamFromDeck(playerDeck, 5).team;
    const selectedPlayerTeam = selectBattleTeamFromDeck(playerDeck, 5);
    const botPlan = buildBotMatchPlan(mode, playerPlanTeam);
    const botProfile = botPlan.profile;
    const botDeck = botPlan.deck;
    const selectedBotTeam = {
      team: botPlan.team,
      benchedEmojiId: botPlan.benchEmojiId,
    };
    const botFormation = botPlan.formation.placements.length == 5
      ? botPlan.formation
      : buildDefaultFormation(selectedBotTeam.team);
    const battleSeed = crypto.randomUUID();

    if (mode === "bot_practice") {
      const botBan = getBotBan(mode, playerDeck);
      const inserted = await insertRows<{ id: string }>("matches", {
        mode,
        status: "banning",
        player_a: requestUserId,
        bot_profile_id: botProfile.id,
        deck_a: playerDeck,
        deck_b: botDeck,
        bans: {
          player_b: botBan,
        },
        current_state: {
          phase: "ban",
          battleSeed,
          playerDeckA: playerDeck,
          playerDeckB: botDeck,
          pendingBotBan: botBan,
          plannedFormationB: botFormation,
          botStrategy: botPlan.strategyLabel,
        },
        rules_version: RULES_VERSION,
      });

      return new Response(JSON.stringify({
        matchId: inserted[0].id,
        mode,
        playerDeckId: payload.activeDeckId ?? null,
        botProfile,
        playerDeck,
        botDeck,
        playerTeam: playerDeck,
        botTeam: botDeck,
        benchEmojiId: null,
        rulesVersion: RULES_VERSION,
        status: "banning",
        phase: "ban",
        botFormation,
        note: `${botPlan.note}\n\nPick one Practice Bot emoji to ban. The bot's ban stays hidden until you lock yours.`,
      }, null, 2), {
        headers: { "Content-Type": "application/json" },
      });
    }

    const inserted = await insertRows<{ id: string }>("matches", {
      mode,
      status: "formation",
      player_a: requestUserId,
      bot_profile_id: botProfile.id,
      deck_a: playerDeck,
      deck_b: botDeck,
      bans: {},
      current_state: {
        phase: "formation",
        battleSeed,
        playerDeckA: playerDeck,
        playerDeckB: botDeck,
        finalTeamA: selectedPlayerTeam.team,
        finalTeamB: selectedBotTeam.team,
        formationB: botFormation,
        botStrategy: botPlan.strategyLabel,
      },
      rules_version: RULES_VERSION,
    });

    const playerNote = selectedPlayerTeam.benchedEmojiId == null
      ? "Arrange your final 5 and lock formation to start the bot battle."
      : `Battle Bot auto-benched ${selectedPlayerTeam.benchedEmojiId} from the 6-emoji active deck. Arrange the final 5 and lock formation.`;

    const botBenchNote = selectedBotTeam.benchedEmojiId == null
      ? ""
      : ` Bot benched ${selectedBotTeam.benchedEmojiId} from its 6-card prep deck.`;

    return new Response(JSON.stringify({
      matchId: inserted[0].id,
      mode,
      playerDeckId: payload.activeDeckId ?? null,
      botProfile,
      playerDeck,
      botDeck,
      playerTeam: selectedPlayerTeam.team,
      botTeam: selectedBotTeam.team,
      benchEmojiId: selectedPlayerTeam.benchedEmojiId,
      rulesVersion: RULES_VERSION,
      status: "formation",
      phase: "formation",
      botFormation,
      note: `${botPlan.note}${botBenchNote}\n\n${playerNote}`,
    }, null, 2), {
      headers: { "Content-Type": "application/json" },
    });
  } catch (error) {
    return new Response(JSON.stringify({ error: error instanceof Error ? error.message : "Unable to start bot match." }), {
      status: 500,
      headers: { "Content-Type": "application/json" },
    });
  }
});
