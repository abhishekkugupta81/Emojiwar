using UnityEngine;

namespace EmojiWar.Client.UI.Common
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class EmojiMotionController : MonoBehaviour
    {
        private UiMotionController motionController;

        private void Awake()
        {
            motionController = GetComponent<UiMotionController>();
            if (motionController == null)
            {
                motionController = gameObject.AddComponent<UiMotionController>();
            }
        }

        public void JumpSelect()
        {
            EnsureMotion().PlayJumpSelect();
        }

        public void StickerSlam()
        {
            EnsureMotion().PlayStickerSlam();
        }

        public void SnapToSlot(Vector2 destination, RectTransform slot)
        {
            EnsureMotion().PlaySnapToSlot(destination, slot);
        }

        private UiMotionController EnsureMotion()
        {
            if (motionController == null)
            {
                motionController = GetComponent<UiMotionController>();
                if (motionController == null)
                {
                    motionController = gameObject.AddComponent<UiMotionController>();
                }
            }

            return motionController;
        }
    }
}
