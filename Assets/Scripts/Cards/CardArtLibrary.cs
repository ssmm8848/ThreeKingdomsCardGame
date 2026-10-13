using UnityEngine;

/// <summary>
/// Provides the small, reusable ink-wash card illustrations cut from the atlas at runtime.
/// Keeping the atlas as one readable texture avoids six separately managed image assets.
/// </summary>
public static class CardArtLibrary
{
    private const int Columns = 2;
    private const int Rows = 3;
    private static readonly Sprite[] CachedSprites = new Sprite[Columns * Rows];
    private static Texture2D atlas;

    public static Sprite GetArtwork(CardData card)
    {
        if (card == null)
        {
            return null;
        }

        if (card.artwork != null)
        {
            return card.artwork;
        }

        int index = GetArtworkIndex(card);
        if (CachedSprites[index] != null)
        {
            return CachedSprites[index];
        }

        if (atlas == null)
        {
            atlas = Resources.Load<Texture2D>("CardArtAtlas_InkWash");
        }

        if (atlas == null)
        {
            return null;
        }

        int column = index % Columns;
        int rowFromTop = index / Columns;
        float cellWidth = atlas.width / (float)Columns;
        float cellHeight = atlas.height / (float)Rows;
        float inset = Mathf.Min(cellWidth, cellHeight) * 0.014f;
        Rect cell = new Rect(
            column * cellWidth + inset,
            atlas.height - (rowFromTop + 1) * cellHeight + inset,
            cellWidth - inset * 2f,
            cellHeight - inset * 2f
        );

        CachedSprites[index] = Sprite.Create(
            atlas,
            cell,
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect
        );
        CachedSprites[index].name = "CardArt_" + index;
        return CachedSprites[index];
    }

    private static int GetArtworkIndex(CardData card)
    {
        switch (card.effectType)
        {
            case CardEffectType.Attack:
                return 0;
            case CardEffectType.FireAttack:
            case CardEffectType.Duel:
            case CardEffectType.BorrowWeapon:
                return 0;
            case CardEffectType.Defense:
                return 1;
            case CardEffectType.Heal:
                return 2;
            case CardEffectType.Armor:
                return 4;
            case CardEffectType.Treasure:
            case CardEffectType.MountPlus:
            case CardEffectType.MountMinus:
                return 5;
            case CardEffectType.Weapon:
                return 0;
        }

        return card.category == CardCategory.Equip ? 5 : 3;
    }
}
