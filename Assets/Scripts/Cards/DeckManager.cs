using System.Collections.Generic;
using UnityEngine;

public class DeckManager : MonoBehaviour
{
    [Header("旧场景里的原创牌模板")]
    [Tooltip("标准/EX/军争160张由固定牌表生成；此列表只收集不在官方牌表内的旧原创牌。")]
    public List<CardData> startingDeck = new List<CardData>();

    [Header("运行时牌堆")]
    public List<CardData> deck = new List<CardData>();

    [Header("弃牌堆")]
    public List<CardData> discardPile = new List<CardData>();

    private readonly List<CardData> runtimeCards = new List<CardData>();

    private struct CardFace
    {
        public CardSuit suit;
        public int rank;

        public CardFace(CardSuit cardSuit, int cardRank)
        {
            suit = cardSuit;
            rank = cardRank;
        }
    }

    [Header("抽牌时打印日志（默认关闭，Player.DrawCard 已有同样日志）")]
    public bool logEveryDraw = false;

    void Start()
    {
        //InitializeDeck();
    }

    public void InitializeDeck()
    {
        DestroyRuntimeCards();
        deck.Clear();
        discardPile.Clear();

        List<CardData> officialCards = OfficialDeckCatalog.CreateStandardMilitaryDeck();
        for (int i = 0; i < officialCards.Count; i++)
        {
            CardData officialCard = officialCards[i];
            if (officialCard == null)
            {
                continue;
            }

            runtimeCards.Add(officialCard);
            deck.Add(officialCard);
        }

        int customCardCount = 0;
        if (GameSelectionData.customExpansionEnabled)
        {
            List<CardData> customTemplates = new List<CardData>();
            AddLegacyCustomCards(customTemplates);
            AddSupplementalCards(customTemplates);
            AddCustomRuntimeCards(customTemplates);
            customCardCount = customTemplates.Count;
        }

        Shuffle();

        Debug.Log(
            "牌堆初始化完成：标准/EX/军争 "
                + officialCards.Count
                + " 张，原创扩展 "
                + customCardCount
                + " 张，总计 "
                + deck.Count
                + " 张"
        );
    }

    private void AddLegacyCustomCards(List<CardData> templates)
    {
        foreach (CardData card in startingDeck)
        {
            if (card != null && !OfficialDeckCatalog.IsOfficialCardName(card.cardName))
            {
                templates.Add(card);
            }
        }
    }

    private void AddSupplementalCards(List<CardData> templates)
    {
        CardData[] supplementalCards = Resources.LoadAll<CardData>("SupplementalCards");
        System.Array.Sort(supplementalCards, (left, right) =>
            string.Compare(
                left != null ? left.name : string.Empty,
                right != null ? right.name : string.Empty,
                System.StringComparison.Ordinal
            )
        );
        foreach (CardData card in supplementalCards)
        {
            if (card == null || (card.fivePlayerOnly && GameSelectionData.gameMode != GameMode.FivePlayer))
            {
                continue;
            }

            int copies = Mathf.Max(0, card.supplementalCopies);
            for (int i = 0; i < copies; i++)
            {
                templates.Add(card);
            }
        }
    }

    private void AddCustomRuntimeCards(List<CardData> templates)
    {
        List<CardFace> faces = CreateShuffledCardFaces(templates.Count);
        for (int i = 0; i < templates.Count; i++)
        {
            CardData template = templates[i];
            if (template == null)
            {
                continue;
            }

            CardData runtimeCard = Instantiate(template);
            runtimeCard.name = template.name + "_Runtime";
            runtimeCard.runtimeTemplate = template;
            runtimeCard.suit = faces[i].suit;
            runtimeCard.rank = faces[i].rank;
            runtimeCard.isOfficialPackageCard = false;
            runtimeCard.cardPackage = "本作原创扩展";
            runtimeCards.Add(runtimeCard);
            deck.Add(runtimeCard);
        }
    }

    private List<CardFace> CreateShuffledCardFaces(int count)
    {
        List<CardFace> faces = new List<CardFace>(count);
        CardSuit[] suits = { CardSuit.Spade, CardSuit.Heart, CardSuit.Club, CardSuit.Diamond };
        // Printed faces stay stable across matches; only card draw order is shuffled.
        System.Random faceRandom = new System.Random(0x4F43494E);

        while (faces.Count < count)
        {
            List<CardFace> pack = new List<CardFace>(52);
            foreach (CardSuit suit in suits)
            {
                for (int rank = 1; rank <= 13; rank++)
                {
                    pack.Add(new CardFace(suit, rank));
                }
            }

            for (int i = pack.Count - 1; i > 0; i--)
            {
                int randomIndex = faceRandom.Next(0, i + 1);
                CardFace temporary = pack[i];
                pack[i] = pack[randomIndex];
                pack[randomIndex] = temporary;
            }

            int remaining = count - faces.Count;
            int takeCount = Mathf.Min(remaining, pack.Count);
            faces.AddRange(pack.GetRange(0, takeCount));
        }

        return faces;
    }

    private void DestroyRuntimeCards()
    {
        for (int i = 0; i < runtimeCards.Count; i++)
        {
            if (runtimeCards[i] != null)
            {
                Destroy(runtimeCards[i]);
            }
        }

        runtimeCards.Clear();
    }

    private void OnDestroy()
    {
        DestroyRuntimeCards();
    }

    public void Shuffle()
    {
        for (int i = 0; i < deck.Count; i++)
        {
            int randomIndex = Random.Range(i, deck.Count);

            CardData temp = deck[i];
            deck[i] = deck[randomIndex];
            deck[randomIndex] = temp;
        }

        Debug.Log("牌堆已洗牌，当前牌堆数量：" + deck.Count);
    }

    public CardData Draw()
    {
        if (deck.Count == 0)
        {
            RecycleDiscardPile();
        }

        if (deck.Count == 0)
        {
            Debug.LogWarning("牌堆为空，无法抽牌");
            return null;
        }

        // 从末尾取牌：RemoveAt(0) 每次都要搬移整个 List 的元素，
        // 而洗牌本身是均匀的，从哪一端取完全等价。
        int lastIndex = deck.Count - 1;
        CardData card = deck[lastIndex];
        deck.RemoveAt(lastIndex);

        if (logEveryDraw)
        {
            Debug.Log("抽到了一张牌：" + card.cardName);
        }

        return card;
    }

    public void Discard(CardData card)
    {
        if (card == null)
        {
            return;
        }

        discardPile.Add(card);

        Debug.Log("一张牌进入弃牌堆：" + card.cardName);
    }

    private void RecycleDiscardPile()
    {
        if (discardPile.Count == 0)
        {
            Debug.LogWarning("弃牌堆也为空，无法重洗");
            return;
        }

        deck.AddRange(discardPile);
        discardPile.Clear();

        Shuffle();

        Debug.Log("弃牌堆已洗回牌堆");
    }
}
