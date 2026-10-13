using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Draws a restrained lacquer-and-brass surface for runtime UI buttons.</summary>
public sealed class GameUIButtonChrome : MaskableGraphic
{
    private const int SegmentsPerCorner = 7;
    private const float CornerRadius = 12f;

    private readonly List<Vector2> outerPoints = new List<Vector2>(32);
    private readonly List<Vector2> innerPoints = new List<Vector2>(32);

    private Color normalColor;
    private Color hoverColor;
    private Color pressedColor;
    private Color trimColor;
    private ButtonVisualState visualState;
    private bool interactable = true;

    private enum ButtonVisualState
    {
        Normal,
        Hover,
        Pressed,
    }

    public void Configure(Color normal, Color hover, Color pressed, Color trim)
    {
        normalColor = normal;
        hoverColor = hover;
        pressedColor = pressed;
        trimColor = trim;
        visualState = ButtonVisualState.Normal;
        interactable = true;
        color = Color.white;
        raycastTarget = false;
        SetVerticesDirty();
    }

    public void SetHovered(bool hovered)
    {
        visualState = interactable && hovered ? ButtonVisualState.Hover : ButtonVisualState.Normal;
        SetVerticesDirty();
    }

    public void SetPressed(bool pressed)
    {
        visualState = !interactable
            ? ButtonVisualState.Normal
            : pressed ? ButtonVisualState.Pressed : ButtonVisualState.Hover;
        SetVerticesDirty();
    }

    public void SetInteractable(bool value)
    {
        interactable = value;
        if (!interactable)
        {
            visualState = ButtonVisualState.Normal;
        }
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        Rect rect = rectTransform.rect;
        if (rect.width <= 0f || rect.height <= 0f)
        {
            return;
        }

        Color surface = GetSurfaceColor();
        Color top = Color.Lerp(surface, Color.white, visualState == ButtonVisualState.Hover ? 0.16f : 0.09f);
        Color bottom = Color.Lerp(surface, Color.black, 0.2f);
        Color trim = visualState == ButtonVisualState.Normal
            ? Color.Lerp(trimColor, surface, 0.18f)
            : Color.Lerp(trimColor, Color.white, visualState == ButtonVisualState.Hover ? 0.32f : 0.12f);
        if (!interactable)
        {
            trim = Color.Lerp(trim, new Color(0.38f, 0.42f, 0.44f, 0.52f), 0.72f);
        }

        float radius = Mathf.Min(CornerRadius, rect.height * 0.22f, rect.width * 0.12f);
        AddRoundedRectPoints(outerPoints, rect, radius);
        AddSurface(vertexHelper, rect, top, bottom);
        AddBorder(vertexHelper, rect, radius, trim);
        AddEndMark(vertexHelper, new Vector2(rect.xMin + 17f, 0f), 3.2f, trim);
        AddEndMark(vertexHelper, new Vector2(rect.xMax - 17f, 0f), 3.2f, trim);
        AddTopGlint(vertexHelper, rect, radius, trim);
    }

    private Color GetSurfaceColor()
    {
        if (!interactable)
        {
            return Color.Lerp(normalColor, new Color(0.08f, 0.10f, 0.12f, 1f), 0.58f);
        }

        switch (visualState)
        {
            case ButtonVisualState.Hover:
                return hoverColor;
            case ButtonVisualState.Pressed:
                return pressedColor;
            default:
                return normalColor;
        }
    }

    private void AddSurface(VertexHelper vh, Rect rect, Color top, Color bottom)
    {
        AddVert(vh, Vector2.zero, Color.Lerp(top, bottom, 0.5f));
        for (int i = 0; i < outerPoints.Count; i++)
        {
            Vector2 point = outerPoints[i];
            float t = Mathf.InverseLerp(rect.yMin, rect.yMax, point.y);
            AddVert(vh, point, Color.Lerp(bottom, top, t));
        }

        int centerIndex = 0;
        for (int i = 0; i < outerPoints.Count; i++)
        {
            int current = i + 1;
            int next = (i + 1) % outerPoints.Count + 1;
            vh.AddTriangle(centerIndex, current, next);
        }
    }

