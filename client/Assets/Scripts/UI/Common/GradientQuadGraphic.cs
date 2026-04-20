using UnityEngine;
using UnityEngine.UI;

namespace EmojiWar.Client.UI.Common
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GradientQuadGraphic : MaskableGraphic
    {
        [SerializeField] private Color topColor = Color.white;
        [SerializeField] private Color middleColor = Color.white;
        [SerializeField] private Color bottomColor = Color.white;
        [SerializeField] private bool useMiddleBand = true;

        public void SetColors(Color top, Color middle, Color bottom)
        {
            topColor = top;
            middleColor = middle;
            bottomColor = bottom;
            useMiddleBand = true;
            SetVerticesDirty();
        }

        public void SetColors(Color top, Color bottom)
        {
            topColor = top;
            middleColor = Color.Lerp(top, bottom, 0.5f);
            bottomColor = bottom;
            useMiddleBand = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            var rect = GetPixelAdjustedRect();

            if (!useMiddleBand)
            {
                AddVert(vertexHelper, rect.xMin, rect.yMin, bottomColor);
                AddVert(vertexHelper, rect.xMin, rect.yMax, topColor);
                AddVert(vertexHelper, rect.xMax, rect.yMax, topColor);
                AddVert(vertexHelper, rect.xMax, rect.yMin, bottomColor);
                vertexHelper.AddTriangle(0, 1, 2);
                vertexHelper.AddTriangle(2, 3, 0);
                return;
            }

            var yMid = Mathf.Lerp(rect.yMin, rect.yMax, 0.54f);
            AddVert(vertexHelper, rect.xMin, rect.yMin, bottomColor);
            AddVert(vertexHelper, rect.xMin, yMid, middleColor);
            AddVert(vertexHelper, rect.xMin, rect.yMax, topColor);
            AddVert(vertexHelper, rect.xMax, rect.yMax, topColor);
            AddVert(vertexHelper, rect.xMax, yMid, middleColor);
            AddVert(vertexHelper, rect.xMax, rect.yMin, bottomColor);

            vertexHelper.AddTriangle(0, 1, 4);
            vertexHelper.AddTriangle(4, 5, 0);
            vertexHelper.AddTriangle(1, 2, 3);
            vertexHelper.AddTriangle(3, 4, 1);
        }

        private static void AddVert(VertexHelper helper, float x, float y, Color32 color)
        {
            helper.AddVert(new Vector3(x, y, 0f), color, Vector2.zero);
        }
    }
}
