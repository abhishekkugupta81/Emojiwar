using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EmojiWar.Client.Content;
using EmojiWar.Client.Core;
using EmojiWar.Client.Core.Decks;
using EmojiWar.Client.UI.Match;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EmojiWar.Client.UI.Common
{
    public sealed class StickerPopArenaFlow : MonoBehaviour
    {
        private const string OverlayName = "StickerPopArenaOverlay";
        private const string StoredSquadKey = "emojiwar.stickerpop.squad";
        private const string FlowActiveKey = "emojiwar.stickerpop.flow_active";
        private const int RequiredSquadCount = 6;
        private const int FormationSlotCount = 5;
        private static readonly bool UseRescueBlindBan = true;

        private static readonly Color Ink = new Color32(0x12, 0x11, 0x26, 0xFF);
        private static readonly Color NightPurple = new Color32(0x1C, 0x17, 0x40, 0xFF);
        private static readonly Color DeepIndigo = new Color32(0x2A, 0x1E, 0x5C, 0xFF);
        private static readonly Color SoftWhite = new Color32(0xF8, 0xF7, 0xFF, 0xFF);
        private static readonly Color HotPink = new Color32(0xFF, 0x4F, 0xD8, 0xFF);
        private static readonly Color ElectricPurple = new Color32(0x8B, 0x5C, 0xFF, 0xFF);
        private static readonly Color NeonBlue = new Color32(0x34, 0xB6, 0xFF, 0xFF);
        private static readonly Color AquaMint = new Color32(0x3D, 0xFF, 0xD1, 0xFF);
        private static readonly Color SunnyYellow = new Color32(0xFF, 0xD8, 0x4D, 0xFF);
        private static readonly Color OrangePop = new Color32(0xFF, 0x8A, 0x3D, 0xFF);
        private static readonly Color LavaRed = new Color32(0xFF, 0x5B, 0x6E, 0xFF);
        private static readonly Color SlimeGreen = new Color32(0x7D, 0xFF, 0x6A, 0xFF);

        private static readonly EmojiId[] DemoOpponentSquad =
        {
            EmojiId.Bomb,
            EmojiId.Magnet,
            EmojiId.Snake,
            EmojiId.Hole,
            EmojiId.Lightning,
            EmojiId.Heart
        };

        private static readonly EmojiId[] DemoDefaultSquad =
        {
            EmojiId.Fire,
            EmojiId.Water,
            EmojiId.Lightning,
            EmojiId.Shield,
            EmojiId.Heart,
            EmojiId.Wind
        };

        private static Font cachedFont;
        private static Font cachedEmojiFont;
        private static Sprite cachedCircleSprite;
        private static Sprite cachedSoftSquareSprite;

        private RectTransform overlayRoot;
        private Coroutine screenEnterRoutine;
        private readonly List<EmojiId> builderSelection = new List<EmojiId>();
        private readonly List<EmojiId> matchSquad = new List<EmojiId>();
        private readonly EmojiId[] formationSlots = new EmojiId[FormationSlotCount];
        private readonly bool[] formationSlotFilled = new bool[FormationSlotCount];
        private EmojiId? selectedEnemyBan;
        private EmojiId opponentBan = EmojiId.Wind;
        private bool banLocked;
        private int activeFormationSlot;
        private bool resultVictory = true;

        private enum MatchStep
        {
            Ban,
            Formation,
            Result
        }

        public static void AttachHome(MonoBehaviour host)
        {
            if (host == null || !host.isActiveAndEnabled)
            {
                return;
            }

            var flow = GetOrAdd(host.gameObject, () => host.GetComponent<StickerPopArenaFlow>());
            flow.RenderHome();
        }

        public static void AttachDeckBuilder(MonoBehaviour host)
        {
            if (host == null || !host.isActiveAndEnabled)
            {
                return;
            }

            var flow = GetOrAdd(host.gameObject, () => host.GetComponent<StickerPopArenaFlow>());
            flow.RenderSquadBuilder();
        }

        public static bool AttachMatch(MonoBehaviour host)
        {
            if (host == null || !host.isActiveAndEnabled)
            {
                return false;
            }

            if (PlayerPrefs.GetInt(FlowActiveKey, 0) != 1)
            {
                return false;
            }

            var flow = GetOrAdd(host.gameObject, () => host.GetComponent<StickerPopArenaFlow>());
            flow.RenderMatch(MatchStep.Ban);
            return true;
        }

        private void RenderHome()
        {
            var squad = ResolveCurrentSquad();
            CreateOverlay("Sticker Pop Home");
            ApplyGradient(DeepIndigo, HotPink, NeonBlue);
            AddAmbientBlobs("HomeBlobs", HotPink, NeonBlue, AquaMint);

            CreatePhaseHeader(overlayRoot, "Emoji War", "Sticker Pop Arena", "Ranked season: Candy Rift • Silver II", 0);

            var hero = CreatePanel("HeroArena", overlayRoot, new Vector2(0.05f, 0.60f), new Vector2(0.95f, 0.885f), new Color(0.10f, 0.04f, 0.24f, 0.58f));
            AddGraphicEffects(hero.GetComponent<Image>(), new Color(1f, 0.31f, 0.85f, 0.70f), new Vector2(0f, -7f));
            CreateText("HeroTitle", hero, "Battle arcade first impression", 35, FontStyle.Bold, TextAnchor.UpperLeft, SoftWhite, new Vector2(0.06f, 0.58f), new Vector2(0.70f, 0.93f));
            CreateText("HeroBody", hero, "Pick a bright squad, lock a ban, then snap fighters into the arena.", 20, FontStyle.Bold, TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 0.86f), new Vector2(0.06f, 0.28f), new Vector2(0.70f, 0.56f));
            CreateFloatingEmoji(hero, EmojiId.Fire, new Vector2(0.73f, 0.50f), 132, -8f);
            CreateFloatingEmoji(hero, EmojiId.Heart, new Vector2(0.88f, 0.70f), 106, 9f);
            CreateFloatingEmoji(hero, EmojiId.Lightning, new Vector2(0.88f, 0.28f), 98, 4f);

            var squadPanel = CreatePanel("StickerSquadTray", overlayRoot, new Vector2(0.05f, 0.395f), new Vector2(0.95f, 0.585f), new Color(0.05f, 0.03f, 0.13f, 0.96f), cachedSoftSquareSprite);
            AddGraphicEffects(squadPanel.GetComponent<Image>(), new Color(0.24f, 1f, 0.82f, 0.44f), new Vector2(0f, -5f));
            var squadTitle = CreateText("StickerSquadTitle", squadPanel, "Current Squad", 24, FontStyle.Bold, TextAnchor.UpperLeft, SoftWhite, new Vector2(0.05f, 0.68f), new Vector2(0.70f, 0.96f));
            squadTitle.resizeTextForBestFit = false;
            var rankChip = CreateText("RankChip", squadPanel, "+26 XP • 2/4 streak", 17, FontStyle.Bold, TextAnchor.MiddleRight, AquaMint, new Vector2(0.34f, 0.68f), new Vector2(0.94f, 0.96f));
            rankChip.resizeTextForBestFit = false;
            var squadGlow = CreatePanel("StickerSquadGlow", squadPanel, new Vector2(0.03f, 0.08f), new Vector2(0.97f, 0.62f), new Color(0.24f, 1f, 0.82f, 0.16f), cachedSoftSquareSprite);
            squadGlow.SetAsFirstSibling();
            var squadRow = CreateRow("StickerSquadRow", squadPanel, new Vector2(0.025f, 0.06f), new Vector2(0.975f, 0.66f), -3f);
            squadRow.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            foreach (var emojiId in squad)
            {
                CreateMiniSticker(squadRow, emojiId, UnitCardState.Selected, 74f);
            }

            var actionBand = CreatePanel("HomeActions", overlayRoot, new Vector2(0.05f, 0.17f), new Vector2(0.95f, 0.37f), new Color(0.10f, 0.04f, 0.24f, 0.44f), cachedSoftSquareSprite);
            CreatePrimaryButton("Play Ranked", actionBand, new Vector2(0.05f, 0.47f), new Vector2(0.95f, 0.92f), HotPink, () =>
            {
                LaunchSelections.BeginRankedMatchSelection();
                LaunchSelections.SetPendingSquad(squad);
                StoreSquad(squad);
                MarkFlowActive();
                SceneManager.LoadScene(SceneNames.DeckBuilder);
            }, breathe: true);

            var editButton = CreateSecondaryButton("Edit Squad", actionBand, new Vector2(0.05f, 0.10f), new Vector2(0.48f, 0.39f), NeonBlue, () =>
            {
                LaunchSelections.SetPendingSquad(squad);
                StoreSquad(squad);
                SceneManager.LoadScene(SceneNames.DeckBuilder);
            });
            AddButtonPop(editButton);
            CreateSecondaryButton("Replay", actionBand, new Vector2(0.52f, 0.10f), new Vector2(0.95f, 0.39f), ElectricPurple, () =>
            {
                StoreSquad(squad);
                MarkFlowActive();
                SceneManager.LoadScene(SceneNames.Match);
            });

            CreateBottomNav("Home", overlayRoot);
            StartScreenEnter();
        }

        private void RenderSquadBuilder()
        {
            builderSelection.Clear();
            builderSelection.AddRange(ResolveCurrentSquad().Take(RequiredSquadCount));

            CreateOverlay("Sticker Pop Squad Builder");
            ApplyGradient(DeepIndigo, NeonBlue, AquaMint);
            AddAmbientBlobs("SquadBlobs", NeonBlue, AquaMint, HotPink);

            CreatePhaseHeader(overlayRoot, "Build your squad", "Step 1 of 4 • Squad", $"{builderSelection.Count}/{RequiredSquadCount} selected", 1);
            CreateFilterBar();
            CreateBuilderGrid();
            CreateSelectedTray();
            CreateBuilderFooter();
            StartScreenEnter();
        }

        private void RenderMatch(MatchStep step)
        {
            if (matchSquad.Count == 0)
            {
                matchSquad.AddRange(ResolveCurrentSquad().Take(RequiredSquadCount));
            }

            if (!matchSquad.Contains(opponentBan))
            {
                opponentBan = matchSquad.LastOrDefault();
            }

            switch (step)
            {
                case MatchStep.Ban:
                    if (UseRescueBlindBan)
                    {
                        RenderRescueBlindBan();
                    }
                    else
                    {
                        RenderBan();
                    }
                    break;
                case MatchStep.Formation:
                    RenderFormation();
                    break;
                case MatchStep.Result:
                    RenderResult();
                    break;
            }
        }

        private void RenderRescueBlindBan()
        {
            CreateOverlay("Sticker Pop Blind Ban Rescue");

            var screen = overlayRoot.gameObject.AddComponent<BlindBanRescueScreen>();
            screen.Bind(
                DemoOpponentSquad.Select(emojiId => BlindBanRescueScreen.UnitView.FromEmojiId(emojiId, enemyTone: true)).ToArray(),
                matchSquad.Select(emojiId => BlindBanRescueScreen.UnitView.FromEmojiId(emojiId, enemyTone: false)).ToArray(),
                BlindBanRescueScreen.BlindBanVisibilityMode.TestingRevealOpponentCards,
                selectedEnemyBan.HasValue ? EmojiIdUtility.ToApiId(selectedEnemyBan.Value) : string.Empty,
                banLocked && selectedEnemyBan.HasValue ? EmojiIdUtility.ToApiId(selectedEnemyBan.Value) : string.Empty,
                EmojiIdUtility.ToApiId(opponentBan),
                banLocked && selectedEnemyBan.HasValue,
                targetId =>
                {
                    if (banLocked || !EmojiIdUtility.TryFromApiId(targetId, out var parsed))
                    {
                        return;
                    }

                    selectedEnemyBan = parsed;
                    RenderRescueBlindBan();
                },
                targetId =>
                {
                    if (banLocked || !EmojiIdUtility.TryFromApiId(targetId, out var parsed))
                    {
                        return;
                    }

                    selectedEnemyBan = parsed;
                    StartCoroutine(ConfirmRescueBanRoutine());
                },
                27);
        }

        private void RenderBan()
        {
            CreateOverlay("Sticker Pop Blind Ban");
            ApplyGradient(NightPurple, LavaRed, OrangePop);
            AddAmbientBlobs("BanBlobs", LavaRed, OrangePop, ElectricPurple);

            CreatePhaseHeader(overlayRoot, "Turn the phase into a face-off", "Step 2 of 4 • Ban", banLocked ? "Ban locked • matchup stays visible" : "Timer 27s • choose one enemy", 2);
            CreateStatusChip(overlayRoot, banLocked ? "LOCKED" : "27s", banLocked ? SlimeGreen : SunnyYellow, new Vector2(0.73f, 0.806f), new Vector2(0.92f, 0.846f));

            var enemyPanel = CreatePanel("EnemySquadPanel", overlayRoot, new Vector2(0.05f, 0.575f), new Vector2(0.95f, 0.785f), new Color(0.08f, 0.03f, 0.12f, 0.56f), cachedSoftSquareSprite);
            AddGraphicEffects(enemyPanel.GetComponent<Image>(), LavaRed, new Vector2(0f, -5f));
            var enemyTitle = CreateText("EnemyTitle", enemyPanel, "Enemy squad", 18, FontStyle.Bold, TextAnchor.MiddleLeft, SoftWhite, new Vector2(0.04f, 0.74f), new Vector2(0.56f, 0.96f));
            CreateText("EnemyInstruction", enemyPanel, banLocked ? "Their locked unit is stamped out." : "Tap one sticker to target the ban.", 14, FontStyle.Bold, TextAnchor.MiddleRight, new Color(1f, 1f, 1f, 0.76f), new Vector2(0.42f, 0.74f), new Vector2(0.96f, 0.96f));
            enemyTitle.gameObject.AddComponent<EmojiMotionController>().JumpSelect();

            var enemyRow = CreateTeamRow("EnemyTeamRow", enemyPanel, new Vector2(0.025f, 0.06f), new Vector2(0.975f, 0.72f));
            enemyRow.GetComponent<HorizontalLayoutGroup>().spacing = -1f;
            foreach (var emojiId in DemoOpponentSquad)
            {
                var state = selectedEnemyBan == emojiId ? UnitCardState.Selected : UnitCardState.Default;
                if (banLocked && selectedEnemyBan == emojiId && selectedEnemyBan.HasValue)
                {
                    state |= UnitCardState.Banned;
                }
                CreateTeamCard(enemyRow, emojiId, state, () =>
                {
                    if (banLocked)
                    {
                        return;
                    }

                    selectedEnemyBan = emojiId;
                    RenderBan();
                });
            }

            var vs = CreatePanel("VsBurst", overlayRoot, new Vector2(0.31f, 0.455f), new Vector2(0.69f, 0.57f), new Color(1f, 0.20f, 0.70f, 0.52f), cachedCircleSprite);
            AddGraphicEffects(vs.GetComponent<Image>(), SunnyYellow, new Vector2(0f, -7f));
            CreateText("VsLabel", vs, "VS", 50, FontStyle.Bold, TextAnchor.MiddleCenter, SunnyYellow, Vector2.zero, Vector2.one);
            vs.gameObject.AddComponent<EmojiIdleMotion>().Configure(true);

            var copy = selectedEnemyBan.HasValue
                ? banLocked
                    ? $"{EmojiIdUtility.ToDisplayName(selectedEnemyBan.Value)} is banned. Your remaining squad is ready to place."
                    : $"Lock the {EmojiIdUtility.ToDisplayName(selectedEnemyBan.Value)} ban and keep the matchup in view."
                : "Tap one enemy sticker. Both squads stay on screen after the ban.";
            CreateText("BanCopy", overlayRoot, copy, 18, FontStyle.Bold, TextAnchor.MiddleCenter, SoftWhite, new Vector2(0.08f, 0.385f), new Vector2(0.92f, 0.44f));

            var yourPanel = CreatePanel("YourSquadPanel", overlayRoot, new Vector2(0.05f, 0.17f), new Vector2(0.95f, 0.355f), new Color(0.05f, 0.02f, 0.11f, 0.50f), cachedSoftSquareSprite);
            AddGraphicEffects(yourPanel.GetComponent<Image>(), OrangePop, new Vector2(0f, -5f));
            CreateText("YourTitle", yourPanel, "Your squad stays visible", 18, FontStyle.Bold, TextAnchor.MiddleLeft, SoftWhite, new Vector2(0.04f, 0.72f), new Vector2(0.82f, 0.96f));
            var yourRow = CreateTeamRow("YourTeamRow", yourPanel, new Vector2(0.025f, 0.08f), new Vector2(0.975f, 0.70f));
            yourRow.GetComponent<HorizontalLayoutGroup>().spacing = -4f;
            foreach (var emojiId in matchSquad)
            {
                CreateMiniSticker(yourRow, emojiId, emojiId == opponentBan ? UnitCardState.Banned : UnitCardState.Default, 66f);
            }

            UnityEngine.Events.UnityAction confirmBanAction = banLocked
                ? () => RenderMatch(MatchStep.Formation)
                : selectedEnemyBan.HasValue
                    ? ConfirmBan
                    : (UnityEngine.Events.UnityAction)null;
            CreatePrimaryButton(
                banLocked ? "Continue to Formation" : selectedEnemyBan.HasValue ? "Confirm Ban" : "Choose Enemy",
                overlayRoot,
                new Vector2(0.08f, 0.08f),
                new Vector2(0.92f, 0.145f),
                LavaRed,
                confirmBanAction,
                breathe: selectedEnemyBan.HasValue || banLocked);

            StartScreenEnter();
        }

        private void ConfirmBan()
        {
            if (!selectedEnemyBan.HasValue)
            {
                return;
            }

            StartCoroutine(ConfirmBanRoutine());
        }

        private IEnumerator ConfirmBanRoutine()
        {
            var stamp = CreatePanel("BanStampOverlay", overlayRoot, new Vector2(0.22f, 0.39f), new Vector2(0.78f, 0.62f), new Color(1f, 0.04f, 0.10f, 0.78f));
            CreateText("BanStampText", stamp, "BANNED", 44, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, Vector2.zero, Vector2.one);
            stamp.localRotation = Quaternion.Euler(0f, 0f, -9f);
            stamp.gameObject.AddComponent<EmojiMotionController>().StickerSlam();
            yield return new WaitForSecondsRealtime(0.34f);
            banLocked = true;
            RenderBan();
        }

        private IEnumerator ConfirmRescueBanRoutine()
        {
            yield return new WaitForSecondsRealtime(0.28f);
            banLocked = true;
            RenderRescueBlindBan();
        }

        private void RenderFormation()
        {
            CreateOverlay("Sticker Pop Formation");
            ApplyGradient(DeepIndigo, AquaMint, NeonBlue);
            AddAmbientBlobs("FormationBlobs", AquaMint, NeonBlue, ElectricPurple);

            CreatePhaseHeader(overlayRoot, "Make positioning feel tactile", "Step 3 of 4 • Positioning", "Opponent placement 3/5", 3);
            CreateBanChips();

            var board = CreatePanel("FormationBoard", overlayRoot, new Vector2(0.06f, 0.31f), new Vector2(0.94f, 0.73f), new Color(0.02f, 0.05f, 0.13f, 0.78f), cachedSoftSquareSprite);
            AddGraphicEffects(board.GetComponent<Image>(), AquaMint, new Vector2(0f, -8f));
            CreateText("BoardTitle", board, "Arena Board", 20, FontStyle.Bold, TextAnchor.UpperLeft, SoftWhite, new Vector2(0.06f, 0.86f), new Vector2(0.92f, 0.98f));
            CreateText("BoardHint", board, "Front row hits first. Back row keeps pressure safe.", 14, FontStyle.Bold, TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 0.76f), new Vector2(0.06f, 0.76f), new Vector2(0.92f, 0.86f));

            CreateFormationSlot(board, 0, "Front", new Vector2(0.07f, 0.40f), new Vector2(0.33f, 0.70f));
            CreateFormationSlot(board, 1, "Front", new Vector2(0.37f, 0.40f), new Vector2(0.63f, 0.70f));
            CreateFormationSlot(board, 2, "Front", new Vector2(0.67f, 0.40f), new Vector2(0.93f, 0.70f));
            CreateFormationSlot(board, 3, "Back", new Vector2(0.20f, 0.08f), new Vector2(0.46f, 0.36f));
            CreateFormationSlot(board, 4, "Back", new Vector2(0.54f, 0.08f), new Vector2(0.80f, 0.36f));

            var tray = CreatePanel("RemainingTray", overlayRoot, new Vector2(0.06f, 0.18f), new Vector2(0.94f, 0.29f), new Color(0.02f, 0.02f, 0.08f, 0.58f), cachedSoftSquareSprite);
            AddGraphicEffects(tray.GetComponent<Image>(), NeonBlue, new Vector2(0f, -5f));
            var trayRow = CreateRow("RemainingUnits", tray, new Vector2(0.025f, 0.10f), new Vector2(0.975f, 0.90f), -4f);
            trayRow.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            foreach (var emojiId in GetFormationTeam())
            {
                var alreadyPlaced = IsPlaced(emojiId);
                UnityEngine.Events.UnityAction placeAction = alreadyPlaced ? (UnityEngine.Events.UnityAction)null : () =>
                {
                    PlaceFormationEmoji(emojiId);
                };
                CreateMiniSticker(trayRow, emojiId, alreadyPlaced ? UnitCardState.Disabled : UnitCardState.Default, 66f, placeAction);
            }

            UnityEngine.Events.UnityAction lockFormationAction = AllFormationSlotsFilled() ? LockFormation : (UnityEngine.Events.UnityAction)null;
            CreateSecondaryButton("Reset", overlayRoot, new Vector2(0.08f, 0.075f), new Vector2(0.40f, 0.145f), ElectricPurple, ResetFormation);
            CreatePrimaryButton(
                AllFormationSlotsFilled() ? "Lock Formation" : $"Place {CountFilledSlots()}/{FormationSlotCount}",
                overlayRoot,
                new Vector2(0.43f, 0.075f),
                new Vector2(0.92f, 0.145f),
                AquaMint,
                lockFormationAction,
                breathe: AllFormationSlotsFilled());

            StartScreenEnter();
        }

        private void RenderResult()
        {
            CreateOverlay("Sticker Pop Result");
            if (resultVictory)
            {
                ApplyGradient(DeepIndigo, AquaMint, SlimeGreen);
                AddAmbientBlobs("ResultVictoryBlobs", SlimeGreen, AquaMint, HotPink);
            }
            else
            {
                ApplyGradient(NightPurple, LavaRed, OrangePop);
                AddAmbientBlobs("ResultDefeatBlobs", LavaRed, OrangePop, ElectricPurple);
            }

            CreatePhaseHeader(overlayRoot, "Emotion first, explanation second", "Step 4 of 4 • Resolution", resultVictory ? "+26 RP • streak saved" : "-12 RP • countered front line", 4);
            var headlinePanel = CreatePanel("ResultHeadline", overlayRoot, new Vector2(0.06f, 0.56f), new Vector2(0.94f, 0.83f), new Color(0.02f, 0.02f, 0.08f, 0.58f), cachedSoftSquareSprite);
            AddGraphicEffects(headlinePanel.GetComponent<Image>(), resultVictory ? SlimeGreen : LavaRed, new Vector2(0f, -8f));
            CreateText("ResultHeadlineText", headlinePanel, resultVictory ? "VICTORY" : "DEFEAT", 58, FontStyle.Bold, TextAnchor.MiddleCenter, resultVictory ? SlimeGreen : LavaRed, new Vector2(0.05f, 0.55f), new Vector2(0.95f, 0.98f));
            var heroRow = CreateRow("ResultHeroEmojis", headlinePanel, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.54f), 14f);
            heroRow.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            foreach (var emojiId in GetFormationTeam().Take(3))
            {
                CreateMiniSticker(heroRow, emojiId, UnitCardState.Selected, 92f);
            }
            CreateText("ResultBurstLeft", headlinePanel, resultVictory ? "✦" : "!", 48, FontStyle.Bold, TextAnchor.MiddleCenter, resultVictory ? SunnyYellow : OrangePop, new Vector2(0.02f, 0.54f), new Vector2(0.18f, 0.96f)).gameObject.AddComponent<EmojiIdleMotion>().Configure(true);
            CreateText("ResultBurstRight", headlinePanel, resultVictory ? "✦" : "!", 48, FontStyle.Bold, TextAnchor.MiddleCenter, resultVictory ? SunnyYellow : OrangePop, new Vector2(0.82f, 0.54f), new Vector2(0.98f, 0.96f)).gameObject.AddComponent<EmojiIdleMotion>().Configure(true);

            CreateProgressBar(overlayRoot, new Vector2(0.09f, 0.505f), new Vector2(0.91f, 0.545f), resultVictory ? 0.72f : 0.42f);
            CreateMomentsStrip();
            CreateWhyPanel();

            CreatePrimaryButton(resultVictory ? "Rematch" : "Run It Back", overlayRoot, new Vector2(0.08f, 0.13f), new Vector2(0.92f, 0.20f), resultVictory ? SlimeGreen : LavaRed, () =>
            {
                ResetMatchState();
                RenderMatch(MatchStep.Ban);
            }, breathe: true);
            CreateSecondaryButton("Edit Squad", overlayRoot, new Vector2(0.08f, 0.045f), new Vector2(0.34f, 0.105f), NeonBlue, () => SceneManager.LoadScene(SceneNames.DeckBuilder));
            CreateSecondaryButton("Replay", overlayRoot, new Vector2(0.37f, 0.045f), new Vector2(0.63f, 0.105f), ElectricPurple, () => RenderResult());
            CreateSecondaryButton("Home", overlayRoot, new Vector2(0.66f, 0.045f), new Vector2(0.92f, 0.105f), SunnyYellow, () =>
            {
                ClearFlowActive();
                SceneManager.LoadScene(SceneNames.Home);
            });

            var headlineMotion = headlinePanel.gameObject.AddComponent<EmojiMotionController>();
            headlineMotion.StickerSlam();
            StartScreenEnter();
        }

        private void CreateFilterBar()
        {
            var filterBar = CreateRow("FilterPills", overlayRoot, new Vector2(0.06f, 0.765f), new Vector2(0.94f, 0.805f), 7f);
            CreatePill(filterBar, "All", HotPink, true);
            CreatePill(filterBar, "Attack", OrangePop, false);
            CreatePill(filterBar, "Control", NeonBlue, false);
            CreatePill(filterBar, "Support", AquaMint, false);
        }

        private void CreateBuilderGrid()
        {
            var gridPanel = CreatePanel("SquadGridPanel", overlayRoot, new Vector2(0.04f, 0.325f), new Vector2(0.96f, 0.755f), new Color(0.02f, 0.02f, 0.09f, 0.42f), cachedSoftSquareSprite);
            AddGraphicEffects(gridPanel.GetComponent<Image>(), NeonBlue, new Vector2(0f, -5f));
            var mask = gridPanel.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;
            var scrollRect = gridPanel.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.viewport = gridPanel;

            var grid = new GameObject("EmojiCardGrid", typeof(RectTransform), typeof(GridLayoutGroup));
            grid.transform.SetParent(gridPanel, false);
            var gridRect = grid.GetComponent<RectTransform>();
            gridRect.anchorMin = new Vector2(0f, 1f);
            gridRect.anchorMax = new Vector2(1f, 1f);
            gridRect.pivot = new Vector2(0.5f, 1f);
            gridRect.offsetMin = new Vector2(0f, -570f);
            gridRect.offsetMax = Vector2.zero;
            gridRect.anchoredPosition = Vector2.zero;
            scrollRect.content = gridRect;
            var layout = grid.GetComponent<GridLayoutGroup>();
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 3;
            layout.padding = new RectOffset(8, 8, 12, 16);
            layout.spacing = new Vector2(7f, 8f);
            layout.cellSize = new Vector2(106f, 128f);
            layout.childAlignment = TextAnchor.UpperCenter;

            foreach (var emojiId in EmojiIdUtility.LaunchRoster.Take(12))
            {
                var state = builderSelection.Contains(emojiId) ? UnitCardState.Selected : UnitCardState.Default;
                if (builderSelection.Count >= RequiredSquadCount && !builderSelection.Contains(emojiId))
                {
                    state |= UnitCardState.Disabled;
                }

                CreateStickerCard(grid.transform, emojiId, state, compact: false, () => ToggleBuilderEmoji(emojiId));
            }
        }

        private void CreateSelectedTray()
        {
            var tray = CreatePanel("SelectedSquadTray", overlayRoot, new Vector2(0.06f, 0.175f), new Vector2(0.94f, 0.310f), new Color(0.03f, 0.03f, 0.12f, 0.78f), cachedSoftSquareSprite);
            AddGraphicEffects(tray.GetComponent<Image>(), AquaMint, new Vector2(0f, -5f));
            CreateText("TrayTitle", tray, "Selected Squad", 18, FontStyle.Bold, TextAnchor.MiddleLeft, SoftWhite, new Vector2(0.05f, 0.68f), new Vector2(0.55f, 0.98f));
            CreateText("TrayCount", tray, $"{builderSelection.Count}/{RequiredSquadCount}", 18, FontStyle.Bold, TextAnchor.MiddleRight, builderSelection.Count == RequiredSquadCount ? SlimeGreen : SunnyYellow, new Vector2(0.55f, 0.68f), new Vector2(0.95f, 0.98f));
            var row = CreateRow("SelectedSquadIcons", tray, new Vector2(0.025f, 0.07f), new Vector2(0.975f, 0.68f), -4f);
            row.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            for (var index = 0; index < RequiredSquadCount; index++)
            {
                if (index < builderSelection.Count)
                {
                    var emojiId = builderSelection[index];
                    CreateMiniSticker(row, emojiId, UnitCardState.Selected, 70f, () => ToggleBuilderEmoji(emojiId));
                }
                else
                {
                    CreateEmptySlot(row, 70f);
                }
            }
        }

        private void CreateBuilderFooter()
        {
            CreateSecondaryButton("Back", overlayRoot, new Vector2(0.08f, 0.075f), new Vector2(0.35f, 0.145f), ElectricPurple, () => SceneManager.LoadScene(SceneNames.Home));
            UnityEngine.Events.UnityAction continueAction = builderSelection.Count == RequiredSquadCount ? ContinueFromBuilder : (UnityEngine.Events.UnityAction)null;
            CreatePrimaryButton(
                builderSelection.Count == RequiredSquadCount ? "Continue" : $"Choose {RequiredSquadCount - builderSelection.Count} more",
                overlayRoot,
                new Vector2(0.38f, 0.075f),
                new Vector2(0.92f, 0.145f),
                builderSelection.Count == RequiredSquadCount ? HotPink : new Color(0.25f, 0.28f, 0.42f, 1f),
                continueAction,
                breathe: builderSelection.Count == RequiredSquadCount);
        }

        private void ToggleBuilderEmoji(EmojiId emojiId)
        {
            if (builderSelection.Contains(emojiId))
            {
                builderSelection.Remove(emojiId);
            }
            else if (builderSelection.Count < RequiredSquadCount)
            {
                builderSelection.Add(emojiId);
            }

            RenderSquadBuilder();
        }

        private void ContinueFromBuilder()
        {
            if (builderSelection.Count != RequiredSquadCount)
            {
                return;
            }

            StoreSquad(builderSelection);
            LaunchSelections.SetPendingSquad(builderSelection);
            LaunchSelections.SetSelectedMode(LaunchSelections.PvpRanked);
            SaveActiveDeck(builderSelection);
            selectedEnemyBan = null;
            banLocked = false;
            ResetFormationState();
            MarkFlowActive();
            SceneManager.LoadScene(SceneNames.Match);
        }

        private void CreateBanChips()
        {
            var chips = CreateRow("BanResultChips", overlayRoot, new Vector2(0.06f, 0.76f), new Vector2(0.94f, 0.81f), 7f);
            CreatePill(chips, selectedEnemyBan.HasValue ? $"You banned {EmojiIdUtility.ToDisplayName(selectedEnemyBan.Value)}" : "You banned Fire", LavaRed, true);
            CreatePill(chips, $"Enemy banned {EmojiIdUtility.ToDisplayName(opponentBan)}", OrangePop, false);
        }

        private void CreateFormationSlot(RectTransform board, int slotIndex, string rowLabel, Vector2 anchorMin, Vector2 anchorMax)
        {
            var filled = formationSlotFilled[slotIndex];
            var slot = CreatePanel($"FormationSlot{slotIndex}", board, anchorMin, anchorMax, filled ? new Color(0.09f, 0.22f, 0.25f, 0.78f) : new Color(0.05f, 0.08f, 0.16f, 0.60f));
            var image = slot.GetComponent<Image>();
            AddGraphicEffects(image, slotIndex == activeFormationSlot && !filled ? AquaMint : new Color(1f, 1f, 1f, 0.32f), new Vector2(0f, -5f));
            if (slotIndex == activeFormationSlot && !filled)
            {
                slot.gameObject.AddComponent<EmojiIdleMotion>().Configure(false, true);
            }

            CreateText("SlotRowLabel", slot, rowLabel, 13, FontStyle.Bold, TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 0.72f), new Vector2(0.10f, 0.76f), new Vector2(0.90f, 0.98f));
            if (filled)
            {
                CreateFloatingEmoji(slot, formationSlots[slotIndex], new Vector2(0.50f, 0.52f), 96, slotIndex % 2 == 0 ? -4f : 4f);
            }
            else
            {
                CreateText("EmptySlotPlus", slot, "+", 32, FontStyle.Bold, TextAnchor.MiddleCenter, AquaMint, new Vector2(0.20f, 0.22f), new Vector2(0.80f, 0.72f));
            }

            var button = slot.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => activeFormationSlot = slotIndex);
            button.onClick.AddListener(() => RenderFormation());
        }

        private void PlaceFormationEmoji(EmojiId emojiId)
        {
            while (activeFormationSlot < FormationSlotCount && formationSlotFilled[activeFormationSlot])
            {
                activeFormationSlot++;
            }

            if (activeFormationSlot >= FormationSlotCount)
            {
                return;
            }

            formationSlots[activeFormationSlot] = emojiId;
            formationSlotFilled[activeFormationSlot] = true;
            activeFormationSlot = Mathf.Clamp(activeFormationSlot + 1, 0, FormationSlotCount - 1);
            RenderFormation();
        }

        private void ResetFormation()
        {
            ResetFormationState();
            RenderFormation();
        }

        private void ResetFormationState()
        {
            Array.Clear(formationSlots, 0, formationSlots.Length);
            Array.Clear(formationSlotFilled, 0, formationSlotFilled.Length);
            activeFormationSlot = 0;
        }

        private void LockFormation()
        {
            StartCoroutine(LockFormationRoutine());
        }

        private IEnumerator LockFormationRoutine()
        {
            var glow = CreatePanel("BoardConfirmationGlow", overlayRoot, new Vector2(0.06f, 0.31f), new Vector2(0.94f, 0.73f), new Color(0.24f, 1f, 0.82f, 0.22f));
            glow.gameObject.AddComponent<EmojiMotionController>().StickerSlam();
            yield return new WaitForSecondsRealtime(0.26f);
            resultVictory = true;
            RenderMatch(MatchStep.Result);
        }

        private void CreateMomentsStrip()
        {
            var strip = CreatePanel("ResultMomentsStrip", overlayRoot, new Vector2(0.06f, 0.38f), new Vector2(0.94f, 0.50f), new Color(0.02f, 0.02f, 0.08f, 0.48f));
            CreateText("MomentTitle", strip, "Decisive moments", 17, FontStyle.Bold, TextAnchor.UpperLeft, SoftWhite, new Vector2(0.05f, 0.70f), new Vector2(0.95f, 0.96f));
            var row = CreateRow("MomentRow", strip, new Vector2(0.04f, 0.12f), new Vector2(0.96f, 0.66f), 8f);
            CreatePill(row, "🔥 front pop", OrangePop, true);
            CreatePill(row, "🛡 held line", AquaMint, false);
            CreatePill(row, "⚡ finisher", SunnyYellow, false);
        }

        private void CreateWhyPanel()
        {
            var panel = CreatePanel("WhyResultPanel", overlayRoot, new Vector2(0.06f, 0.22f), new Vector2(0.94f, 0.36f), new Color(0.02f, 0.02f, 0.08f, 0.55f));
            CreateText("WhyTitle", panel, resultVictory ? "Why you won" : "Why it slipped", 18, FontStyle.Bold, TextAnchor.UpperLeft, SoftWhite, new Vector2(0.05f, 0.68f), new Vector2(0.95f, 0.96f));
            var body = resultVictory
                ? "Fire drew the ban, Shield protected the back row, and Lightning landed the final burst."
                : "Bomb forced your front line early, then Magnet pulled pressure away from Heart.";
            CreateText("WhyBody", panel, body, 15, FontStyle.Bold, TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 0.80f), new Vector2(0.05f, 0.12f), new Vector2(0.95f, 0.66f));
        }

        private void CreateProgressBar(RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, float fill)
        {
            var shell = CreatePanel("RankProgressShell", parent, anchorMin, anchorMax, new Color(1f, 1f, 1f, 0.18f));
            var fillObject = CreatePanel("RankProgressFill", shell, new Vector2(0.02f, 0.18f), new Vector2(Mathf.Lerp(0.08f, 0.98f, fill), 0.82f), resultVictory ? SlimeGreen : LavaRed);
            fillObject.gameObject.AddComponent<EmojiMotionController>().StickerSlam();
        }

        private void CreateBottomNav(string active, RectTransform parent)
        {
            var nav = CreatePanel("BottomNav", parent, new Vector2(0.05f, 0.035f), new Vector2(0.95f, 0.125f), new Color(0.05f, 0.04f, 0.13f, 0.82f), cachedSoftSquareSprite);
            AddGraphicEffects(nav.GetComponent<Image>(), new Color(1f, 1f, 1f, 0.28f), new Vector2(0f, -5f));
            var row = CreateRow("BottomNavRow", nav, new Vector2(0.025f, 0.14f), new Vector2(0.975f, 0.86f), 7f);
            CreateNavButton(row, "Home", active == "Home", () => SceneManager.LoadScene(SceneNames.Home));
            CreateNavButton(row, "Squad", active == "Squad", () => SceneManager.LoadScene(SceneNames.DeckBuilder));
            CreateNavButton(row, "Codex", false, () => SceneManager.LoadScene(SceneNames.Codex));
            CreateNavButton(row, "Ranks", false, () => SceneManager.LoadScene(SceneNames.Leaderboard));
        }

        private void CreatePhaseHeader(RectTransform parent, string kicker, string title, string status, int step)
        {
            CreateText("HeaderKicker", parent, kicker, 14, FontStyle.Bold, TextAnchor.MiddleLeft, SunnyYellow, new Vector2(0.07f, 0.925f), new Vector2(0.93f, 0.965f));
            CreateText("HeaderTitle", parent, title, 30, FontStyle.Bold, TextAnchor.MiddleLeft, SoftWhite, new Vector2(0.07f, 0.865f), new Vector2(0.93f, 0.925f));
            CreateText("HeaderStatus", parent, status, 14, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.78f), new Vector2(0.07f, 0.825f), new Vector2(0.93f, 0.865f));

            if (step <= 0)
            {
                return;
            }

            var stepper = CreateRow("RankedStepper", parent, new Vector2(0.07f, 0.810f), new Vector2(0.93f, 0.822f), 4f);
            for (var index = 1; index <= 4; index++)
            {
                var color = index <= step ? HotPink : new Color(1f, 1f, 1f, 0.18f);
                var stepNode = CreatePanel($"Step{index}", stepper, Vector2.zero, Vector2.one, color);
                var layout = stepNode.gameObject.AddComponent<LayoutElement>();
                layout.preferredWidth = 70f;
                layout.preferredHeight = 8f;
            }
        }

        private void CreateFloatingEmoji(RectTransform parent, EmojiId emojiId, Vector2 normalizedCenter, float size, float zTilt)
        {
            var holder = CreatePanel($"{emojiId}FloatSticker", parent, Vector2.zero, Vector2.zero, new Color(1f, 1f, 1f, 0.14f), cachedCircleSprite);
            holder.anchorMin = normalizedCenter;
            holder.anchorMax = normalizedCenter;
            holder.sizeDelta = new Vector2(size, size);
            holder.anchoredPosition = Vector2.zero;
            holder.localRotation = Quaternion.Euler(0f, 0f, zTilt);
            AddGraphicEffects(holder.GetComponent<Image>(), UiThemeRuntime.ResolveRoleAccent(emojiId), new Vector2(0f, -6f));
            var glyph = CreateText("Glyph", holder, string.Empty, Mathf.RoundToInt(size * 0.66f), FontStyle.Normal, TextAnchor.MiddleCenter, Color.white, new Vector2(0f, 0.08f), new Vector2(1f, 1f));
            ConfigureEmojiGlyph(glyph, emojiId, Mathf.RoundToInt(size * 0.66f));
            var fallback = CreateText("EmojiFallback", holder, EmojiIdUtility.ToDisplayName(emojiId).ToUpperInvariant(), Mathf.RoundToInt(size * 0.16f), FontStyle.Bold, TextAnchor.MiddleCenter, SunnyYellow, new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.26f));
            fallback.resizeTextForBestFit = true;
            fallback.resizeTextMinSize = 10;
            fallback.resizeTextMaxSize = Mathf.RoundToInt(size * 0.16f);
            holder.gameObject.AddComponent<EmojiIdleMotion>().Configure(true);
        }

        private RectTransform CreateStickerCard(Transform parent, EmojiId emojiId, UnitCardState state, bool compact, UnityEngine.Events.UnityAction onClick)
        {
            var card = new GameObject($"{emojiId}StickerCard", typeof(RectTransform), typeof(Image), typeof(Button), typeof(EmojiStickerCard), typeof(EmojiMotionController));
            card.transform.SetParent(parent, false);
            var rect = card.GetComponent<RectTransform>();
            rect.sizeDelta = compact ? new Vector2(88f, 104f) : new Vector2(106f, 128f);
            var image = card.GetComponent<Image>();
            image.sprite = cachedSoftSquareSprite;
            image.type = Image.Type.Sliced;

            var aura = CreatePanel("Aura", rect, new Vector2(0.18f, 0.34f), new Vector2(0.82f, 0.90f), new Color(1f, 1f, 1f, 0.20f), cachedCircleSprite);
            aura.SetAsFirstSibling();
            var glyph = CreateText("Glyph", rect, string.Empty, compact ? 58 : 70, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, new Vector2(0.04f, 0.31f), new Vector2(0.96f, 0.93f));
            glyph.gameObject.AddComponent<EmojiIdleMotion>().Configure(true);
            CreateText("Title", rect, string.Empty, compact ? 16 : 18, FontStyle.Bold, TextAnchor.MiddleCenter, SoftWhite, new Vector2(0.06f, 0.12f), new Vector2(0.94f, 0.32f));
            CreateText("Role", rect, string.Empty, compact ? 12 : 13, FontStyle.Bold, TextAnchor.MiddleCenter, AquaMint, new Vector2(0.12f, 0.01f), new Vector2(0.88f, 0.15f));
            CreateText("Badge", rect, string.Empty, compact ? 14 : 16, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, new Vector2(0.62f, 0.76f), new Vector2(0.98f, 0.98f));
            card.GetComponent<EmojiStickerCard>().Bind(emojiId, state, compact, onClick);
            return rect;
        }

        private void CreateTeamCard(Transform parent, EmojiId emojiId, UnitCardState state, UnityEngine.Events.UnityAction onClick)
        {
            var card = CreateStickerCard(parent, emojiId, state, compact: true, onClick);
            var layout = card.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = 84f;
            layout.preferredHeight = 102f;
            layout.minWidth = 80f;
            layout.minHeight = 98f;
        }

        private void CreateMiniSticker(Transform parent, EmojiId emojiId, UnitCardState state, float size, UnityEngine.Events.UnityAction onClick = null)
        {
            var accent = UiThemeRuntime.ResolveRoleAccent(emojiId);
            var itemColor = state.HasFlag(UnitCardState.Banned)
                ? new Color(1f, 0.20f, 0.28f, 0.70f)
                : state.HasFlag(UnitCardState.Selected)
                    ? Color.Lerp(new Color(0.10f, 0.06f, 0.22f, 0.94f), accent, 0.28f)
                    : new Color(0.06f, 0.07f, 0.15f, 0.86f);
            var item = CreatePanel($"{emojiId}MiniSticker", parent as RectTransform, Vector2.zero, Vector2.one, itemColor, cachedSoftSquareSprite);
            var itemHeight = size * 1.18f;
            item.sizeDelta = new Vector2(size, itemHeight);
            var layout = item.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = size;
            layout.preferredHeight = itemHeight;
            layout.minWidth = size;
            layout.minHeight = itemHeight;
            AddGraphicEffects(item.GetComponent<Image>(), state.HasFlag(UnitCardState.Selected) ? accent : new Color(1f, 1f, 1f, 0.34f), new Vector2(0f, -5f));
            var aura = CreatePanel("MiniAura", item, new Vector2(0.20f, 0.34f), new Vector2(0.80f, 0.88f), new Color(accent.r, accent.g, accent.b, 0.34f), cachedCircleSprite);
            aura.SetAsFirstSibling();
            var glyph = CreateText("Glyph", item, string.Empty, Mathf.RoundToInt(size * 0.58f), FontStyle.Normal, TextAnchor.MiddleCenter, Color.white, new Vector2(0.02f, 0.32f), new Vector2(0.98f, 0.96f));
            ConfigureEmojiGlyph(glyph, emojiId, Mathf.RoundToInt(size * 0.58f));
            glyph.gameObject.AddComponent<EmojiIdleMotion>().Configure(size >= 58f);
            var fallback = CreateText("EmojiName", item, EmojiIdUtility.ToDisplayName(emojiId).ToUpperInvariant(), Mathf.RoundToInt(size * 0.15f), FontStyle.Bold, TextAnchor.MiddleCenter, SunnyYellow, new Vector2(0.02f, 0.09f), new Vector2(0.98f, 0.30f));
            fallback.resizeTextForBestFit = true;
            fallback.resizeTextMinSize = 8;
            fallback.resizeTextMaxSize = Mathf.RoundToInt(size * 0.15f);
            CreateText("MiniRole", item, EmojiUiFormatter.BuildRoleTag(emojiId), Mathf.RoundToInt(size * 0.12f), FontStyle.Bold, TextAnchor.MiddleCenter, accent, new Vector2(0.04f, 0.00f), new Vector2(0.96f, 0.12f));

            if (state.HasFlag(UnitCardState.Banned))
            {
                CreateText("BanBadge", item, "X", Mathf.RoundToInt(size * 0.48f), FontStyle.Bold, TextAnchor.MiddleCenter, LavaRed, Vector2.zero, Vector2.one);
            }

            if (onClick != null)
            {
                var button = item.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(onClick);
                button.onClick.AddListener(() => item.gameObject.AddComponent<EmojiMotionController>().JumpSelect());
            }
        }

        private void CreateEmptySlot(Transform parent, float size)
        {
            var slot = CreatePanel("EmptyMiniSlot", parent as RectTransform, Vector2.zero, Vector2.one, new Color(1f, 1f, 1f, 0.08f), cachedCircleSprite);
            var layout = slot.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = size;
            layout.preferredHeight = size;
            layout.minWidth = size;
            layout.minHeight = size;
            CreateText("EmptyPlus", slot, "+", Mathf.RoundToInt(size * 0.42f), FontStyle.Bold, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.38f), Vector2.zero, Vector2.one);
        }

        private RectTransform CreateTeamRow(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var row = CreateRow(name, parent, anchorMin, anchorMax, 7f);
            row.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            return row;
        }

        private RectTransform CreateRow(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, float spacing)
        {
            var rowObject = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            rowObject.transform.SetParent(parent, false);
            var rect = rowObject.GetComponent<RectTransform>();
            Stretch(rect, anchorMin, anchorMax);
            var layout = rowObject.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return rect;
        }

        private void CreatePill(Transform parent, string label, Color accent, bool active)
        {
            var pill = CreatePanel($"{label}Pill", parent as RectTransform, Vector2.zero, Vector2.one, active ? new Color(accent.r, accent.g, accent.b, 0.92f) : new Color(0.02f, 0.02f, 0.08f, 0.58f), cachedSoftSquareSprite);
            var layout = pill.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = Mathf.Clamp(label.Length * 8.5f + 34f, 66f, 150f);
            layout.preferredHeight = 36f;
            AddGraphicEffects(pill.GetComponent<Image>(), active ? accent : new Color(1f, 1f, 1f, 0.20f), new Vector2(0f, -3f));
            CreateText("PillLabel", pill, label, 14, FontStyle.Bold, TextAnchor.MiddleCenter, active ? Ink : SoftWhite, Vector2.zero, Vector2.one);
        }

        private void CreateStatusChip(RectTransform parent, string label, Color accent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var chip = CreatePanel("StatusChip", parent, anchorMin, anchorMax, new Color(accent.r, accent.g, accent.b, 0.92f), cachedSoftSquareSprite);
            AddGraphicEffects(chip.GetComponent<Image>(), accent, new Vector2(0f, -4f));
            CreateText("StatusChipLabel", chip, label, 16, FontStyle.Bold, TextAnchor.MiddleCenter, IsBright(accent) ? Ink : SoftWhite, Vector2.zero, Vector2.one);
        }

        private void CreateNavButton(Transform parent, string label, bool active, UnityEngine.Events.UnityAction onClick)
        {
            var button = CreateSecondaryButton(label, parent as RectTransform, Vector2.zero, Vector2.one, active ? HotPink : ElectricPurple, onClick);
            var rect = button.transform as RectTransform;
            if (rect != null)
            {
                rect.sizeDelta = new Vector2(82f, 46f);
            }

            var layout = button.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = 82f;
            layout.preferredHeight = 46f;
            layout.minWidth = 70f;
            layout.minHeight = 42f;
            var labelText = button.GetComponentInChildren<Text>(true);
            if (labelText != null)
            {
                labelText.fontSize = active ? 17 : 15;
                labelText.resizeTextForBestFit = false;
            }
            if (active)
            {
                button.gameObject.AddComponent<EmojiMotionController>().JumpSelect();
            }
        }

        private Button CreatePrimaryButton(string label, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Color color, UnityEngine.Events.UnityAction onClick, bool breathe)
        {
            var button = CreateButtonBase($"{label}PrimaryCTAButton", parent, anchorMin, anchorMax, color, label, onClick);
            var labelText = button.GetComponentInChildren<Text>(true);
            if (labelText != null)
            {
                labelText.fontSize = 26;
                labelText.color = IsBright(color) ? Ink : Color.white;
                labelText.resizeTextForBestFit = false;
                labelText.verticalOverflow = VerticalWrapMode.Overflow;
            }

            if (onClick == null)
            {
                button.interactable = false;
                button.image.color = new Color(color.r, color.g, color.b, 0.44f);
            }

            if (breathe)
            {
                var idle = button.gameObject.AddComponent<EmojiIdleMotion>();
                idle.Configure(false, true);
            }

            AddButtonPop(button);
            return button;
        }

        private Button CreateSecondaryButton(string label, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Color color, UnityEngine.Events.UnityAction onClick)
        {
            var button = CreateButtonBase($"{label}SecondaryButton", parent, anchorMin, anchorMax, new Color(color.r, color.g, color.b, 0.82f), label, onClick);
            var labelText = button.GetComponentInChildren<Text>(true);
            if (labelText != null)
            {
                labelText.fontSize = 18;
                labelText.color = IsBright(color) ? Ink : Color.white;
                labelText.resizeTextForBestFit = false;
                labelText.verticalOverflow = VerticalWrapMode.Overflow;
            }

            AddButtonPop(button);
            return button;
        }

        private Button CreateButtonBase(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Color color, string label, UnityEngine.Events.UnityAction onClick)
        {
            var rect = CreatePanel(name, parent, anchorMin, anchorMax, color, cachedSoftSquareSprite);
            var button = rect.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }

            AddGraphicEffects(rect.GetComponent<Image>(), new Color(1f, 1f, 1f, 0.50f), new Vector2(0f, -5f));
            var buttonLabel = CreateText("Label", rect, label, 18, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.92f));
            buttonLabel.resizeTextForBestFit = false;
            buttonLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            buttonLabel.verticalOverflow = VerticalWrapMode.Overflow;
            return button;
        }

        private static void AddButtonPop(Button button)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.AddListener(() =>
            {
                var motion = button.GetComponent<EmojiMotionController>();
                if (motion == null)
                {
                    motion = button.gameObject.AddComponent<EmojiMotionController>();
                }
                motion.JumpSelect();
            });
        }

        private RectTransform CreatePanel(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Color color, Sprite sprite = null)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            var rect = panel.GetComponent<RectTransform>();
            Stretch(rect, anchorMin, anchorMax);
            var image = panel.GetComponent<Image>();
            image.color = color;
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
            }

            return rect;
        }

        private Text CreateText(string name, RectTransform parent, string text, int fontSize, FontStyle style, TextAnchor alignment, Color color, Vector2 anchorMin, Vector2 anchorMax)
        {
            var labelObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(parent, false);
            var rect = labelObject.GetComponent<RectTransform>();
            Stretch(rect, anchorMin, anchorMax);
            var label = labelObject.GetComponent<Text>();
            label.font = ResolveFont();
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.alignment = alignment;
            label.color = color;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = Mathf.Max(10, fontSize - 7);
            label.resizeTextMaxSize = fontSize;
            return label;
        }

        private static void ConfigureEmojiGlyph(Text label, EmojiId emojiId, int fontSize)
        {
            if (label == null)
            {
                return;
            }

            label.font = ResolveEmojiFont();
            label.text = EmojiIdUtility.ToEmojiGlyph(emojiId);
            label.fontSize = fontSize;
            label.fontStyle = FontStyle.Normal;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.resizeTextForBestFit = false;

            var outline = GetOrAdd(label.gameObject, () => label.GetComponent<Outline>());
            outline.effectColor = new Color(1f, 1f, 1f, 0.42f);
            outline.effectDistance = new Vector2(3f, -3f);

            var shadow = GetOrAdd(label.gameObject, () => label.GetComponent<Shadow>());
            shadow.effectColor = new Color(0f, 0f, 0f, 0.46f);
            shadow.effectDistance = new Vector2(0f, -5f);
        }

        private void CreateOverlay(string debugName)
        {
            EnsureGeneratedSprites();
            if (screenEnterRoutine != null)
            {
                StopCoroutine(screenEnterRoutine);
                screenEnterRoutine = null;
            }

            var canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(390f, 844f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }

            var canvasRect = canvas.transform as RectTransform;
            var existing = canvasRect != null ? canvasRect.Find(OverlayName) : null;
            if (existing != null)
            {
                Destroy(existing.gameObject);
            }

            var overlay = new GameObject(OverlayName, typeof(RectTransform), typeof(CanvasGroup));
            overlay.transform.SetParent(canvas.transform, false);
            overlay.transform.SetAsLastSibling();
            overlayRoot = overlay.GetComponent<RectTransform>();
            Stretch(overlayRoot, Vector2.zero, Vector2.one);
            overlay.name = OverlayName;
        }

        private void StartScreenEnter()
        {
            if (screenEnterRoutine != null)
            {
                StopCoroutine(screenEnterRoutine);
            }

            screenEnterRoutine = StartCoroutine(PlayScreenEnter(overlayRoot));
        }

        private void ApplyGradient(Color top, Color middle, Color bottom)
        {
            var gradientObject = new GameObject("GradientScreenShell", typeof(RectTransform), typeof(GradientQuadGraphic), typeof(GradientScreenShell));
            gradientObject.transform.SetParent(overlayRoot, false);
            var rect = gradientObject.GetComponent<RectTransform>();
            Stretch(rect, Vector2.zero, Vector2.one);
            gradientObject.GetComponent<GradientScreenShell>().Apply(top, middle, bottom);
            gradientObject.transform.SetAsFirstSibling();
        }

        private void AddAmbientBlobs(string name, Color a, Color b, Color c)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            root.transform.SetParent(overlayRoot, false);
            var rect = root.GetComponent<RectTransform>();
            Stretch(rect, Vector2.zero, Vector2.one);
            CreateBlob(rect, new Vector2(0.10f, 0.80f), 180f, a, 0.26f);
            CreateBlob(rect, new Vector2(0.86f, 0.74f), 160f, b, 0.22f);
            CreateBlob(rect, new Vector2(0.68f, 0.20f), 210f, c, 0.18f);
        }

        private void CreateBlob(RectTransform parent, Vector2 center, float size, Color color, float alpha)
        {
            var blob = CreatePanel("ColorBlob", parent, Vector2.zero, Vector2.zero, new Color(color.r, color.g, color.b, alpha), cachedCircleSprite);
            blob.anchorMin = center;
            blob.anchorMax = center;
            blob.sizeDelta = new Vector2(size, size);
            blob.anchoredPosition = Vector2.zero;
            blob.gameObject.AddComponent<EmojiIdleMotion>().Configure(false);
        }

        private IEnumerator PlayScreenEnter(RectTransform root)
        {
            if (root == null)
            {
                yield break;
            }

            var entries = new List<(RectTransform rect, CanvasGroup group, Vector2 basePos)>();
            for (var index = 0; index < root.childCount; index++)
            {
                var child = root.GetChild(index) as RectTransform;
                if (child == null ||
                    child.GetComponent<GradientQuadGraphic>() != null ||
                    child.name.IndexOf("Blobs", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                var group = child.GetComponent<CanvasGroup>();
                if (group == null)
                {
                    group = child.gameObject.AddComponent<CanvasGroup>();
                }

                if (group == null)
                {
                    continue;
                }

                group.alpha = 1f;
                entries.Add((child, group, child.anchoredPosition));
                child.anchoredPosition -= new Vector2(0f, 20f);
                child.localScale = Vector3.one * 0.96f;
            }

            var elapsed = 0f;
            const float duration = 0.26f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = 1f - Mathf.Pow(1f - t, 3f);
                for (var index = 0; index < entries.Count; index++)
                {
                    var entry = entries[index];
                    if (entry.group == null || entry.rect == null)
                    {
                        continue;
                    }

                    var delay = Mathf.Min(0.10f, index * 0.014f);
                    var localT = Mathf.Clamp01((elapsed - delay) / duration);
                    var localEase = 1f - Mathf.Pow(1f - localT, 3f);
                    entry.group.alpha = 1f;
                    entry.rect.anchoredPosition = Vector2.Lerp(entry.basePos - new Vector2(0f, 20f), entry.basePos, localEase);
                    entry.rect.localScale = Vector3.Lerp(Vector3.one * 0.96f, Vector3.one, eased);
                }

                yield return null;
            }

            foreach (var entry in entries)
            {
                if (entry.rect == null)
                {
                    continue;
                }

                if (entry.group != null)
                {
                    entry.group.alpha = 1f;
                }
                entry.rect.anchoredPosition = entry.basePos;
                entry.rect.localScale = Vector3.one;
            }
        }

        private IReadOnlyList<EmojiId> ResolveCurrentSquad()
        {
            var pending = LaunchSelections.GetPendingSquad();
            if (pending != null && pending.Count == RequiredSquadCount)
            {
                return pending;
            }

            var stored = LoadStoredSquad();
            if (stored.Count == RequiredSquadCount)
            {
                return stored;
            }

            var bootstrap = AppBootstrap.Instance;
            if (bootstrap != null)
            {
                bootstrap.ActiveDeckService.EnsureInitialized(bootstrap.SessionState.UserId);
                if (bootstrap.ActiveDeckService.ActiveDeckEmojiIds.Count == RequiredSquadCount)
                {
                    return bootstrap.ActiveDeckService.ActiveDeckEmojiIds;
                }
            }

            return DemoDefaultSquad;
        }

        private IReadOnlyList<EmojiId> LoadStoredSquad()
        {
            var encoded = PlayerPrefs.GetString(StoredSquadKey, string.Empty);
            if (string.IsNullOrWhiteSpace(encoded))
            {
                return Array.Empty<EmojiId>();
            }

            return EmojiIdUtility.ParseApiIds(encoded.Split(','));
        }

        private void StoreSquad(IReadOnlyList<EmojiId> squad)
        {
            if (squad == null || squad.Count == 0)
            {
                return;
            }

            PlayerPrefs.SetString(StoredSquadKey, string.Join(",", EmojiIdUtility.ToApiIds(squad)));
            PlayerPrefs.Save();
        }

        private static void MarkFlowActive()
        {
            PlayerPrefs.SetInt(FlowActiveKey, 1);
            PlayerPrefs.Save();
        }

        private static void ClearFlowActive()
        {
            PlayerPrefs.DeleteKey(FlowActiveKey);
            PlayerPrefs.Save();
        }

        private void SaveActiveDeck(IReadOnlyList<EmojiId> squad)
        {
            var bootstrap = AppBootstrap.Instance;
            if (bootstrap == null)
            {
                return;
            }

            bootstrap.ActiveDeckService.EnsureInitialized(bootstrap.SessionState.UserId);
            bootstrap.ActiveDeckService.TrySaveActiveDeck(squad, out _);
        }

        private IReadOnlyList<EmojiId> GetFormationTeam()
        {
            return matchSquad
                .Where(emojiId => emojiId != opponentBan)
                .Take(FormationSlotCount)
                .ToArray();
        }

        private bool IsPlaced(EmojiId emojiId)
        {
            for (var index = 0; index < formationSlots.Length; index++)
            {
                if (formationSlotFilled[index] && formationSlots[index] == emojiId)
                {
                    return true;
                }
            }

            return false;
        }

        private bool AllFormationSlotsFilled()
        {
            return formationSlotFilled.All(filled => filled);
        }

        private int CountFilledSlots()
        {
            return formationSlotFilled.Count(filled => filled);
        }

        private void ResetMatchState()
        {
            selectedEnemyBan = null;
            banLocked = false;
            opponentBan = matchSquad.Count > 0 ? matchSquad.Last() : EmojiId.Wind;
            ResetFormationState();
        }

        private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void AddGraphicEffects(Graphic graphic, Color outlineColor, Vector2 shadowDistance)
        {
            if (graphic == null)
            {
                return;
            }

            var outline = GetOrAdd(graphic.gameObject, () => graphic.GetComponent<Outline>());
            outline.effectColor = outlineColor;
            outline.effectDistance = new Vector2(2.5f, 2.5f);
            var shadow = GetOrAdd(graphic.gameObject, () => graphic.GetComponent<Shadow>());
            shadow.effectColor = new Color(0f, 0f, 0f, 0.36f);
            shadow.effectDistance = shadowDistance;
        }

        private static T GetOrAdd<T>(GameObject target, Func<T> getter) where T : Component
        {
            if (target == null)
            {
                return null;
            }

            var component = getter != null ? getter() : target.GetComponent<T>();
            if (component == null)
            {
                component = target.AddComponent<T>();
            }

            return component;
        }

        private static bool IsBright(Color color)
        {
            return color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f > 0.62f;
        }

        private static Font ResolveFont()
        {
            if (cachedFont != null)
            {
                return cachedFont;
            }

            cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (cachedFont == null)
            {
                cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            return cachedFont;
        }

        private static Font ResolveEmojiFont()
        {
            if (cachedEmojiFont != null)
            {
                return cachedEmojiFont;
            }

            var candidates = new[]
            {
                "Segoe UI Emoji",
                "Segoe UI Symbol",
                "Noto Color Emoji",
                "Apple Color Emoji"
            };

            foreach (var candidate in candidates)
            {
                try
                {
                    cachedEmojiFont = Font.CreateDynamicFontFromOSFont(candidate, 128);
                }
                catch (Exception)
                {
                    cachedEmojiFont = null;
                }

                if (cachedEmojiFont != null)
                {
                    return cachedEmojiFont;
                }
            }

            return ResolveFont();
        }

        private static void EnsureGeneratedSprites()
        {
            if (cachedCircleSprite == null)
            {
                cachedCircleSprite = BuildCircleSprite();
            }

            if (cachedSoftSquareSprite == null)
            {
                cachedSoftSquareSprite = BuildSoftSquareSprite();
            }
        }

        private static Sprite BuildCircleSprite()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "StickerPopCircle"
            };
            var center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            var radius = size * 0.46f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var distance = Vector2.Distance(new Vector2(x, y), center);
                    var alpha = Mathf.Clamp01((radius - distance) / 2f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite BuildSoftSquareSprite()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "StickerPopSoftSquare"
            };
            const float radius = 6f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = Mathf.Max(Mathf.Max(radius - x, 0f), x - (size - 1 - radius));
                    var dy = Mathf.Max(Mathf.Max(radius - y, 0f), y - (size - 1 - radius));
                    var distance = Mathf.Sqrt(dx * dx + dy * dy);
                    var alpha = Mathf.Clamp01((radius - distance) / 2f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(8f, 8f, 8f, 8f));
            sprite.name = "StickerPopSoftSquare";
            return sprite;
        }
    }
}
