using UnityEngine;
using UnityEngine.UI;

/// <summary>Two tapered light trails used instead of sending the physical Sha card.</summary>
public sealed class AttackSlashGraphic : MaskableGraphic
{
    public Color coreColor = new Color(1f, 0.91f, 0.68f, 0.96f);
    public Color trailColor = new Color(0.82f, 0.19f, 0.12f, 0.38f);

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        Rect rect = rectTransform.rect;
        if (rect.width <= 0f || rect.height <= 0f)
        {
            return;
        }

        Vector2 mainStart = new Vector2(rect.xMin + rect.width * 0.08f, rect.yMin + rect.height * 0.2f);
        Vector2 mainEnd = new Vector2(rect.xMax - rect.width * 0.08f, rect.yMax - rect.height * 0.16f);
        AddTaperedTrail(vertexHelper, mainStart, mainEnd, 8f, 0.6f, trailColor, coreColor);

        Color echoColor = trailColor;
        echoColor.a *= 0.55f;
        Vector2 echoStart = new Vector2(rect.xMin + rect.width * 0.22f, rect.yMin + rect.height * 0.05f);
        Vector2 echoEnd = new Vector2(rect.xMax - rect.width * 0.08f, rect.yMax - rect.height * 0.38f);
        AddTaperedTrail(vertexHelper, echoStart, echoEnd, 2.6f, 0.3f, echoColor, coreColor);
    }

    private static void AddTaperedTrail(
        VertexHelper vertexHelper,
        Vector2 start,
        Vector2 end,
        float startWidth,
        float endWidth,
        Color startColor,
        Color endColor
    )
    {
        Vector2 direction = (end - start).normalized;
        Vector2 side = new Vector2(-direction.y, direction.x);
        int first = vertexHelper.currentVertCount;
        AddVertex(vertexHelper, start - side * startWidth * 0.5f, startColor);
        AddVertex(vertexHelper, start + side * startWidth * 0.5f, startColor);
        AddVertex(vertexHelper, end + side * endWidth * 0.5f, endColor);
        AddVertex(vertexHelper, end - side * endWidth * 0.5f, endColor);
        vertexHelper.AddTriangle(first, first + 1, first + 2);
        vertexHelper.AddTriangle(first, first + 2, first + 3);
    }

    private static void AddVertex(VertexHelper vertexHelper, Vector2 point, Color color)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = point;
        vertex.color = color;
        vertexHelper.AddVert(vertex);
    }
}
