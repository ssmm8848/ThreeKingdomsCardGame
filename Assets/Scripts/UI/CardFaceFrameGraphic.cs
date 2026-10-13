using UnityEngine;
using UnityEngine.UI;

/// <summary>Draws the original lacquer-and-bronze trim around the compact hand-card face.</summary>
public class CardFaceFrameGraphic : MaskableGraphic
{
    private const float OuterInset = 2.5f;
    private const float InnerInset = 6.5f;

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        Rect rect = rectTransform.rect;
        Color bronze = color;
        Color inner = new Color(0.83f, 0.68f, 0.39f, 0.7f) * color;

        AddRectFrame(vertexHelper, Inset(rect, OuterInset), 2.2f, bronze);
        AddRectFrame(vertexHelper, Inset(rect, InnerInset), 0.8f, inner);

        float width = rect.width;
        float height = rect.height;
        float left = rect.xMin + width * 0.075f;
        float right = rect.xMax - width * 0.075f;
        float artTop = rect.yMin + height * 0.72f;
        float artBottom = rect.yMin + height * 0.30f;
        AddLine(vertexHelper, new Vector2(left, artTop), new Vector2(right, artTop), 1.1f, inner);
        AddLine(vertexHelper, new Vector2(left, artBottom), new Vector2(right, artBottom), 1.1f, inner);

        AddCorner(vertexHelper, rect.xMin + OuterInset + 2f, rect.yMin + OuterInset + 2f, 1f, 1f, bronze);
        AddCorner(vertexHelper, rect.xMax - OuterInset - 2f, rect.yMin + OuterInset + 2f, -1f, 1f, bronze);
        AddCorner(vertexHelper, rect.xMin + OuterInset + 2f, rect.yMax - OuterInset - 2f, 1f, -1f, bronze);
        AddCorner(vertexHelper, rect.xMax - OuterInset - 2f, rect.yMax - OuterInset - 2f, -1f, -1f, bronze);
    }

    private static Rect Inset(Rect rect, float inset)
    {
        rect.xMin += inset;
        rect.xMax -= inset;
        rect.yMin += inset;
        rect.yMax -= inset;
        return rect;
    }

    private static void AddRectFrame(VertexHelper vh, Rect rect, float thickness, Color tint)
    {
        AddLine(vh, new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMax, rect.yMin), thickness, tint);
        AddLine(vh, new Vector2(rect.xMax, rect.yMin), new Vector2(rect.xMax, rect.yMax), thickness, tint);
        AddLine(vh, new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMin, rect.yMax), thickness, tint);
        AddLine(vh, new Vector2(rect.xMin, rect.yMax), new Vector2(rect.xMin, rect.yMin), thickness, tint);
    }

    private static void AddCorner(VertexHelper vh, float x, float y, float xDirection, float yDirection, Color tint)
    {
        AddLine(vh, new Vector2(x, y + yDirection * 3f), new Vector2(x, y + yDirection * 13f), 1.2f, tint);
        AddLine(vh, new Vector2(x + xDirection * 3f, y), new Vector2(x + xDirection * 13f, y), 1.2f, tint);
        AddLine(
            vh,
            new Vector2(x + xDirection * 4f, y + yDirection * 4f),
            new Vector2(x + xDirection * 9f, y + yDirection * 9f),
            0.9f,
            tint
        );
        AddDiamond(vh, new Vector2(x + xDirection * 4f, y + yDirection * 4f), 2.1f, tint);
    }

    private static void AddDiamond(VertexHelper vh, Vector2 center, float radius, Color tint)
    {
        int first = vh.currentVertCount;
        vh.AddVert(center + Vector2.up * radius, tint, Vector2.zero);
        vh.AddVert(center + Vector2.right * radius, tint, Vector2.zero);
        vh.AddVert(center + Vector2.down * radius, tint, Vector2.zero);
        vh.AddVert(center + Vector2.left * radius, tint, Vector2.zero);
        vh.AddTriangle(first, first + 1, first + 2);
        vh.AddTriangle(first, first + 2, first + 3);
    }

    private static void AddLine(VertexHelper vh, Vector2 from, Vector2 to, float width, Color tint)
    {
        Vector2 direction = to - from;
        if (direction.sqrMagnitude < 0.01f)
        {
            return;
        }

        Vector2 side = new Vector2(-direction.y, direction.x).normalized * width * 0.5f;
        int first = vh.currentVertCount;
        vh.AddVert(from - side, tint, Vector2.zero);
        vh.AddVert(from + side, tint, Vector2.zero);
        vh.AddVert(to + side, tint, Vector2.zero);
        vh.AddVert(to - side, tint, Vector2.zero);
        vh.AddTriangle(first, first + 1, first + 2);
        vh.AddTriangle(first, first + 2, first + 3);
    }
}
