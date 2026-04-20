using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace EmojiWar.Client.UI.Common
{
    /// <summary>
    /// Dependency-free UGUI motion helpers for the Sticker Pop rescue UI.
    /// These map directly to UI moments: card entry, card select, emoji idle,
    /// CTA pulse, ban stamp, formation drop, and screen enter stagger.
    /// </summary>
    public static class NativeMotionKit
    {
        public static Coroutine PopIn(
            MonoBehaviour host,
            RectTransform target,
            CanvasGroup canvasGroup = null,
            float duration = 0.28f,
            float startScale = 0.72f)
        {
            return Run(host, target, "pop-in", PopInRoutine(target, canvasGroup, duration, startScale));
        }

        public static Coroutine PunchScale(
            MonoBehaviour host,
            RectTransform target,
            float punch = 0.16f,
            float duration = 0.20f)
        {
            return Run(host, target, "punch-scale", PunchScaleRoutine(target, punch, duration));
        }

        public static Coroutine IdleBob(
            MonoBehaviour host,
            RectTransform target,
            float amplitude = 8f,
            float duration = 1.25f,
            bool randomPhase = true)
        {
            return Run(host, target, "idle-bob", IdleBobRoutine(target, amplitude, duration, randomPhase));
        }

        public static Coroutine BreatheScale(
            MonoBehaviour host,
            RectTransform target,
            float amount = 0.035f,
            float duration = 1.4f,
            bool randomPhase = true)
        {
            return Run(host, target, "breathe-scale", BreatheScaleRoutine(target, amount, duration, randomPhase));
        }

        public static Coroutine PulseGraphic(
            MonoBehaviour host,
            Graphic graphic,
            Color from,
            Color to,
            float duration = 0.75f)
        {
            if (!CanRun(host) || graphic == null)
            {
                return null;
            }

            return Run(host, graphic.gameObject, "pulse-graphic", PulseGraphicRoutine(graphic, from, to, duration));
        }

        public static Coroutine Shake(
            MonoBehaviour host,
            RectTransform target,
            float strength = 12f,
            float duration = 0.22f)
        {
            return Run(host, target, "shake", ShakeRoutine(target, strength, duration));
        }

        public static Coroutine SlideFadeIn(
            MonoBehaviour host,
            RectTransform target,
            CanvasGroup canvasGroup,
            Vector2 offset,
            float duration = 0.32f)
        {
            return Run(host, target, "slide-fade-in", SlideFadeInRoutine(target, canvasGroup, offset, duration));
        }

        public static Coroutine StampSlam(
            MonoBehaviour host,
            RectTransform target,
            float overscale = 1.22f,
            float duration = 0.24f)
        {
            return Run(host, target, "stamp-slam", StampSlamRoutine(target, overscale, duration));
        }

        public static Coroutine DropIntoSlot(
            MonoBehaviour host,
            RectTransform target,
            Vector2 fromOffset,
            float duration = 0.34f)
        {
            return Run(host, target, "drop-into-slot", DropIntoSlotRoutine(target, fromOffset, duration));
        }

        public static Coroutine StaggerChildrenPopIn(
            MonoBehaviour host,
            Transform parent,
            float delayStep = 0.045f)
        {
            if (!CanRun(host) || parent == null)
            {
                return null;
            }

            return Run(host, parent.gameObject, "stagger-children-pop-in", StaggerChildrenPopInRoutine(host, parent, delayStep));
        }

        public static float EaseOutBack(float t, float overshoot = 1.70158f)
        {
            t = Mathf.Clamp01(t) - 1f;
            return 1f + t * t * ((overshoot + 1f) * t + overshoot);
        }

        public static float EaseOutCubic(float t)
        {
            t = Mathf.Clamp01(t) - 1f;
            return 1f + t * t * t;
        }

        public static float EaseInOutSine(float t)
        {
            return -(Mathf.Cos(Mathf.PI * Mathf.Clamp01(t)) - 1f) * 0.5f;
        }

        private static Coroutine Run(MonoBehaviour host, RectTransform target, string channel, IEnumerator routine)
        {
            if (!CanRun(host) || target == null)
            {
                return null;
            }

            return Run(host, target.gameObject, channel, routine);
        }

        private static Coroutine Run(MonoBehaviour host, GameObject owner, string channel, IEnumerator routine)
        {
            if (!CanRun(host) || owner == null || routine == null)
            {
                return null;
            }

            var tracker = owner.GetComponent<MotionTracker>();
            if (tracker == null)
            {
                tracker = owner.AddComponent<MotionTracker>();
            }

            tracker.StopChannel(channel);
            var coroutine = host.StartCoroutine(TrackRoutine(tracker, channel, routine));
            tracker.Set(channel, host, coroutine);
            return coroutine;
        }

        private static IEnumerator TrackRoutine(MotionTracker tracker, string channel, IEnumerator routine)
        {
            yield return routine;
            if (tracker != null)
            {
                tracker.Clear(channel);
            }
        }

        private static bool CanRun(MonoBehaviour host)
        {
            return host != null && host.isActiveAndEnabled;
        }

        private static IEnumerator PopInRoutine(RectTransform target, CanvasGroup canvasGroup, float duration, float startScale)
        {
            if (target == null)
            {
                yield break;
            }

            var baseScale = target.localScale;
            var safeDuration = Mathf.Max(0.01f, duration);
            var clampedStart = Mathf.Clamp(startScale, 0.1f, 1f);
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }

            target.localScale = baseScale * clampedStart;
            var elapsed = 0f;
            while (elapsed < safeDuration && target != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / safeDuration);
                var scaleT = EaseOutBack(t, 1.35f);
                target.localScale = Vector3.LerpUnclamped(baseScale * clampedStart, baseScale, scaleT);
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = EaseOutCubic(t);
                }

                yield return null;
            }

            if (target != null)
            {
                target.localScale = baseScale;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }
        }

        private static IEnumerator PunchScaleRoutine(RectTransform target, float punch, float duration)
        {
            if (target == null)
            {
                yield break;
            }

            var baseScale = target.localScale;
            var safeDuration = Mathf.Max(0.01f, duration);
            var upDuration = safeDuration * 0.42f;
            var elapsed = 0f;
            while (elapsed < upDuration && target != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / upDuration);
                target.localScale = Vector3.Lerp(baseScale, baseScale * (1f + punch), EaseOutCubic(t));
                yield return null;
            }

            elapsed = 0f;
            var downDuration = Mathf.Max(0.01f, safeDuration - upDuration);
            while (elapsed < downDuration && target != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / downDuration);
                target.localScale = Vector3.Lerp(baseScale * (1f + punch), baseScale, EaseOutBack(t, 1.2f));
                yield return null;
            }

            if (target != null)
            {
                target.localScale = baseScale;
            }
        }

        private static IEnumerator IdleBobRoutine(RectTransform target, float amplitude, float duration, bool randomPhase)
        {
            if (target == null)
            {
                yield break;
            }

            var basePosition = target.anchoredPosition;
            var safeDuration = Mathf.Max(0.1f, duration);
            var phase = randomPhase ? StablePhase(target) : 0f;
            var elapsed = phase * safeDuration;
            while (target != null && target.gameObject.activeInHierarchy)
            {
                elapsed += Time.unscaledDeltaTime;
                var wave = Mathf.Sin((elapsed / safeDuration) * Mathf.PI * 2f);
                target.anchoredPosition = basePosition + new Vector2(0f, wave * amplitude);
                yield return null;
            }

            if (target != null)
            {
                target.anchoredPosition = basePosition;
            }
        }

        private static IEnumerator BreatheScaleRoutine(RectTransform target, float amount, float duration, bool randomPhase)
        {
            if (target == null)
            {
                yield break;
            }

            var baseScale = target.localScale;
            var safeDuration = Mathf.Max(0.1f, duration);
            var phase = randomPhase ? StablePhase(target) : 0f;
            var elapsed = phase * safeDuration;
            while (target != null && target.gameObject.activeInHierarchy)
            {
                elapsed += Time.unscaledDeltaTime;
                var wave = Mathf.Sin((elapsed / safeDuration) * Mathf.PI * 2f);
                target.localScale = baseScale * (1f + wave * amount);
                yield return null;
            }

            if (target != null)
            {
                target.localScale = baseScale;
            }
        }

        private static IEnumerator PulseGraphicRoutine(Graphic graphic, Color from, Color to, float duration)
        {
            var safeDuration = Mathf.Max(0.1f, duration);
            var elapsed = StablePhase(graphic != null ? graphic.rectTransform : null) * safeDuration;
            while (graphic != null && graphic.gameObject.activeInHierarchy)
            {
                elapsed += Time.unscaledDeltaTime;
                var wave = EaseInOutSine((Mathf.Sin((elapsed / safeDuration) * Mathf.PI * 2f) + 1f) * 0.5f);
                graphic.color = Color.Lerp(from, to, wave);
                yield return null;
            }
        }

        private static IEnumerator ShakeRoutine(RectTransform target, float strength, float duration)
        {
            if (target == null)
            {
                yield break;
            }

            var basePosition = target.anchoredPosition;
            var safeDuration = Mathf.Max(0.01f, duration);
            var elapsed = 0f;
            while (elapsed < safeDuration && target != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / safeDuration);
                var falloff = 1f - t;
                var seed = elapsed * 91f;
                var offset = new Vector2(Mathf.Sin(seed) * strength, Mathf.Cos(seed * 1.37f) * strength * 0.55f) * falloff;
                target.anchoredPosition = basePosition + offset;
                yield return null;
            }

            if (target != null)
            {
                target.anchoredPosition = basePosition;
            }
        }

        private static IEnumerator SlideFadeInRoutine(RectTransform target, CanvasGroup canvasGroup, Vector2 offset, float duration)
        {
            if (target == null)
            {
                yield break;
            }

            var basePosition = target.anchoredPosition;
            var start = basePosition + offset;
            var safeDuration = Mathf.Max(0.01f, duration);
            target.anchoredPosition = start;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }

            var elapsed = 0f;
            while (elapsed < safeDuration && target != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = EaseOutCubic(elapsed / safeDuration);
                target.anchoredPosition = Vector2.LerpUnclamped(start, basePosition, t);
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = t;
                }

                yield return null;
            }

            if (target != null)
            {
                target.anchoredPosition = basePosition;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }
        }

        private static IEnumerator StampSlamRoutine(RectTransform target, float overscale, float duration)
        {
            if (target == null)
            {
                yield break;
            }

            var baseScale = target.localScale;
            var safeDuration = Mathf.Max(0.01f, duration);
            target.localScale = baseScale * Mathf.Max(1f, overscale);
            var elapsed = 0f;
            while (elapsed < safeDuration && target != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = EaseOutBack(elapsed / safeDuration, 1.65f);
                target.localScale = Vector3.LerpUnclamped(baseScale * overscale, baseScale, t);
                yield return null;
            }

            if (target != null)
            {
                target.localScale = baseScale;
            }
        }

        private static IEnumerator DropIntoSlotRoutine(RectTransform target, Vector2 fromOffset, float duration)
        {
            if (target == null)
            {
                yield break;
            }

            var basePosition = target.anchoredPosition;
            var baseScale = target.localScale;
            var startPosition = basePosition + fromOffset;
            var safeDuration = Mathf.Max(0.01f, duration);
            target.anchoredPosition = startPosition;
            target.localScale = baseScale * 1.08f;
            var elapsed = 0f;
            while (elapsed < safeDuration && target != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / safeDuration);
                var travel = EaseOutCubic(t);
                var bounce = Mathf.Sin(t * Mathf.PI) * 18f;
                target.anchoredPosition = Vector2.LerpUnclamped(startPosition, basePosition, travel) + new Vector2(0f, bounce);
                target.localScale = Vector3.LerpUnclamped(baseScale * 1.08f, baseScale, EaseOutBack(t, 1.35f));
                yield return null;
            }

            if (target != null)
            {
                target.anchoredPosition = basePosition;
                target.localScale = baseScale;
            }
        }

        private static IEnumerator StaggerChildrenPopInRoutine(MonoBehaviour host, Transform parent, float delayStep)
        {
            if (!CanRun(host) || parent == null)
            {
                yield break;
            }

            var delay = Mathf.Max(0f, delayStep);
            for (var index = 0; index < parent.childCount; index++)
            {
                var child = parent.GetChild(index);
                if (child == null || !child.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var rect = child as RectTransform;
                if (rect == null)
                {
                    continue;
                }

                PopIn(host, rect, child.GetComponent<CanvasGroup>());
                if (delay > 0f)
                {
                    yield return WaitUnscaled(delay);
                }
            }
        }

        private static IEnumerator WaitUnscaled(float seconds)
        {
            var elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private static float StablePhase(RectTransform target)
        {
            if (target == null)
            {
                return 0f;
            }

            var hash = Mathf.Abs((target.name, target.GetSiblingIndex()).GetHashCode()) % 997;
            return hash / 997f;
        }

        private sealed class MotionTracker : MonoBehaviour
        {
            private readonly Dictionary<string, RunningMotion> running = new Dictionary<string, RunningMotion>();

            public void Set(string channel, MonoBehaviour host, Coroutine coroutine)
            {
                if (string.IsNullOrEmpty(channel) || host == null || coroutine == null)
                {
                    return;
                }

                running[channel] = new RunningMotion(host, coroutine);
            }

            public void StopChannel(string channel)
            {
                if (string.IsNullOrEmpty(channel) || !running.TryGetValue(channel, out var motion))
                {
                    return;
                }

                if (motion.Host != null && motion.Coroutine != null)
                {
                    motion.Host.StopCoroutine(motion.Coroutine);
                }

                running.Remove(channel);
            }

            public void Clear(string channel)
            {
                if (!string.IsNullOrEmpty(channel))
                {
                    running.Remove(channel);
                }
            }

            private void OnDisable()
            {
                foreach (var motion in running.Values)
                {
                    if (motion.Host != null && motion.Coroutine != null)
                    {
                        motion.Host.StopCoroutine(motion.Coroutine);
                    }
                }

                running.Clear();
            }
        }

        private readonly struct RunningMotion
        {
            public readonly MonoBehaviour Host;
            public readonly Coroutine Coroutine;

            public RunningMotion(MonoBehaviour host, Coroutine coroutine)
            {
                Host = host;
                Coroutine = coroutine;
            }
        }
    }
}
