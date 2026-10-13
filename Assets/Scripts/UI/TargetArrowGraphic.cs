using UnityEngine;
using UnityEngine.UI;

/// <summary>Draws a curved, filled arrow in canvas-local coordinates.</summary>
public class TargetArrowGraphic : MaskableGraphic
{
    private const int CurveSegments = 16;
    private Vector2 startPoint;
    private Vector2 endPoint;
    private bool hasPath;

    public float lineWidth = 4f;
    public float arrowLength = 19f;
    public float arrowWidth = 19f;
    public float curveHeightScale = 0.07f;

    public void SetPath(Vector2 start, Vector2 end)
    {
        startPoint = start;
        endPoint = end;
        hasPath = true;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        if (!hasPath)
        {
            return;
        }

        Vector2 direction = endPoint - startPoint;
        float distance = direction.magnitude;
        if (distance < 1f)
        {
            return;
        }

        Vector2 control = (startPoint + endPoint) * 0.5f
            + Vector2.up * Mathf.Clamp(distance * curveHeightScale, 18f, 68f);
        Vector2 previous = startPoint;
        Vector2 beforeEnd = startPoint;

        for (int i = 1; i <= CurveSegments; i++)
        {
            float t = i / (float)CurveSegments;
            Vector2 point = (1f - t) * (1f - t) * startPoint
                + 2f * (1f - t) * t * control
                + t * t * endPoint;
            if (i == CurveSegments)
            {
                beforeEnd = previous;
            }

            float segmentStart = (i - 1) / (float)CurveSegments;
            float segmentEnd = i / (float)CurveSegments;
            Color segmentColor = color;
            segmentColor.a *= Mathf.Lerp(0.55f, 1f, segmentStart);
            AddQuad(
                vertexHelper,
                previous,
                point,
                Mathf.Lerp(lineWidth, lineWidth * 0.7f, (segmentStart + segmentEnd) * 0.5f),
                segmentColor
            );
            previous = point;
        }

        Vector2 arrowDirection = (endPoint - beforeEnd).normalized;
        float headLength = Mathf.Min(arrowLength, distance * 0.38f);
        float headWidth = Mathf.Min(arrowWidth, distance * 0.42f);
        Vector2 arrowBase = endPoint - arrowDirection * headLength;
        Vector2 side = new Vector2(-arrowDirection.y, arrowDirection.x) * headWidth * 0.5f;
        int first = vertexHelper.currentVertCount;
        vertexHelper.AddVert(endPoint, color, Vector2.zero);
        vertexHelper.AddVert(arrowBase + side, color, Vector2.zero);
        vertexHelper.AddVert(arrowBase - side, color, Vector2.zero);
        vertexHelper.AddTriangle(first, first + 1, first + 2);
    }

    private static void AddQuad(VertexHelper vertexHelper, Vector2 from, Vector2 to, float width, Color tint)
    {
        Vector2 direction = to - from;
        if (direction.sqrMagnitude < 0.01f)
        {
            return;
        }

        Vector2 side = new Vector2(-direction.y, direction.x).normalized * width * 0.5f;
        int first = vertexHelper.currentVertCount;
        vertexHelper.AddVert(from - side, tint, Vector2.zero);
        vertexHelper.AddVert(from + side, tint, Vector2.zero);
        vertexHelper.AddVert(to + side, tint, Vector2.zero);
        vertexHelper.AddVert(to - side, tint, Vector2.zero);
        vertexHelper.AddTriangle(first, first + 1, first + 2);
        vertexHelper.AddTriangle(first, first + 2, first + 3);
    }
}
