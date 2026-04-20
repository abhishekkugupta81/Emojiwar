using UnityEngine;

namespace EmojiWar.Client.UI.Common
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class EmojiIdleMotion : MonoBehaviour
    {
        [SerializeField] private bool tilt = true;
        [SerializeField] private bool ctaBreathe;

        private UiMotionController motionController;

        private void Awake()
        {
            motionController = GetComponent<UiMotionController>();
            if (motionController == null)
            {
                motionController = gameObject.AddComponent<UiMotionController>();
            }
        }

        private void OnEnable()
        {
            motionController.Configure(!ctaBreathe, ctaBreathe, tilt);
        }

        public void Configure(bool enableTilt, bool enableCtaBreathe = false)
        {
            tilt = enableTilt;
            ctaBreathe = enableCtaBreathe;
            if (motionController == null)
            {
                motionController = GetComponent<UiMotionController>();
                if (motionController == null)
                {
                    motionController = gameObject.AddComponent<UiMotionController>();
                }
            }

            motionController.Configure(!ctaBreathe, ctaBreathe, tilt);
        }
    }
}
