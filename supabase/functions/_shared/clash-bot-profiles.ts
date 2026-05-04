export type ClashBotSkillBand = "novice" | "standard" | "skilled" | "elite";
export type ClashBotPersona = "tempo" | "trick" | "control" | "comeback";

export interface ClashBotProfile {
  bot_profile_id: string;
  display_name: string;
  avatar_key: string;
  persona: ClashBotPersona;
  skill_band: ClashBotSkillBand;
  timing_profile: "quick" | "steady" | "patient";
  active: boolean;
}

export const CLASH_BOT_PROFILES: ClashBotProfile[] = [
  profile("qc_rival_001", "PixelNova", "lightning", "tempo", "standard", "quick"),
  profile("qc_rival_002", "MintRival", "plant", "control", "standard", "steady"),
  profile("qc_rival_003", "ZapMochi", "magnet", "trick", "skilled", "quick"),
  profile("qc_rival_004", "LumaDash", "wind", "tempo", "standard", "steady"),
  profile("qc_rival_005", "OrbitKix", "ghost", "trick", "skilled", "patient"),
  profile("qc_rival_006", "NeonPip", "heart", "comeback", "novice", "steady"),
  profile("qc_rival_007", "GlitchPop", "mirror", "trick", "standard", "quick"),
  profile("qc_rival_008", "SunnyByte", "fire", "tempo", "novice", "quick"),
  profile("qc_rival_009", "AquaRift", "water", "control", "standard", "patient"),
  profile("qc_rival_010", "EmberAce", "bomb", "tempo", "skilled", "quick"),
  profile("qc_rival_011", "EchoJolt", "chain", "control", "skilled", "steady"),
  profile("qc_rival_012", "PrismDash", "ice", "control", "standard", "steady"),
  profile("qc_rival_013", "VibeRune", "hole", "trick", "standard", "patient"),
  profile("qc_rival_014", "TurboMint", "soap", "tempo", "novice", "quick"),
  profile("qc_rival_015", "ChillSpark", "ice", "control", "novice", "steady"),
  profile("qc_rival_016", "NovaNoodle", "snake", "comeback", "standard", "patient"),
  profile("qc_rival_017", "BerryVolt", "lightning", "tempo", "skilled", "steady"),
  profile("qc_rival_018", "CosmoTap", "shield", "control", "elite", "patient"),
  profile("qc_rival_019", "JellyArc", "heart", "comeback", "standard", "steady"),
  profile("qc_rival_020", "BlinkTempo", "wind", "tempo", "elite", "quick"),
];

export function getClashBotProfile(profileId: string): ClashBotProfile | null {
  return CLASH_BOT_PROFILES.find((profile) => profile.bot_profile_id === profileId && profile.active) ?? null;
}

export function selectClashBotProfile(
  rating: number,
  seed: string,
  recentProfileId = "",
): ClashBotProfile {
  const preferredBand = skillBandForRating(rating);
  const preferred = CLASH_BOT_PROFILES.filter((profile) => profile.active && profile.skill_band === preferredBand);
  const candidates = preferred.length > 0 ? preferred : CLASH_BOT_PROFILES.filter((profile) => profile.active);
  const nonRepeat = candidates.filter((profile) => profile.bot_profile_id !== recentProfileId);
  const pool = nonRepeat.length > 0 ? nonRepeat : candidates;
  return pool[stableHash(`${seed}|${rating}|${preferredBand}`) % pool.length];
}

function profile(
  bot_profile_id: string,
  display_name: string,
  avatar_key: string,
  persona: ClashBotPersona,
  skill_band: ClashBotSkillBand,
  timing_profile: ClashBotProfile["timing_profile"],
): ClashBotProfile {
  return { bot_profile_id, display_name, avatar_key, persona, skill_band, timing_profile, active: true };
}

function skillBandForRating(rating: number): ClashBotSkillBand {
  if (rating < 900) return "novice";
  if (rating < 1150) return "standard";
  if (rating < 1350) return "skilled";
  return "elite";
}

function stableHash(value: string): number {
  let hash = 2166136261;
  for (let index = 0; index < value.length; index++) {
    hash ^= value.charCodeAt(index);
    hash = Math.imul(hash, 16777619);
  }

  return hash >>> 0;
}
