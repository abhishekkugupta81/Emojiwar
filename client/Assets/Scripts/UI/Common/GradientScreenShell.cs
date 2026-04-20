using UnityEngine;

namespace EmojiWar.Client.UI.Common
{
    public sealed class GradientScreenShell : MonoBehaviour
    {
        [SerializeField] private GradientQuadGraphic gradient;

        private void Awake()
        {
            EnsureGradient();
        }

        public void Apply(UiThemeProfile.GradientPair pair, Color middleAccent)
        {
            EnsureGradient();
            if (gradient == null)
            {
                return;
            }

            gradient.SetColors(pair.Top, middleAccent, pair.Bottom);
        }

        public void Apply(Color top, Color middle, Color bottom)
        {
            EnsureGradient();
            if (gradient == null)
            {
                return;
            }

            gradient.SetColors(top, middle, bottom);
        }

        private void EnsureGradient()
        {
            if (gradient != null)
            {
                return;
            }

            gradient = GetComponent<GradientQuadGraphic>();
            if (gradient == null)
            {
                gradient = gameObject.AddComponent<GradientQuadGraphic>();
            }
        }
    }
}
