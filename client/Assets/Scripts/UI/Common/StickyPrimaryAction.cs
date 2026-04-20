using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace EmojiWar.Client.UI.Common
{
    /// <summary>
    /// V2 naming alias for the sticky footer CTA surface.
    /// Existing scenes may still use StickyFooterAction; both are supported.
    /// </summary>
    public sealed class StickyPrimaryAction : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Text label;
        [SerializeField] private Image background;
        [SerializeField] private UiMotionController motionController;

        public void Bind(string cta, bool interactable, UnityAction onClick, bool emphasize = true)
        {
            EnsureReferences();
            if (label != null)
            {
                label.text = cta;
                label.fontSize = UiThemeRuntime.Theme.HeadingFontSize;
                label.color = Color.white;
            }

            if (button != null)
            {
                button.interactable = interactable;
                button.onClick.RemoveAllListeners();
                if (onClick != null)
                {
                    button.onClick.AddListener(onClick);
                }
            }

            if (background != null)
            {
                var color = emphasize
                    ? UiThemeRuntime.Theme.PrimaryCtaColor
                    : UiThemeRuntime.Theme.SecondaryCtaColor;
                background.color = interactable
                    ? color
                    : color * new Color(1f, 1f, 1f, 0.72f);
            }

            if (motionController != null)
            {
                motionController.Configure(
                    enableIdle: interactable,
                    enableCtaBreathe: interactable && emphasize);
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
