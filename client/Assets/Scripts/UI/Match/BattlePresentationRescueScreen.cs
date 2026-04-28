using System;
using System.Collections;
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
    /// Short battle presentation bridge shown after formation lock and before result.
    /// Uses real battle summary/event data when available, and falls back to summary-only
    /// presentation without inventing combat details.
    /// </summary>
    public sealed class BattlePresentationRescueScreen : MonoBehaviour
    {
        private static readonly string[] SlotShortLabels = { "FL", "FC", "FR", "BL", "BR" };
        private static readonly Vector2 BoardTileSize = new(64f, 74f);

        private readonly List<RectTransform> enterTargets = new();
        private Coroutine playbackRoutine;

        private RectTransform root;
        private RectTransform contentRoot;
        private RectTransform banBandRoot;
        private RectTransform yourBoardRoot;
        private RectTransform opponentBoardRoot;
        private RectTransform actorIconRoot;
        private RectTransform targetIconRoot;

        private TMP_Text stepChipLabel;
        private TMP_Text modeChipLabel;
        private TMP_Text headerTitleLabel;
        private TMP_Text stageChipLabel;
        private TMP_Text stageTitleLabel;
        private TMP_Text stageBodyLabel;
        private TMP_Text presentationHintLabel;
        private TMP_Text skipButtonLabel;

        private Button skipButton;

        private PresentationModel currentPresentation;
        private Action onComplete;
        private string boundPresentationKey = string.Empty;

        public sealed class PresentationModel
        {
            public string PresentationKey = string.Empty;
            public string StepText = "STEP 4 OF 4";
            public string ModeChipText = "Ranked PvP";
            public string HeaderTitle = "Battle Presentation";
            public string IntroTitle = "Squads Deploy";
            public string IntroSummary = string.Empty;
            public string FinishTitle = "RESULT";
            public string FinishSummary = string.Empty;
            public bool IsVictory;
            public bool IsDefeat;
            public bool IsDraw;
            public bool UsedEventPlayback;
            public bool ShowBanRecap = true;
            public string YourBanId = string.Empty;
            public string OpponentBanId = string.Empty;
            public string YourBanFallbackText = string.Empty;
            public string OpponentBanFallbackText = string.Empty;
            public ResultRescueScreen.UnitView[] YourBoard = Array.Empty<ResultRescueScreen.UnitView>();
            public ResultRescueScreen.UnitView[] OpponentBoard = Array.Empty<ResultRescueScreen.UnitView>();
            public ResultRescueScreen.MomentView[] Moments = Array.Empty<ResultRescueScreen.MomentView>();
        }

        public void Bind(PresentationModel model, Action complete)
        {
            currentPresentation = model ?? new PresentationModel();
            onComplete = complete;

            var newKey = string.IsNullOrWhiteSpace(currentPresentation.PresentationKey)
                ? $"{currentPresentation.HeaderTitle}|{currentPresentation.FinishTitle}|{currentPresentation.YourBanId}|{currentPresentation.OpponentBanId}"
                : currentPresentation.PresentationKey;

            var keyChanged = !string.Equals(boundPresentationKey, newKey, StringComparison.Ordinal);
            boundPresentationKey = newKey;

            BuildOnce();
            RefreshHeader();
            RefreshBanBand();
            RebuildBoardRow(yourBoardRoot, currentPresentation.YourBoard, enemyTone: false);
            RebuildBoardRow(opponentBoardRoot, currentPresentation.OpponentBoard, enemyTone: true);
            RefreshStage(
                "DEPLOY",
                string.IsNullOrWhiteSpace(currentPresentation.IntroTitle) ? "Squads Deploy" : currentPresentation.IntroTitle,
                string.IsNullOrWhiteSpace(currentPresentation.IntroSummary) ? "Both formations crash into the arena." : currentPresentation.IntroSummary,
                null);

            if (skipButton != null)
            {
                skipButton.gameObject.SetActive(false);
            }

            if (presentationHintLabel != null)
            {
                presentationHintLabel.text = currentPresentation.UsedEventPlayback
                    ? "Using real battle log highlights"
                    : "Using battle summary highlights";
            }

            if (keyChanged)
            {
                PlayIntroOnce();
            }

            BeginPlayback();
        }

        public void Hide()
        {
            if (playbackRoutine != null)
            {
                StopCoroutine(playbackRoutine);
                playbackRoutine = null;
            }

            enterTargets.Clear();
            currentPresentation = null;
            onComplete = null;
            boundPresentationKey = string.Empty;

            if (root != null)
            {
                Destroy(root.gameObject);
            }

            root = null;
            contentRoot = null;
            banBandRoot = null;
            yourBoardRoot = null;
            opponentBoardRoot = null;
            actorIconRoot = null;
            targetIconRoot = null;
            stepChipLabel = null;
            modeChipLabel = null;
            headerTitleLabel = null;
            stageChipLabel = null;
            stageTitleLabel = null;
            stageBodyLabel = null;
            presentationHintLabel = null;
            skipButtonLabel = null;
            skipButton = null;
        }

        private void OnDisable()
        {
            Hide();
        }

        private void BuildOnce()
        {
            if (root != null)
            {
                return;
            }

            root = RescueStickerFactory.CreateScreenRoot(transform, "BattlePresentationRescueRoot").GetComponent<RectTransform>();
            root.SetAsLastSibling();

            var topColor = currentPresentation != null && currentPresentation.IsDefeat
                ? Color.Lerp(RescueStickerFactory.Palette.Coral, RescueStickerFactory.Palette.ElectricPurple, 0.40f)
                : Color.Lerp(RescueStickerFactory.Palette.ElectricPurple, RescueStickerFactory.Palette.Aqua, 0.26f);
            RescueStickerFactory.CreateGradientLikeBackground(
                root,
                "BattlePresentationGradient",
                topColor,
                RescueStickerFactory.Palette.Mint);

            contentRoot = CreateRect("BattlePresentationContent", root);
            Stretch(contentRoot);

            CreateHeader();
            CreateBanBand();
            CreateArenaPanel();
            CreateFooter();
        }

        private void CreateHeader()
        {
            var header = CreateRect("PresentationHeader", contentRoot);
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
                "Ranked PvP",
                RescueStickerFactory.Palette.Mint,
                RescueStickerFactory.Palette.InkPurple);
            SetAnchors(modeChip.GetComponent<RectTransform>(), new Vector2(0.68f, 0.54f), new Vector2(1f, 1f));
            modeChipLabel = modeChip.GetComponentInChildren<TMP_Text>(true);

            headerTitleLabel = RescueStickerFactory.CreateLabel(
                header,
                "HeaderTitle",
                "Battle Presentation",
                30f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SoftWhite,
                TextAlignmentOptions.Left,
                new Vector2(0f, 0.02f),
                new Vector2(0.85f, 0.50f));
        }

        private void CreateBanBand()
        {
            banBandRoot = CreateRect("PresentationBanBand", contentRoot);
            SetAnchors(banBandRoot, new Vector2(0.055f, 0.79f), new Vector2(0.945f, 0.875f));
            AddEnterTarget(banBandRoot);

            CreateBanCard("YourBanCard", "You banned", new Vector2(0f, 0f), new Vector2(0.49f, 1f));
            CreateBanCard("OpponentBanCard", "Opponent banned", new Vector2(0.51f, 0f), new Vector2(1f, 1f));
        }

        private void CreateArenaPanel()
        {
            var panel = RescueStickerFactory.CreateArenaSurface(
                contentRoot,
                "PresentationArenaPanel",
                new Color(1f, 1f, 1f, 0.22f),
                RescueStickerFactory.Palette.Aqua,
                Vector2.zero);
            var panelRect = panel.GetComponent<RectTransform>();
            SetAnchors(panelRect, new Vector2(0.055f, 0.17f), new Vector2(0.945f, 0.76f));
            AddEnterTarget(panelRect);

            RescueStickerFactory.CreateBlob(
                panelRect,
                "ArenaBlobLeft",
                RescueStickerFactory.Palette.HotPink,
                new Vector2(-120f, 40f),
                new Vector2(170f, 170f),
                0.12f);
            RescueStickerFactory.CreateBlob(
                panelRect,
                "ArenaBlobRight",
                RescueStickerFactory.Palette.SunnyYellow,
                new Vector2(140f, -24f),
                new Vector2(140f, 140f),
                0.10f);

            var stageChip = RescueStickerFactory.CreateStatusChip(
                panelRect,
                "DEPLOY",
                RescueStickerFactory.Palette.HotPink,
                RescueStickerFactory.Palette.SoftWhite);
            SetAnchors(stageChip.GetComponent<RectTransform>(), new Vector2(0.04f, 0.88f), new Vector2(0.26f, 0.97f));
            stageChipLabel = stageChip.GetComponentInChildren<TMP_Text>(true);

            presentationHintLabel = RescueStickerFactory.CreateLabel(
                panelRect,
                "PresentationHint",
                "Using battle highlights",
                12f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.Mint,
                TextAlignmentOptions.Right,
                new Vector2(0.46f, 0.89f),
                new Vector2(0.95f, 0.97f));

            stageTitleLabel = RescueStickerFactory.CreateLabel(
                panelRect,
                "StageTitle",
                "Squads Deploy",
                34f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SoftWhite,
                TextAlignmentOptions.Center,
                new Vector2(0.06f, 0.72f),
                new Vector2(0.94f, 0.86f));

            stageBodyLabel = RescueStickerFactory.CreateLabel(
                panelRect,
                "StageBody",
                "Both formations crash into the arena.",
                16f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SoftWhite,
                TextAlignmentOptions.Center,
                new Vector2(0.10f, 0.61f),
                new Vector2(0.90f, 0.72f));
            stageBodyLabel.textWrappingMode = TextWrappingModes.Normal;

            actorIconRoot = CreateRect("ActorIconRoot", panelRect);
            actorIconRoot.anchorMin = new Vector2(0.34f, 0.51f);
            actorIconRoot.anchorMax = new Vector2(0.34f, 0.51f);
            actorIconRoot.sizeDelta = new Vector2(54f, 54f);

            targetIconRoot = CreateRect("TargetIconRoot", panelRect);
            targetIconRoot.anchorMin = new Vector2(0.66f, 0.51f);
            targetIconRoot.anchorMax = new Vector2(0.66f, 0.51f);
            targetIconRoot.sizeDelta = new Vector2(54f, 54f);

            var versusLabel = RescueStickerFactory.CreateLabel(
                panelRect,
                "VersusLabel",
                "VS",
                28f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SunnyYellow,
                TextAlignmentOptions.Center,
                new Vector2(0.46f, 0.46f),
                new Vector2(0.54f, 0.56f));
            versusLabel.textWrappingMode = TextWrappingModes.NoWrap;

            var yourCard = RescueStickerFactory.CreateArenaSurface(
                panelRect,
                "YourBoardCard",
                new Color(0.21f, 0.24f, 0.52f, 0.72f),
                RescueStickerFactory.Palette.SoftWhite,
                Vector2.zero);
            SetAnchors(yourCard.GetComponent<RectTransform>(), new Vector2(0.03f, 0.07f), new Vector2(0.97f, 0.32f));
            RescueStickerFactory.CreateLabel(
                yourCard.transform,
                "YourBoardTitle",
                "Your Formation",
                17f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SoftWhite,
                TextAlignmentOptions.Left,
                new Vector2(0.04f, 0.79f),
                new Vector2(0.48f, 0.97f));
            yourBoardRoot = CreateRect("YourBoardRoot", yourCard.transform);
            SetAnchors(yourBoardRoot, new Vector2(0.03f, 0.10f), new Vector2(0.97f, 0.74f));

            var opponentCard = RescueStickerFactory.CreateArenaSurface(
                panelRect,
                "OpponentBoardCard",
                new Color(0.23f, 0.18f, 0.47f, 0.72f),
                RescueStickerFactory.Palette.SoftWhite,
                Vector2.zero);
            SetAnchors(opponentCard.GetComponent<RectTransform>(), new Vector2(0.03f, 0.33f), new Vector2(0.97f, 0.58f));
            RescueStickerFactory.CreateLabel(
                opponentCard.transform,
                "OpponentBoardTitle",
                "Rival Formation",
                17f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SoftWhite,
                TextAlignmentOptions.Left,
                new Vector2(0.04f, 0.79f),
                new Vector2(0.48f, 0.97f));
            opponentBoardRoot = CreateRect("OpponentBoardRoot", opponentCard.transform);
            SetAnchors(opponentBoardRoot, new Vector2(0.03f, 0.10f), new Vector2(0.97f, 0.74f));
        }

        private void CreateFooter()
        {
            var footer = CreateRect("PresentationFooter", contentRoot);
            SetAnchors(footer, new Vector2(0.055f, 0.04f), new Vector2(0.945f, 0.125f));
            AddEnterTarget(footer);

            skipButton = RescueStickerFactory.CreateToyButton(
                footer,
                "Skip to Result",
                new Color(0.48f, 0.42f, 0.72f, 0.96f),
                RescueStickerFactory.Palette.SoftWhite,
                new Vector2(420f, 74f),
                primary: false);
            SetAnchors(skipButton.transform as RectTransform, new Vector2(0.21f, 0f), new Vector2(0.79f, 1f));
            skipButtonLabel = skipButton.GetComponentInChildren<TMP_Text>(true);
            skipButton.onClick.RemoveAllListeners();
            skipButton.onClick.AddListener(HandleSkipPressed);
        }

        private void RefreshHeader()
        {
            if (stepChipLabel != null)
            {
                stepChipLabel.text = string.IsNullOrWhiteSpace(currentPresentation?.StepText)
                    ? "STEP 4 OF 4"
                    : currentPresentation.StepText;
            }

            if (modeChipLabel != null)
            {
                modeChipLabel.text = string.IsNullOrWhiteSpace(currentPresentation?.ModeChipText)
                    ? "Ranked PvP"
                    : currentPresentation.ModeChipText;
            }

            if (headerTitleLabel != null)
            {
                headerTitleLabel.text = string.IsNullOrWhiteSpace(currentPresentation?.HeaderTitle)
                    ? "Battle Presentation"
                    : currentPresentation.HeaderTitle;
            }
        }

        private void RefreshBanBand()
        {
            if (banBandRoot == null)
            {
                return;
            }

            var showBanRecap = currentPresentation == null || currentPresentation.ShowBanRecap;
            banBandRoot.gameObject.SetActive(showBanRecap);
            if (!showBanRecap)
            {
                return;
            }

            RefreshBanCard(
                "YourBanCard",
                "You banned",
                currentPresentation?.YourBanId,
                currentPresentation?.YourBanFallbackText,
                enemyTone: true);
            RefreshBanCard(
                "OpponentBanCard",
                "Opponent banned",
                currentPresentation?.OpponentBanId,
                currentPresentation?.OpponentBanFallbackText,
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

            var unit = ResultRescueScreen.UnitView.FromApiId(unitId, enemyTone);
            var avatar = RescueStickerFactory.CreateEmojiAvatar(iconRoot, unit.Id, unit.Name, unit.AuraColor, new Vector2(42f, 42f));
            var rect = avatar.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            NativeMotionKit.StampSlam(this, rect, 1.14f, 0.18f);
        }

        private void RefreshStage(string stageChip, string title, string body, ResultRescueScreen.MomentView moment)
        {
            if (stageChipLabel != null)
            {
                stageChipLabel.text = string.IsNullOrWhiteSpace(stageChip) ? "BATTLE" : stageChip;
                stageChipLabel.color = ResolveOutcomeAccentText();
                var chipImage = stageChipLabel.transform.parent.GetComponent<Image>();
                if (chipImage != null)
                {
                    chipImage.color = ResolveOutcomeAccentBody();
                }
            }

            if (stageTitleLabel != null)
            {
                stageTitleLabel.text = string.IsNullOrWhiteSpace(title) ? "Battle Presentation" : title;
                stageTitleLabel.color = ResolveOutcomeAccentText();
                NativeMotionKit.PunchScale(this, stageTitleLabel.transform as RectTransform, 0.08f, 0.16f);
            }

            if (stageBodyLabel != null)
            {
                stageBodyLabel.text = string.IsNullOrWhiteSpace(body) ? "Battle recap available." : body;
            }

            RefreshMomentIcons(moment);
        }

        private void RefreshMomentIcons(ResultRescueScreen.MomentView moment)
        {
            ClearChildren(actorIconRoot);
            ClearChildren(targetIconRoot);

            var actorId = moment != null ? moment.ActorId : string.Empty;
            var targetId = moment != null ? moment.TargetId : string.Empty;

            if (!string.IsNullOrWhiteSpace(actorId))
            {
                var actor = ResultRescueScreen.UnitView.FromApiId(actorId, false);
                var avatar = RescueStickerFactory.CreateEmojiAvatar(actorIconRoot, actor.Id, actor.Name, actor.AuraColor, new Vector2(54f, 54f));
                var rect = avatar.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                NativeMotionKit.PopIn(this, rect, null, 0.18f, 0.84f);
                NativeMotionKit.PunchScale(this, rect, 0.10f, 0.18f);
            }

            if (!string.IsNullOrWhiteSpace(targetId))
            {
                var target = ResultRescueScreen.UnitView.FromApiId(targetId, true);
                var avatar = RescueStickerFactory.CreateEmojiAvatar(targetIconRoot, target.Id, target.Name, target.AuraColor, new Vector2(54f, 54f));
                var rect = avatar.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                NativeMotionKit.PopIn(this, rect, null, 0.18f, 0.84f);
                NativeMotionKit.Shake(this, rect, 10f, 0.18f);
            }
        }

        private void BeginPlayback()
        {
            if (playbackRoutine != null)
            {
                StopCoroutine(playbackRoutine);
            }

            playbackRoutine = StartCoroutine(PlaybackRoutine());
        }

        private IEnumerator PlaybackRoutine()
        {
            NativeMotionKit.StaggerChildrenPopIn(this, opponentBoardRoot, 0.035f);
            NativeMotionKit.StaggerChildrenPopIn(this, yourBoardRoot, 0.035f);
            yield return new WaitForSecondsRealtime(0.85f);

            var moments = currentPresentation?.Moments?
                .Where(moment => moment != null && !string.IsNullOrWhiteSpace(moment.Caption))
                .Take(3)
                .ToArray() ?? Array.Empty<ResultRescueScreen.MomentView>();

            for (var index = 0; index < moments.Length; index++)
            {
                if (index == 0 && skipButton != null)
                {
                    skipButton.gameObject.SetActive(true);
                    NativeMotionKit.PopIn(this, skipButton.transform as RectTransform, null, 0.18f, 0.92f);
                }

                var moment = moments[index];
                RefreshStage(
                    $"CLASH {index + 1}",
                    $"Impact Beat {index + 1}",
                    moment.Caption,
                    moment);
                yield return new WaitForSecondsRealtime(1.05f);
            }

            RefreshStage(
                "FINISH",
                string.IsNullOrWhiteSpace(currentPresentation?.FinishTitle) ? "RESULT" : currentPresentation.FinishTitle,
                string.IsNullOrWhiteSpace(currentPresentation?.FinishSummary) ? "Battle recap available below." : currentPresentation.FinishSummary,
                null);
            yield return new WaitForSecondsRealtime(0.95f);
            CompletePresentation();
        }

        private void CompletePresentation()
        {
            if (playbackRoutine != null)
            {
                StopCoroutine(playbackRoutine);
                playbackRoutine = null;
            }

            onComplete?.Invoke();
        }

        private void HandleSkipPressed()
        {
            CompletePresentation();
        }

        private void PlayIntroOnce()
        {
            for (var index = 0; index < enterTargets.Count; index++)
            {
                var target = enterTargets[index];
                if (target == null)
                {
                    continue;
                }

                var group = target.GetComponent<CanvasGroup>() ?? target.gameObject.AddComponent<CanvasGroup>();
                NativeMotionKit.SlideFadeIn(this, target, group, new Vector2(0f, -18f), 0.24f + index * 0.025f);
            }
        }

        private void RebuildBoardRow(RectTransform parent, IReadOnlyList<ResultRescueScreen.UnitView> units, bool enemyTone)
        {
            ClearChildren(parent);
            if (parent == null)
            {
                return;
            }

            var row = CreateRect("BoardRow", parent);
            Stretch(row);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            for (var index = 0; index < 5; index++)
            {
                var unit = units != null && index < units.Count ? units[index] : default;
                if (string.IsNullOrWhiteSpace(unit.Id))
                {
                    var empty = RescueStickerFactory.CreateArenaSurface(
                        row,
                        $"EmptySlot{index}",
                        new Color(1f, 1f, 1f, 0.10f),
                        RescueStickerFactory.Palette.SoftWhite,
                        BoardTileSize);
                    AddFixedLayout(empty, BoardTileSize.x, BoardTileSize.y);
                    RescueStickerFactory.CreateLabel(
                        empty.transform,
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
                    NativeMotionKit.IdleBob(this, avatar, 3f, 1.25f + index * 0.04f, true);
                    if (unit.IsWinner)
                    {
                        NativeMotionKit.BreatheScale(this, avatar, 0.018f, 1.12f, true);
                    }
                    else if (unit.IsAliveKnown && !unit.IsAlive && enemyTone)
                    {
                        NativeMotionKit.BreatheScale(this, avatar, 0.008f, 1.16f, true);
                    }
                }
            }
        }

        private void CreateBanCard(string name, string label, Vector2 min, Vector2 max)
        {
            var card = RescueStickerFactory.CreateArenaSurface(
                banBandRoot,
                name,
                new Color(0.20f, 0.21f, 0.48f, 0.72f),
                RescueStickerFactory.Palette.SoftWhite,
                Vector2.zero);
            var rect = card.GetComponent<RectTransform>();
            SetAnchors(rect, min, max);

            var iconRoot = CreateRect("IconRoot", rect);
            iconRoot.anchorMin = new Vector2(0.13f, 0.50f);
            iconRoot.anchorMax = new Vector2(0.13f, 0.50f);
            iconRoot.sizeDelta = new Vector2(42f, 42f);
            iconRoot.anchoredPosition = Vector2.zero;

            var labelText = RescueStickerFactory.CreateLabel(
                rect,
                "ResultLabel",
                $"{label}: Pending",
                15f,
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

        private void AddSlotBadge(RectTransform parent, string slotText)
        {
            if (parent == null || string.IsNullOrWhiteSpace(slotText))
            {
                return;
            }

            var badge = RescueStickerFactory.CreateStatusChip(
                parent,
                slotText,
                new Color(0.19f, 0.20f, 0.42f, 0.94f),
                RescueStickerFactory.Palette.SoftWhite);
            SetAnchors(badge.GetComponent<RectTransform>(), new Vector2(0.05f, 0.82f), new Vector2(0.38f, 0.98f));
        }

        private void AddStateBadge(RectTransform parent, ResultRescueScreen.UnitView unit)
        {
            if (parent == null || string.IsNullOrWhiteSpace(unit.Id))
            {
                return;
            }

            if (unit.IsWinner)
            {
                var badge = RescueStickerFactory.CreateStatusChip(
                    parent,
                    "WIN",
                    RescueStickerFactory.Palette.SunnyYellow,
                    RescueStickerFactory.Palette.InkPurple);
                SetAnchors(badge.GetComponent<RectTransform>(), new Vector2(0.62f, 0.82f), new Vector2(0.95f, 0.98f));
                return;
            }

            if (unit.IsAliveKnown && !unit.IsAlive)
            {
                var badge = RescueStickerFactory.CreateStatusChip(
                    parent,
                    "KO",
                    RescueStickerFactory.Palette.Coral,
                    RescueStickerFactory.Palette.SoftWhite);
                SetAnchors(badge.GetComponent<RectTransform>(), new Vector2(0.65f, 0.82f), new Vector2(0.95f, 0.98f));
            }
        }

        private static string BuildBanLabel(string unitId, string fallbackText)
        {
            if (!string.IsNullOrWhiteSpace(unitId))
            {
                var normalized = UnitIconLibrary.NormalizeUnitKey(unitId);
                if (EmojiIdUtility.TryFromApiId(normalized, out var emojiId))
                {
                    return EmojiIdUtility.ToDisplayName(emojiId);
                }

                return normalized;
            }

            return string.IsNullOrWhiteSpace(fallbackText) ? "Pending" : fallbackText;
        }

        private Color ResolveOutcomeAccentBody()
        {
            if (currentPresentation != null && currentPresentation.IsDraw)
            {
                return RescueStickerFactory.Palette.Mint;
            }

            if (currentPresentation != null && currentPresentation.IsDefeat)
            {
                return RescueStickerFactory.Palette.Coral;
            }

            return RescueStickerFactory.Palette.SunnyYellow;
        }

        private Color ResolveOutcomeAccentText()
        {
            return currentPresentation != null && currentPresentation.IsDraw
                ? RescueStickerFactory.Palette.InkPurple
                : currentPresentation != null && currentPresentation.IsDefeat
                    ? RescueStickerFactory.Palette.SoftWhite
                    : RescueStickerFactory.Palette.InkPurple;
        }

        private void AddEnterTarget(RectTransform target)
        {
            if (target != null)
            {
                enterTargets.Add(target);
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

        private static void ClearChildren(RectTransform parent)
        {
            if (parent == null)
            {
                return;
            }

            for (var index = parent.childCount - 1; index >= 0; index--)
            {
                UnityEngine.Object.Destroy(parent.GetChild(index).gameObject);
            }
        }
    }
}
