using UnityEngine;
using UnityEngine.UI;

namespace EmojiWar.Client.UI.Common
{
    public sealed class PhaseBar : MonoBehaviour
    {
        [SerializeField] private Text label;
        [SerializeField] private Image background;
        [SerializeField] private PhaseStep currentStep = PhaseStep.Squad;
        [SerializeField] private UiMotionController motionController;
        private PhaseStep lastRenderedStep = (PhaseStep)(-1);

        public void SetStep(PhaseStep step)
        {
            currentStep = step;
            Refresh();
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void Refresh()
        {
            EnsureReferences();
            if (label == null)
            {
                return;
            }

            label.text = EmojiUiFormatter.BuildPhaseBar(currentStep);
            label.fontSize = Mathf.Max(16, UiThemeRuntime.Theme.ChipFontSize + 1);
            label.color = Color.white;
            label.resizeTextForBestFit = false;
            if (background != null)
            {
                background.color = currentStep switch
                {
                    PhaseStep.Ban => UiThemeRuntime.Theme.BanGradient.Top * new Color(1f, 1f, 1f, 0.72f),
                    PhaseStep.Formation => UiThemeRuntime.Theme.FormationGradient.Top * new Color(1f, 1f, 1f, 0.72f),
                    PhaseStep.Result => UiThemeRuntime.Theme.ResultGradient.Top * new Color(1f, 1f, 1f, 0.72f),
                    _ => UiThemeRuntime.Theme.SurfaceColor
                };
            }

            if (motionController != null)
            {
                motionController.Configure(enableIdle: false, enableCtaBreathe: false);
                if (lastRenderedStep != currentStep)
                {
                    motionController.PlayJumpSelect();
                }
            }

            lastRenderedStep = currentStep;
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

            if (motionController == null)
            {
                motionController = GetComponent<UiMotionController>();
                if (motionController == null)
                {
                    motionController = gameObject.AddComponent<UiMotionController>();
                }
            }
        }
    }
}
