using System.Collections;
using UnityEngine;
using UnityEngine.UI;
#if EMOJIWAR_DOTWEEN && DOTWEEN_ENABLED
using DG.Tweening;
#endif

namespace EmojiWar.Client.UI.Common
{
    public sealed class UiMotionController : MonoBehaviour
    {
        [SerializeField] private RectTransform target;
        [SerializeField] private bool playIdleOnEnable;
        [SerializeField] private bool ctaBreathe;
        [SerializeField] private bool idleTilt;

        private Coroutine idleRoutine;
        private Vector2 baseAnchoredPosition;
        private Vector3 baseScale;
        private Quaternion baseRotation;
        private bool allowPositionAnimation = true;
#if EMOJIWAR_DOTWEEN && DOTWEEN_ENABLED
        private Tween idleTween;
        private Tween jumpTween;
        private Tween slamTween;
        private Tween snapTween;
#endif

        private void Awake()
        {
            if (target == null)
            {
                target = transform as RectTransform;
            }
        }

        private void OnEnable()
        {
            if (target == null)
            {
                return;
            }

            baseAnchoredPosition = target.anchoredPosition;
            baseScale = target.localScale;
            baseRotation = target.localRotation;
            allowPositionAnimation = !IsLayoutDriven(target);

            if (playIdleOnEnable || ctaBreathe)
            {
                PlayIdle();
            }
        }

        private void OnDisable()
        {
            StopIdle();
#if EMOJIWAR_DOTWEEN && DOTWEEN_ENABLED
            KillActionTweens();
#endif
            if (target == null)
            {
                return;
            }

            target.anchoredPosition = baseAnchoredPosition;
            target.localScale = baseScale;
            target.localRotation = baseRotation;
        }

        public void PlayIdle()
        {
            if (target == null)
            {
                return;
            }

            StopIdle();
#if EMOJIWAR_DOTWEEN && DOTWEEN_ENABLED
            StartIdleTween();
#else
            idleRoutine = StartCoroutine(IdleRoutine());
#endif
        }

        public void Configure(bool enableIdle, bool enableCtaBreathe, bool enableTilt = false)
        {
            playIdleOnEnable = enableIdle;
            ctaBreathe = enableCtaBreathe;
            idleTilt = enableTilt;

            if (enableIdle || enableCtaBreathe)
            {
                PlayIdle();
            }
            else
            {
                StopIdle();
                if (target != null)
                {
                    target.anchoredPosition = baseAnchoredPosition;
                    target.localScale = baseScale;
                    target.localRotation = baseRotation;
                }
            }
        }

        public void StopIdle()
        {
            if (idleRoutine == null)
            {
                KillIdleTween();
                return;
            }

            StopCoroutine(idleRoutine);
            idleRoutine = null;
            KillIdleTween();
        }

        public void PlayJumpSelect()
        {
#if EMOJIWAR_DOTWEEN && DOTWEEN_ENABLED
            StartJumpTween();
#else
            StartCoroutine(JumpSelectRoutine());
#endif
        }

        public void PlayStickerSlam()
        {
#if EMOJIWAR_DOTWEEN && DOTWEEN_ENABLED
            StartStickerSlamTween();
#else
            StartCoroutine(StickerSlamRoutine());
#endif
        }

        public void PlaySnapToSlot(Vector2 destination, RectTransform slot)
        {
#if EMOJIWAR_DOTWEEN && DOTWEEN_ENABLED
            StartSnapTween(destination, slot);
#else
            StartCoroutine(SnapToSlotRoutine(destination, slot));
#endif
        }

