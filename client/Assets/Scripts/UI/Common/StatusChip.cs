using UnityEngine;
using UnityEngine.UI;

namespace EmojiWar.Client.UI.Common
{
    public sealed class StatusChip : MonoBehaviour
    {
        [SerializeField] private Text label;
        [SerializeField] private Image background;
        [SerializeField] private MatchUiPanelState panelState = MatchUiPanelState.Waiting;

        public void SetStatus(string text)
        {
            EnsureReferences();
            if (label == null)
            {
                return;
            }

            label.text = EmojiUiFormatter.BuildStatusChip(text);
            label.fontSize = Mathf.Max(16, UiThemeRuntime.Theme.ChipFontSize + 1);
            label.resizeTextForBestFit = false;
            if (background != null)
            {
                background.color = UiThemeRuntime.ResolvePanelTop(panelState) * new Color(1f, 1f, 1f, 0.82f);
            }
            gameObject.SetActive(!string.IsNullOrWhiteSpace(text));
        }

        public void SetPanelState(MatchUiPanelState state)
        {
            panelState = state;
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
