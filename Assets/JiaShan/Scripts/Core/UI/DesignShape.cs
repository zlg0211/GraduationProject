using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace JiaShan.UI
{
    /// <summary>将设计稿中的圆角矩形、圆和图标路径绘制为可编辑 UGUI 图形。</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DesignShape : MaskableGraphic
    {
        [Header("形状与描边")]
        public string Kind = "box";
        public float Radius;
        public float StrokeWidth;
        public Color Fill = Color.white;
        public Color Stroke = Color.clear;
        public Vector2[] Points;
        public bool Closed;

        /// <summary>构建填充与描边，坐标使用当前 RectTransform 的局部空间。</summary>
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            List<Vector2> outline = new List<Vector2>();
            Rect rect = rectTransform.rect;
            if (Kind == "path")
            {
                if (Points != null) outline.AddRange(Points);
            }
            else if (Kind == "circle")
            {
                for (int i = 0; i < 64; i++)
                {
                    float angle = i * Mathf.PI * 2 / 64;
                    outline.Add(rect.center + new Vector2(Mathf.Cos(angle) * rect.width / 2, Mathf.Sin(angle) * rect.height / 2));
                }
            }
            else
            {
                float radius = Mathf.Min(Radius, Mathf.Min(rect.width, rect.height) / 2);
                Vector2[] centers = { new Vector2(rect.xMax - radius, rect.yMax - radius), new Vector2(rect.xMin + radius, rect.yMax - radius), new Vector2(rect.xMin + radius, rect.yMin + radius), new Vector2(rect.xMax - radius, rect.yMin + radius) };
                for (int corner = 0; corner < 4; corner++)
                    for (int j = 0; j <= 12; j++)
                    {
                        float angle = (corner * 90 + j * 90f / 12) * Mathf.Deg2Rad;
                        outline.Add(centers[corner] + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
                    }
            }
            if (outline.Count < 2) return;
            if (Fill.a > 0 && outline.Count > 2)
            {
                Vector2 center = Vector2.zero;
                foreach (Vector2 point in outline) center += point;
                center /= outline.Count;
                int start = vh.currentVertCount;
                vh.AddVert(center, Fill * color, Vector2.zero);
                foreach (Vector2 point in outline) vh.AddVert(point, Fill * color, Vector2.zero);
                for (int i = 0; i < outline.Count; i++) vh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % outline.Count);
            }
            if (Stroke.a <= 0 || StrokeWidth <= 0) return;
            int segments = Kind != "path" || Closed ? outline.Count : outline.Count - 1;
            for (int i = 0; i < segments; i++)
            {
                Vector2 a = outline[i], b = outline[(i + 1) % outline.Count];
                Vector2 normal = new Vector2(-(b - a).y, (b - a).x).normalized * StrokeWidth / 2;
                int start = vh.currentVertCount;
                vh.AddVert(a + normal, Stroke * color, Vector2.zero);
                vh.AddVert(a - normal, Stroke * color, Vector2.zero);
                vh.AddVert(b - normal, Stroke * color, Vector2.zero);
                vh.AddVert(b + normal, Stroke * color, Vector2.zero);
                vh.AddTriangle(start, start + 1, start + 2);
                vh.AddTriangle(start, start + 2, start + 3);
            }
        }
    }
}