        private IEnumerator IdleRoutine()
        {
            var motion = UiThemeRuntime.Motion;
            var startOffset = StableStagger(0.24f);
            if (startOffset > 0f)
            {
                yield return new WaitForSeconds(startOffset);
            }

            while (enabled && gameObject.activeInHierarchy)
            {
                var t = 0f;
                var duration = ctaBreathe
                    ? motion.CtaBreatheSeconds
                    : ResolveDuration(motion.IdleBobSeconds);
                var bob = ctaBreathe ? motion.IdleBobPixels * 0.35f : motion.IdleBobPixels;
                var scaleAmp = ctaBreathe ? motion.CtaBreatheScale : motion.IdleScaleAmount;
                while (t < duration)
                {
                    t += Time.unscaledDeltaTime;
                    var normalized = Mathf.Clamp01(t / duration);
                    var curve = Mathf.Sin(normalized * Mathf.PI * 2f);
                    if (allowPositionAnimation)
                    {
                        target.anchoredPosition = baseAnchoredPosition + new Vector2(0f, curve * bob);
                    }
                    target.localScale = baseScale * (1f + curve * scaleAmp);
                    if (idleTilt)
                    {
                        target.localRotation = baseRotation * Quaternion.Euler(0f, 0f, curve * UiThemeRuntime.Motion.IdleTiltDegrees);
                    }
                    yield return null;
                }
            }
        }

        private IEnumerator JumpSelectRoutine()
        {
            if (target == null)
            {
                yield break;
            }

            var motion = UiThemeRuntime.Motion;
            var total = ResolveDuration(motion.JumpSelectSeconds);
            var half = Mathf.Max(0.01f, total * 0.45f);
            var rise = 14f;

            var elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += Time.unscaledDeltaTime;
                var normalized = Mathf.Clamp01(elapsed / half);
                if (allowPositionAnimation)
                {
                    target.anchoredPosition = baseAnchoredPosition + new Vector2(0f, Mathf.Lerp(0f, rise, normalized));
                }
                target.localScale = baseScale * Mathf.Lerp(1f, motion.JumpScaleOvershoot, normalized);
                yield return null;
            }

            elapsed = 0f;
            var down = Mathf.Max(0.01f, total - half);
            while (elapsed < down)
            {
                elapsed += Time.unscaledDeltaTime;
                var normalized = Mathf.Clamp01(elapsed / down);
                if (allowPositionAnimation)
                {
                    target.anchoredPosition = baseAnchoredPosition + new Vector2(0f, Mathf.Lerp(rise, 0f, normalized));
                }
                target.localScale = Vector3.Lerp(baseScale * motion.JumpScaleOvershoot, baseScale, normalized);
                yield return null;
            }

            if (allowPositionAnimation)
            {
                target.anchoredPosition = baseAnchoredPosition;
            }
            target.localScale = baseScale;
        }

        private IEnumerator StickerSlamRoutine()
        {
            if (target == null)
            {
                yield break;
            }

            var motion = UiThemeRuntime.Motion;
            var total = ResolveDuration(motion.StickerSlamSeconds);
            var elapsed = 0f;
            var start = baseScale * 1.2f;
            while (elapsed < total)
            {
                elapsed += Time.unscaledDeltaTime;
                var normalized = Mathf.Clamp01(elapsed / total);
                var eased = 1f - Mathf.Pow(1f - normalized, 3f);
                target.localScale = Vector3.Lerp(start, baseScale, eased);
                yield return null;
            }

            target.localScale = baseScale;
        }

        private IEnumerator SnapToSlotRoutine(Vector2 destination, RectTransform slot)
        {
            if (target == null)
            {
                yield break;
            }

            var motion = UiThemeRuntime.Motion;
            var total = ResolveDuration(motion.SnapToSlotSeconds);
            var elapsed = 0f;
            var startPos = target.anchoredPosition;
            var arc = motion.SnapArcPixels;
            if (!allowPositionAnimation)
            {
                target.localScale = baseScale;
                if (slot != null)
                {
                    var immediateRing = slot.GetComponent<UiMotionController>();
                    immediateRing?.PlayStickerSlam();
                }

                yield break;
            }

            while (elapsed < total)
            {
                elapsed += Time.unscaledDeltaTime;
                var normalized = Mathf.Clamp01(elapsed / total);
                var arcOffset = Mathf.Sin(normalized * Mathf.PI) * arc;
                target.anchoredPosition = Vector2.Lerp(startPos, destination, normalized) + new Vector2(0f, arcOffset);
                yield return null;
            }

            target.anchoredPosition = destination;
            target.localScale = baseScale;
            if (slot != null)
            {
                var ring = slot.GetComponent<UiMotionController>();
                ring?.PlayStickerSlam();
            }
        }

