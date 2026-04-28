using System;
using System.Collections.Generic;
using System.Linq;
using EmojiWar.Client.Content;
using EmojiWar.Client.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EmojiWar.Client.UI.Match
{
    /// <summary>
    /// Result-only rescue screen for the sticker-pop match flow.
    /// The layout is fixed, built once, and refreshed in place so the result state
    /// stays stable if the server snapshot is refreshed more than once.
    /// </summary>
    public sealed class ResultRescueScreen : MonoBehaviour
    {
        private static readonly string[] SlotShortLabels = { "FL", "FC", "FR", "BL", "BR" };
        private static readonly Vector2 HeroTileSize = EmojiWarVisualStyle.Layout.ResultHeroPortrait;
        private static readonly Vector2 BoardTileSize = new(68f, 82f);

        private readonly List<RectTransform> enterTargets = new();

        private RectTransform root;
        private RectTransform contentRoot;
        private RectTransform heroIconRow;
        private RectTransform banBandRoot;
        private RectTransform momentsGridRoot;
        private RectTransform yourBoardRoot;
        private RectTransform opponentBoardRoot;

        private TMP_Text headerTitleLabel;
        private TMP_Text stepChipLabel;
        private TMP_Text modeChipLabel;
        private TMP_Text outcomeLabel;
        private TMP_Text outcomeSublineLabel;
        private TMP_Text outcomeSupportLabel;
        private TMP_Text momentsTitleLabel;
        private TMP_Text replayHintLabel;
        private TMP_Text yourTeamTitleLabel;
        private TMP_Text opponentTeamTitleLabel;
        private TMP_Text primaryButtonLabel;
        private TMP_Text replayButtonLabel;

        private Button primaryButton;
        private Button editSquadButton;
        private Button homeButton;
        private Button replayButton;

        private bool introPlayed;
        private bool primaryPulseActive;
        private bool showingReplayDetails;
        private string boundResultKey = string.Empty;

        private ResultModel currentResult;
        private Action onPlayAgain;
        private Action onEditSquad;
        private Action onHome;
        private Action onReplay;

        public sealed class ResultModel
        {
            public string ResultKey = string.Empty;
            public string StepText = "STEP 4 OF 4";
            public string HeaderTitle = "Battle Result";
            public string ModeChipText = "Ranked";
            public string OutcomeTitle = "VICTORY";
            public bool IsVictory;
            public bool IsDefeat;
            public bool IsDraw;
            public string OutcomeSummary = string.Empty;
            public string WhyTitle = "Battle Recap";
            public string WhySummary = string.Empty;
            public UnitView[] HeroUnits = Array.Empty<UnitView>();
            public bool ShowBanRecap = true;
            public string YourBanId = string.Empty;
            public string OpponentBanId = string.Empty;
            public string YourBanFallbackText = string.Empty;
            public string OpponentBanFallbackText = string.Empty;
            public MomentView[] RecapHighlights = Array.Empty<MomentView>();
            public MomentView[] Moments = Array.Empty<MomentView>();
            public TeamView YourTeam = new();
            public TeamView OpponentTeam = new();
        }

        public sealed class TeamView
        {
            public string Title = string.Empty;
            public string StatusText = string.Empty;
            public UnitView[] BoardUnits = Array.Empty<UnitView>();
        }

        public sealed class MomentView
        {
            public string Caption = string.Empty;
            public string ActorId = string.Empty;
            public string TargetId = string.Empty;
        }

        public readonly struct UnitView
        {
            public UnitView(
                string id,
                string name,
                string role,
                Color cardColor,
                Color auraColor,
                string slotLabel,
                bool isWinner,
                bool isAliveKnown,
                bool isAlive)
            {
                Id = id ?? string.Empty;
                Name = string.IsNullOrWhiteSpace(name) ? "Unit" : name;
                Role = string.IsNullOrWhiteSpace(role) ? "UNIT" : role;
                CardColor = cardColor;
                AuraColor = auraColor;
                SlotLabel = slotLabel ?? string.Empty;
                IsWinner = isWinner;
                IsAliveKnown = isAliveKnown;
                IsAlive = isAlive;
            }

            public string Id { get; }
            public string Name { get; }
            public string Role { get; }
            public Color CardColor { get; }
            public Color AuraColor { get; }
            public string SlotLabel { get; }
            public bool IsWinner { get; }
            public bool IsAliveKnown { get; }
            public bool IsAlive { get; }

            public static UnitView FromApiId(
                string apiId,
                bool enemyTone,
                string slotLabel = "",
                bool isWinner = false,
                bool isAliveKnown = false,
                bool isAlive = false)
            {
                var key = NormalizeOptionalKey(apiId);
                var displayName = ToDisplayName(apiId, key);
                var role = ToRole(apiId);
                var primary = UnitIconLibrary.GetPrimaryColor(key);
                var secondary = UnitIconLibrary.GetSecondaryColor(key);
                var cardColor = enemyTone
                    ? Color.Lerp(primary, RescueStickerFactory.Palette.Coral, 0.34f)
                    : Color.Lerp(primary, RescueStickerFactory.Palette.InkPurple, 0.22f);
                var aura = Color.Lerp(primary, secondary, 0.24f);
                return new UnitView(key, displayName, role, cardColor, aura, slotLabel, isWinner, isAliveKnown, isAlive);
            }
        }

        public void Bind(
            ResultModel result,
            Action playAgain,
            Action editSquad,
            Action home,
            Action replay)
        {
            currentResult = result ?? new ResultModel();
            onPlayAgain = playAgain;
            onEditSquad = editSquad;
            onHome = home;
            onReplay = replay;

            var newKey = string.IsNullOrWhiteSpace(currentResult.ResultKey)
                ? $"{currentResult.OutcomeTitle}|{currentResult.YourBanId}|{currentResult.OpponentBanId}"
                : currentResult.ResultKey;

            if (!string.Equals(boundResultKey, newKey, StringComparison.Ordinal))
            {
                showingReplayDetails = false;
                introPlayed = false;
                boundResultKey = newKey;
            }

            BuildOnce();
            RefreshHeader();
            RefreshHeroBanner();
            RefreshBanBand();
            RefreshMomentsPanel();
            RefreshTeamBoards();
            RefreshActions();
            PlayIntroOnce();
        }

        public void Hide()
        {
            enterTargets.Clear();
            introPlayed = false;
            primaryPulseActive = false;
            showingReplayDetails = false;
            boundResultKey = string.Empty;
            currentResult = null;
            onPlayAgain = null;
            onEditSquad = null;
            onHome = null;
            onReplay = null;

            if (root != null)
            {
                Destroy(root.gameObject);
            }

            root = null;
            contentRoot = null;
            heroIconRow = null;
            banBandRoot = null;
            momentsGridRoot = null;
            yourBoardRoot = null;
            opponentBoardRoot = null;
            headerTitleLabel = null;
            stepChipLabel = null;
            modeChipLabel = null;
            outcomeLabel = null;
            outcomeSublineLabel = null;
            outcomeSupportLabel = null;
            momentsTitleLabel = null;
            replayHintLabel = null;
            yourTeamTitleLabel = null;
            opponentTeamTitleLabel = null;
            primaryButtonLabel = null;
            replayButtonLabel = null;
            primaryButton = null;
            editSquadButton = null;
            homeButton = null;
            replayButton = null;
        }

        private void OnDisable()
        {
            Hide();
        }

        private bool HasReplayMoments =>
            currentResult?.Moments != null &&
            currentResult.Moments.Any(moment => !string.IsNullOrWhiteSpace(moment?.Caption));

        private bool HasRecapHighlights =>
            currentResult?.RecapHighlights != null &&
            currentResult.RecapHighlights.Any(moment => !string.IsNullOrWhiteSpace(moment?.Caption));

        private void BuildOnce()
        {
            if (root != null)
            {
                return;
            }

            root = RescueStickerFactory.CreateScreenRoot(transform, "ResultRescueRoot").GetComponent<RectTransform>();
            root.SetAsLastSibling();

            var topColor = currentResult != null && currentResult.IsDefeat
                ? Color.Lerp(RescueStickerFactory.Palette.Coral, RescueStickerFactory.Palette.BlueViolet, 0.42f)
                : Color.Lerp(RescueStickerFactory.Palette.BlueViolet, RescueStickerFactory.Palette.Sky, 0.20f);
            RescueStickerFactory.CreateGradientLikeBackground(
                root,
                "ResultStickerPopGradient",
                topColor,
                EmojiWarVisualStyle.Colors.BgBottom);

            contentRoot = CreateRect("ResultContent", root);
            Stretch(contentRoot);

            CreateHeader();
            CreateHeroBanner();
            CreateBanBand();
            CreateMomentsPanel();
            CreateTeamPanel();
            CreateActionsPanel();
        }

        private void CreateHeader()
        {
            var header = CreateRect("ResultHeader", contentRoot);
            SetAnchors(header, new Vector2(0.06f, 0.89f), new Vector2(0.94f, 0.975f));
            AddEnterTarget(header);

            var stepChip = RescueStickerFactory.CreateStatusChip(
                header,
                "STEP 4 OF 4",
                RescueStickerFactory.Palette.SunnyYellow,
                RescueStickerFactory.Palette.InkPurple);
            SetAnchors(stepChip.GetComponent<RectTransform>(), new Vector2(0f, 0.54f), new Vector2(0.30f, 1f));
            stepChipLabel = stepChip.GetComponentInChildren<TMP_Text>(true);

            var modeChip = RescueStickerFactory.CreateStatusChip(
                header,
                "Ranked",
                RescueStickerFactory.Palette.Mint,
                RescueStickerFactory.Palette.InkPurple);
            SetAnchors(modeChip.GetComponent<RectTransform>(), new Vector2(0.68f, 0.54f), new Vector2(1f, 1f));
            modeChipLabel = modeChip.GetComponentInChildren<TMP_Text>(true);

            headerTitleLabel = RescueStickerFactory.CreateLabel(
                header,
                "HeaderTitle",
                "Battle Result",
                30f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SoftWhite,
                TextAlignmentOptions.Left,
                new Vector2(0f, 0.02f),
                new Vector2(0.85f, 0.50f));
        }

        private void CreateHeroBanner()
        {
            var banner = RescueStickerFactory.CreateGlassPanel(
                contentRoot,
                "ResultHeroBanner",
                Vector2.zero,
                strong: true);
            var bannerRect = banner.GetComponent<RectTransform>();
            SetAnchors(bannerRect, new Vector2(0.04f, 0.54f), new Vector2(0.96f, 0.89f));
            AddEnterTarget(bannerRect);

            RescueStickerFactory.CreateBlob(
                bannerRect,
                "HeroBlobLeft",
                ResolveOutcomeAccent(),
                new Vector2(-158f, 18f),
                new Vector2(232f, 232f),
                0.16f);
            RescueStickerFactory.CreateBlob(
                bannerRect,
                "HeroBlobRight",
                currentResult != null && currentResult.IsDefeat
                    ? RescueStickerFactory.Palette.HotPink
                    : RescueStickerFactory.Palette.SunnyYellow,
                new Vector2(162f, 26f),
                new Vector2(200f, 200f),
                0.14f);
            RescueStickerFactory.CreateBlob(
                bannerRect,
                "HeroBlobCenter",
                currentResult != null && currentResult.IsDefeat
                    ? RescueStickerFactory.Palette.Coral
                    : RescueStickerFactory.Palette.Mint,
                new Vector2(0f, -10f),
                new Vector2(300f, 216f),
                0.11f);

            var heroModeChip = RescueStickerFactory.CreateStatusChip(
                bannerRect,
                "Match Complete",
                new Color(0.10f, 0.12f, 0.30f, 0.72f),
                ResolveOutcomeAccent());
            SetAnchors(heroModeChip.GetComponent<RectTransform>(), new Vector2(0.36f, 0.86f), new Vector2(0.64f, 0.96f));

            var payoffChip = RescueStickerFactory.CreateStatusChip(
                bannerRect,
                "Final Swing",
                new Color(0.10f, 0.12f, 0.30f, 0.64f),
                currentResult != null && currentResult.IsDefeat
                    ? RescueStickerFactory.Palette.Coral
                    : RescueStickerFactory.Palette.SunnyYellow);
            SetAnchors(payoffChip.GetComponent<RectTransform>(), new Vector2(0.39f, 0.75f), new Vector2(0.61f, 0.84f));

            outcomeLabel = RescueStickerFactory.CreateLabel(
                bannerRect,
                "OutcomeLabel",
                "VICTORY",
                74f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SoftWhite,
                TextAlignmentOptions.Center,
                new Vector2(0.04f, 0.57f),
                new Vector2(0.96f, 0.83f));

            var summaryCard = RescueStickerFactory.CreateGlassPanel(bannerRect, "OutcomeSummaryCard", Vector2.zero);
            SetAnchors(summaryCard.GetComponent<RectTransform>(), new Vector2(0.24f, 0.41f), new Vector2(0.76f, 0.56f));

            outcomeSupportLabel = RescueStickerFactory.CreateLabel(
                summaryCard.transform,
                "OutcomeSupport",
                "FINAL RESULT",
                14f,
                FontStyles.Bold,
                ResolveOutcomeAccent(),
                TextAlignmentOptions.Center,
                new Vector2(0.10f, 0.62f),
                new Vector2(0.90f, 0.92f));

            outcomeSublineLabel = RescueStickerFactory.CreateLabel(
                summaryCard.transform,
                "OutcomeSubline",
                "Battle recap available below.",
                21f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SoftWhite,
                TextAlignmentOptions.Center,
                new Vector2(0.08f, 0.14f),
                new Vector2(0.92f, 0.66f));
            outcomeSublineLabel.textWrappingMode = TextWrappingModes.Normal;

            heroIconRow = CreateRect("HeroIconRow", bannerRect);
            SetAnchors(heroIconRow, new Vector2(0.06f, 0.02f), new Vector2(0.94f, 0.42f));
        }

        private void CreateBanBand()
        {
            banBandRoot = CreateRect("ResultBanBand", contentRoot);
            SetAnchors(banBandRoot, new Vector2(0.045f, 0.51f), new Vector2(0.955f, 0.58f));
            AddEnterTarget(banBandRoot);

            CreateBanCard("YourBanCard", "You banned", new Vector2(0f, 0f), new Vector2(0.49f, 1f));
            CreateBanCard("OpponentBanCard", "Opponent banned", new Vector2(0.51f, 0f), new Vector2(1f, 1f));
        }

        private void CreateBanCard(string name, string label, Vector2 min, Vector2 max)
        {
            var card = RescueStickerFactory.CreateGlassPanel(
                banBandRoot,
                name,
                Vector2.zero);
            var rect = card.GetComponent<RectTransform>();
            SetAnchors(rect, min, max);

            var iconRoot = CreateRect("IconRoot", rect);
            iconRoot.anchorMin = new Vector2(0.13f, 0.50f);
            iconRoot.anchorMax = new Vector2(0.13f, 0.50f);
            iconRoot.sizeDelta = new Vector2(48f, 48f);
            iconRoot.anchoredPosition = Vector2.zero;

            var labelText = RescueStickerFactory.CreateLabel(
                rect,
                "ResultLabel",
                $"{label}: Pending",
                16f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SoftWhite,
                TextAlignmentOptions.Left,
                new Vector2(0.24f, 0.16f),
                new Vector2(0.92f, 0.84f));
            labelText.textWrappingMode = TextWrappingModes.NoWrap;

            var stamp = RescueStickerFactory.CreateStatusChip(
                rect,
                "BAN",
                RescueStickerFactory.Palette.Coral,
                RescueStickerFactory.Palette.SoftWhite);
            stamp.name = "BanStamp";
            SetAnchors(stamp.GetComponent<RectTransform>(), new Vector2(0.76f, 0.56f), new Vector2(0.98f, 0.92f));
        }

        private void CreateMomentsPanel()
        {
            var panel = RescueStickerFactory.CreateGlassPanel(
                contentRoot,
                "ResultMomentsPanel",
                Vector2.zero,
                strong: true);
            var panelRect = panel.GetComponent<RectTransform>();
            SetAnchors(panelRect, new Vector2(0.045f, 0.285f), new Vector2(0.955f, 0.47f));
            AddEnterTarget(panelRect);

            momentsTitleLabel = RescueStickerFactory.CreateLabel(
                panelRect,
                "MomentsTitle",
                "Decisive Moments",
                20f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SoftWhite,
                TextAlignmentOptions.Left,
                new Vector2(0.05f, 0.82f),
                new Vector2(0.60f, 0.97f));

            replayHintLabel = RescueStickerFactory.CreateLabel(
                panelRect,
                "ReplayHint",
                "Tap Replay for detail view",
                12f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.Mint,
                TextAlignmentOptions.Right,
                new Vector2(0.42f, 0.84f),
                new Vector2(0.95f, 0.97f));

            momentsGridRoot = CreateRect("MomentsGrid", panelRect);
            SetAnchors(momentsGridRoot, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.78f));
        }

        private void CreateTeamPanel()
        {
            var panel = RescueStickerFactory.CreateGlassPanel(
                contentRoot,
                "ResultTeamsPanel",
                Vector2.zero,
                strong: true);
            var panelRect = panel.GetComponent<RectTransform>();
            SetAnchors(panelRect, new Vector2(0.045f, 0.10f), new Vector2(0.955f, 0.26f));
            AddEnterTarget(panelRect);

            var yourCard = RescueStickerFactory.CreateGlassPanel(
                panelRect,
                "YourTeamCard",
                Vector2.zero);
            SetAnchors(yourCard.GetComponent<RectTransform>(), new Vector2(0.02f, 0.52f), new Vector2(0.98f, 0.98f));
            yourTeamTitleLabel = RescueStickerFactory.CreateLabel(
                yourCard.transform,
                "YourTeamTitle",
                "Your Team",
                17f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SoftWhite,
                TextAlignmentOptions.Left,
                new Vector2(0.04f, 0.80f),
                new Vector2(0.60f, 0.98f));
            yourBoardRoot = CreateRect("YourBoardRoot", yourCard.transform);
            SetAnchors(yourBoardRoot, new Vector2(0.03f, 0.08f), new Vector2(0.97f, 0.74f));

            var opponentCard = RescueStickerFactory.CreateGlassPanel(
                panelRect,
                "OpponentTeamCard",
                Vector2.zero);
            SetAnchors(opponentCard.GetComponent<RectTransform>(), new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.48f));
            opponentTeamTitleLabel = RescueStickerFactory.CreateLabel(
                opponentCard.transform,
                "OpponentTeamTitle",
                "Opponent Team",
                17f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SoftWhite,
                TextAlignmentOptions.Left,
                new Vector2(0.04f, 0.80f),
                new Vector2(0.60f, 0.98f));
            opponentBoardRoot = CreateRect("OpponentBoardRoot", opponentCard.transform);
            SetAnchors(opponentBoardRoot, new Vector2(0.03f, 0.08f), new Vector2(0.97f, 0.74f));
        }

        private void CreateActionsPanel()
        {
            var panel = CreateRect("ResultActionsPanel", contentRoot);
            SetAnchors(panel, new Vector2(0.045f, 0.015f), new Vector2(0.955f, 0.105f));
            AddEnterTarget(panel);

            primaryButton = RescueStickerFactory.CreatePrimaryGoldButton(
                panel,
                "Play Again",
                new Vector2(0f, 0f));
            SetAnchors(primaryButton.transform as RectTransform, new Vector2(0f, 0f), new Vector2(0.48f, 1f));
            primaryButtonLabel = primaryButton.GetComponentInChildren<TMP_Text>(true);
            primaryButton.onClick.RemoveAllListeners();
            primaryButton.onClick.AddListener(HandlePlayAgainPressed);

            var secondaryRow = CreateRect("SecondaryActions", panel);
            SetAnchors(secondaryRow, new Vector2(0.60f, 0f), new Vector2(1f, 1f));

            editSquadButton = RescueStickerFactory.CreateSecondaryActionButton(
                secondaryRow,
                "Edit Squad",
                new Vector2(0f, 0f));
            SetAnchors(editSquadButton.transform as RectTransform, new Vector2(0f, 0f), new Vector2(0.31f, 1f));
            editSquadButton.onClick.RemoveAllListeners();
            editSquadButton.onClick.AddListener(HandleEditSquadPressed);

            homeButton = RescueStickerFactory.CreateSecondaryActionButton(
                secondaryRow,
                "Home",
                new Vector2(0f, 0f));
            SetAnchors(homeButton.transform as RectTransform, new Vector2(0.345f, 0f), new Vector2(0.655f, 1f));
            homeButton.onClick.RemoveAllListeners();
            homeButton.onClick.AddListener(HandleHomePressed);

            replayButton = RescueStickerFactory.CreateSecondaryActionButton(
                secondaryRow,
                "Replay",
                new Vector2(0f, 0f));
            SetAnchors(replayButton.transform as RectTransform, new Vector2(0.69f, 0f), new Vector2(1f, 1f));
            replayButtonLabel = replayButton.GetComponentInChildren<TMP_Text>(true);
            replayButton.onClick.RemoveAllListeners();
            replayButton.onClick.AddListener(HandleReplayPressed);
        }

        private void RefreshHeader()
        {
            if (stepChipLabel != null)
            {
                stepChipLabel.text = string.IsNullOrWhiteSpace(currentResult?.StepText)
                    ? "STEP 4 OF 4"
                    : currentResult.StepText;
            }

            if (headerTitleLabel != null)
            {
                headerTitleLabel.text = string.IsNullOrWhiteSpace(currentResult?.HeaderTitle)
                    ? "Battle Result"
                    : currentResult.HeaderTitle;
            }

            if (modeChipLabel != null)
            {
                modeChipLabel.text = string.IsNullOrWhiteSpace(currentResult?.ModeChipText)
                    ? "Match"
                    : currentResult.ModeChipText;
            }
        }

        private void RefreshHeroBanner()
        {
            if (outcomeLabel != null)
            {
                outcomeLabel.text = string.IsNullOrWhiteSpace(currentResult?.OutcomeTitle)
                    ? "RESULT"
                    : currentResult.OutcomeTitle;
                outcomeLabel.color = ResolveOutcomeAccent();
            }

            if (outcomeSublineLabel != null)
            {
                var summary = string.IsNullOrWhiteSpace(currentResult?.OutcomeSummary)
                    ? "Battle recap available below."
                    : currentResult.OutcomeSummary;
                outcomeSublineLabel.text = summary;
            }

            if (outcomeSupportLabel != null)
            {
                outcomeSupportLabel.color = ResolveOutcomeAccent();
            }

            RebuildHeroUnits();
        }

        private void RefreshBanBand()
        {
            if (banBandRoot == null)
            {
                return;
            }

            var showBanRecap = currentResult == null || currentResult.ShowBanRecap;
            banBandRoot.gameObject.SetActive(showBanRecap);
            if (!showBanRecap)
            {
                return;
            }

            RefreshBanCard(
                "YourBanCard",
                "You banned",
                currentResult?.YourBanId,
                currentResult?.YourBanFallbackText,
                enemyTone: true);
            RefreshBanCard(
                "OpponentBanCard",
                "Opponent banned",
                currentResult?.OpponentBanId,
                currentResult?.OpponentBanFallbackText,
                enemyTone: false);
        }

        private void RefreshBanCard(string cardName, string prefix, string unitId, string fallbackText, bool enemyTone)
        {
            var card = banBandRoot != null ? banBandRoot.Find(cardName) as RectTransform : null;
            if (card == null)
            {
                return;
            }

            var label = card.Find("ResultLabel")?.GetComponent<TMP_Text>();
            var iconRoot = card.Find("IconRoot") as RectTransform;
            var stamp = card.Find("BanStamp");
            var hasActualBan = !string.IsNullOrWhiteSpace(unitId);

            if (label != null)
            {
                label.text = $"{prefix}: {BuildBanLabel(unitId, fallbackText)}";
            }

            ClearChildren(iconRoot);
            if (stamp != null)
            {
                stamp.gameObject.SetActive(hasActualBan);
            }

            if (iconRoot == null || !hasActualBan)
            {
                return;
            }

            var unit = UnitView.FromApiId(unitId, enemyTone);
            var avatar = RescueStickerFactory.CreateEmojiAvatar(iconRoot, unit.Id, unit.Name, unit.AuraColor, new Vector2(42f, 42f));
            var rect = avatar.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            NativeMotionKit.StampSlam(this, rect, 1.14f, 0.18f);
        }

        private void RefreshMomentsPanel()
        {
            if (momentsTitleLabel != null)
            {
                momentsTitleLabel.text = showingReplayDetails
                    ? "Replay Details"
                    : HasRecapHighlights
                        ? "Battle Recap"
                        : "Decisive Moments";
            }

            if (replayHintLabel != null)
            {
                replayHintLabel.text = HasReplayMoments
                    ? showingReplayDetails
                        ? "Tap Replay to collapse"
                        : HasRecapHighlights
                            ? "Battle recap first, Replay for event detail"
                            : "Tap Replay for detail view"
                    : "Battle recap available";
                replayHintLabel.color = HasReplayMoments
                    ? RescueStickerFactory.Palette.Mint
                    : RescueStickerFactory.Palette.SoftWhite;
            }

            ClearChildren(momentsGridRoot);

            var moments = BuildDisplayedMoments(showingReplayDetails);

            if (moments.Length == 0)
            {
                CreateFallbackMomentCard();
                return;
            }

            for (var index = 0; index < moments.Length; index++)
            {
                CreateMomentCard(moments[index], index, showingReplayDetails);
            }
        }

        private MomentView[] BuildDisplayedMoments(bool detailMode)
        {
            IEnumerable<MomentView> source = detailMode
                ? currentResult?.Moments
                : ComposeOverviewMoments();

            return source?
                .Where(moment => moment != null && !string.IsNullOrWhiteSpace(moment.Caption))
                .Take(4)
                .ToArray() ?? Array.Empty<MomentView>();
        }

        private IEnumerable<MomentView> ComposeOverviewMoments()
        {
            var combined = new List<MomentView>();
            var seenCaptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void Append(IEnumerable<MomentView> moments)
            {
                if (moments == null)
                {
                    return;
                }

                foreach (var moment in moments)
                {
                    if (moment == null || string.IsNullOrWhiteSpace(moment.Caption))
                    {
                        continue;
                    }

                    var caption = moment.Caption.Trim();
                    if (!seenCaptions.Add(caption))
                    {
                        continue;
                    }

                    combined.Add(new MomentView
                    {
                        Caption = caption,
                        ActorId = moment.ActorId ?? string.Empty,
                        TargetId = moment.TargetId ?? string.Empty
                    });
                }
            }

            Append(currentResult?.RecapHighlights);
            Append(currentResult?.Moments);
            return combined;
        }

        private void RefreshTeamBoards()
        {
            if (yourTeamTitleLabel != null)
            {
                yourTeamTitleLabel.text = ComposeTeamTitle(currentResult?.YourTeam);
            }

            if (opponentTeamTitleLabel != null)
            {
                opponentTeamTitleLabel.text = ComposeTeamTitle(currentResult?.OpponentTeam);
            }

            RebuildBoardRow(yourBoardRoot, currentResult?.YourTeam?.BoardUnits, enemyTone: false);
            RebuildBoardRow(opponentBoardRoot, currentResult?.OpponentTeam?.BoardUnits, enemyTone: true);
        }

        private void RefreshActions()
        {
            if (primaryButton != null)
            {
                primaryButton.interactable = onPlayAgain != null;
                var image = primaryButton.GetComponent<Image>();
                if (image != null)
                {
                    image.color = Color.Lerp(EmojiWarVisualStyle.Colors.GoldLight, EmojiWarVisualStyle.Colors.GoldDark, currentResult != null && currentResult.IsDefeat ? 0.32f : 0.24f);
                }
            }

            if (primaryButtonLabel != null)
            {
                primaryButtonLabel.text = "Play Again";
            }

            if (editSquadButton != null)
            {
                editSquadButton.interactable = onEditSquad != null;
            }

            if (homeButton != null)
            {
                homeButton.interactable = onHome != null;
            }

            if (replayButton != null)
            {
                replayButton.interactable = HasReplayMoments;
                var image = replayButton.GetComponent<Image>();
                if (image != null)
                {
                    image.color = replayButton.interactable
                        ? Color.Lerp(EmojiWarVisualStyle.Colors.SecondaryAction, EmojiWarVisualStyle.Colors.SecondaryActionDark, 0.50f)
                        : new Color(0.36f, 0.32f, 0.50f, 0.82f);
                }
            }

            if (replayButtonLabel != null)
            {
                replayButtonLabel.text = HasReplayMoments
                    ? showingReplayDetails ? "Hide Replay" : "Replay"
                    : "Replay Soon";
            }

            var primaryRect = primaryButton != null ? primaryButton.transform as RectTransform : null;
            if (primaryRect != null && !primaryPulseActive)
            {
                NativeMotionKit.BreatheScale(this, primaryRect, 0.018f, 1.10f, true);
                primaryPulseActive = true;
            }
        }

        private void RebuildHeroUnits()
        {
            ClearChildren(heroIconRow);

            var heroUnits = currentResult?.HeroUnits ?? Array.Empty<UnitView>();
            var displayed = heroUnits.Where(unit => !string.IsNullOrWhiteSpace(unit.Id)).Take(3).ToArray();
            if (displayed.Length == 0)
            {
                return;
            }

            var positions = displayed.Length switch
            {
                1 => new[] { new Vector2(0.50f, 0.48f) },
                2 => new[] { new Vector2(0.34f, 0.43f), new Vector2(0.66f, 0.43f) },
                _ => new[] { new Vector2(0.50f, 0.49f), new Vector2(0.24f, 0.40f), new Vector2(0.76f, 0.40f) }
            };

            for (var index = 0; index < displayed.Length; index++)
            {
                var unit = displayed[index];
                var isCenter = displayed.Length == 1 || index == 0;
                var tileSize = isCenter
                    ? new Vector2(HeroTileSize.x + 44f, HeroTileSize.y + 48f)
                    : new Vector2(HeroTileSize.x + 8f, HeroTileSize.y + 10f);
                var slot = CreateRect($"HeroPortraitSlot{index}", heroIconRow);
                slot.sizeDelta = tileSize;
                AddFixedLayout(slot.gameObject, tileSize.x, tileSize.y);
                slot.anchorMin = positions[index];
                slot.anchorMax = positions[index];
                slot.anchoredPosition = Vector2.zero;
                slot.localRotation = Quaternion.Euler(0f, 0f, isCenter ? 0f : index == 1 ? -7f : 7f);

                var fighter = RescueStickerFactory.CreateResultHeroFighter(
                    slot,
                    unit.Id,
                    unit.Name,
                    unit.AuraColor,
                    tileSize);
                var avatar = fighter.GetComponent<RectTransform>();
                avatar.anchorMin = new Vector2(0.5f, isCenter ? 0.61f : 0.58f);
                avatar.anchorMax = new Vector2(0.5f, isCenter ? 0.61f : 0.58f);
                avatar.anchoredPosition = Vector2.zero;

                RescueStickerFactory.CreateLabel(
                    slot,
                    "HeroUnitName",
                    unit.Name,
                    isCenter ? 18f : 15f,
                    FontStyles.Bold,
                    RescueStickerFactory.Palette.SoftWhite,
                    TextAlignmentOptions.Center,
                    new Vector2(0.04f, 0.01f),
                    new Vector2(0.96f, isCenter ? 0.12f : 0.11f));

                if (avatar != null)
                {
                    NativeMotionKit.PopIn(this, avatar, avatar.GetComponent<CanvasGroup>(), 0.22f + index * 0.04f, 0.80f);
                    if (currentResult.IsVictory)
                    {
                        NativeMotionKit.IdleBob(this, avatar, 6f, 1.28f + index * 0.08f, true);
                    }
                    else
                    {
                        NativeMotionKit.BreatheScale(this, avatar, 0.020f, 1.26f + index * 0.06f, true);
                    }
                }

                AddStateBadge(slot, unit);
            }
        }

        private void RebuildBoardRow(RectTransform parent, IReadOnlyList<UnitView> units, bool enemyTone)
        {
            ClearChildren(parent);
            if (parent == null)
            {
                return;
            }

            var row = CreateRect("BoardRow", parent);
            Stretch(row);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            for (var index = 0; index < 5; index++)
            {
                var unit = units != null && index < units.Count
                    ? units[index]
                    : default;

                if (string.IsNullOrWhiteSpace(unit.Id))
                {
                    var empty = RescueStickerFactory.CreateArenaSurface(
                        row,
                        $"EmptySlot{index}",
                        new Color(1f, 1f, 1f, 0.10f),
                        RescueStickerFactory.Palette.SoftWhite,
                        BoardTileSize);
                    AddFixedLayout(empty, BoardTileSize.x, BoardTileSize.y);
                    var emptyRect = empty.GetComponent<RectTransform>();
                    RescueStickerFactory.CreateLabel(
                        emptyRect,
                        "EmptyLabel",
                        SlotShortLabels[index],
                        11f,
                        FontStyles.Bold,
                        RescueStickerFactory.Palette.SoftWhite,
                        TextAlignmentOptions.Center,
                        new Vector2(0.10f, 0.34f),
                        new Vector2(0.90f, 0.66f));
                    continue;
                }

                var tile = RescueStickerFactory.CreateCompactUnitStickerTile(
                    row,
                    unit.Name,
                    unit.Id,
                    unit.Role,
                    unit.CardColor,
                    unit.AuraColor,
                    unit.IsWinner,
                    false,
                    BoardTileSize,
                    0);
                AddFixedLayout(tile, BoardTileSize.x, BoardTileSize.y);

                AddSlotBadge(tile.transform as RectTransform, string.IsNullOrWhiteSpace(unit.SlotLabel) ? SlotShortLabels[index] : unit.SlotLabel);
                AddStateBadge(tile.transform as RectTransform, unit);

                var avatar = tile.transform.Find("EmojiAvatar") as RectTransform;
                if (avatar != null)
                {
                    NativeMotionKit.IdleBob(this, avatar, 3.5f, 1.35f + index * 0.06f, true);
                    if (unit.IsWinner)
                    {
                        NativeMotionKit.BreatheScale(this, avatar, 0.018f, 1.16f, true);
                    }
                }
            }
        }

        private void CreateFallbackMomentCard()
        {
            var card = RescueStickerFactory.CreateGlassPanel(
                momentsGridRoot,
                "FallbackMomentCard",
                Vector2.zero,
                strong: true);
            SetAnchors(card.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 1f));

            var title = string.IsNullOrWhiteSpace(currentResult?.WhyTitle)
                ? "Battle Recap"
                : currentResult.WhyTitle;
            var body = string.IsNullOrWhiteSpace(currentResult?.WhySummary)
                ? "Battle recap unavailable. Replay details coming soon."
                : currentResult.WhySummary;

            RescueStickerFactory.CreateLabel(
                card.transform,
                "FallbackTitle",
                title,
                18f,
                FontStyles.Bold,
                ResolveOutcomeAccent(),
                TextAlignmentOptions.Left,
                new Vector2(0.05f, 0.68f),
                new Vector2(0.95f, 0.92f));

            RescueStickerFactory.CreateLabel(
                card.transform,
                "FallbackBody",
                body,
                15f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SoftWhite,
                TextAlignmentOptions.Left,
                new Vector2(0.05f, 0.14f),
                new Vector2(0.95f, 0.66f));
        }

        private void CreateMomentCard(MomentView moment, int index, bool detailMode)
        {
            var isTopRow = index < 2;
            var column = index % 2;
            var min = detailMode
                ? new Vector2(0f, 0.76f - index * 0.24f)
                : new Vector2(column == 0 ? 0f : 0.51f, isTopRow ? 0.52f : 0f);
            var max = detailMode
                ? new Vector2(1f, 0.96f - index * 0.24f)
                : new Vector2(column == 0 ? 0.49f : 1f, isTopRow ? 1f : 0.48f);

            var card = RescueStickerFactory.CreateGlassPanel(
                momentsGridRoot,
                $"MomentCard{index}",
                Vector2.zero);
            var rect = card.GetComponent<RectTransform>();
            SetAnchors(rect, min, max);

            var actorRoot = CreateRect("ActorRoot", rect);
            actorRoot.anchorMin = new Vector2(0.12f, 0.52f);
            actorRoot.anchorMax = new Vector2(0.12f, 0.52f);
            actorRoot.sizeDelta = new Vector2(38f, 38f);

            CreateMomentIcon(actorRoot, moment.ActorId, enemyTone: false, size: new Vector2(38f, 38f));

            if (!string.IsNullOrWhiteSpace(moment.TargetId))
            {
                var targetRoot = CreateRect("TargetRoot", rect);
                targetRoot.anchorMin = new Vector2(0.30f, 0.52f);
                targetRoot.anchorMax = new Vector2(0.30f, 0.52f);
                targetRoot.sizeDelta = new Vector2(30f, 30f);
                CreateMomentIcon(targetRoot, moment.TargetId, enemyTone: true, size: new Vector2(30f, 30f));
            }

            var numberChip = RescueStickerFactory.CreateStatusChip(
                rect,
                (index + 1).ToString(),
                ResolveOutcomeAccent(),
                RescueStickerFactory.Palette.InkPurple);
            SetAnchors(numberChip.GetComponent<RectTransform>(), new Vector2(0.80f, 0.58f), new Vector2(0.98f, 0.92f));

            RescueStickerFactory.CreateLabel(
                rect,
                "MomentCaption",
                moment.Caption,
                detailMode ? 15f : 14f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SoftWhite,
                TextAlignmentOptions.Left,
                new Vector2(0.39f, 0.14f),
                new Vector2(0.94f, 0.86f));
        }

        private void CreateMomentIcon(RectTransform parent, string unitId, bool enemyTone, Vector2? size = null)
        {
            if (parent == null || string.IsNullOrWhiteSpace(unitId))
            {
                return;
            }

            var unit = UnitView.FromApiId(unitId, enemyTone);
            var avatar = RescueStickerFactory.CreateEmojiAvatar(
                parent,
                unit.Id,
                unit.Name,
                unit.AuraColor,
                size ?? new Vector2(32f, 32f));
            var rect = avatar.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
        }

        private void AddSlotBadge(RectTransform rect, string label)
        {
            if (rect == null || string.IsNullOrWhiteSpace(label))
            {
                return;
            }

            var badge = RescueStickerFactory.CreateStatusChip(
                rect,
                label,
                new Color(0.22f, 0.18f, 0.49f, 0.96f),
                RescueStickerFactory.Palette.SoftWhite);
            badge.name = "SlotBadge";
            SetAnchors(badge.GetComponent<RectTransform>(), new Vector2(0.06f, 0.80f), new Vector2(0.40f, 0.98f));
        }

        private void AddStateBadge(RectTransform rect, UnitView unit)
        {
            if (rect == null)
            {
                return;
            }

            var stateText = unit.IsAliveKnown
                ? unit.IsAlive ? "ALIVE" : "OUT"
                : unit.IsWinner ? "WIN" : string.Empty;
            if (string.IsNullOrWhiteSpace(stateText))
            {
                return;
            }

            var bodyColor = unit.IsAliveKnown
                ? unit.IsAlive ? RescueStickerFactory.Palette.Mint : RescueStickerFactory.Palette.Coral
                : RescueStickerFactory.Palette.SunnyYellow;
            var badge = RescueStickerFactory.CreateStatusChip(
                rect,
                stateText,
                bodyColor,
                RescueStickerFactory.Palette.InkPurple);
            badge.name = "StateBadge";
            SetAnchors(badge.GetComponent<RectTransform>(), new Vector2(0.56f, 0.80f), new Vector2(0.96f, 0.98f));
        }

        private void HandlePlayAgainPressed()
        {
            if (primaryButton != null)
            {
                NativeMotionKit.PunchScale(this, primaryButton.transform as RectTransform, 0.08f, 0.15f);
            }

            onPlayAgain?.Invoke();
        }

        private void HandleEditSquadPressed()
        {
            if (editSquadButton != null)
            {
                NativeMotionKit.PunchScale(this, editSquadButton.transform as RectTransform, 0.08f, 0.15f);
            }

            onEditSquad?.Invoke();
        }

        private void HandleHomePressed()
        {
            if (homeButton != null)
            {
                NativeMotionKit.PunchScale(this, homeButton.transform as RectTransform, 0.08f, 0.15f);
            }

            onHome?.Invoke();
        }

        private void HandleReplayPressed()
        {
            if (!HasReplayMoments)
            {
                return;
            }

            showingReplayDetails = !showingReplayDetails;
            if (replayButton != null)
            {
                NativeMotionKit.PunchScale(this, replayButton.transform as RectTransform, 0.08f, 0.15f);
            }

            onReplay?.Invoke();
            RefreshMomentsPanel();
            RefreshActions();
        }

        private void PlayIntroOnce()
        {
            if (introPlayed)
            {
                return;
            }

            introPlayed = true;
            for (var index = 0; index < enterTargets.Count; index++)
            {
                var target = enterTargets[index];
                if (target == null)
                {
                    continue;
                }

                var group = target.GetComponent<CanvasGroup>() ?? target.gameObject.AddComponent<CanvasGroup>();
                NativeMotionKit.SlideFadeIn(this, target, group, new Vector2(0f, -18f), 0.24f + index * 0.03f);
            }

            if (outcomeLabel != null)
            {
                NativeMotionKit.StampSlam(this, outcomeLabel.rectTransform, 1.12f, 0.22f);
            }
        }

        private Color ResolveOutcomeAccent()
        {
            if (currentResult == null)
            {
                return RescueStickerFactory.Palette.SunnyYellow;
            }

            if (currentResult.IsDefeat)
            {
                return RescueStickerFactory.Palette.Coral;
            }

            if (currentResult.IsDraw)
            {
                return RescueStickerFactory.Palette.SunnyYellow;
            }

            return RescueStickerFactory.Palette.Mint;
        }

        private static string ComposeTeamTitle(TeamView team)
        {
            if (team == null)
            {
                return "Team";
            }

            return string.IsNullOrWhiteSpace(team.StatusText)
                ? team.Title
                : $"{team.Title}  {team.StatusText}";
        }

        private static string BuildBanLabel(string unitId, string fallbackText)
        {
            if (!string.IsNullOrWhiteSpace(unitId))
            {
                var normalized = NormalizeOptionalKey(unitId);
                return ToDisplayName(unitId, normalized);
            }

            return string.IsNullOrWhiteSpace(fallbackText)
                ? "Ban result unavailable"
                : fallbackText;
        }

        private static void ClearChildren(RectTransform parent)
        {
            if (parent == null)
            {
                return;
            }

            for (var index = parent.childCount - 1; index >= 0; index--)
            {
                Destroy(parent.GetChild(index).gameObject);
            }
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            var rect = gameObject.GetComponent<RectTransform>();
            rect.localScale = Vector3.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            SetAnchors(rect, Vector2.zero, Vector2.one);
        }

        private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void AddFixedLayout(GameObject target, float width, float height)
        {
            var layout = target.GetComponent<LayoutElement>() ?? target.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.preferredHeight = height;
            layout.minWidth = width;
            layout.minHeight = height;
        }

        private void AddEnterTarget(RectTransform target)
        {
            if (target != null)
            {
                enterTargets.Add(target);
            }
        }

        private static string NormalizeOptionalKey(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : UnitIconLibrary.NormalizeUnitKey(value);
        }

        private static string ToDisplayName(string apiId, string normalizedKey)
        {
            if (EmojiIdUtility.TryFromApiId(UnitIconLibrary.NormalizeUnitKey(apiId), out var emojiId))
            {
                return EmojiIdUtility.ToDisplayName(emojiId);
            }

            var source = string.IsNullOrWhiteSpace(normalizedKey) ? "unit" : normalizedKey;
            return string.Join(" ", source.Split(new[] { '_', '-' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Length == 0 ? part : char.ToUpperInvariant(part[0]) + part.Substring(1)));
        }

        private static string ToRole(string apiId)
        {
            return EmojiIdUtility.TryFromApiId(UnitIconLibrary.NormalizeUnitKey(apiId), out var emojiId)
                ? EmojiUiFormatter.BuildRoleTag(emojiId)
                : "UNIT";
        }
    }
}