    private void AddBorder(VertexHelper vh, Rect rect, float radius, Color trim)
    {
        float thickness = Mathf.Clamp(Mathf.Min(rect.height, rect.width) * 0.035f, 1.4f, 2.4f);
        Rect innerRect = new Rect(
            rect.xMin + thickness,
            rect.yMin + thickness,
            rect.width - thickness * 2f,
            rect.height - thickness * 2f
        );
        AddRoundedRectPoints(innerPoints, innerRect, Mathf.Max(1f, radius - thickness));

        int outerStart = vh.currentVertCount;
        for (int i = 0; i < outerPoints.Count; i++)
        {
            AddVert(vh, outerPoints[i], trim);
        }

        int innerStart = vh.currentVertCount;
        for (int i = 0; i < innerPoints.Count; i++)
        {
            float t = Mathf.InverseLerp(rect.yMin, rect.yMax, innerPoints[i].y);
            Color innerTrim = Color.Lerp(trim, GetSurfaceAt(t), 0.45f);
            AddVert(vh, innerPoints[i], innerTrim);
        }

        for (int i = 0; i < outerPoints.Count; i++)
        {
            int next = (i + 1) % outerPoints.Count;
            vh.AddTriangle(outerStart + i, outerStart + next, innerStart + next);
            vh.AddTriangle(outerStart + i, innerStart + next, innerStart + i);
        }
    }

    private void AddEndMark(VertexHelper vh, Vector2 center, float halfSize, Color markColor)
    {
        int start = vh.currentVertCount;
        AddVert(vh, center + Vector2.up * halfSize, markColor);
        AddVert(vh, center + Vector2.right * halfSize, markColor);
        AddVert(vh, center + Vector2.down * halfSize, markColor);
        AddVert(vh, center + Vector2.left * halfSize, markColor);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }

    private void AddTopGlint(VertexHelper vh, Rect rect, float radius, Color trim)
    {
        float left = rect.xMin + radius + 12f;
        float right = rect.xMax - radius - 12f;
        if (right <= left)
        {
            return;
        }

        Color glint = new Color(trim.r, trim.g, trim.b, visualState == ButtonVisualState.Hover ? 0.72f : 0.38f);
        float top = rect.yMax - 2f;
        int start = vh.currentVertCount;
        AddVert(vh, new Vector2(left, top), glint);
        AddVert(vh, new Vector2(right, top), glint);
        AddVert(vh, new Vector2(right, top - 1.1f), glint);
        AddVert(vh, new Vector2(left, top - 1.1f), glint);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }

    private Color GetSurfaceAt(float t)
    {
        Color surface = GetSurfaceColor();
        Color bottom = Color.Lerp(surface, Color.black, 0.2f);
        Color top = Color.Lerp(surface, Color.white, visualState == ButtonVisualState.Hover ? 0.16f : 0.09f);
        return Color.Lerp(bottom, top, t);
    }

    private static void AddRoundedRectPoints(List<Vector2> points, Rect rect, float radius)
    {
        points.Clear();
        radius = Mathf.Clamp(radius, 0f, Mathf.Min(rect.width, rect.height) * 0.5f);

        Vector2[] centers =
        {
            new Vector2(rect.xMax - radius, rect.yMax - radius),
            new Vector2(rect.xMin + radius, rect.yMax - radius),
            new Vector2(rect.xMin + radius, rect.yMin + radius),
            new Vector2(rect.xMax - radius, rect.yMin + radius),
        };
        float[] startAngles = { 0f, 90f, 180f, 270f };

        for (int corner = 0; corner < centers.Length; corner++)
        {
            for (int segment = 0; segment <= SegmentsPerCorner; segment++)
            {
                float angle = (startAngles[corner] + 90f * segment / SegmentsPerCorner) * Mathf.Deg2Rad;
                points.Add(centers[corner] + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
        }
    }

    private static void AddVert(VertexHelper vh, Vector2 point, Color color)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = point;
        vertex.color = color;
        vh.AddVert(vertex);
    }
}