        private void KillIdleTween()
        {
#if EMOJIWAR_DOTWEEN && DOTWEEN_ENABLED
            if (idleTween != null && idleTween.IsActive())
            {
                idleTween.Kill();
            }

            idleTween = null;
#endif
        }

#if EMOJIWAR_DOTWEEN && DOTWEEN_ENABLED
        private void KillActionTweens()
        {
            if (jumpTween != null && jumpTween.IsActive())
            {
                jumpTween.Kill();
            }

            if (slamTween != null && slamTween.IsActive())
            {
                slamTween.Kill();
            }

            if (snapTween != null && snapTween.IsActive())
            {
                snapTween.Kill();
            }

            jumpTween = null;
            slamTween = null;
            snapTween = null;
        }

        private void StartIdleTween()
        {
            KillIdleTween();
            if (target == null)
            {
                return;
            }

            var motion = UiThemeRuntime.Motion;
            var duration = ctaBreathe
                ? motion.CtaBreatheSeconds
                : ResolveDuration(motion.IdleBobSeconds);
            var bob = ctaBreathe ? motion.IdleBobPixels * 0.35f : motion.IdleBobPixels;
            var scaleAmp = ctaBreathe ? motion.CtaBreatheScale : motion.IdleScaleAmount;
            var tilt = idleTilt ? motion.IdleTiltDegrees : 0f;
            var startDelay = StableStagger(0.24f);

            var sequence = DOTween.Sequence();
            sequence.SetUpdate(true);
            if (startDelay > 0f)
            {
                sequence.SetDelay(startDelay);
            }

            if (allowPositionAnimation)
            {
                sequence.Append(target.DOAnchorPosY(baseAnchoredPosition.y + bob, duration * 0.5f).SetEase(Ease.InOutSine));
                sequence.Join(target.DOScale(baseScale * (1f + scaleAmp), duration * 0.5f).SetEase(Ease.InOutSine));
            }
            else
            {
                sequence.Append(target.DOScale(baseScale * (1f + scaleAmp), duration * 0.5f).SetEase(Ease.InOutSine));
            }

            if (idleTilt)
            {
                sequence.Join(target.DOLocalRotateQuaternion(baseRotation * Quaternion.Euler(0f, 0f, tilt), duration * 0.5f).SetEase(Ease.InOutSine));
            }

            if (allowPositionAnimation)
            {
                sequence.Append(target.DOAnchorPosY(baseAnchoredPosition.y - bob, duration).SetEase(Ease.InOutSine));
                sequence.Join(target.DOScale(baseScale * (1f - scaleAmp), duration).SetEase(Ease.InOutSine));
            }
            else
            {
                sequence.Append(target.DOScale(baseScale * (1f - scaleAmp), duration).SetEase(Ease.InOutSine));
            }

            if (idleTilt)
            {
                sequence.Join(target.DOLocalRotateQuaternion(baseRotation * Quaternion.Euler(0f, 0f, -tilt), duration).SetEase(Ease.InOutSine));
            }

            if (allowPositionAnimation)
            {
                sequence.Append(target.DOAnchorPosY(baseAnchoredPosition.y, duration * 0.5f).SetEase(Ease.InOutSine));
                sequence.Join(target.DOScale(baseScale, duration * 0.5f).SetEase(Ease.InOutSine));
            }
            else
            {
                sequence.Append(target.DOScale(baseScale, duration * 0.5f).SetEase(Ease.InOutSine));
            }

            if (idleTilt)
            {
                sequence.Join(target.DOLocalRotateQuaternion(baseRotation, duration * 0.5f).SetEase(Ease.InOutSine));
            }

            sequence.SetLoops(-1, LoopType.Restart);
            idleTween = sequence;
        }

