using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace EmojiWar.Client.UI.Common
{
    public sealed class StickyFooterAction : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Text label;
        [SerializeField] private Image background;
        [SerializeField] private UiMotionController motionController;

        public void Bind(string cta, bool interactable, UnityAction onClick)
        {
            EnsureReferences();
            if (label != null)
            {
                label.text = cta;
                label.fontSize = UiThemeRuntime.Theme.HeadingFontSize;
            }

            if (button == null)
            {
                return;
            }

            button.interactable = interactable;
            button.onClick.RemoveAllListeners();
            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }

            if (background != null)
            {
                background.color = interactable
                    ? UiThemeRuntime.Theme.PrimaryCtaColor
                    : UiThemeRuntime.Theme.SecondaryCtaColor * new Color(1f, 1f, 1f, 0.75f);
            }

            if (motionController != null)
            {
                motionController.Configure(
                    enableIdle: interactable,
                    enableCtaBreathe: interactable);
            }
        }

        private void EnsureReferences()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }

            if (label == null)
            {
                label = GetComponentInChildren<Text>(true);
            }

            if (background == null)
            {
                background = GetComponent<Image>();
            }

            if (motionController == null)
            {
                motionController = GetComponent<UiMotionController>();
            }
        }
    }
}
