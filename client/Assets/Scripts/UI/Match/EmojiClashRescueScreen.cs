using System;
using System.Collections.Generic;
using System.Linq;
using EmojiWar.Client.Gameplay.Clash;
using EmojiWar.Client.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EmojiWar.Client.UI.Match
{
    public sealed class EmojiClashRescueScreen : MonoBehaviour
    {
        private readonly List<TMP_Text> turnValueLabels = new();

        private RectTransform root;
        private RectTransform contentRoot;
        private RectTransform turnViewRoot;
        private RectTransform resultViewRoot;
        private RectTransform boardGridRoot;
        private RectTransform benchStickerVisualLayer;
        private RectTransform clashStageRoot;
        private RectTransform clashCinematicLayer;
        private RectTransform dragLayerRoot;
        private RectTransform momentumTrackRoot;
        private RectTransform momentumPlayerFill;
        private RectTransform momentumRivalFill;
        private RectTransform recapListRoot;
        private RectTransform resultTurnsRoot;
        private RectTransform resultHeroDecorRoot;
        private RectTransform resultHeroScoreCardRoot;
        private RectTransform activePlayerCardRoot;
        private RectTransform activeRivalCardRoot;
        private RectTransform activeClashCoreRoot;
        private RectTransform activeSummonPadRoot;
        private RectTransform activeBenchLaneRoot;
        private Coroutine clashSequenceCoroutine;
        private Coroutine deferredTurnBindCoroutine;
        private bool isBenchDragActive;
        private bool isSummonHoverActive;
        private bool activePlayerSlotEmpty;
        private bool activeRivalHidden;
        private float clashSequenceVisibleUntilTime;
        private float suppressNextPlayerEntryDropUntilTime;
        private float pendingSummonUntilTime;
        private float summonTransitionLockUntilTime;
        private EmojiClashTurnViewModel pendingTurnModel;
        private EmojiClashResultViewModel pendingResultModel;
        private Action<string> pendingPickAction;
        private Action pendingHomeAction;
        private Action pendingEditSquadAction;
        private Action pendingPlayAgainAction;
        private RectTransform pendingSummonVisual;
        private string pendingSummonUnitKey = string.Empty;
        private bool pendingSummonWasDrag;
        private readonly List<GameObject> activeClashCinematicObjects = new();
        private readonly Dictionary<CanvasGroup, float> trackedCardFighterAlphas = new();
        private readonly Dictionary<string, Sprite> clashVfxSpriteCache = new(StringComparer.OrdinalIgnoreCase);
        private Material clashVfxAlphaTintMaterial;

        private static readonly Vector2 BattleBenchHitTileSize = new(168f, 122f);
        private static readonly Vector2 BattleBenchVisualSize = new(154f, 110f);
        private static readonly Vector2[] BattleBenchScatterAnchors =
        {
            new(0.10f, 0.88f), new(0.35f, 0.82f), new(0.64f, 0.88f), new(0.90f, 0.80f),
            new(0.22f, 0.62f), new(0.47f, 0.58f), new(0.76f, 0.64f), new(0.94f, 0.54f),
            new(0.07f, 0.46f), new(0.30f, 0.32f), new(0.60f, 0.42f), new(0.80f, 0.28f),
            new(0.15f, 0.14f), new(0.47f, 0.17f), new(0.66f, 0.10f), new(0.93f, 0.12f)
        };
        private static readonly float[] BattleBenchScatterRotations =
        {
            -4.2f, 2.1f, -1.7f, 4.0f,
            3.2f, -3.6f, 2.8f, -2.4f,
            -2.6f, 3.8f, -3.1f, 2.4f,
            2.2f, -2.9f, 3.4f, -3.8f
        };
        private static readonly float[] BattleBenchScatterScales =
        {
            1.02f, 0.98f, 1.03f, 0.99f,
            0.97f, 1.01f, 0.98f, 1.02f,
            1.00f, 0.97f, 1.02f, 0.99f,
            0.98f, 1.03f, 0.97f, 1.01f
        };

        private const float ResolvedClashStandoffSeconds = 0.20f;
        private const float ResolvedClashAdvanceSeconds = 0.35f;
        private const float ResolvedClashCloudBuildSeconds = 0.87f;
        private const float ResolvedClashImpactSeconds = 0.12f;
        private const float ResolvedClashLoserExitSeconds = 0.25f;
        private const float ResolvedClashWinnerHoldSeconds = 1.10f;
        private const float ResolvedClashDrawHoldSeconds = 0.90f;
        private const float ResolvedClashSafetyBufferSeconds = 0.10f;
        private const float ImmediateSummonTravelSeconds = 0.34f;
        private const float ImmediateSummonLandingSeconds = 0.18f;
        private const float SuppressPlayerEntryDropSeconds = 0.90f;
        private const float SummonTransitionLockSeconds = 0.46f;

        private TMP_Text topEyebrowLabel;
        private TMP_Text topTitleLabel;
        private TMP_Text turnStatusLabel;
        private TMP_Text turnValueLabel;
        private TMP_Text scoreLabel;
        private TMP_Text outcomeLabel;
        private TMP_Text reasonLabel;
        private TMP_Text autoAdvanceHintLabel;
        private TMP_Text resultOutcomeLabel;
        private TMP_Text resultScoreLabel;

        private Button turnHomeButton;
        private Button resultPlayAgainButton;
        private Button resultHomeButton;
        private Button resultEditButton;

        private Action<string> onPick;
        private Action onHome;
        private Action onEditSquad;
        private Action onPlayAgain;

        public void BindTurn(
            EmojiClashTurnViewModel model,
            Action<string> pickAction,
            Action homeAction,
            Action editSquadAction)
        {
            if (ShouldDeferTurnBind())
            {
                QueueDeferredTurnBind(model, pickAction, homeAction, editSquadAction);
                return;
            }

            ApplyTurnBind(model, pickAction, homeAction, editSquadAction);
        }

        private void ApplyTurnBind(
            EmojiClashTurnViewModel model,
            Action<string> pickAction,
            Action homeAction,
            Action editSquadAction)
        {
            onPick = pickAction;
            onHome = homeAction;
            onEditSquad = editSquadAction;
            onPlayAgain = null;

            BuildOnce();
            turnViewRoot.gameObject.SetActive(true);
            resultViewRoot.gameObject.SetActive(false);

            RefreshTurnHeader(model);
            RefreshTurnSequence(model);
            RefreshScoreBand(model);
            RefreshClashStage(model);
            RefreshBoard(model);
            RefreshTurnActions(model);
        }

        public void BindResult(
            EmojiClashResultViewModel model,
            Action playAgainAction,
            Action homeAction,
            Action editSquadAction)
        {
            if (ShouldDeferTurnBind())
            {
                QueueDeferredResultBind(model, playAgainAction, homeAction, editSquadAction);
                return;
            }

            CancelDeferredTurnBind();
            ApplyResultBind(model, playAgainAction, homeAction, editSquadAction);
        }

        private void ApplyResultBind(
            EmojiClashResultViewModel model,
            Action playAgainAction,
            Action homeAction,
            Action editSquadAction)
        {
            ClearPendingSummonState(true);
            onPlayAgain = playAgainAction;
            onHome = homeAction;
            onEditSquad = editSquadAction;
            onPick = null;

            BuildOnce();
            turnViewRoot.gameObject.SetActive(false);
            resultViewRoot.gameObject.SetActive(true);

            topEyebrowLabel.text = "QUICK PLAY";
            topTitleLabel.text = "Emoji Clash";
            turnStatusLabel.text = $"Turn {Mathf.Max(1, model?.TurnLines?.Length ?? 5)} / {Mathf.Max(1, model?.TurnLines?.Length ?? 5)}";
            turnValueLabel.text = "FINAL TURN";
            resultOutcomeLabel.text = model?.OutcomeTitle ?? "DRAW";
            resultOutcomeLabel.color = ResolveResultAccent(model?.OutcomeTitle);
            resultScoreLabel.text = FormatResultScore(model?.FinalScoreLine ?? "Final Score: You 0 - 0 Rival");
            RefreshResultHeroDecor(model);
            RebuildResultRecaps(model?.RecapLines ?? Array.Empty<string>());
            RebuildResultTurns(model?.TurnLines ?? Array.Empty<string>());
            WireButton(resultPlayAgainButton, () => onPlayAgain?.Invoke(), 0.05f);
            WireButton(resultHomeButton, () => onHome?.Invoke(), 0.05f);
            WireButton(resultEditButton, () => onEditSquad?.Invoke(), 0.05f);

            NativeMotionKit.PopIn(this, resultViewRoot, resultViewRoot.GetComponent<CanvasGroup>(), 0.24f, 0.92f);
        }

        public void Hide()
        {
            ClearClashCinematicLayer(true);
            ClearPendingSummonState(true);
            CancelDeferredTurnBind();
            if (clashSequenceCoroutine != null)
            {
                StopCoroutine(clashSequenceCoroutine);
                clashSequenceCoroutine = null;
            }

            if (root != null)
            {
                Destroy(root.gameObject);
            }

            if (clashVfxAlphaTintMaterial != null)
            {
                Destroy(clashVfxAlphaTintMaterial);
                clashVfxAlphaTintMaterial = null;
            }

            root = null;
            contentRoot = null;
            turnViewRoot = null;
            resultViewRoot = null;
            boardGridRoot = null;
            benchStickerVisualLayer = null;
            clashStageRoot = null;
            clashCinematicLayer = null;
            momentumTrackRoot = null;
            momentumPlayerFill = null;
            momentumRivalFill = null;
            recapListRoot = null;
            resultTurnsRoot = null;
            activePlayerCardRoot = null;
            activeRivalCardRoot = null;
            activeClashCoreRoot = null;
            activeSummonPadRoot = null;
            activeBenchLaneRoot = null;
            clashSequenceVisibleUntilTime = 0f;
            suppressNextPlayerEntryDropUntilTime = 0f;
            pendingSummonUntilTime = 0f;
            summonTransitionLockUntilTime = 0f;
            pendingSummonVisual = null;
            pendingSummonUnitKey = string.Empty;
            pendingSummonWasDrag = false;
            pendingResultModel = null;
            pendingPlayAgainAction = null;
            topEyebrowLabel = null;
            topTitleLabel = null;
            turnStatusLabel = null;
            turnValueLabel = null;
            scoreLabel = null;
            outcomeLabel = null;
            reasonLabel = null;
            autoAdvanceHintLabel = null;
            resultOutcomeLabel = null;
            resultScoreLabel = null;
            turnHomeButton = null;
            resultPlayAgainButton = null;
            resultHomeButton = null;
            resultEditButton = null;
            isBenchDragActive = false;
            isSummonHoverActive = false;
            activePlayerSlotEmpty = false;
            activeRivalHidden = false;
            turnValueLabels.Clear();
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

            root = RescueStickerFactory.CreateScreenRoot(transform, "EmojiClashRescueRoot").GetComponent<RectTransform>();
            root.SetAsLastSibling();
            RescueStickerFactory.CreateGradientLikeBackground(
                root,
                "EmojiClashGradient",
                EmojiWarVisualStyle.Colors.BgTop,
                EmojiWarVisualStyle.Colors.BgBottom);

            contentRoot = CreateRect("EmojiClashContent", root);
            Stretch(contentRoot);
            dragLayerRoot = CreateRect("DragLayer", root);
            Stretch(dragLayerRoot);
            dragLayerRoot.SetAsLastSibling();

            CreateHeader();
            CreateTurnView();
            CreateResultView();
        }

        private void CreateHeader()
        {
            var header = CreateRect("EmojiClashHeader", contentRoot);
            SetAnchors(header, new Vector2(0.05f, 0.925f), new Vector2(0.95f, 0.982f));

            topEyebrowLabel = RescueStickerFactory.CreateLabel(
                header,
                "Eyebrow",
                "QUICK PLAY",
                12f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SunnyYellow,
                TextAlignmentOptions.Left,
                new Vector2(0f, 0.62f),
                new Vector2(0.42f, 1f));

            topTitleLabel = RescueStickerFactory.CreateLabel(
                header,
                "Title",
                "Emoji Clash",
                30f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SoftWhite,
                TextAlignmentOptions.Left,
                new Vector2(0f, 0.04f),
                new Vector2(0.56f, 0.76f));

            turnStatusLabel = RescueStickerFactory.CreateLabel(
                header,
                "TurnStatus",
                "Turn 1 / 5",
                17f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SoftWhite,
                TextAlignmentOptions.Right,
                new Vector2(0.58f, 0.06f),
                new Vector2(1f, 0.54f));

            turnValueLabel = RescueStickerFactory.CreateLabel(
                header,
                "TurnValue",
                "+1 pt",
                16f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.Mint,
                TextAlignmentOptions.Right,
                new Vector2(0.58f, 0.56f),
                new Vector2(1f, 1f));
        }

        private void CreateTurnView()
        {
            turnViewRoot = CreateRect("TurnView", contentRoot);
            Stretch(turnViewRoot);
            turnViewRoot.gameObject.AddComponent<CanvasGroup>();

            var sequenceBand = RescueStickerFactory.CreateGlassPanel(
                turnViewRoot,
                "TurnValueBand",
                Vector2.zero);
            var sequenceRect = sequenceBand.GetComponent<RectTransform>();
            SetAnchors(sequenceRect, new Vector2(0.05f, 0.874f), new Vector2(0.95f, 0.910f));

            var sequenceRow = CreateRect("TurnValuesRow", sequenceRect);
            SetAnchors(sequenceRow, new Vector2(0.03f, 0.14f), new Vector2(0.97f, 0.86f));
            var sequenceLayout = sequenceRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            sequenceLayout.spacing = 6f;
            sequenceLayout.childAlignment = TextAnchor.MiddleCenter;
            sequenceLayout.childControlWidth = true;
            sequenceLayout.childControlHeight = true;
            sequenceLayout.childForceExpandWidth = true;
            sequenceLayout.childForceExpandHeight = false;

            for (var index = 0; index < 5; index++)
            {
                var chip = RescueStickerFactory.CreateGlassPanel(
                    sequenceRow,
                    $"TurnValueChip{index}",
                    new Vector2(78f, 32f));
                var layout = chip.AddComponent<LayoutElement>();
                layout.preferredWidth = 72f;
                layout.preferredHeight = 28f;
                turnValueLabels.Add(RescueStickerFactory.CreateLabel(
                    chip.transform,
                    "Label",
                    "1",
                    15f,
                    FontStyles.Bold,
                    RescueStickerFactory.Palette.SoftWhite,
                    TextAlignmentOptions.Center,
                    Vector2.zero,
                    Vector2.one));
            }

            var scoreBand = RescueStickerFactory.CreateGlassPanel(
                turnViewRoot,
                "ScoreBand",
                Vector2.zero,
                strong: true);
            var scoreRect = scoreBand.GetComponent<RectTransform>();
            SetAnchors(scoreRect, new Vector2(0.03f, 0.812f), new Vector2(0.97f, 0.870f));

            scoreLabel = RescueStickerFactory.CreateLabel(
                scoreRect,
                "ScoreLabel",
                "You 0 - 0 Rival",
                28f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SoftWhite,
                TextAlignmentOptions.Center,
                new Vector2(0.05f, 0.42f),
                new Vector2(0.95f, 0.96f));

            momentumTrackRoot = CreateRect("MomentumTrack", scoreRect);
            SetAnchors(momentumTrackRoot, new Vector2(0.15f, 0.08f), new Vector2(0.85f, 0.26f));
            var trackSurface = momentumTrackRoot.gameObject.AddComponent<Image>();
            trackSurface.color = new Color(0.10f, 0.07f, 0.20f, 0.82f);

            var centerLine = CreateRect("CenterLine", momentumTrackRoot);
            centerLine.anchorMin = new Vector2(0.5f, 0.04f);
            centerLine.anchorMax = new Vector2(0.5f, 0.96f);
            centerLine.sizeDelta = new Vector2(4f, 0f);
            centerLine.anchoredPosition = Vector2.zero;
            var centerLineImage = centerLine.gameObject.AddComponent<Image>();
            centerLineImage.color = new Color(1f, 1f, 1f, 0.35f);

            momentumPlayerFill = CreateRect("MomentumPlayerFill", momentumTrackRoot);
            var playerFillImage = momentumPlayerFill.gameObject.AddComponent<Image>();
            playerFillImage.color = Color.Lerp(RescueStickerFactory.Palette.Mint, RescueStickerFactory.Palette.Aqua, 0.22f);

            momentumRivalFill = CreateRect("MomentumRivalFill", momentumTrackRoot);
            var rivalFillImage = momentumRivalFill.gameObject.AddComponent<Image>();
            rivalFillImage.color = Color.Lerp(RescueStickerFactory.Palette.Coral, RescueStickerFactory.Palette.HotPink, 0.24f);

            var clashSurface = RescueStickerFactory.CreateGlassPanel(
                turnViewRoot,
                "ClashArenaSurface",
                Vector2.zero,
                strong: true);
            var clashSurfaceRect = clashSurface.GetComponent<RectTransform>();
            SetAnchors(clashSurfaceRect, new Vector2(0.02f, 0.395f), new Vector2(0.98f, 0.800f));

            RescueStickerFactory.CreateBlob(
                clashSurfaceRect,
                "ArenaGlow",
                RescueStickerFactory.Palette.SunnyYellow,
                new Vector2(-86f, -12f),
                new Vector2(420f, 260f),
                0.12f);
            RescueStickerFactory.CreateBlob(
                clashSurfaceRect,
                "ArenaBlueGlow",
                RescueStickerFactory.Palette.Aqua,
                new Vector2(96f, 72f),
                new Vector2(450f, 240f),
                0.10f);
            RescueStickerFactory.CreateBlob(
                clashSurfaceRect,
                "ArenaFloor",
                RescueStickerFactory.Palette.Aqua,
                new Vector2(0f, -182f),
                new Vector2(620f, 166f),
                0.18f);
            RescueStickerFactory.CreateBlob(
                clashSurfaceRect,
                "ArenaCorePulse",
                RescueStickerFactory.Palette.SoftWhite,
                new Vector2(0f, -4f),
                new Vector2(180f, 180f),
                0.05f);

            RescueStickerFactory.CreateLabel(
                clashSurfaceRect,
                "ClashTitle",
                "CLASH ARENA",
                13f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SunnyYellow,
                TextAlignmentOptions.Center,
                new Vector2(0.38f, 0.92f),
                new Vector2(0.62f, 0.98f));

            clashStageRoot = CreateRect("ClashStageRoot", clashSurfaceRect);
            SetAnchors(clashStageRoot, new Vector2(0.02f, 0.04f), new Vector2(0.98f, 0.91f));

            var benchBridge = CreateRect("BenchBridge", turnViewRoot);
            SetAnchors(benchBridge, new Vector2(0.05f, 0.368f), new Vector2(0.95f, 0.444f));
            RescueStickerFactory.CreateBlob(
                benchBridge,
                "BenchBridgeGlow",
                RescueStickerFactory.Palette.Aqua,
                new Vector2(0f, 0f),
                new Vector2(620f, 126f),
                0.14f);
            RescueStickerFactory.CreateBlob(
                benchBridge,
                "BenchBridgeWarm",
                RescueStickerFactory.Palette.SunnyYellow,
                new Vector2(-54f, 10f),
                new Vector2(260f, 82f),
                0.10f);
            RescueStickerFactory.CreateBlob(
                benchBridge,
                "BenchBridgeLane",
                RescueStickerFactory.Palette.Cloud,
                new Vector2(0f, -8f),
                new Vector2(540f, 58f),
                0.06f);

            var boardSurface = CreateRect("BoardSurface", turnViewRoot);
            SetAnchors(boardSurface, new Vector2(0.03f, 0.058f), new Vector2(0.97f, 0.408f));
            var boardSurfaceImage = boardSurface.gameObject.AddComponent<Image>();
            boardSurfaceImage.color = new Color(0.10f, 0.14f, 0.31f, 0.06f);
            var boardSurfaceRect = boardSurface;
            activeBenchLaneRoot = boardSurfaceRect;

            RescueStickerFactory.CreateBlob(
                boardSurfaceRect,
                "BenchTopGlow",
                RescueStickerFactory.Palette.Aqua,
                new Vector2(0f, 170f),
                new Vector2(700f, 176f),
                0.18f);
            RescueStickerFactory.CreateBlob(
                boardSurfaceRect,
                "BenchFloorGlow",
                RescueStickerFactory.Palette.Mint,
                new Vector2(0f, -184f),
                new Vector2(660f, 150f),
                0.14f);
            RescueStickerFactory.CreateBlob(
                boardSurfaceRect,
                "BenchLaneGlow",
                RescueStickerFactory.Palette.Cloud,
                new Vector2(0f, 6f),
                new Vector2(680f, 210f),
                0.05f);
            RescueStickerFactory.CreateBlob(
                boardSurfaceRect,
                "BenchStageSweep",
                RescueStickerFactory.Palette.Aqua,
                new Vector2(0f, -28f),
                new Vector2(600f, 84f),
                0.06f);

            RescueStickerFactory.CreateLabel(
                boardSurfaceRect,
                "BoardTitle",
                "Bench",
                18f,
                FontStyles.Bold,
                new Color(0.93f, 0.96f, 1f, 0.86f),
                TextAlignmentOptions.Left,
                new Vector2(0.05f, 0.90f),
                new Vector2(0.28f, 0.96f));

            RescueStickerFactory.CreateLabel(
                boardSurfaceRect,
                "BoardHint",
                string.Empty,
                9f,
                FontStyles.Bold,
                new Color(0.90f, 0.94f, 1f, 0.0f),
                TextAlignmentOptions.Right,
                new Vector2(0.62f, 0.90f),
                new Vector2(0.95f, 0.96f));

            boardGridRoot = CreateRect("BoardGrid", boardSurfaceRect);
            SetAnchors(boardGridRoot, new Vector2(0.00f, 0.025f), new Vector2(1.00f, 0.895f));

            benchStickerVisualLayer = CreateRect("BenchStickerVisualLayer", boardSurfaceRect);
            SetAnchors(benchStickerVisualLayer, new Vector2(0.00f, 0.025f), new Vector2(1.00f, 0.895f));
            var visualLayerCanvasGroup = benchStickerVisualLayer.gameObject.AddComponent<CanvasGroup>();
            visualLayerCanvasGroup.blocksRaycasts = false;
            visualLayerCanvasGroup.interactable = false;

            var turnActions = CreateRect("TurnActions", turnViewRoot);
            SetAnchors(turnActions, new Vector2(0.05f, 0.012f), new Vector2(0.95f, 0.046f));

            turnHomeButton = RescueStickerFactory.CreateSecondaryActionButton(
                turnActions,
                "Home",
                new Vector2(106f, 34f));
            SetAnchors(turnHomeButton.transform as RectTransform, new Vector2(0f, 0.10f), new Vector2(0.125f, 0.90f));

            var autoAdvanceCard = CreateRect("AutoAdvanceCard", turnActions);
            SetAnchors(autoAdvanceCard, new Vector2(0.18f, 0f), new Vector2(1f, 1f));
            autoAdvanceHintLabel = RescueStickerFactory.CreateLabel(
                autoAdvanceCard,
                "AutoAdvanceLabel",
                "Drag or tap to clash",
                9f,
                FontStyles.Bold,
                new Color(0.93f, 0.95f, 1f, 0.28f),
                TextAlignmentOptions.Right,
                new Vector2(0.32f, 0.14f),
                new Vector2(0.99f, 0.52f));

            outcomeLabel = RescueStickerFactory.CreateLabel(
                turnViewRoot,
                "OutcomeLabel",
                string.Empty,
                24f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SunnyYellow,
                TextAlignmentOptions.Center,
                new Vector2(0.08f, 0.382f),
                new Vector2(0.92f, 0.418f));

            reasonLabel = RescueStickerFactory.CreateLabel(
                turnViewRoot,
                "ReasonLabel",
                string.Empty,
                16f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SoftWhite,
                TextAlignmentOptions.Center,
                new Vector2(0.08f, 0.352f),
                new Vector2(0.92f, 0.388f));
        }

        private void CreateResultView()
        {
            resultViewRoot = CreateRect("ResultView", contentRoot);
            Stretch(resultViewRoot);
            resultViewRoot.gameObject.AddComponent<CanvasGroup>();

            var heroRect = RescueStickerFactory.CreateOpenHeroStage(resultViewRoot, "QuickResultHeroStage");
            SetAnchors(heroRect, new Vector2(0.04f, 0.47f), new Vector2(0.96f, 0.88f));

            resultOutcomeLabel = RescueStickerFactory.CreateLabel(
                heroRect,
                "ResultOutcome",
                "VICTORY",
                82f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SoftWhite,
                TextAlignmentOptions.Center,
                new Vector2(0.06f, 0.48f),
                new Vector2(0.94f, 0.84f));

            resultHeroDecorRoot = CreateRect("ResultHeroDecorRoot", heroRect);
            SetAnchors(resultHeroDecorRoot, new Vector2(0f, 0f), new Vector2(1f, 0.96f));

            var scoreCard = RescueStickerFactory.CreateResultHeroScoreCard(heroRect, "Final Score", "You 0 - 0 Rival");
            resultHeroScoreCardRoot = scoreCard.GetComponent<RectTransform>();
            SetAnchors(resultHeroScoreCardRoot, new Vector2(0.32f, 0.18f), new Vector2(0.68f, 0.44f));
            resultScoreLabel = scoreCard.transform.Find("ScoreLine")?.GetComponent<TMP_Text>();

            var recapSurface = RescueStickerFactory.CreateGlassPanel(
                resultViewRoot,
                "RecapSurface",
                Vector2.zero,
                strong: true);
            var recapRect = recapSurface.GetComponent<RectTransform>();
            SetAnchors(recapRect, new Vector2(0.045f, 0.29f), new Vector2(0.955f, 0.445f));

            RescueStickerFactory.CreateLabel(
                recapRect,
                "RecapTitle",
                "Clash Recap",
                20f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SoftWhite,
                TextAlignmentOptions.Left,
                new Vector2(0.04f, 0.82f),
                new Vector2(0.42f, 0.96f));

            recapListRoot = CreateRect("RecapList", recapRect);
            SetAnchors(recapListRoot, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.78f));
            var recapLayout = recapListRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            recapLayout.spacing = 8f;
            recapLayout.childAlignment = TextAnchor.UpperLeft;
            recapLayout.childControlHeight = true;
            recapLayout.childControlWidth = true;
            recapLayout.childForceExpandHeight = false;
            recapLayout.childForceExpandWidth = true;

            var turnsSurface = RescueStickerFactory.CreateGlassPanel(
                resultViewRoot,
                "TurnsSurface",
                Vector2.zero,
                strong: true);
            var turnsRect = turnsSurface.GetComponent<RectTransform>();
            SetAnchors(turnsRect, new Vector2(0.045f, 0.12f), new Vector2(0.955f, 0.265f));

            RescueStickerFactory.CreateLabel(
                turnsRect,
                "TurnsTitle",
                "Turn Timeline",
                20f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SoftWhite,
                TextAlignmentOptions.Left,
                new Vector2(0.04f, 0.86f),
                new Vector2(0.45f, 0.98f));

            resultTurnsRoot = CreateRect("ResultTurns", turnsRect);
            SetAnchors(resultTurnsRoot, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.82f));
            var turnsLayout = resultTurnsRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            turnsLayout.spacing = 6f;
            turnsLayout.childAlignment = TextAnchor.UpperLeft;
            turnsLayout.childControlHeight = true;
            turnsLayout.childControlWidth = true;
            turnsLayout.childForceExpandHeight = false;
            turnsLayout.childForceExpandWidth = true;

            var actions = CreateRect("ResultActions", resultViewRoot);
            SetAnchors(actions, new Vector2(0.045f, 0.025f), new Vector2(0.955f, 0.095f));

            resultPlayAgainButton = RescueStickerFactory.CreatePrimaryGoldButton(
                actions,
                "Play Again",
                new Vector2(320f, 68f));
            SetAnchors(resultPlayAgainButton.transform as RectTransform, new Vector2(0f, 0f), new Vector2(0.48f, 1f));

            resultHomeButton = RescueStickerFactory.CreateSecondaryActionButton(
                actions,
                "Home",
                new Vector2(160f, 58f));
            SetAnchors(resultHomeButton.transform as RectTransform, new Vector2(0.52f, 0f), new Vector2(0.72f, 1f));

            resultEditButton = RescueStickerFactory.CreateSecondaryActionButton(
                actions,
                "Edit Squad",
                new Vector2(200f, 58f));
            SetAnchors(resultEditButton.transform as RectTransform, new Vector2(0.76f, 0f), new Vector2(1f, 1f));
        }

        private void RefreshTurnHeader(EmojiClashTurnViewModel model)
        {
            topEyebrowLabel.text = "QUICK PLAY";
            topTitleLabel.text = "Emoji Clash";
            turnStatusLabel.text = $"Turn {model.TurnNumber} / {model.TotalTurns}";
            turnValueLabel.text = model.TurnNumber == model.TotalTurns
                ? $"FINAL  +{model.TurnValue}"
                : $"+{model.TurnValue} pt";
        }

        private void RefreshTurnSequence(EmojiClashTurnViewModel model)
        {
            for (var index = 0; index < turnValueLabels.Count; index++)
            {
                var label = turnValueLabels[index];
                if (label == null)
                {
                    continue;
                }

                label.text = model.WeightedTurnValues != null && index < model.WeightedTurnValues.Length
                    ? model.WeightedTurnValues[index].ToString()
                    : "?";
                var isCurrent = index == model.TurnNumber - 1;
                label.color = isCurrent ? RescueStickerFactory.Palette.InkPurple : new Color(0.94f, 0.96f, 1f, 0.90f);
                var image = label.transform.parent.GetComponent<Image>();
                if (image != null)
                {
                    image.color = isCurrent
                        ? Color.Lerp(EmojiWarVisualStyle.Colors.GoldLight, RescueStickerFactory.Palette.Mint, 0.12f)
                        : new Color(0.18f, 0.16f, 0.42f, 0.72f);
                }
            }
        }

        private void RefreshScoreBand(EmojiClashTurnViewModel model)
        {
            var scoreView = model != null && model.IsResolved
                ? CreatePreRevealScoreView(model)
                : CreateCurrentScoreView(model);

            ApplyScoreBand(scoreView.scoreSummary, scoreView.momentumNormalized, false);
        }

        private void RevealResolvedScoreBand(EmojiClashTurnViewModel model)
        {
            var scoreView = CreateCurrentScoreView(model);
            ApplyScoreBand(scoreView.scoreSummary, scoreView.momentumNormalized, true);
        }

        private void ApplyScoreBand(string scoreSummary, float momentumNormalized, bool punch)
        {
            if (scoreLabel == null || momentumPlayerFill == null || momentumRivalFill == null)
            {
                return;
            }

            scoreLabel.text = scoreSummary;

            momentumPlayerFill.gameObject.SetActive(momentumNormalized > 0.001f);
            momentumRivalFill.gameObject.SetActive(momentumNormalized < -0.001f);

            if (momentumNormalized >= 0f)
            {
                SetAnchors(momentumPlayerFill, new Vector2(0.5f, 0.14f), new Vector2(0.5f + (momentumNormalized * 0.48f), 0.86f));
                SetAnchors(momentumRivalFill, new Vector2(0.5f, 0.14f), new Vector2(0.5f, 0.86f));
            }
            else
            {
                SetAnchors(momentumRivalFill, new Vector2(0.5f + (momentumNormalized * 0.48f), 0.14f), new Vector2(0.5f, 0.86f));
                SetAnchors(momentumPlayerFill, new Vector2(0.5f, 0.14f), new Vector2(0.5f, 0.86f));
            }

            if (punch)
            {
                NativeMotionKit.PunchScale(this, momentumTrackRoot, 0.035f, 0.16f);
                NativeMotionKit.PunchScale(this, scoreLabel.rectTransform, 0.05f, 0.16f);
            }
        }

        private static (string scoreSummary, float momentumNormalized) CreateCurrentScoreView(EmojiClashTurnViewModel model)
        {
            if (model == null)
            {
                return ("You 0 - 0 Rival", 0f);
            }

            return (model.ScoreSummary, model.MomentumNormalized);
        }

        private static (string scoreSummary, float momentumNormalized) CreatePreRevealScoreView(EmojiClashTurnViewModel model)
        {
            if (model == null)
            {
                return ("You 0 - 0 Rival", 0f);
            }

            var playerScore = model.PlayerScore;
            var opponentScore = model.OpponentScore;
            if (model.OutcomeTitle.StartsWith("YOU WIN", StringComparison.OrdinalIgnoreCase))
            {
                playerScore = Mathf.Max(0, playerScore - model.TurnValue);
            }
            else if (model.OutcomeTitle.StartsWith("RIVAL", StringComparison.OrdinalIgnoreCase))
            {
                opponentScore = Mathf.Max(0, opponentScore - model.TurnValue);
            }

            var maxScore = Mathf.Max(1, model.WeightedTurnValues?.Sum() ?? 1);
            var momentum = Mathf.Clamp((playerScore - opponentScore) / (float)maxScore, -1f, 1f);
            return ($"You {playerScore} - {opponentScore} Rival", momentum);
        }

        private void RefreshClashStage(EmojiClashTurnViewModel model)
        {
            if (clashSequenceCoroutine != null)
            {
                StopCoroutine(clashSequenceCoroutine);
                clashSequenceCoroutine = null;
            }

            clashSequenceVisibleUntilTime = 0f;
            ClearClashCinematicLayer(true);
            ClearExpiredPendingSummon();

            ClearChildren(clashStageRoot);
            activePlayerCardRoot = null;
            activeRivalCardRoot = null;
            activeClashCoreRoot = null;
            activeSummonPadRoot = null;
            activePlayerSlotEmpty = string.IsNullOrWhiteSpace(model.PlayerPickKey);
            activeRivalHidden = model.ShowOpponentMystery;
            isBenchDragActive = false;
            isSummonHoverActive = false;

            var leftCard = CreateUnitCard(
                clashStageRoot,
                string.IsNullOrWhiteSpace(model.PlayerPickKey) ? "Pick One" : EmojiClashRules.ToDisplayName(model.PlayerPickKey),
                model.PlayerPickKey,
                false,
                model.IsResolved || model.IsLocked,
                model.IsResolved,
                EmojiWarVisualStyle.Layout.ClashCard);
            SetAnchors(leftCard.GetComponent<RectTransform>(), new Vector2(0.01f, 0.02f), new Vector2(0.42f, 0.98f));
            activePlayerCardRoot = leftCard.GetComponent<RectTransform>();
            activeSummonPadRoot = leftCard.transform.Find("SummonPadRoot") as RectTransform;
            ConfigureDropZone(leftCard.GetComponent<RectTransform>(), model);

            var versusRect = CreateRect("ClashCore", clashStageRoot);
            SetAnchors(versusRect, new Vector2(0.39f, 0.28f), new Vector2(0.61f, 0.74f));
            var versusCanvasGroup = versusRect.gameObject.AddComponent<CanvasGroup>();
            var coreVisual = CreateRect("ClashCoreVisual", versusRect);
            Stretch(coreVisual);
            activeClashCoreRoot = coreVisual;
            RescueStickerFactory.CreateBlob(
                coreVisual,
                "VsBridge",
                RescueStickerFactory.Palette.Cloud,
                Vector2.zero,
                new Vector2(220f, 82f),
                0.08f);
            RescueStickerFactory.CreateBlob(
                coreVisual,
                "VsBridgeWarm",
                RescueStickerFactory.Palette.SunnyYellow,
                new Vector2(-34f, 0f),
                new Vector2(126f, 56f),
                0.10f);
            RescueStickerFactory.CreateBlob(
                coreVisual,
                "VsBridgeCool",
                RescueStickerFactory.Palette.Aqua,
                new Vector2(34f, 0f),
                new Vector2(126f, 56f),
                0.10f);
            RescueStickerFactory.CreateBlob(
                coreVisual,
                "VsBurstOuter",
                RescueStickerFactory.Palette.SunnyYellow,
                Vector2.zero,
                new Vector2(176f, 176f),
                0.18f);
            RescueStickerFactory.CreateBlob(
                coreVisual,
                "VsBurstMid",
                RescueStickerFactory.Palette.Aqua,
                Vector2.zero,
                new Vector2(132f, 132f),
                0.20f);
            RescueStickerFactory.CreateBlob(
                coreVisual,
                "VsBurstInnerCut",
                EmojiWarVisualStyle.Colors.Depth,
                Vector2.zero,
                new Vector2(98f, 98f),
                0.88f);
            RescueStickerFactory.CreateBlob(
                coreVisual,
                "VsGlowLeft",
                RescueStickerFactory.Palette.Coral,
                new Vector2(-46f, 0f),
                new Vector2(74f, 106f),
                0.16f);
            RescueStickerFactory.CreateBlob(
                coreVisual,
                "VsGlowRight",
                RescueStickerFactory.Palette.Aqua,
                new Vector2(46f, 0f),
                new Vector2(74f, 106f),
                0.16f);
            RescueStickerFactory.CreateBlob(
                coreVisual,
                "VsCore",
                RescueStickerFactory.Palette.SoftWhite,
                Vector2.zero,
                new Vector2(34f, 34f),
                0.22f);
            RescueStickerFactory.CreateBlob(
                coreVisual,
                "VsSparkA",
                RescueStickerFactory.Palette.SunnyYellow,
                new Vector2(0f, 56f),
                new Vector2(24f, 24f),
                0.20f);
            RescueStickerFactory.CreateBlob(
                coreVisual,
                "VsSparkB",
                RescueStickerFactory.Palette.Cloud,
                new Vector2(-54f, -14f),
                new Vector2(16f, 16f),
                0.18f);
            RescueStickerFactory.CreateBlob(
                coreVisual,
                "VsSparkC",
                RescueStickerFactory.Palette.Cloud,
                new Vector2(56f, 18f),
                new Vector2(14f, 14f),
                0.16f);
            RescueStickerFactory.CreateLabel(
                coreVisual,
                "VsLabel",
                model.IsResolved ? "CLASH" : model.IsLocked ? "LOCKED" : "VS",
                31f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SunnyYellow,
                TextAlignmentOptions.Center,
                new Vector2(0.12f, 0.20f),
                new Vector2(0.88f, 0.80f));

            var rightCard = model.ShowOpponentMystery
                ? CreateMysteryCard(clashStageRoot, model.IsLocked, EmojiWarVisualStyle.Layout.ClashCard)
                : CreateUnitCard(
                    clashStageRoot,
                    EmojiClashRules.ToDisplayName(model.OpponentPickKey),
                    model.OpponentPickKey,
                    true,
                    true,
                    model.IsResolved,
                    EmojiWarVisualStyle.Layout.ClashCard);
            SetAnchors(rightCard.GetComponent<RectTransform>(), new Vector2(0.58f, 0.04f), new Vector2(0.99f, 0.96f));
            activeRivalCardRoot = rightCard.GetComponent<RectTransform>();

            outcomeLabel.gameObject.SetActive(!model.IsResolved && !string.IsNullOrWhiteSpace(model.OutcomeTitle));
            var showReason = model.IsResolved && !string.IsNullOrWhiteSpace(model.ReasonText);
            if (reasonLabel != null)
            {
                reasonLabel.gameObject.SetActive(false);
                reasonLabel.text = showReason ? model.ReasonText : string.Empty;
            }

            outcomeLabel.text = model.OutcomeTitle;

            var consumePendingSummonWasDrag = pendingSummonWasDrag;
            var consumePendingSummon = !string.IsNullOrWhiteSpace(model.PlayerPickKey) &&
                                       !model.IsResolved &&
                                       TryConsumePendingSummon(model.PlayerPickKey);
            if (!consumePendingSummon &&
                !string.IsNullOrWhiteSpace(model.PlayerPickKey) &&
                !string.IsNullOrWhiteSpace(pendingSummonUnitKey) &&
                !string.Equals(pendingSummonUnitKey, model.PlayerPickKey, StringComparison.OrdinalIgnoreCase))
            {
                ClearPendingSummonState(true);
            }

            if (!consumePendingSummon)
            {
                NativeMotionKit.PopIn(this, leftCard.GetComponent<RectTransform>(), leftCard.GetComponent<CanvasGroup>(), 0.22f, 0.84f);
            }

            NativeMotionKit.PopIn(this, rightCard.GetComponent<RectTransform>(), rightCard.GetComponent<CanvasGroup>(), 0.22f, 0.84f);
            NativeMotionKit.PopIn(this, versusRect, versusCanvasGroup, 0.18f, 0.86f);

            PlayAmbientArenaMotion(model, leftCard.GetComponent<RectTransform>(), rightCard.GetComponent<RectTransform>(), coreVisual);

            if (!string.IsNullOrWhiteSpace(model.PlayerPickKey) && !model.IsResolved)
            {
                var fighterRect = leftCard.transform.Find("EmojiAvatar") as RectTransform;
                if (fighterRect != null)
                {
                    var fighterCanvasGroup = EnsureCanvasGroup(fighterRect);
                    if (consumePendingSummon)
                    {
                        if (fighterCanvasGroup != null)
                        {
                            fighterCanvasGroup.alpha = 1f;
                        }

                        if (!consumePendingSummonWasDrag)
                        {
                            fighterRect.localScale = Vector3.one;
                        }
                    }
                    else if (Time.unscaledTime >= suppressNextPlayerEntryDropUntilTime)
                    {
                        NativeMotionKit.DropIntoSlot(this, fighterRect, new Vector2(0f, 84f), 0.28f);
                        NativeMotionKit.PunchScale(this, fighterRect, 0.06f, 0.18f);
                    }
                }
            }

            if (model.IsResolved)
            {
                clashSequenceVisibleUntilTime = Time.unscaledTime +
                    ResolvedClashStandoffSeconds +
                    ResolvedClashAdvanceSeconds +
                    ResolvedClashCloudBuildSeconds +
                    ResolvedClashImpactSeconds +
                    ResolvedClashLoserExitSeconds +
                    ResolvedClashWinnerHoldSeconds +
                    ResolvedClashSafetyBufferSeconds;
                clashSequenceCoroutine = StartCoroutine(PlayResolvedClashSequence(
                    model,
                    leftCard.GetComponent<RectTransform>(),
                    rightCard.GetComponent<RectTransform>(),
                    versusRect,
                    coreVisual));
            }
            else if (model.IsLocked)
            {
                NativeMotionKit.PunchScale(this, leftCard.GetComponent<RectTransform>(), 0.06f, 0.14f);
                NativeMotionKit.PunchScale(this, rightCard.GetComponent<RectTransform>(), 0.06f, 0.14f);
                NativeMotionKit.PunchScale(this, coreVisual, 0.06f, 0.14f);
            }
        }

        private void RefreshBoard(EmojiClashTurnViewModel model)
        {
            ClearChildren(boardGridRoot);
            ClearChildren(benchStickerVisualLayer);

            for (var index = 0; index < model.BoardItems.Length; index++)
            {
                var item = model.BoardItems[index];
                var tile = CreateBattleBenchHitTile(boardGridRoot, item.DisplayName);
                PositionBattleBenchHitTile(tile, index);
                var visual = RescueStickerFactory.CreateBattleBenchSticker(
                    benchStickerVisualLayer,
                    item.DisplayName,
                    item.UnitKey,
                    item.AuraColor,
                    item.IsSelected,
                    item.IsUsed,
                    BattleBenchVisualSize,
                    0);
                var visualRect = visual.GetComponent<RectTransform>();
                PositionBattleBenchVisual(visualRect, index);
                DisableRaycasts(visual);

                var button = tile.GetComponent<Button>() ?? tile.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.interactable = item.IsAvailable;
                if (item.IsAvailable)
                {
                    var capturedKey = item.UnitKey;
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(() =>
                    {
                        if (HandleSuccessfulDropFeedback(visualRect, item, false))
                        {
                            onPick?.Invoke(capturedKey);
                        }
                    });
                    AttachDragBehaviour(tile.GetComponent<RectTransform>(), visualRect, item, model);
                }
                else
                {
                    button.onClick.RemoveAllListeners();
                }

                var tilt = GetBattleBenchScatterRotation(index);
                var rowLift = GetBattleBenchScatterLift(index);
                if (item.IsUsed)
                {
                    var stamp = RescueStickerFactory.CreateStatusChip(
                        visual.transform,
                        "USED",
                        RescueStickerFactory.Palette.Coral,
                        RescueStickerFactory.Palette.SoftWhite);
                    var stampRect = stamp.GetComponent<RectTransform>();
                    SetAnchors(stampRect, new Vector2(0.48f, 0.74f), new Vector2(0.88f, 0.89f));
                    stampRect.localRotation = Quaternion.Euler(0f, 0f, -7f);
                    DisableRaycasts(stamp);
                    NativeMotionKit.StampSlam(this, stampRect, 1.14f, 0.16f);
                }

                NativeMotionKit.PopIn(this, visualRect, visual.GetComponent<CanvasGroup>(), 0.18f + index * 0.008f, 0.88f);
                var avatar = visual.transform.Find("EmojiAvatar") as RectTransform;
                if (avatar != null)
                {
                    EnsureCanvasGroup(avatar);
                    avatar.anchoredPosition += new Vector2(0f, rowLift);
                    avatar.localRotation = Quaternion.Euler(0f, 0f, -tilt * 0.42f);
                }

                if (item.IsAvailable && avatar != null)
                {
                    NativeMotionKit.IdleBob(this, avatar, 1.55f + (index % 4) * 0.20f, 1.30f + index * 0.04f, true);
                    NativeMotionKit.BreatheScale(this, avatar, 0.008f + (index % 3) * 0.0015f, 1.22f + index * 0.03f, true);
                }

                if (item.IsSelected && !item.IsUsed)
                {
                    NativeMotionKit.PunchScale(this, visualRect, 0.06f, 0.16f);
                }
            }
        }

        private static RectTransform CreateBattleBenchHitTile(Transform parent, string displayName)
        {
            var tile = CreateRect($"{displayName}BattleBenchHitTile", parent);
            tile.sizeDelta = BattleBenchHitTileSize;
            var image = tile.gameObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.001f);
            image.raycastTarget = true;
            tile.gameObject.AddComponent<CanvasGroup>();
            return tile;
        }

        private static void PositionBattleBenchHitTile(RectTransform tile, int index)
        {
            if (tile == null)
            {
                return;
            }

            var center = GetBattleBenchScatterAnchor(index);
            tile.anchorMin = center;
            tile.anchorMax = center;
            tile.pivot = new Vector2(0.5f, 0.5f);
            tile.sizeDelta = BattleBenchHitTileSize;
            tile.anchoredPosition = Vector2.zero;
            tile.localRotation = Quaternion.identity;
            tile.localScale = Vector3.one;
        }

        private static void PositionBattleBenchVisual(RectTransform visual, int index)
        {
            if (visual == null)
            {
                return;
            }

            var center = GetBattleBenchScatterAnchor(index);
            var scale = GetBattleBenchScatterScale(index);
            visual.anchorMin = center;
            visual.anchorMax = center;
            visual.pivot = new Vector2(0.5f, 0.5f);
            visual.sizeDelta = BattleBenchVisualSize;
            visual.anchoredPosition = Vector2.zero;
            visual.localRotation = Quaternion.Euler(0f, 0f, GetBattleBenchScatterRotation(index));
            visual.localScale = Vector3.one * scale;
        }

        private static Vector2 GetBattleBenchScatterAnchor(int index)
        {
            return BattleBenchScatterAnchors[Mathf.Abs(index) % BattleBenchScatterAnchors.Length];
        }

        private static float GetBattleBenchScatterRotation(int index)
        {
            return BattleBenchScatterRotations[Mathf.Abs(index) % BattleBenchScatterRotations.Length];
        }

        private static float GetBattleBenchScatterScale(int index)
        {
            return BattleBenchScatterScales[Mathf.Abs(index) % BattleBenchScatterScales.Length];
        }

        private static float GetBattleBenchScatterLift(int index)
        {
            return ((index * 5) % 7 - 3) * 0.34f;
        }

        private static void DisableRaycasts(GameObject rootObject)
        {
            if (rootObject == null)
            {
                return;
            }

            foreach (var graphic in rootObject.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
            }

            foreach (var group in rootObject.GetComponentsInChildren<CanvasGroup>(true))
            {
                group.blocksRaycasts = false;
                group.interactable = false;
            }
        }

        private void RefreshTurnActions(EmojiClashTurnViewModel model)
        {
            WireButton(turnHomeButton, () => onHome?.Invoke(), 0.05f);
            if (autoAdvanceHintLabel != null)
            {
                autoAdvanceHintLabel.text = model.IsResolved
                    ? model.TurnNumber == model.TotalTurns
                        ? "Result incoming"
                        : "Next clash"
                    : model.IsLocked
                        ? "Reveal incoming"
                        : string.Empty;
            }
        }

        private void RebuildResultRecaps(IReadOnlyList<string> recapLines)
        {
            ClearChildren(recapListRoot);
            if (recapLines == null || recapLines.Count == 0)
            {
                recapLines = new[] { "Five turns resolved. No extra recap was generated." };
            }

            foreach (var line in recapLines)
            {
                var entry = RescueStickerFactory.CreateCompactTimelineRow(recapListRoot, "CLASH", line);
                entry.AddComponent<LayoutElement>().preferredHeight = 48f;
            }
        }

        private void RebuildResultTurns(IReadOnlyList<string> turnLines)
        {
            ClearChildren(resultTurnsRoot);
            foreach (var line in turnLines ?? Array.Empty<string>())
            {
                var lead = ExtractTurnLead(line);
                var body = StripTurnLead(line);
                var entry = RescueStickerFactory.CreateCompactTimelineRow(resultTurnsRoot, lead, body);
                entry.AddComponent<LayoutElement>().preferredHeight = 48f;
            }
        }

        private void RefreshResultHeroDecor(EmojiClashResultViewModel model)
        {
            ClearChildren(resultHeroDecorRoot);
            if (resultHeroDecorRoot == null)
            {
                return;
            }

            var featured = ResolveFeaturedUnits(model);
            for (var index = 0; index < featured.Count; index++)
            {
                var key = featured[index];
                var name = EmojiClashRules.ToDisplayName(key);
                var fighter = RescueStickerFactory.CreateResultHeroFighter(
                    resultHeroDecorRoot,
                    key,
                    name,
                    UnitIconLibrary.GetPrimaryColor(key),
                    EmojiWarVisualStyle.Layout.QuickResultHeroFighter);
                var rect = fighter.GetComponent<RectTransform>();
                rect.anchorMin = index == 0 ? new Vector2(0.18f, 0.37f) : new Vector2(0.82f, 0.37f);
                rect.anchorMax = rect.anchorMin;
                rect.localRotation = Quaternion.Euler(0f, 0f, index == 0 ? -8f : 8f);
                NativeMotionKit.IdleBob(this, rect, 7f + index, 1.26f + index * 0.08f, true);
                NativeMotionKit.BreatheScale(this, rect, 0.020f, 1.18f + index * 0.04f, true);
            }

            if (resultOutcomeLabel != null)
            {
                NativeMotionKit.StampSlam(this, resultOutcomeLabel.rectTransform, 1.14f, 0.20f);
            }

            if (resultHeroScoreCardRoot != null)
            {
                NativeMotionKit.PopIn(this, resultHeroScoreCardRoot, resultHeroScoreCardRoot.GetComponent<CanvasGroup>(), 0.20f, 0.90f);
            }
        }

        private GameObject CreateUnitCard(
            Transform parent,
            string displayName,
            string unitKey,
            bool enemyTone,
            bool selected,
            bool reveal,
            Vector2 size)
        {
            var normalizedKey = string.IsNullOrWhiteSpace(unitKey) ? "fire" : EmojiClashRules.NormalizeUnitKey(unitKey);
            var isPlaceholder = string.IsNullOrWhiteSpace(unitKey);
            var primary = UnitIconLibrary.GetPrimaryColor(normalizedKey);
            var aura = Color.Lerp(primary, UnitIconLibrary.GetSecondaryColor(normalizedKey), 0.28f);
            var card = RescueStickerFactory.CreateArenaSurface(
                parent,
                enemyTone ? "RivalClashCard" : "PlayerClashCard",
                enemyTone ? new Color(0.18f, 0.10f, 0.28f, 0.88f) : new Color(0.14f, 0.16f, 0.34f, 0.86f),
                aura,
                size);
            card.AddComponent<CanvasGroup>();

            RescueStickerFactory.CreateBlob(
                card.transform,
                "CardAura",
                aura,
                new Vector2(enemyTone ? size.x * 0.03f : -size.x * 0.03f, size.y * 0.04f),
                new Vector2(size.x * 0.94f, size.y * 0.70f),
                enemyTone ? 0.10f : 0.13f);
            RescueStickerFactory.CreateBlob(
                card.transform,
                "CardFloorGlow",
                Color.Lerp(primary, RescueStickerFactory.Palette.SunnyYellow, 0.10f),
                new Vector2(0f, -size.y * 0.29f),
                new Vector2(size.x * 0.66f, size.y * 0.12f),
                0.20f);
            RescueStickerFactory.CreateBlob(
                card.transform,
                enemyTone ? "EdgeGlowRival" : "EdgeGlowPlayer",
                enemyTone ? RescueStickerFactory.Palette.Coral : RescueStickerFactory.Palette.Aqua,
                new Vector2(enemyTone ? size.x * 0.22f : -size.x * 0.22f, 0f),
                new Vector2(size.x * 0.28f, size.y * 0.56f),
                0.08f);

            var tag = RescueStickerFactory.CreateStatusChip(
                card.transform,
                enemyTone ? "RIVAL" : isPlaceholder ? "SUMMON" : "YOUR SIDE",
                enemyTone ? new Color(0.32f, 0.14f, 0.50f, 0.86f) : new Color(0.14f, 0.22f, 0.48f, 0.86f),
                RescueStickerFactory.Palette.SoftWhite);
            SetAnchors(tag.GetComponent<RectTransform>(), new Vector2(0.06f, 0.85f), new Vector2(enemyTone ? 0.34f : 0.40f, 0.96f));

            if (isPlaceholder && !enemyTone)
            {
                var summonPadRoot = CreateRect("SummonPadRoot", card.transform);
                SetAnchors(summonPadRoot, new Vector2(0.10f, 0.24f), new Vector2(0.90f, 0.80f));
                RescueStickerFactory.CreateBlob(summonPadRoot, "SummonAuraOuter", RescueStickerFactory.Palette.SunnyYellow, new Vector2(0f, 0f), new Vector2(size.x * 0.82f, size.x * 0.82f), 0.15f);
                RescueStickerFactory.CreateBlob(summonPadRoot, "SummonAuraMid", RescueStickerFactory.Palette.Aqua, new Vector2(0f, 0f), new Vector2(size.x * 0.64f, size.x * 0.64f), 0.18f);
                RescueStickerFactory.CreateBlob(summonPadRoot, "SummonRingCutOuter", EmojiWarVisualStyle.Colors.Depth, new Vector2(0f, 0f), new Vector2(size.x * 0.51f, size.x * 0.51f), 0.92f);
                RescueStickerFactory.CreateBlob(summonPadRoot, "SummonRingInner", RescueStickerFactory.Palette.Mint, new Vector2(0f, 0f), new Vector2(size.x * 0.37f, size.x * 0.37f), 0.15f);
                RescueStickerFactory.CreateBlob(summonPadRoot, "SummonRingCutInner", EmojiWarVisualStyle.Colors.Depth, new Vector2(0f, 0f), new Vector2(size.x * 0.25f, size.x * 0.25f), 0.96f);
                RescueStickerFactory.CreateBlob(summonPadRoot, "SummonCore", RescueStickerFactory.Palette.SoftWhite, Vector2.zero, new Vector2(size.x * 0.10f, size.x * 0.10f), 0.34f);
                RescueStickerFactory.CreateBlob(summonPadRoot, "SummonBeam", RescueStickerFactory.Palette.Cloud, new Vector2(0f, size.y * 0.09f), new Vector2(size.x * 0.22f, size.y * 0.44f), 0.08f);
                RescueStickerFactory.CreateBlob(summonPadRoot, "PromptFloor", RescueStickerFactory.Palette.Aqua, new Vector2(0f, -size.y * 0.31f), new Vector2(size.x * 0.62f, size.y * 0.14f), 0.20f);
                RescueStickerFactory.CreateBlob(summonPadRoot, "PromptFloorWarm", RescueStickerFactory.Palette.SunnyYellow, new Vector2(0f, -size.y * 0.31f), new Vector2(size.x * 0.38f, size.y * 0.10f), 0.16f);
                RescueStickerFactory.CreateBlob(summonPadRoot, "SummonSparkLeft", RescueStickerFactory.Palette.Cloud, new Vector2(-size.x * 0.18f, size.y * 0.11f), new Vector2(14f, 14f), 0.18f);
                RescueStickerFactory.CreateBlob(summonPadRoot, "SummonSparkRight", RescueStickerFactory.Palette.SunnyYellow, new Vector2(size.x * 0.19f, -size.y * 0.04f), new Vector2(18f, 18f), 0.20f);
                RescueStickerFactory.CreateBlob(summonPadRoot, "SummonSparkTop", RescueStickerFactory.Palette.Aqua, new Vector2(0f, size.y * 0.21f), new Vector2(16f, 16f), 0.18f);
                RescueStickerFactory.CreateLabel(card.transform, "PromptTitle", "Drop your fighter here", 18f, FontStyles.Bold, EmojiWarVisualStyle.Colors.GoldLight, TextAlignmentOptions.Center, new Vector2(0.10f, 0.10f), new Vector2(0.90f, 0.18f));
            }
            else
            {
                var fighter = RescueStickerFactory.CreateClashFighter(card.transform, normalizedKey, displayName, aura, new Vector2(size.x * 1.04f, size.y * 0.94f));
                var fighterRect = fighter.GetComponent<RectTransform>();
                fighterRect.anchorMin = new Vector2(0.50f, 0.60f);
                fighterRect.anchorMax = new Vector2(0.50f, 0.60f);
                fighterRect.anchoredPosition = Vector2.zero;

                RescueStickerFactory.CreateLabel(
                    card.transform,
                    "CardName",
                    displayName,
                    25f,
                    FontStyles.Bold,
                    RescueStickerFactory.Palette.SoftWhite,
                    TextAlignmentOptions.Center,
                    new Vector2(0.08f, 0.07f),
                    new Vector2(0.92f, 0.16f));

                if (selected)
                {
                    NativeMotionKit.BreatheScale(this, fighterRect, 0.018f, 1.18f, true);
                }
            }

            if (reveal)
            {
                var stamp = RescueStickerFactory.CreateStatusChip(
                    card.transform,
                    enemyTone ? "REVEALED" : "LOCKED IN",
                    enemyTone ? RescueStickerFactory.Palette.Coral : RescueStickerFactory.Palette.Mint,
                    enemyTone ? RescueStickerFactory.Palette.SoftWhite : RescueStickerFactory.Palette.InkPurple);
                SetAnchors(stamp.GetComponent<RectTransform>(), new Vector2(0.54f, 0.85f), new Vector2(0.96f, 0.97f));
            }

            return card;
        }

        private GameObject CreateMysteryCard(Transform parent, bool locked, Vector2 size)
        {
            var mystery = RescueStickerFactory.CreateArenaSurface(
                parent,
                "MysteryCard",
                new Color(0.07f, 0.07f, 0.20f, 0.92f),
                RescueStickerFactory.Palette.ElectricPurple,
                size);
            mystery.AddComponent<CanvasGroup>();
            var pulseRoot = CreateRect("MysteryPulseRoot", mystery.transform);
            Stretch(pulseRoot);

            RescueStickerFactory.CreateBlob(
                pulseRoot,
                "MysteryGlow",
                RescueStickerFactory.Palette.ElectricPurple,
                new Vector2(0f, 16f),
                new Vector2(size.x * 0.80f, size.x * 0.80f),
                0.13f);
            RescueStickerFactory.CreateBlob(
                pulseRoot,
                "MysteryFloor",
                RescueStickerFactory.Palette.ElectricPurple,
                new Vector2(0f, -size.y * 0.29f),
                new Vector2(size.x * 0.58f, size.y * 0.12f),
                0.18f);
            RescueStickerFactory.CreateBlob(
                pulseRoot,
                "MysterySideGlow",
                RescueStickerFactory.Palette.Coral,
                new Vector2(size.x * 0.18f, 0f),
                new Vector2(size.x * 0.20f, size.y * 0.48f),
                0.08f);

            RescueStickerFactory.CreateLabel(
                pulseRoot,
                "MysteryMark",
                "?",
                118f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SoftWhite,
                TextAlignmentOptions.Center,
                new Vector2(0.18f, 0.30f),
                new Vector2(0.82f, 0.80f));

            RescueStickerFactory.CreateLabel(
                pulseRoot,
                "MysteryTitle",
                "Rival",
                24f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SoftWhite,
                TextAlignmentOptions.Center,
                new Vector2(0.16f, 0.12f),
                new Vector2(0.84f, 0.20f));

            var chip = RescueStickerFactory.CreateStatusChip(
                mystery.transform,
                locked ? "LOCKED" : "HIDDEN",
                locked ? RescueStickerFactory.Palette.Coral : RescueStickerFactory.Palette.ElectricPurple,
                RescueStickerFactory.Palette.SoftWhite);
            SetAnchors(chip.GetComponent<RectTransform>(), new Vector2(0.28f, 0.84f), new Vector2(0.72f, 0.95f));
            return mystery;
        }

        private void PlayAmbientArenaMotion(
            EmojiClashTurnViewModel model,
            RectTransform leftCard,
            RectTransform rightCard,
            RectTransform coreVisual)
        {
            if (activeSummonPadRoot != null && !model.IsResolved && !model.IsLocked)
            {
                NativeMotionKit.BreatheScale(this, activeSummonPadRoot, 0.018f, 1.22f, true);
                var outer = FindImage(activeSummonPadRoot, "SummonAuraOuter");
                var floor = FindImage(activeSummonPadRoot, "PromptFloor");
                if (outer != null)
                {
                    NativeMotionKit.PulseGraphic(
                        this,
                        outer,
                        WithAlpha(RescueStickerFactory.Palette.SunnyYellow, 0.12f),
                        WithAlpha(RescueStickerFactory.Palette.SunnyYellow, 0.20f),
                        1.08f);
                }

                if (floor != null)
                {
                    NativeMotionKit.PulseGraphic(
                        this,
                        floor,
                        WithAlpha(RescueStickerFactory.Palette.Aqua, 0.14f),
                        WithAlpha(RescueStickerFactory.Palette.Aqua, 0.22f),
                        0.92f);
                }

                UpdateSummonPadState();
            }

            if (coreVisual != null && !model.IsResolved)
            {
                NativeMotionKit.BreatheScale(this, coreVisual, model.IsLocked ? 0.018f : 0.012f, model.IsLocked ? 0.86f : 1.16f, false);
                var bridge = FindImage(coreVisual, "VsBridge");
                var burst = FindImage(coreVisual, "VsBurstOuter");
                if (bridge != null)
                {
                    NativeMotionKit.PulseGraphic(
                        this,
                        bridge,
                        WithAlpha(RescueStickerFactory.Palette.Cloud, 0.06f),
                        WithAlpha(RescueStickerFactory.Palette.Cloud, model.IsLocked ? 0.12f : 0.10f),
                        0.90f);
                }

                if (burst != null)
                {
                    NativeMotionKit.PulseGraphic(
                        this,
                        burst,
                        WithAlpha(RescueStickerFactory.Palette.SunnyYellow, 0.14f),
                        WithAlpha(RescueStickerFactory.Palette.SunnyYellow, model.IsLocked ? 0.24f : 0.18f),
                        1.12f);
                }
            }

            if (activeRivalHidden && rightCard != null)
            {
                var mysteryRoot = rightCard.Find("MysteryPulseRoot") as RectTransform ?? rightCard;
                NativeMotionKit.BreatheScale(this, mysteryRoot, 0.015f, 1.24f, true);
                NativeMotionKit.IdleBob(this, mysteryRoot, 2.6f, 1.42f, true);
                var mysteryGlow = FindImage(mysteryRoot, "MysteryGlow");
                if (mysteryGlow != null)
                {
                    NativeMotionKit.PulseGraphic(
                        this,
                        mysteryGlow,
                        WithAlpha(RescueStickerFactory.Palette.ElectricPurple, 0.10f),
                        WithAlpha(RescueStickerFactory.Palette.ElectricPurple, 0.18f),
                        0.96f);
                }
            }
        }

        private void HandleBenchDragStateChanged(RectTransform sourceTile, bool isDragging)
        {
            isBenchDragActive = isDragging;
            if (sourceTile != null && isDragging)
            {
                NativeMotionKit.PunchScale(this, sourceTile, 0.05f, 0.12f);
            }

            if (activeSummonPadRoot != null)
            {
                if (isDragging)
                {
                    NativeMotionKit.PunchScale(this, activeSummonPadRoot, 0.06f, 0.16f);
                }

                UpdateSummonPadState();
            }

            if (activeClashCoreRoot != null && isDragging)
            {
                NativeMotionKit.PunchScale(this, activeClashCoreRoot, 0.05f, 0.14f);
            }
        }

        private void HandleSummonHoverChanged(bool isHovering)
        {
            isSummonHoverActive = isHovering;
            UpdateSummonPadState();
            if (isHovering)
            {
                if (activeSummonPadRoot != null)
                {
                    NativeMotionKit.PunchScale(this, activeSummonPadRoot, 0.05f, 0.14f);
                }

                if (activeClashCoreRoot != null)
                {
                    NativeMotionKit.PunchScale(this, activeClashCoreRoot, 0.04f, 0.12f);
                }
            }
        }

        private void HandleSuccessfulDropFeedback(RectTransform sourceTile)
        {
            if (activeSummonPadRoot != null)
            {
                NativeMotionKit.DropIntoSlot(this, activeSummonPadRoot, new Vector2(0f, 26f), 0.22f);
            }

            if (activeClashCoreRoot != null)
            {
                NativeMotionKit.PunchScale(this, activeClashCoreRoot, 0.08f, 0.18f);
            }

            if (sourceTile != null)
            {
                NativeMotionKit.PunchScale(this, sourceTile, 0.06f, 0.12f);
            }
        }

        private bool HandleSuccessfulDropFeedback(RectTransform sourceTile, EmojiClashBoardItemViewModel item, bool fromDrag)
        {
            if (sourceTile == null || item == null)
            {
                return false;
            }

            ClearExpiredPendingSummon();
            if (IsSummonTransitionLocked())
            {
                return false;
            }

            suppressNextPlayerEntryDropUntilTime = Time.unscaledTime + SuppressPlayerEntryDropSeconds;
            pendingSummonUntilTime = Time.unscaledTime + SuppressPlayerEntryDropSeconds;
            summonTransitionLockUntilTime = Time.unscaledTime + SummonTransitionLockSeconds;
            pendingSummonUnitKey = item.UnitKey ?? string.Empty;
            pendingSummonWasDrag = fromDrag;
            ClearPendingSummonVisual();

            if (!fromDrag)
            {
                StartCoroutine(PlayImmediateSummonTravel(sourceTile, item, fromDrag));
            }

            HandleSuccessfulDropFeedback(sourceTile);
            return true;
        }

        private void UpdateSummonPadState()
        {
            if (activeSummonPadRoot == null || !activePlayerSlotEmpty)
            {
                return;
            }

            var intensity = isSummonHoverActive ? 1f : isBenchDragActive ? 0.74f : 0f;
            var outer = FindImage(activeSummonPadRoot, "SummonAuraOuter");
            if (outer != null)
            {
                NativeMotionKit.PulseGraphic(
                    this,
                    outer,
                    WithAlpha(RescueStickerFactory.Palette.SunnyYellow, Mathf.Lerp(0.12f, 0.18f, intensity)),
                    WithAlpha(RescueStickerFactory.Palette.SunnyYellow, Mathf.Lerp(0.20f, 0.30f, intensity)),
                    1.02f);
            }

            var floor = FindImage(activeSummonPadRoot, "PromptFloor");
            if (floor != null)
            {
                NativeMotionKit.PulseGraphic(
                    this,
                    floor,
                    WithAlpha(RescueStickerFactory.Palette.Aqua, Mathf.Lerp(0.14f, 0.22f, intensity)),
                    WithAlpha(RescueStickerFactory.Palette.Aqua, Mathf.Lerp(0.22f, 0.36f, intensity)),
                    0.88f);
            }

            SetImageColor(activeSummonPadRoot, "SummonAuraMid", WithAlpha(RescueStickerFactory.Palette.Aqua, Mathf.Lerp(0.18f, 0.30f, intensity)));
            SetImageColor(activeSummonPadRoot, "SummonRingInner", WithAlpha(RescueStickerFactory.Palette.Mint, Mathf.Lerp(0.15f, 0.26f, intensity)));
            SetImageColor(activeSummonPadRoot, "SummonCore", WithAlpha(RescueStickerFactory.Palette.SoftWhite, Mathf.Lerp(0.34f, 0.52f, intensity)));
            SetImageColor(activeSummonPadRoot, "SummonBeam", WithAlpha(RescueStickerFactory.Palette.Cloud, Mathf.Lerp(0.08f, 0.16f, intensity)));
            SetImageColor(activeSummonPadRoot, "PromptFloorWarm", WithAlpha(RescueStickerFactory.Palette.SunnyYellow, Mathf.Lerp(0.16f, 0.28f, intensity)));
        }

        private System.Collections.IEnumerator PlayResolvedClashSequence(
            EmojiClashTurnViewModel model,
            RectTransform leftCard,
            RectTransform rightCard,
            RectTransform clashCoreContainer,
            RectTransform clashCoreVisual)
        {
            if (leftCard == null || rightCard == null || clashCoreContainer == null || clashCoreVisual == null)
            {
                yield break;
            }

            var leftFighter = GetCardFighterRect(leftCard);
            var rightFighter = GetCardFighterRect(rightCard);
            var leftActor = CreateClashActor(leftFighter ?? leftCard, model.PlayerPickKey, EmojiClashRules.ToDisplayName(model.PlayerPickKey), UnitIconLibrary.GetPrimaryColor(model.PlayerPickKey), true);
            var rightActor = CreateClashActor(rightFighter ?? rightCard, model.OpponentPickKey, EmojiClashRules.ToDisplayName(model.OpponentPickKey), UnitIconLibrary.GetPrimaryColor(model.OpponentPickKey), false);
            var center = clashCoreContainer.anchoredPosition;
            var leftTarget = center + new Vector2(-104f, 0f);
            var rightTarget = center + new Vector2(104f, 0f);
            var playerAura = ResolveClashVfxColor(model.PlayerPickKey, UnitIconLibrary.GetPrimaryColor(model.PlayerPickKey), true);
            var rivalAura = ResolveClashVfxColor(model.OpponentPickKey, UnitIconLibrary.GetPrimaryColor(model.OpponentPickKey), false);

            SetCardFighterVisibility(leftCard, 0.04f);
            SetCardFighterVisibility(rightCard, 0.04f);

            if (outcomeLabel != null)
            {
                outcomeLabel.gameObject.SetActive(false);
            }

            if (reasonLabel != null)
            {
                reasonLabel.gameObject.SetActive(false);
            }

            if (leftActor != null)
            {
                leftActor.SetAsLastSibling();
            }

            if (rightActor != null)
            {
                rightActor.SetAsLastSibling();
            }

            NativeMotionKit.PunchScale(this, clashCoreVisual, 0.08f, 0.14f);
            SetImageColor(clashCoreVisual, "VsGlowLeft", WithAlpha(RescueStickerFactory.Palette.Coral, 0.20f));
            SetImageColor(clashCoreVisual, "VsGlowRight", WithAlpha(RescueStickerFactory.Palette.Aqua, 0.20f));
            SetImageColor(clashCoreVisual, "VsCore", WithAlpha(RescueStickerFactory.Palette.SoftWhite, 0.34f));

            if (leftActor != null)
            {
                StartCoroutine(PlayOverlayStandoff(leftActor, 1f, ResolvedClashStandoffSeconds));
            }

            if (rightActor != null)
            {
                StartCoroutine(PlayOverlayStandoff(rightActor, -1f, ResolvedClashStandoffSeconds));
            }

            yield return new WaitForSecondsRealtime(ResolvedClashStandoffSeconds);

            if (leftActor != null)
            {
                StartCoroutine(PlayOverlayAdvance(leftActor, leftTarget, 1f, ResolvedClashAdvanceSeconds));
            }

            if (rightActor != null)
            {
                StartCoroutine(PlayOverlayAdvance(rightActor, rightTarget, -1f, ResolvedClashAdvanceSeconds));
            }

            var clashCloud = CreateResolvedClashCloudFromSprites(center, playerAura, rivalAura, out var clashCloudCanvasGroup);
            if (clashCloud != null)
            {
                clashCloud.SetAsLastSibling();
                StartCoroutine(PlayResolvedClashCloudFromSprites(clashCloud, clashCloudCanvasGroup, ResolvedClashCloudBuildSeconds));
            }

            if (leftActor != null)
            {
                StartCoroutine(PlayOverlayCloudObscure(leftActor, ResolvedClashCloudBuildSeconds, 0.02f));
            }

            if (rightActor != null)
            {
                StartCoroutine(PlayOverlayCloudObscure(rightActor, ResolvedClashCloudBuildSeconds, 0.02f));
            }

            EnsureClashCinematicLayer()?.SetAsLastSibling();
            yield return new WaitForSecondsRealtime(ResolvedClashCloudBuildSeconds);

            NativeMotionKit.PunchScale(this, clashCoreVisual, 0.18f, ResolvedClashImpactSeconds);
            NativeMotionKit.Shake(this, clashCoreVisual, 14f, ResolvedClashImpactSeconds);
            SetImageColor(clashCoreVisual, "VsBurstOuter", WithAlpha(RescueStickerFactory.Palette.SunnyYellow, 0.34f));
            SetImageColor(clashCoreVisual, "VsBurstMid", WithAlpha(RescueStickerFactory.Palette.Aqua, 0.32f));
            SetImageColor(clashCoreVisual, "VsCore", WithAlpha(RescueStickerFactory.Palette.SoftWhite, 0.66f));

            StartCoroutine(PlayImpactBurstFromSprite(center, ResolvedClashImpactSeconds));

            yield return new WaitForSecondsRealtime(ResolvedClashImpactSeconds);

            var outcome = model?.OutcomeTitle ?? string.Empty;
            var playerWon = outcome.StartsWith("YOU WIN", StringComparison.OrdinalIgnoreCase);
            var rivalWon = outcome.StartsWith("RIVAL", StringComparison.OrdinalIgnoreCase);
            var winnerActor = playerWon ? leftActor : rivalWon ? rightActor : null;
            var loserActor = playerWon ? rightActor : rivalWon ? leftActor : null;
            var winnerDirection = playerWon ? 1f : rivalWon ? -1f : 0f;

            if (clashCloud != null)
            {
                if (winnerActor != null)
                {
                    winnerActor.SetAsLastSibling();
                }

                StartCoroutine(FadeResolvedClashCloud(clashCloud.gameObject, clashCloudCanvasGroup, 0.24f));
            }

            if (winnerActor != null && loserActor != null)
            {
                StartCoroutine(PlayOverlayLoserExit(loserActor, playerWon ? -1f : 1f, ResolvedClashLoserExitSeconds));
                StartCoroutine(PlayOverlayWinnerReveal(winnerActor, 0.28f));
                RevealResolvedScoreBand(model);
            }
            else
            {
                if (leftActor != null)
                {
                    StartCoroutine(PlayOverlayDrawReveal(leftActor, -1f, ResolvedClashDrawHoldSeconds));
                }

                if (rightActor != null)
                {
                    StartCoroutine(PlayOverlayDrawReveal(rightActor, 1f, ResolvedClashDrawHoldSeconds));
                }
            }

            yield return new WaitForSecondsRealtime(winnerActor != null ? ResolvedClashLoserExitSeconds : 0.10f);

            if (outcomeLabel != null)
            {
                outcomeLabel.gameObject.SetActive(true);
                NativeMotionKit.StampSlam(this, outcomeLabel.rectTransform, 1.12f, 0.20f);
            }

            if (reasonLabel != null && !string.IsNullOrWhiteSpace(reasonLabel.text))
            {
                reasonLabel.gameObject.SetActive(true);
            }

            if (winnerActor != null)
            {
                StartCoroutine(PlayOverlayWinnerTaunt(winnerActor, winnerDirection, ResolvedClashWinnerHoldSeconds));
                yield return new WaitForSecondsRealtime(ResolvedClashWinnerHoldSeconds);
            }
            else
            {
                yield return new WaitForSecondsRealtime(ResolvedClashDrawHoldSeconds);
            }

            ClearClashCinematicLayer(true);
            clashSequenceCoroutine = null;
        }

        private System.Collections.IEnumerator PlayImmediateSummonTravel(RectTransform sourceTile, EmojiClashBoardItemViewModel item, bool fromDrag)
        {
            if (sourceTile == null ||
                item == null ||
                dragLayerRoot == null ||
                activePlayerCardRoot == null ||
                string.IsNullOrWhiteSpace(item.UnitKey) ||
                fromDrag)
            {
                yield break;
            }

            var summonTarget = activeSummonPadRoot != null ? activeSummonPadRoot : activePlayerCardRoot;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    dragLayerRoot,
                    RectTransformUtility.WorldToScreenPoint(null, summonTarget.TransformPoint(new Vector3(0f, 12f, 0f))),
                    null,
                    out var endPosition))
            {
                yield break;
            }

            var sourceAnchor = sourceTile.Find("EmojiAvatar") as RectTransform ?? sourceTile;
            var startPosition = endPosition;
            if (!fromDrag)
            {
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        dragLayerRoot,
                        RectTransformUtility.WorldToScreenPoint(null, sourceAnchor.TransformPoint(Vector3.zero)),
                        null,
                        out startPosition))
                {
                    yield break;
                }
            }

            var travelVisual = RescueStickerFactory.CreateClashFighter(
                dragLayerRoot,
                EmojiClashRules.NormalizeUnitKey(item.UnitKey),
                item.DisplayName,
                item.AuraColor,
                new Vector2(EmojiWarVisualStyle.Layout.ClashCard.x * 0.68f, EmojiWarVisualStyle.Layout.ClashCard.y * 0.66f));
            var travelRect = travelVisual.GetComponent<RectTransform>();
            if (travelRect == null)
            {
                Destroy(travelVisual);
                yield break;
            }

            travelRect.anchorMin = new Vector2(0.5f, 0.5f);
            travelRect.anchorMax = new Vector2(0.5f, 0.5f);
            travelRect.pivot = new Vector2(0.5f, 0.5f);
            travelRect.anchoredPosition = startPosition;
            travelRect.localScale = Vector3.one * (fromDrag ? 0.92f : 0.54f);
            travelRect.localRotation = fromDrag ? Quaternion.identity : sourceTile.localRotation;
            travelRect.SetAsLastSibling();
            EnsureCanvasGroup(travelRect);

            foreach (var graphic in travelVisual.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
            }

            var canvasGroup = travelVisual.GetComponent<CanvasGroup>() ?? travelVisual.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0.96f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            pendingSummonVisual = travelRect;

            var elapsed = 0f;
            var startRotation = travelRect.localRotation;
            var travelDuration = ImmediateSummonTravelSeconds;
            while (elapsed < travelDuration && travelRect != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / travelDuration);
                var eased = NativeMotionKit.EaseOutCubic(t);
                var arc = Mathf.Sin(t * Mathf.PI) * 34f;
                travelRect.anchoredPosition = Vector2.LerpUnclamped(startPosition, endPosition, eased) + new Vector2(0f, arc);
                travelRect.localScale = Vector3.one * Mathf.Lerp(0.54f, 0.92f, eased);
                travelRect.localRotation = Quaternion.Slerp(startRotation, Quaternion.identity, eased);
                canvasGroup.alpha = Mathf.Lerp(0.96f, 0.78f, t);
                yield return null;
            }

            if (travelRect != null)
            {
                NativeMotionKit.DropIntoSlot(this, travelRect, new Vector2(0f, 26f), ImmediateSummonLandingSeconds);
                if (activeSummonPadRoot != null)
                {
                    NativeMotionKit.PunchScale(this, activeSummonPadRoot, 0.10f, 0.18f);
                }

                if (activeClashCoreRoot != null)
                {
                    NativeMotionKit.PunchScale(this, activeClashCoreRoot, 0.07f, 0.16f);
                }
            }

            yield return new WaitForSecondsRealtime(ImmediateSummonLandingSeconds);

            if (travelRect != null)
            {
                NativeMotionKit.PunchScale(this, travelRect, 0.08f, 0.16f);
            }
        }

        private System.Collections.IEnumerator PlayEnergyBurst(
            RectTransform source,
            Vector2 targetPosition,
            Color color,
            float duration,
            Vector2 size,
            Vector2 arcOffset)
        {
            if (source == null || clashStageRoot == null)
            {
                yield break;
            }

            var worldPoint = source.TransformPoint(Vector3.zero);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                clashStageRoot,
                RectTransformUtility.WorldToScreenPoint(null, worldPoint),
                null,
                out var startPosition);
            var cloud = RescueStickerFactory.CreateBlob(
                clashStageRoot,
                "EnergyCloud",
                color,
                startPosition,
                size,
                0.22f);
            var cloudRect = cloud.GetComponent<RectTransform>();
            var canvasGroup = cloud.AddComponent<CanvasGroup>();
            RescueStickerFactory.CreateBlob(
                cloud.transform,
                "EnergyCore",
                RescueStickerFactory.Palette.SoftWhite,
                Vector2.zero,
                size * 0.56f,
                0.10f);
            var safeDuration = Mathf.Max(0.10f, duration);
            var elapsed = 0f;
            var midPoint = Vector2.Lerp(startPosition, targetPosition, 0.52f) + arcOffset;
            while (elapsed < safeDuration && cloudRect != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / safeDuration);
                var eased = NativeMotionKit.EaseOutCubic(t);
                cloudRect.anchoredPosition = QuadraticBezier(startPosition, midPoint, targetPosition, eased);
                cloudRect.localScale = Vector3.one * Mathf.Lerp(0.78f, 1.28f, eased);
                canvasGroup.alpha = Mathf.Lerp(0.16f, 0.38f, 1f - Mathf.Abs(t - 0.5f) * 1.8f);
                yield return null;
            }

            if (cloud != null)
            {
                Destroy(cloud);
            }
        }

        private System.Collections.IEnumerator FadeDropCard(RectTransform target, float duration)
        {
            if (target == null)
            {
                yield break;
            }

            var canvasGroup = target.GetComponent<CanvasGroup>() ?? target.gameObject.AddComponent<CanvasGroup>();
            var basePosition = target.anchoredPosition;
            var safeDuration = Mathf.Max(0.10f, duration);
            var elapsed = 0f;
            while (elapsed < safeDuration && target != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / safeDuration);
                target.anchoredPosition = basePosition + new Vector2(0f, -32f * t);
                canvasGroup.alpha = Mathf.Lerp(1f, 0.32f, t);
                yield return null;
            }

            if (target != null)
            {
                target.anchoredPosition = basePosition + new Vector2(0f, -32f);
                canvasGroup.alpha = 0.32f;
            }
        }

        private Image FindImage(Transform root, string name)
        {
            return root != null ? root.Find(name)?.GetComponent<Image>() ?? root.GetComponent<Image>() : null;
        }

        private void SetImageColor(Transform root, string name, Color color)
        {
            var image = FindImage(root, name);
            if (image != null)
            {
                image.color = color;
            }
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        private static CanvasGroup EnsureCanvasGroup(Component target)
        {
            if (target == null)
            {
                return null;
            }

            return target.GetComponent<CanvasGroup>() ?? target.gameObject.AddComponent<CanvasGroup>();
        }

        private RectTransform EnsureClashCinematicLayer()
        {
            if (clashStageRoot == null)
            {
                return null;
            }

            if (clashCinematicLayer != null)
            {
                return clashCinematicLayer;
            }

            clashCinematicLayer = CreateRect("ClashCinematicLayer", clashStageRoot);
            Stretch(clashCinematicLayer);
            clashCinematicLayer.SetAsLastSibling();
            return clashCinematicLayer;
        }

        private void TrackCinematicObject(GameObject target)
        {
            if (target != null)
            {
                activeClashCinematicObjects.Add(target);
            }
        }

        private void ClearClashCinematicLayer(bool restoreCardFighters = true)
        {
            if (restoreCardFighters)
            {
                RestoreTrackedCardFighterAlpha();
            }

            foreach (var target in activeClashCinematicObjects)
            {
                if (target != null)
                {
                    Destroy(target);
                }
            }

            activeClashCinematicObjects.Clear();
            if (clashCinematicLayer != null)
            {
                Destroy(clashCinematicLayer.gameObject);
                clashCinematicLayer = null;
            }
        }

        private void TrackCardFighterAlpha(RectTransform fighterRect)
        {
            var canvasGroup = EnsureCanvasGroup(fighterRect);
            if (canvasGroup != null && !trackedCardFighterAlphas.ContainsKey(canvasGroup))
            {
                trackedCardFighterAlphas[canvasGroup] = canvasGroup.alpha;
            }
        }

        private void RestoreTrackedCardFighterAlpha()
        {
            foreach (var entry in trackedCardFighterAlphas)
            {
                if (entry.Key != null)
                {
                    entry.Key.alpha = entry.Value;
                }
            }

            trackedCardFighterAlphas.Clear();
        }

        private RectTransform GetCardFighterRect(RectTransform cardRoot)
        {
            return cardRoot != null ? cardRoot.Find("EmojiAvatar") as RectTransform : null;
        }

        private void SetCardFighterVisibility(RectTransform cardRoot, float alpha)
        {
            var fighterRect = GetCardFighterRect(cardRoot);
            if (fighterRect == null)
            {
                return;
            }

            TrackCardFighterAlpha(fighterRect);
            var canvasGroup = EnsureCanvasGroup(fighterRect);
            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.Clamp01(alpha);
            }
        }

        private Vector2 GetStageLocalPointFromRect(RectTransform sourceRect, Vector2 localOffset = default)
        {
            var layer = EnsureClashCinematicLayer();
            if (sourceRect == null || layer == null)
            {
                return Vector2.zero;
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    layer,
                    RectTransformUtility.WorldToScreenPoint(null, sourceRect.TransformPoint(localOffset)),
                    null,
                    out var localPoint))
            {
                return localPoint;
            }

            return sourceRect.anchoredPosition;
        }

        private RectTransform CreateClashActor(RectTransform sourceRect, string unitKey, string displayName, Color auraColor, bool playerSide)
        {
            var layer = EnsureClashCinematicLayer();
            if (layer == null || sourceRect == null || string.IsNullOrWhiteSpace(unitKey))
            {
                return null;
            }

            var normalizedKey = EmojiClashRules.NormalizeUnitKey(unitKey);
            var actorSize = sourceRect.rect.size.sqrMagnitude > 1f
                ? sourceRect.rect.size * 1.08f
                : new Vector2(EmojiWarVisualStyle.Layout.ClashCard.x * 0.96f, EmojiWarVisualStyle.Layout.ClashCard.y * 0.86f);
            var actor = RescueStickerFactory.CreateClashFighter(
                layer,
                normalizedKey,
                string.IsNullOrWhiteSpace(displayName) ? EmojiClashRules.ToDisplayName(normalizedKey) : displayName,
                auraColor,
                actorSize);
            TrackCinematicObject(actor);
            var actorRect = actor.GetComponent<RectTransform>();
            if (actorRect == null)
            {
                return null;
            }

            actorRect.anchorMin = new Vector2(0.5f, 0.5f);
            actorRect.anchorMax = new Vector2(0.5f, 0.5f);
            actorRect.pivot = new Vector2(0.5f, 0.5f);
            actorRect.anchoredPosition = GetStageLocalPointFromRect(sourceRect);
            actorRect.localRotation = Quaternion.Euler(0f, 0f, playerSide ? -3f : 3f);
            actorRect.localScale = Vector3.one * 0.86f;
            EnsureCanvasGroup(actorRect).alpha = 1f;
            actorRect.SetAsLastSibling();
            return actorRect;
        }

        private Sprite LoadClashVfxSprite(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            if (clashVfxSpriteCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var sprite = Resources.Load<Sprite>($"EmojiWar/ClashVfx/{key}");
            if (sprite == null)
            {
                Debug.LogError($"Emoji Clash VFX sprite missing or not imported as Sprite: EmojiWar/ClashVfx/{key}");
                return null;
            }

            clashVfxSpriteCache[key] = sprite;
            return sprite;
        }

        private Color ResolveClashVfxColor(string unitKey, Color auraColor, bool playerSide)
        {
            var normalizedKey = EmojiClashRules.NormalizeUnitKey(unitKey);
            Color baseColor = normalizedKey switch
            {
                "fire" => new Color(1.00f, 0.42f, 0.08f, 1f),
                "plant" => new Color(0.34f, 0.92f, 0.22f, 1f),
                "water" => new Color(0.15f, 0.56f, 1.00f, 1f),
                "wind" => new Color(0.18f, 0.92f, 0.94f, 1f),
                "lightning" => new Color(1.00f, 0.92f, 0.18f, 1f),
                "heart" => new Color(1.00f, 0.28f, 0.58f, 1f),
                "shield" => new Color(0.34f, 0.50f, 1.00f, 1f),
                "mirror" => new Color(playerSide ? 0.70f : 0.40f, 0.36f, playerSide ? 1.00f : 0.96f, 1f),
                "bomb" => new Color(1.00f, 0.64f, 0.12f, 1f),
                "hole" => new Color(0.64f, 0.28f, 1.00f, 1f),
                "chain" => new Color(0.46f, 0.72f, 0.96f, 1f),
                "ghost" => new Color(0.78f, 0.54f, 1.00f, 1f),
                "ice" => new Color(0.34f, 0.88f, 1.00f, 1f),
                "soap" => new Color(0.28f, 0.82f, 1.00f, 1f),
                _ => auraColor.a > 0.01f ? auraColor : UnitIconLibrary.GetPrimaryColor(unitKey)
            };

            var secondary = UnitIconLibrary.GetSecondaryColor(unitKey);
            if (baseColor.maxColorComponent < 0.42f)
            {
                baseColor = Color.Lerp(secondary, baseColor, 0.44f);
            }

            Color.RGBToHSV(baseColor, out var hue, out var saturation, out var value);
            saturation = Mathf.Clamp01(Mathf.Max(saturation, 0.96f));
            value = Mathf.Clamp01(Mathf.Max(value, 0.97f));
            return Color.HSVToRGB(hue, saturation, value);
        }

        private Material GetClashVfxAlphaTintMaterial()
        {
            if (clashVfxAlphaTintMaterial != null)
            {
                return clashVfxAlphaTintMaterial;
            }

            var shader = Shader.Find("UI/EmojiWarAlphaTint");
            if (shader == null)
            {
                Debug.LogError("Emoji Clash VFX shader missing: UI/EmojiWarAlphaTint");
                return null;
            }

            clashVfxAlphaTintMaterial = new Material(shader)
            {
                name = "EmojiWarAlphaTint (Runtime)",
                hideFlags = HideFlags.HideAndDontSave
            };
            return clashVfxAlphaTintMaterial;
        }

        private void ApplyClashVfxTint(Image image, Color tint)
        {
            if (image == null)
            {
                return;
            }

            image.color = tint;
            var material = GetClashVfxAlphaTintMaterial();
            if (material != null)
            {
                image.material = material;
            }
        }

        private Image CreateVfxImage(Transform parent, string name, Sprite sprite, Color color, Vector2 anchoredPosition, Vector2 size, float alpha, bool preserveAspect = true)
        {
            if (parent == null || sprite == null)
            {
                return null;
            }

            var rect = CreateRect(name, parent);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = preserveAspect;
            image.raycastTarget = false;
            ApplyClashVfxTint(image, WithAlpha(color, alpha));
            return image;
        }

        private Image CreateTintedVfx(Transform parent, string name, string spriteKey, Color color, Vector2 anchoredPosition, Vector2 size, float alpha, bool preserveAspect = true)
        {
            return CreateVfxImage(parent, name, LoadClashVfxSprite(spriteKey), color, anchoredPosition, size, alpha, preserveAspect);
        }

        private RectTransform CreateResolvedClashCloudFromSprites(Vector2 center, Color playerColor, Color rivalColor, out CanvasGroup canvasGroup)
        {
            canvasGroup = null;
            var layer = EnsureClashCinematicLayer();
            if (layer == null)
            {
                return null;
            }

            var lobeSprite = LoadClashVfxSprite("cloud_lobe");
            var mergedSprite = LoadClashVfxSprite("cloud_merged");
            var smokeSprite = LoadClashVfxSprite("smoke_cap");
            if (lobeSprite == null || mergedSprite == null || smokeSprite == null)
            {
                return null;
            }

            var cloudRoot = CreateRect("ResolvedClashCloud", layer);
            cloudRoot.anchorMin = new Vector2(0.5f, 0.5f);
            cloudRoot.anchorMax = new Vector2(0.5f, 0.5f);
            cloudRoot.pivot = new Vector2(0.5f, 0.5f);
            cloudRoot.sizeDelta = new Vector2(700f, 360f);
            cloudRoot.anchoredPosition = center;
            canvasGroup = cloudRoot.gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            TrackCinematicObject(cloudRoot.gameObject);

            var leftColor = playerColor;
            var rightColor = rivalColor;
            var centerColor = Color.Lerp(RescueStickerFactory.Palette.SunnyYellow, RescueStickerFactory.Palette.SoftWhite, 0.46f);
            RescueStickerFactory.CreateBlob(cloudRoot, "ColoredLobeUnderlayLeft", leftColor, new Vector2(-256f, 10f), new Vector2(470f, 300f), 0.01f);
            RescueStickerFactory.CreateBlob(cloudRoot, "ColoredLobeUnderlayRight", rightColor, new Vector2(256f, -8f), new Vector2(470f, 300f), 0.01f);
            CreateVfxImage(cloudRoot, "CloudPlayerLobe", lobeSprite, leftColor, new Vector2(-250f, 10f), new Vector2(430f, 280f), 0.01f, false);
            CreateVfxImage(cloudRoot, "CloudRivalLobe", lobeSprite, rightColor, new Vector2(250f, -8f), new Vector2(430f, 280f), 0.01f, false);
            CreateVfxImage(cloudRoot, "CloudMergedPlayerColor", mergedSprite, leftColor, new Vector2(-40f, 10f), new Vector2(820f, 430f), 0.01f, false);
            CreateVfxImage(cloudRoot, "CloudMergedRivalColor", mergedSprite, rightColor, new Vector2(40f, 10f), new Vector2(820f, 430f), 0.01f, false);
            CreateVfxImage(cloudRoot, "CloudMergedCenter", mergedSprite, centerColor, new Vector2(0f, 10f), new Vector2(820f, 430f), 0.01f, false);
            CreateVfxImage(cloudRoot, "CloudSmokeCap", smokeSprite, RescueStickerFactory.Palette.SoftWhite, new Vector2(0f, 24f), new Vector2(720f, 350f), 0.01f, false);
            CreateVfxImage(cloudRoot, "CloudCoreFlash", mergedSprite, Color.Lerp(RescueStickerFactory.Palette.SunnyYellow, RescueStickerFactory.Palette.SoftWhite, 0.36f), new Vector2(0f, 0f), new Vector2(300f, 230f), 0.01f, false);
            cloudRoot.SetAsLastSibling();
            return cloudRoot;
        }

        private System.Collections.IEnumerator PlayResolvedClashCloudFromSprites(RectTransform cloudRoot, CanvasGroup canvasGroup, float duration)
        {
            if (cloudRoot == null || canvasGroup == null)
            {
                yield break;
            }

            var safeDuration = Mathf.Max(0.16f, duration);
            var elapsed = 0f;
            var basePosition = cloudRoot.anchoredPosition;
            var leftUnderlay = FindImage(cloudRoot, "ColoredLobeUnderlayLeft");
            var rightUnderlay = FindImage(cloudRoot, "ColoredLobeUnderlayRight");
            var leftLobe = FindImage(cloudRoot, "CloudPlayerLobe");
            var rightLobe = FindImage(cloudRoot, "CloudRivalLobe");
            var mergedPlayer = FindImage(cloudRoot, "CloudMergedPlayerColor");
            var mergedRival = FindImage(cloudRoot, "CloudMergedRivalColor");
            var mergedCenter = FindImage(cloudRoot, "CloudMergedCenter");
            var smokeCap = FindImage(cloudRoot, "CloudSmokeCap");
            var core = FindImage(cloudRoot, "CloudCoreFlash");
            const float lobeOnlySeconds = 0.42f;
            var lobeOnlyPhaseEnd = Mathf.Clamp01(lobeOnlySeconds / safeDuration);
            var mergePhaseDuration = Mathf.Max(0.18f, safeDuration - lobeOnlySeconds);

            while (elapsed < safeDuration && cloudRoot != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / safeDuration);
                var eased = NativeMotionKit.EaseOutCubic(t);
                var lobeTravel = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / lobeOnlyPhaseEnd));
                var mergeBuild = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elapsed - lobeOnlySeconds) / mergePhaseDuration));
                var pulse = 0.5f + (Mathf.Sin(t * Mathf.PI * 3.6f) * 0.5f);

                cloudRoot.localScale = Vector3.one * Mathf.Lerp(0.52f, 1.08f, eased);
                cloudRoot.anchoredPosition = basePosition + new Vector2(0f, Mathf.Sin(t * Mathf.PI * 1.8f) * 5f);
                canvasGroup.alpha = Mathf.Lerp(0f, 0.98f, Mathf.Clamp01(eased * 1.16f));

                if (leftUnderlay != null)
                {
                    leftUnderlay.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-346f, -108f, lobeTravel), 8f);
                    leftUnderlay.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.80f, 1.18f, lobeTravel);
                    leftUnderlay.color = WithAlpha(leftUnderlay.color, Mathf.Lerp(0.18f, 0.68f, lobeTravel));
                }

                if (rightUnderlay != null)
                {
                    rightUnderlay.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(346f, 108f, lobeTravel), -8f);
                    rightUnderlay.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.80f, 1.18f, lobeTravel);
                    rightUnderlay.color = WithAlpha(rightUnderlay.color, Mathf.Lerp(0.18f, 0.68f, lobeTravel));
                }

                if (leftLobe != null)
                {
                    leftLobe.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-336f, -104f, lobeTravel), Mathf.Sin(t * Mathf.PI) * 12f);
                    leftLobe.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.78f, 1.24f, lobeTravel);
                    leftLobe.color = WithAlpha(leftLobe.color, Mathf.Lerp(0.88f, 1.00f, lobeTravel));
                }

                if (rightLobe != null)
                {
                    rightLobe.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(336f, 104f, lobeTravel), -Mathf.Sin(t * Mathf.PI) * 12f);
                    rightLobe.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.78f, 1.24f, lobeTravel);
                    rightLobe.color = WithAlpha(rightLobe.color, Mathf.Lerp(0.88f, 1.00f, lobeTravel));
                }

                if (mergedCenter != null)
                {
                    mergedCenter.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.24f, 1.04f, mergeBuild);
                    mergedCenter.color = WithAlpha(mergedCenter.color, Mathf.Lerp(0.00f, 0.16f, mergeBuild));
                }

                if (mergedPlayer != null)
                {
                    mergedPlayer.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.24f, 1.06f, mergeBuild);
                    mergedPlayer.color = WithAlpha(mergedPlayer.color, Mathf.Lerp(0.00f, 0.84f, mergeBuild));
                }

                if (mergedRival != null)
                {
                    mergedRival.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.24f, 1.06f, mergeBuild);
                    mergedRival.color = WithAlpha(mergedRival.color, Mathf.Lerp(0.00f, 0.84f, mergeBuild));
                }

                if (smokeCap != null)
                {
                    var smokeBuild = Mathf.Clamp01((mergeBuild - 0.72f) / 0.28f);
                    smokeCap.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.90f, 1.08f, Mathf.Clamp01(smokeBuild + pulse * 0.06f));
                    smokeCap.color = WithAlpha(smokeCap.color, Mathf.Lerp(0.00f, 0.04f, smokeBuild));
                }

                if (core != null)
                {
                    var coreBuild = Mathf.Clamp01((mergeBuild - 0.74f) / 0.26f);
                    core.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.20f, 1.06f, Mathf.Clamp01(coreBuild + pulse * 0.12f));
                    core.color = WithAlpha(core.color, Mathf.Lerp(0.00f, 0.24f, Mathf.Clamp01(coreBuild + pulse * 0.08f)));
                }

                yield return null;
            }
        }

        private System.Collections.IEnumerator PlayImpactBurstFromSprite(Vector2 center, float duration)
        {
            var layer = EnsureClashCinematicLayer();
            var impactSprite = LoadClashVfxSprite("impact_burst");
            if (layer == null || impactSprite == null)
            {
                yield break;
            }

            var warm = CreateVfxImage(layer, "ImpactBurstWarm", impactSprite, RescueStickerFactory.Palette.SunnyYellow, center, new Vector2(300f, 230f), 0.42f);
            var white = CreateVfxImage(layer, "ImpactBurstWhite", impactSprite, RescueStickerFactory.Palette.SoftWhite, center, new Vector2(248f, 186f), 0.58f);
            if (warm != null)
            {
                TrackCinematicObject(warm.gameObject);
                warm.rectTransform.SetAsLastSibling();
            }

            if (white != null)
            {
                TrackCinematicObject(white.gameObject);
                white.rectTransform.SetAsLastSibling();
            }

            var warmCanvas = warm != null ? EnsureCanvasGroup(warm.rectTransform) : null;
            var whiteCanvas = white != null ? EnsureCanvasGroup(white.rectTransform) : null;
            var safeDuration = Mathf.Max(0.10f, duration);
            var elapsed = 0f;
            while (elapsed < safeDuration && (warm != null || white != null))
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / safeDuration);
                var eased = NativeMotionKit.EaseOutCubic(t);
                if (warm != null)
                {
                    warm.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.54f, 1.18f, eased);
                    warm.color = WithAlpha(warm.color, Mathf.Lerp(0.42f, 0f, t));
                    if (warmCanvas != null)
                    {
                        warmCanvas.alpha = Mathf.Lerp(1f, 0f, t);
                    }
                }

                if (white != null)
                {
                    white.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.44f, 1.06f, eased);
                    white.color = WithAlpha(white.color, Mathf.Lerp(0.58f, 0f, t));
                    if (whiteCanvas != null)
                    {
                        whiteCanvas.alpha = Mathf.Lerp(1f, 0f, t);
                    }
                }

                yield return null;
            }

            if (warm != null)
            {
                Destroy(warm.gameObject);
            }

            if (white != null)
            {
                Destroy(white.gameObject);
            }
        }

        private System.Collections.IEnumerator FadeResolvedClashCloud(GameObject target, CanvasGroup canvasGroup, float duration)
        {
            if (target == null)
            {
                yield break;
            }

            var safeDuration = Mathf.Max(0.08f, duration);
            var elapsed = 0f;
            var startAlpha = canvasGroup != null ? canvasGroup.alpha : 1f;
            while (elapsed < safeDuration && target != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / safeDuration);
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = Mathf.Lerp(startAlpha, 0.04f, t);
                }

                yield return null;
            }
        }

        private System.Collections.IEnumerator PlayOverlayStandoff(RectTransform actor, float towardCenterDirection, float duration)
        {
            if (actor == null)
            {
                yield break;
            }

            var basePosition = actor.anchoredPosition;
            var baseRotation = actor.localRotation;
            var baseScale = actor.localScale;
            var safeDuration = Mathf.Max(0.10f, duration);
            var elapsed = 0f;
            while (elapsed < safeDuration && actor != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / safeDuration);
                var eased = NativeMotionKit.EaseOutCubic(t);
                actor.anchoredPosition = basePosition + new Vector2(towardCenterDirection * 8f * eased, Mathf.Sin(t * Mathf.PI) * 10f);
                actor.localRotation = Quaternion.Euler(0f, 0f, -towardCenterDirection * Mathf.Lerp(0f, 6f, eased));
                actor.localScale = baseScale * Mathf.Lerp(1f, 1.06f, eased);
                yield return null;
            }

            if (actor != null)
            {
                actor.anchoredPosition = basePosition;
                actor.localRotation = baseRotation;
                actor.localScale = baseScale;
            }
        }

        private System.Collections.IEnumerator PlayOverlayAdvance(RectTransform actor, Vector2 targetPosition, float towardCenterDirection, float duration)
        {
            if (actor == null)
            {
                yield break;
            }

            var basePosition = actor.anchoredPosition;
            var baseRotation = actor.localRotation;
            var baseScale = actor.localScale;
            var safeDuration = Mathf.Max(0.12f, duration);
            var elapsed = 0f;
            while (elapsed < safeDuration && actor != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / safeDuration);
                var eased = NativeMotionKit.EaseOutCubic(t);
                var overshoot = Mathf.Sin(t * Mathf.PI) * 10f;
                actor.anchoredPosition = Vector2.LerpUnclamped(basePosition, targetPosition, eased) + new Vector2(towardCenterDirection * overshoot, Mathf.Sin(t * Mathf.PI) * 10f);
                actor.localRotation = Quaternion.Euler(0f, 0f, -towardCenterDirection * Mathf.Lerp(0f, 13f, eased));
                actor.localScale = baseScale * Mathf.Lerp(1f, 1.14f, eased);
                yield return null;
            }

            if (actor != null)
            {
                actor.anchoredPosition = targetPosition;
                actor.localRotation = Quaternion.Euler(0f, 0f, -towardCenterDirection * 10f);
                actor.localScale = baseScale * 1.10f;
            }
        }

        private System.Collections.IEnumerator PlayOverlayCloudObscure(RectTransform actor, float duration, float minimumAlpha)
        {
            if (actor == null)
            {
                yield break;
            }

            var canvasGroup = EnsureCanvasGroup(actor);
            if (canvasGroup == null)
            {
                yield break;
            }

            var safeDuration = Mathf.Max(0.12f, duration);
            var startAlpha = canvasGroup.alpha;
            var elapsed = 0f;
            while (elapsed < safeDuration && actor != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / safeDuration);
                var obscured = Mathf.SmoothStep(startAlpha, minimumAlpha, Mathf.Clamp01((t - 0.06f) / 0.56f));
                canvasGroup.alpha = obscured;
                yield return null;
            }

            if (actor != null)
            {
                canvasGroup.alpha = minimumAlpha;
            }
        }

        private System.Collections.IEnumerator PlayOverlayLoserExit(RectTransform actor, float exitDirection, float duration)
        {
            if (actor == null)
            {
                yield break;
            }

            var canvasGroup = EnsureCanvasGroup(actor);
            var basePosition = actor.anchoredPosition;
            var baseRotation = actor.localRotation;
            var baseScale = actor.localScale;
            var safeDuration = Mathf.Max(0.10f, duration);
            var startAlpha = canvasGroup != null ? canvasGroup.alpha : 1f;
            var elapsed = 0f;
            while (elapsed < safeDuration && actor != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / safeDuration);
                actor.anchoredPosition = basePosition + new Vector2(exitDirection * 56f * t, -44f * t);
                actor.localRotation = Quaternion.Euler(0f, 0f, exitDirection * Mathf.Lerp(0f, 18f, t));
                actor.localScale = Vector3.Lerp(baseScale, baseScale * 0.84f, t);
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
                }

                yield return null;
            }
        }

        private System.Collections.IEnumerator PlayOverlayWinnerReveal(RectTransform actor, float duration)
        {
            if (actor == null)
            {
                yield break;
            }

            var canvasGroup = EnsureCanvasGroup(actor);
            var floorGlow = FindImage(actor, "FloorGlow");
            var baseFloorColor = floorGlow != null ? floorGlow.color : Color.clear;
            var basePosition = actor.anchoredPosition;
            var baseScale = actor.localScale;
            var safeDuration = Mathf.Max(0.10f, duration);
            var elapsed = 0f;
            var startAlpha = canvasGroup != null ? Mathf.Min(canvasGroup.alpha, 0.08f) : 1f;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = startAlpha;
            }

            actor.localScale = baseScale * 0.82f;
            while (elapsed < safeDuration && actor != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / safeDuration);
                var eased = NativeMotionKit.EaseOutCubic(t);
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, eased);
                }

                actor.anchoredPosition = basePosition + new Vector2(0f, Mathf.Lerp(-28f, 14f, eased));
                actor.localScale = Vector3.LerpUnclamped(baseScale * 0.82f, baseScale * 1.20f, eased);
                if (floorGlow != null)
                {
                    floorGlow.color = WithAlpha(baseFloorColor, Mathf.Lerp(baseFloorColor.a + 0.14f, baseFloorColor.a + 0.30f, eased));
                }

                yield return null;
            }

            if (actor != null)
            {
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = 1f;
                }

                actor.anchoredPosition = basePosition + new Vector2(0f, 14f);
                actor.localScale = baseScale * 1.20f;
                NativeMotionKit.PunchScale(this, actor, 0.12f, 0.22f);
            }

            if (floorGlow != null)
            {
                floorGlow.color = WithAlpha(baseFloorColor, baseFloorColor.a + 0.30f);
            }
        }

        private System.Collections.IEnumerator PlayOverlayWinnerTaunt(RectTransform actor, float facingDirection, float duration)
        {
            if (actor == null)
            {
                yield break;
            }

            var floorGlow = FindImage(actor, "FloorGlow");
            var baseFloorColor = floorGlow != null ? floorGlow.color : Color.clear;
            var basePosition = actor.anchoredPosition;
            var baseRotation = actor.localRotation;
            var baseScale = actor.localScale;
            var safeDuration = Mathf.Max(0.20f, duration);
            var elapsed = 0f;
            while (elapsed < safeDuration && actor != null && actor.gameObject.activeInHierarchy)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / safeDuration);
                var proudLift = Mathf.Sin(Mathf.Clamp01(t / 0.30f) * Mathf.PI * 0.5f);
                var wave = Mathf.Sin(t * Mathf.PI * 2.1f) * (1f - t * 0.22f);
                var challenge = Mathf.Sin(Mathf.Clamp01((t - 0.46f) / 0.24f) * Mathf.PI);
                var pulse = Mathf.Sin(Mathf.Clamp01((t - 0.62f) / 0.18f) * Mathf.PI);
                actor.anchoredPosition = basePosition + new Vector2(
                    facingDirection * ((wave * 7f) + (challenge * 11f)),
                    (proudLift * 10f) + Mathf.Sin(t * Mathf.PI * 1.08f) * 3.6f + (challenge * 4f) + (pulse * 2f));
                actor.localRotation = Quaternion.Euler(0f, 0f, facingDirection * ((proudLift * 2.8f) + (wave * 5.8f) + (challenge * 5.2f)));
                actor.localScale = baseScale * (1f + (proudLift * 0.05f) + Mathf.Max(0f, wave) * 0.024f + (challenge * 0.042f) + (pulse * 0.020f));

                if (floorGlow != null)
                {
                    floorGlow.color = WithAlpha(baseFloorColor, Mathf.Clamp01(baseFloorColor.a + 0.06f + (challenge * 0.14f) + (proudLift * 0.06f) + (pulse * 0.10f)));
                }

                yield return null;
            }

            if (actor != null)
            {
                actor.anchoredPosition = basePosition;
                actor.localRotation = baseRotation;
                actor.localScale = baseScale;
            }

            if (floorGlow != null)
            {
                floorGlow.color = baseFloorColor;
            }
        }

        private System.Collections.IEnumerator PlayOverlayDrawReveal(RectTransform actor, float facingDirection, float duration)
        {
            if (actor == null)
            {
                yield break;
            }

            var canvasGroup = EnsureCanvasGroup(actor);
            var floorGlow = FindImage(actor, "FloorGlow");
            var baseFloorColor = floorGlow != null ? floorGlow.color : Color.clear;
            var basePosition = actor.anchoredPosition;
            var baseRotation = actor.localRotation;
            var baseScale = actor.localScale;
            var safeDuration = Mathf.Max(0.20f, duration);
            var elapsed = 0f;
            var startAlpha = canvasGroup != null ? Mathf.Min(canvasGroup.alpha, 0.08f) : 1f;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = startAlpha;
            }

            actor.localScale = baseScale * 0.90f;
            while (elapsed < safeDuration && actor != null && actor.gameObject.activeInHierarchy)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / safeDuration);
                var eased = NativeMotionKit.EaseOutCubic(t);
                var daze = Mathf.Sin(t * Mathf.PI * 1.6f) * (1f - t * 0.30f);
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = Mathf.Lerp(startAlpha, 0.92f, Mathf.Clamp01(eased * 1.2f));
                }

                actor.anchoredPosition = basePosition + new Vector2(facingDirection * daze * 6f, Mathf.Lerp(-10f, 2f, eased) + Mathf.Sin(t * Mathf.PI) * 5f);
                actor.localRotation = Quaternion.Euler(0f, 0f, facingDirection * daze * 5f);
                actor.localScale = Vector3.LerpUnclamped(baseScale * 0.90f, baseScale * 1.04f, eased);
                if (floorGlow != null)
                {
                    floorGlow.color = WithAlpha(baseFloorColor, Mathf.Clamp01(baseFloorColor.a + 0.06f + Mathf.Max(0f, daze) * 0.05f));
                }

                yield return null;
            }

            if (actor != null)
            {
                actor.anchoredPosition = basePosition;
                actor.localRotation = baseRotation;
                actor.localScale = baseScale;
            }

            if (floorGlow != null)
            {
                floorGlow.color = baseFloorColor;
            }
        }

        private RectTransform CreateMixedClashCloud(Vector2 center, out CanvasGroup canvasGroup)
        {
            canvasGroup = null;
            if (clashStageRoot == null)
            {
                return null;
            }

            var cloudRoot = CreateRect("MixedClashCloud", clashStageRoot);
            cloudRoot.anchorMin = new Vector2(0.5f, 0.5f);
            cloudRoot.anchorMax = new Vector2(0.5f, 0.5f);
            cloudRoot.pivot = new Vector2(0.5f, 0.5f);
            cloudRoot.sizeDelta = new Vector2(306f, 306f);
            cloudRoot.anchoredPosition = center;
            cloudRoot.SetAsLastSibling();
            canvasGroup = cloudRoot.gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;

            RescueStickerFactory.CreateBlob(
                cloudRoot,
                "CloudCool",
                Color.Lerp(RescueStickerFactory.Palette.Aqua, RescueStickerFactory.Palette.Mint, 0.32f),
                new Vector2(-78f, 8f),
                new Vector2(154f, 132f),
                0.26f);
            RescueStickerFactory.CreateBlob(
                cloudRoot,
                "CloudWarm",
                Color.Lerp(RescueStickerFactory.Palette.Coral, RescueStickerFactory.Palette.SunnyYellow, 0.30f),
                new Vector2(78f, -4f),
                new Vector2(154f, 132f),
                0.26f);
            RescueStickerFactory.CreateBlob(
                cloudRoot,
                "CloudMix",
                Color.Lerp(RescueStickerFactory.Palette.Cloud, RescueStickerFactory.Palette.SoftWhite, 0.42f),
                Vector2.zero,
                new Vector2(176f, 164f),
                0.20f);
            RescueStickerFactory.CreateBlob(
                cloudRoot,
                "CloudMist",
                RescueStickerFactory.Palette.SoftWhite,
                new Vector2(0f, 12f),
                new Vector2(224f, 184f),
                0.12f);
            RescueStickerFactory.CreateBlob(
                cloudRoot,
                "CloudCore",
                RescueStickerFactory.Palette.SunnyYellow,
                Vector2.zero,
                new Vector2(88f, 88f),
                0.24f);
            RescueStickerFactory.CreateBlob(
                cloudRoot,
                "CloudVeil",
                RescueStickerFactory.Palette.Cloud,
                new Vector2(0f, 2f),
                new Vector2(238f, 202f),
                0.10f);
            return cloudRoot;
        }

        private System.Collections.IEnumerator PlayMixedClashCloud(RectTransform cloudRoot, CanvasGroup canvasGroup, float duration)
        {
            if (cloudRoot == null || canvasGroup == null)
            {
                yield break;
            }

            var safeDuration = Mathf.Max(0.16f, duration);
            var elapsed = 0f;
            var basePosition = cloudRoot.anchoredPosition;
            var cool = FindImage(cloudRoot, "CloudCool");
            var warm = FindImage(cloudRoot, "CloudWarm");
            var mix = FindImage(cloudRoot, "CloudMix");
            var mist = FindImage(cloudRoot, "CloudMist");
            var core = FindImage(cloudRoot, "CloudCore");
            var veil = FindImage(cloudRoot, "CloudVeil");

            while (elapsed < safeDuration && cloudRoot != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / safeDuration);
                var eased = NativeMotionKit.EaseOutCubic(t);
                var pulse = 0.5f + (Mathf.Sin(t * Mathf.PI * 3.2f) * 0.5f);
                var collide = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.32f) / 0.42f));
                cloudRoot.localScale = Vector3.one * Mathf.Lerp(0.34f, 1.12f, eased);
                cloudRoot.anchoredPosition = basePosition + new Vector2(0f, Mathf.Sin(t * Mathf.PI * 2f) * 3f);
                canvasGroup.alpha = Mathf.Lerp(0f, 0.94f, Mathf.Clamp01(eased * 1.18f));

                if (cool != null)
                {
                    cool.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-90f, -18f, collide), Mathf.Sin(t * Mathf.PI) * 8f);
                    cool.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.84f, 1.08f, collide);
                    cool.color = WithAlpha(cool.color, Mathf.Lerp(0.14f, 0.30f, collide));
                }

                if (warm != null)
                {
                    warm.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(90f, 18f, collide), -Mathf.Sin(t * Mathf.PI) * 8f);
                    warm.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.84f, 1.08f, collide);
                    warm.color = WithAlpha(warm.color, Mathf.Lerp(0.14f, 0.30f, collide));
                }

                if (mix != null)
                {
                    mix.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.62f, 1.20f, collide);
                    mix.color = WithAlpha(mix.color, Mathf.Lerp(0.06f, 0.24f, collide));
                }

                if (mist != null)
                {
                    mist.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.72f, 1.14f, Mathf.Clamp01(collide + (pulse * 0.08f)));
                    mist.color = WithAlpha(mist.color, Mathf.Lerp(0.03f, 0.16f, collide));
                }

                if (core != null)
                {
                    core.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.54f, 1.28f, Mathf.Clamp01(collide + (pulse * 0.12f)));
                    core.color = WithAlpha(core.color, Mathf.Lerp(0.14f, 0.38f, collide));
                }

                if (veil != null)
                {
                    veil.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.72f, 1.10f, collide);
                    veil.color = WithAlpha(veil.color, Mathf.Lerp(0.02f, 0.16f, collide));
                }

                yield return null;
            }
        }

        private System.Collections.IEnumerator FadeAndDestroyVisual(GameObject target, CanvasGroup canvasGroup, float duration)
        {
            if (target == null)
            {
                yield break;
            }

            var safeDuration = Mathf.Max(0.08f, duration);
            var elapsed = 0f;
            var startAlpha = canvasGroup != null ? canvasGroup.alpha : 1f;
            while (elapsed < safeDuration && target != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / safeDuration);
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
                }

                yield return null;
            }

            if (target != null)
            {
                Destroy(target);
            }
        }

        private bool IsSummonTransitionLocked()
        {
            return Time.unscaledTime < summonTransitionLockUntilTime;
        }

        private void ClearExpiredPendingSummon()
        {
            if (pendingSummonUntilTime > 0f && Time.unscaledTime > pendingSummonUntilTime)
            {
                ClearPendingSummonState(true);
            }
        }

        private bool TryConsumePendingSummon(string unitKey)
        {
            if (string.IsNullOrWhiteSpace(unitKey) ||
                string.IsNullOrWhiteSpace(pendingSummonUnitKey) ||
                Time.unscaledTime > pendingSummonUntilTime ||
                !string.Equals(pendingSummonUnitKey, unitKey, StringComparison.OrdinalIgnoreCase))
            {
                ClearExpiredPendingSummon();
                return false;
            }

            ClearPendingSummonState(true);
            return true;
        }

        private void ClearPendingSummonVisual()
        {
            if (pendingSummonVisual != null)
            {
                Destroy(pendingSummonVisual.gameObject);
            }
 
            pendingSummonVisual = null;
        }

        private void ClearPendingSummonState(bool destroyVisual)
        {
            if (destroyVisual)
            {
                ClearPendingSummonVisual();
            }

            pendingSummonUnitKey = string.Empty;
            pendingSummonUntilTime = 0f;
            pendingSummonWasDrag = false;
            summonTransitionLockUntilTime = 0f;
        }

        private static Vector2 QuadraticBezier(Vector2 start, Vector2 control, Vector2 end, float t)
        {
            var oneMinusT = 1f - t;
            return oneMinusT * oneMinusT * start +
                   2f * oneMinusT * t * control +
                   t * t * end;
        }

        private System.Collections.IEnumerator PlayFighterReadiness(RectTransform fighter, float towardCenterDirection, float duration)
        {
            if (fighter == null)
            {
                yield break;
            }

            var basePosition = fighter.anchoredPosition;
            var baseRotation = fighter.localRotation;
            var safeDuration = Mathf.Max(0.10f, duration);
            var elapsed = 0f;
            while (elapsed < safeDuration && fighter != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / safeDuration);
                var eased = NativeMotionKit.EaseOutCubic(t);
                fighter.anchoredPosition = basePosition + new Vector2(towardCenterDirection * 12f * eased, Mathf.Sin(t * Mathf.PI) * 10f);
                fighter.localRotation = Quaternion.Euler(0f, 0f, -towardCenterDirection * Mathf.Lerp(0f, 4.5f, eased));
                yield return null;
            }

            if (fighter != null)
            {
                fighter.anchoredPosition = basePosition;
                fighter.localRotation = baseRotation;
            }
        }

        private System.Collections.IEnumerator PlayFighterAdvance(RectTransform fighter, float towardCenterDirection, float duration)
        {
            if (fighter == null)
            {
                yield break;
            }

            var basePosition = fighter.anchoredPosition;
            var baseRotation = fighter.localRotation;
            var baseScale = fighter.localScale;
            var safeDuration = Mathf.Max(0.14f, duration);
            var elapsed = 0f;
            while (elapsed < safeDuration && fighter != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / safeDuration);
                var eased = NativeMotionKit.EaseOutCubic(t);
                var forwardPeak = Mathf.Lerp(0f, 54f, eased);
                var settleBack = Mathf.Lerp(0f, 10f, Mathf.Clamp01((t - 0.72f) / 0.28f));
                var forward = forwardPeak - settleBack;
                var settle = Mathf.Sin(t * Mathf.PI) * 8f;
                fighter.anchoredPosition = basePosition + new Vector2(towardCenterDirection * forward, settle);
                fighter.localRotation = Quaternion.Euler(0f, 0f, -towardCenterDirection * Mathf.Lerp(0f, 10.5f, eased));
                fighter.localScale = baseScale * Mathf.Lerp(1f, 1.14f, eased);
                yield return null;
            }

            if (fighter != null)
            {
                fighter.anchoredPosition = basePosition + new Vector2(towardCenterDirection * 44f, 0f);
                fighter.localRotation = Quaternion.Euler(0f, 0f, -towardCenterDirection * 8.5f);
                fighter.localScale = baseScale * 1.10f;
            }
        }

        private System.Collections.IEnumerator PlayWinnerRevealFromCloud(RectTransform fighter, float duration)
        {
            if (fighter == null)
            {
                yield break;
            }

            var canvasGroup = EnsureCanvasGroup(fighter);
            var baseScale = fighter.localScale;
            var safeDuration = Mathf.Max(0.10f, duration);
            var elapsed = 0f;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0.74f;
            }

            fighter.localScale = baseScale * 0.96f;
            while (elapsed < safeDuration && fighter != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / safeDuration);
                var eased = NativeMotionKit.EaseOutCubic(t);
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = Mathf.Lerp(0.74f, 1f, eased);
                }

                fighter.localScale = Vector3.LerpUnclamped(baseScale * 0.96f, baseScale * 1.06f, eased);
                yield return null;
            }

            if (fighter != null)
            {
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = 1f;
                }

                fighter.localScale = baseScale * 1.06f;
                NativeMotionKit.PunchScale(this, fighter, 0.08f, 0.18f);
            }
        }

        private System.Collections.IEnumerator PlayWinnerTaunt(RectTransform fighter, float facingDirection, float duration)
        {
            if (fighter == null)
            {
                yield break;
            }

            var basePosition = fighter.anchoredPosition;
            var baseRotation = fighter.localRotation;
            var baseScale = fighter.localScale;
            var safeDuration = Mathf.Max(0.20f, duration);
            var elapsed = 0f;
            while (elapsed < safeDuration && fighter != null && fighter.gameObject.activeInHierarchy)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / safeDuration);
                var proudLift = Mathf.Sin(Mathf.Clamp01(t / 0.34f) * Mathf.PI * 0.5f);
                var wave = Mathf.Sin(t * Mathf.PI * 2.4f) * (1f - t * 0.26f);
                var challenge = Mathf.Sin(Mathf.Clamp01((t - 0.52f) / 0.28f) * Mathf.PI);
                fighter.anchoredPosition = basePosition + new Vector2(
                    facingDirection * ((wave * 5.2f) + (challenge * 7.5f)),
                    (proudLift * 7.5f) + Mathf.Sin(t * Mathf.PI * 1.15f) * 2.8f + (challenge * 3.6f));
                fighter.localRotation = Quaternion.Euler(0f, 0f, facingDirection * ((proudLift * 2.2f) + (wave * 4.8f) + (challenge * 4.5f)));
                fighter.localScale = baseScale * (1f + (proudLift * 0.04f) + Mathf.Max(0f, wave) * 0.02f + (challenge * 0.034f));
                yield return null;
            }

            if (fighter != null)
            {
                fighter.anchoredPosition = basePosition;
                fighter.localRotation = baseRotation;
                fighter.localScale = baseScale;
            }
        }

        private void ConfigureDropZone(RectTransform target, EmojiClashTurnViewModel model)
        {
            if (target == null)
            {
                return;
            }

            var dropZone = target.GetComponent<EmojiClashDropZone>();
            if (dropZone == null)
            {
                dropZone = target.gameObject.AddComponent<EmojiClashDropZone>();
            }

            var canAcceptDrop = !model.IsResolved && !model.IsLocked;
            dropZone.Initialize(
                canAcceptDrop,
                isHovering => HandleSummonHoverChanged(isHovering));
        }

        private void AttachDragBehaviour(RectTransform tile, RectTransform visual, EmojiClashBoardItemViewModel item, EmojiClashTurnViewModel model)
        {
            if (tile == null)
            {
                return;
            }

            var dragItem = tile.GetComponent<EmojiClashDragItem>();
            if (dragItem == null)
            {
                dragItem = tile.gameObject.AddComponent<EmojiClashDragItem>();
            }

            dragItem.Initialize(
                dragLayerRoot,
                item.UnitKey,
                () => !model.IsResolved && !model.IsLocked && item.IsAvailable,
                key =>
                {
                    if (HandleSuccessfulDropFeedback(visual != null ? visual : tile, item, true))
                    {
                        onPick?.Invoke(key);
                    }
                },
                visual,
                () => HandleBenchDragStateChanged(tile, true),
                () => HandleBenchDragStateChanged(tile, false));
        }

        private bool ShouldDeferTurnBind()
        {
            return clashSequenceCoroutine != null && Time.unscaledTime < clashSequenceVisibleUntilTime;
        }

        private void QueueDeferredTurnBind(
            EmojiClashTurnViewModel model,
            Action<string> pickAction,
            Action homeAction,
            Action editSquadAction)
        {
            pendingTurnModel = model;
            pendingPickAction = pickAction;
            pendingHomeAction = homeAction;
            pendingEditSquadAction = editSquadAction;
            if (deferredTurnBindCoroutine == null)
            {
                deferredTurnBindCoroutine = StartCoroutine(ApplyDeferredTurnBindWhenReady());
            }
        }

        private void QueueDeferredResultBind(
            EmojiClashResultViewModel model,
            Action playAgainAction,
            Action homeAction,
            Action editSquadAction)
        {
            pendingResultModel = model;
            pendingPlayAgainAction = playAgainAction;
            pendingHomeAction = homeAction;
            pendingEditSquadAction = editSquadAction;
            pendingTurnModel = null;
            pendingPickAction = null;
            if (deferredTurnBindCoroutine == null)
            {
                deferredTurnBindCoroutine = StartCoroutine(ApplyDeferredTurnBindWhenReady());
            }
        }

        private System.Collections.IEnumerator ApplyDeferredTurnBindWhenReady()
        {
            while (clashSequenceCoroutine != null && Time.unscaledTime < clashSequenceVisibleUntilTime)
            {
                yield return null;
            }

            deferredTurnBindCoroutine = null;
            if (pendingResultModel != null)
            {
                var resultModel = pendingResultModel;
                var playAgainAction = pendingPlayAgainAction;
                var homeAction = pendingHomeAction;
                var editSquadAction = pendingEditSquadAction;
                pendingResultModel = null;
                pendingPlayAgainAction = null;
                pendingHomeAction = null;
                pendingEditSquadAction = null;
                ApplyResultBind(resultModel, playAgainAction, homeAction, editSquadAction);
                yield break;
            }

            if (pendingTurnModel == null)
            {
                yield break;
            }

            var model = pendingTurnModel;
            var pickAction = pendingPickAction;
            var deferredHomeAction = pendingHomeAction;
            var deferredEditSquadAction = pendingEditSquadAction;
            pendingTurnModel = null;
            pendingPickAction = null;
            pendingHomeAction = null;
            pendingEditSquadAction = null;
            ApplyTurnBind(model, pickAction, deferredHomeAction, deferredEditSquadAction);
            if (activeSummonPadRoot != null)
            {
                NativeMotionKit.PunchScale(this, activeSummonPadRoot, 0.08f, 0.18f);
            }
        }

        private void CancelDeferredTurnBind()
        {
            if (deferredTurnBindCoroutine != null)
            {
                StopCoroutine(deferredTurnBindCoroutine);
                deferredTurnBindCoroutine = null;
            }

            pendingTurnModel = null;
            pendingResultModel = null;
            pendingPickAction = null;
            pendingHomeAction = null;
            pendingEditSquadAction = null;
            pendingPlayAgainAction = null;
            ClearExpiredPendingSummon();
        }

        private static List<string> ResolveFeaturedUnits(EmojiClashResultViewModel model)
        {
            var featured = new List<string>();
            void TryAddFromText(string text)
            {
                foreach (var candidate in new[]
                {
                    "snake", "heart", "fire", "water", "lightning", "shield", "wind", "ice",
                    "magnet", "bomb", "plant", "ghost", "mirror", "soap", "chain", "hole"
                })
                {
                    if (featured.Count >= 2)
                    {
                        return;
                    }

                    if (!featured.Contains(candidate, StringComparer.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace(text) &&
                        text.IndexOf(EmojiClashRules.ToDisplayName(candidate), StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        featured.Add(candidate);
                    }
                }
            }

            foreach (var line in model?.RecapLines ?? Array.Empty<string>())
            {
                TryAddFromText(line);
            }

            foreach (var line in model?.TurnLines ?? Array.Empty<string>())
            {
                TryAddFromText(line);
            }

            if (featured.Count == 0)
            {
                featured.Add(model != null && model.IsDraw ? "shield" : "fire");
            }

            if (featured.Count == 1)
            {
                featured.Add(model != null && model.IsDraw ? "water" : "heart");
            }

            return featured;
        }

        private static string ExtractTurnLead(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return "T?";
            }

            var trimmed = line.Trim();
            var space = trimmed.IndexOf(' ');
            return space > 0 && trimmed[0] == 'T'
                ? trimmed.Substring(0, space).ToUpperInvariant()
                : "CLASH";
        }

        private static string StripTurnLead(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return string.Empty;
            }

            var trimmed = line.Trim();
            var space = trimmed.IndexOf(' ');
            return space > 0 && trimmed[0] == 'T'
                ? trimmed.Substring(space + 1)
                : trimmed;
        }

        private void WireButton(Button button, Action action, float delay)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                NativeMotionKit.PunchScale(this, button.transform as RectTransform, 0.06f, 0.12f);
                if (action != null)
                {
                    StartCoroutine(InvokeAfter(delay, action));
                }
            });
        }

        private System.Collections.IEnumerator InvokeAfter(float delay, Action action)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, delay));
            action?.Invoke();
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

        private static void ClearChildren(Transform root)
        {
            if (root == null)
            {
                return;
            }

            for (var index = root.childCount - 1; index >= 0; index--)
            {
                Destroy(root.GetChild(index).gameObject);
            }
        }

        private static string FormatResultScore(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Final Score\nYou 0 - 0 Rival";
            }

            return value.StartsWith("Final Score:", StringComparison.OrdinalIgnoreCase)
                ? $"Final Score\n{value.Substring("Final Score:".Length).Trim()}"
                : value;
        }

        private static Color ResolveResultAccent(string outcomeTitle)
        {
            if (string.IsNullOrWhiteSpace(outcomeTitle))
            {
                return RescueStickerFactory.Palette.SoftWhite;
            }

            if (outcomeTitle.IndexOf("VICTORY", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return EmojiWarVisualStyle.Colors.GoldLight;
            }

            if (outcomeTitle.IndexOf("DEFEAT", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return RescueStickerFactory.Palette.Coral;
            }

            return RescueStickerFactory.Palette.Mint;
        }
    }

    internal sealed class EmojiClashDragItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private RectTransform dragLayerRoot;
        private string unitKey = string.Empty;
        private Func<bool> canDrag;
        private Action<string> onDropAccepted;
        private RectTransform dragVisualSource;
        private Action onDragStarted;
        private Action onDragEnded;
        private RectTransform rectTransform;
        private CanvasGroup sourceCanvasGroup;
        private CanvasGroup sourceVisualCanvasGroup;
        private GameObject dragGhost;
        private RectTransform dragGhostRect;
        private bool acceptedDrop;

        public void Initialize(
            RectTransform dragLayer,
            string key,
            Func<bool> canDragPredicate,
            Action<string> acceptedDropAction,
            RectTransform visualSource,
            Action dragStartedAction,
            Action dragEndedAction)
        {
            dragLayerRoot = dragLayer;
            unitKey = key ?? string.Empty;
            canDrag = canDragPredicate;
            onDropAccepted = acceptedDropAction;
            dragVisualSource = visualSource;
            onDragStarted = dragStartedAction;
            onDragEnded = dragEndedAction;
            rectTransform = transform as RectTransform;
            sourceCanvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            sourceVisualCanvasGroup = dragVisualSource != null
                ? dragVisualSource.GetComponent<CanvasGroup>() ?? dragVisualSource.gameObject.AddComponent<CanvasGroup>()
                : null;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!CanDragNow())
            {
                return;
            }

            acceptedDrop = false;
            sourceCanvasGroup.alpha = 0.40f;
            if (sourceVisualCanvasGroup != null)
            {
                sourceVisualCanvasGroup.alpha = 0.58f;
            }

            var ghostSource = dragVisualSource != null ? dragVisualSource.gameObject : gameObject;
            dragGhost = Instantiate(ghostSource, dragLayerRoot);
            dragGhost.name = $"{gameObject.name}_DragGhost";
            dragGhostRect = dragGhost.transform as RectTransform;
            if (dragGhostRect != null)
            {
                dragGhostRect.SetAsLastSibling();
                dragGhostRect.sizeDelta = dragVisualSource != null
                    ? dragVisualSource.sizeDelta
                    : rectTransform != null
                        ? rectTransform.sizeDelta
                        : dragGhostRect.sizeDelta;
                var sourceScale = dragVisualSource != null ? dragVisualSource.localScale.x : 1f;
                dragGhostRect.localScale = Vector3.one * sourceScale * 1.08f;
                dragGhostRect.localRotation = dragVisualSource != null ? dragVisualSource.localRotation : Quaternion.identity;
            }

            foreach (var graphic in dragGhost.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
            }

            var ghostCanvasGroup = dragGhost.GetComponent<CanvasGroup>() ?? dragGhost.AddComponent<CanvasGroup>();
            ghostCanvasGroup.alpha = 0.94f;
            ghostCanvasGroup.blocksRaycasts = false;
            ghostCanvasGroup.interactable = false;

            var ghostButton = dragGhost.GetComponent<Button>();
            if (ghostButton != null)
            {
                ghostButton.enabled = false;
            }

            var nestedDrag = dragGhost.GetComponent<EmojiClashDragItem>();
            if (nestedDrag != null)
            {
                nestedDrag.enabled = false;
            }

            onDragStarted?.Invoke();
            UpdateGhostPosition(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (dragGhostRect == null)
            {
                return;
            }

            UpdateGhostPosition(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            CleanupDragVisual();
            acceptedDrop = false;
        }

        public void AcceptDrop()
        {
            if (!CanDragNow() || acceptedDrop)
            {
                return;
            }

            acceptedDrop = true;
            CleanupDragVisual();
            onDropAccepted?.Invoke(unitKey);
        }

        private bool CanDragNow()
        {
            return dragLayerRoot != null &&
                   rectTransform != null &&
                   canDrag != null &&
                   canDrag.Invoke() &&
                   !string.IsNullOrWhiteSpace(unitKey);
        }

        private void UpdateGhostPosition(PointerEventData eventData)
        {
            if (dragGhostRect == null || dragLayerRoot == null)
            {
                return;
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    dragLayerRoot,
                    eventData.position,
                    eventData.pressEventCamera,
                    out var localPoint))
            {
                dragGhostRect.anchorMin = new Vector2(0.5f, 0.5f);
                dragGhostRect.anchorMax = new Vector2(0.5f, 0.5f);
                dragGhostRect.anchoredPosition = localPoint + new Vector2(0f, 18f);
            }
        }

        private void CleanupDragVisual()
        {
            var wasDragging =
                dragGhost != null ||
                (sourceCanvasGroup != null && sourceCanvasGroup.alpha < 0.99f) ||
                (sourceVisualCanvasGroup != null && sourceVisualCanvasGroup.alpha < 0.99f);
            if (sourceCanvasGroup != null)
            {
                sourceCanvasGroup.alpha = 1f;
            }
            if (sourceVisualCanvasGroup != null)
            {
                sourceVisualCanvasGroup.alpha = 1f;
            }

            if (dragGhost != null)
            {
                Destroy(dragGhost);
            }

            dragGhost = null;
            dragGhostRect = null;
            if (wasDragging)
            {
                onDragEnded?.Invoke();
            }
        }
    }

    internal sealed class EmojiClashDropZone : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private bool canAcceptDrop;
        private Image targetImage;
        private Color baseColor;
        private Action<bool> hoverChanged;

        public void Initialize(bool acceptsDrop, Action<bool> hoverChangedAction)
        {
            canAcceptDrop = acceptsDrop;
            hoverChanged = hoverChangedAction;
            targetImage = GetComponent<Image>();
            baseColor = targetImage != null ? targetImage.color : Color.white;
            ResetHighlight();
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (!canAcceptDrop)
            {
                return;
            }

            var dragItem = eventData.pointerDrag != null
                ? eventData.pointerDrag.GetComponent<EmojiClashDragItem>()
                : null;
            if (dragItem == null)
            {
                return;
            }

            dragItem.AcceptDrop();
            hoverChanged?.Invoke(false);
            ResetHighlight();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!canAcceptDrop || targetImage == null || eventData.pointerDrag == null)
            {
                return;
            }

            if (eventData.pointerDrag.GetComponent<EmojiClashDragItem>() == null)
            {
                return;
            }

            targetImage.color = Color.Lerp(targetImage.color, RescueStickerFactory.Palette.SunnyYellow, 0.18f);
            hoverChanged?.Invoke(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hoverChanged?.Invoke(false);
            ResetHighlight();
        }

        private void ResetHighlight()
        {
            if (targetImage == null)
            {
                return;
            }

            targetImage.color = baseColor;
        }
    }
}