        private void StartJumpTween()
        {
            if (target == null)
            {
                return;
            }

            KillActionTweens();
            var motion = UiThemeRuntime.Motion;
            var total = ResolveDuration(motion.JumpSelectSeconds);
            var rise = 14f;

            var sequence = DOTween.Sequence();
            sequence.SetUpdate(true);
            if (allowPositionAnimation)
            {
                sequence.Append(target.DOAnchorPos(baseAnchoredPosition + new Vector2(0f, rise), total * 0.45f).SetEase(Ease.OutQuad));
                sequence.Join(target.DOScale(baseScale * motion.JumpScaleOvershoot, total * 0.45f).SetEase(Ease.OutBack));
                sequence.Append(target.DOAnchorPos(baseAnchoredPosition, total * 0.55f).SetEase(Ease.InOutSine));
                sequence.Join(target.DOScale(baseScale, total * 0.55f).SetEase(Ease.InOutSine));
            }
            else
            {
                sequence.Append(target.DOScale(baseScale * motion.JumpScaleOvershoot, total * 0.45f).SetEase(Ease.OutBack));
                sequence.Append(target.DOScale(baseScale, total * 0.55f).SetEase(Ease.InOutSine));
            }
            jumpTween = sequence;
        }

        private void StartStickerSlamTween()
        {
            if (target == null)
            {
                return;
            }

            KillActionTweens();
            var motion = UiThemeRuntime.Motion;
            var total = ResolveDuration(motion.StickerSlamSeconds);
            target.localScale = baseScale * 1.2f;
            slamTween = target.DOScale(baseScale, total)
                .SetEase(Ease.OutBack)
                .SetUpdate(true);
        }

        private void StartSnapTween(Vector2 destination, RectTransform slot)
        {
            if (target == null)
            {
                return;
            }

            KillActionTweens();
            var motion = UiThemeRuntime.Motion;
            var total = ResolveDuration(motion.SnapToSlotSeconds);
            var arc = motion.SnapArcPixels;
            var peak = (target.anchoredPosition + destination) * 0.5f + new Vector2(0f, arc);
            if (!allowPositionAnimation)
            {
                if (slot != null)
                {
                    var immediateRing = slot.GetComponent<UiMotionController>();
                    immediateRing?.PlayStickerSlam();
                }

                return;
            }

            var sequence = DOTween.Sequence();
            sequence.SetUpdate(true);
            sequence.Append(target.DOAnchorPos(peak, total * 0.45f).SetEase(Ease.OutQuad));
            sequence.Append(target.DOAnchorPos(destination, total * 0.55f).SetEase(Ease.InQuad));
            sequence.OnComplete(() =>
            {
                target.anchoredPosition = destination;
                target.localScale = baseScale;
                if (slot != null)
                {
                    var ring = slot.GetComponent<UiMotionController>();
                    ring?.PlayStickerSlam();
                }
            });
            snapTween = sequence;
        }
#endif

        private static float ResolveDuration(UiMotionProfile.Range range)
        {
            var min = Mathf.Max(0.01f, range.Min);
            var max = Mathf.Max(min, range.Max);
            return (min + max) * 0.5f;
        }

        private float StableStagger(float maxSeconds)
        {
            if (maxSeconds <= 0f)
            {
                return 0f;
            }

            var hash = Mathf.Abs(gameObject.GetInstanceID()) % 7;
            var normalized = hash / 6f;
            return normalized * maxSeconds;
        }

        private static bool IsLayoutDriven(RectTransform rectTransform)
        {
            if (rectTransform == null)
            {
                return false;
            }

            var parent = rectTransform.parent as RectTransform;
            while (parent != null)
            {
                if (parent.GetComponent<LayoutGroup>() != null)
                {
                    return true;
                }

                parent = parent.parent as RectTransform;
            }

            return false;
        }
    }
}
