using UnityEngine;
using UnityEngine.UI;

namespace EmojiWar.Client.UI.Common
{
    public sealed class DecisiveMomentChip : MonoBehaviour
    {
        [SerializeField] private Text label;
        [SerializeField] private Image background;

        public void Bind(string caption, int index)
        {
            EnsureReferences();
            if (label != null)
            {
                label.text = string.IsNullOrWhiteSpace(caption)
                    ? $"Moment {index + 1}"
                    : $"{index + 1}. {caption}";
                label.fontSize = UiThemeRuntime.Theme.ChipFontSize;
                label.color = Color.white;
            }

            if (background != null)
            {
                var accents = new[]
                {
                    UiThemeRuntime.Theme.ControlAccent,
                    UiThemeRuntime.Theme.AttackAccent,
                    UiThemeRuntime.Theme.BurstAccent,
                    UiThemeRuntime.Theme.SupportAccent,
                    UiThemeRuntime.Theme.RampAccent
                };
                background.color = accents[index % accents.Length] * new Color(1f, 1f, 1f, 0.35f);
            }
        }

        private void EnsureReferences()
        {
            if (background == null)
            {
                background = GetComponent<Image>();
            }

            if (label == null)
            {
                label = GetComponentInChildren<Text>(true);
            }
        }
    }
}
