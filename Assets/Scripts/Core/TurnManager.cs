using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum TurnPhase
{
    Draw,
    Play,
    Response,
    Dying,
    Discard,
    End,
}

public class TurnManager : MonoBehaviour
{
    [Header("玩家")]
    public Player player;

    [Header("敌人")]
    public Player enemy;

    [Header("三人场敌人B")]
    public Player enemyB;

    [Header("五人场敌人C")]
    public Player enemyC;

    [Header("五人场敌人D")]
    public Player enemyD;

    [Header("所有角色")]
    public List<Player> players = new List<Player>();

    [Header("当前行动角色索引")]
    public int currentPlayerIndex = 0;

    [Header("牌堆管理器")]
    public DeckManager deckManager;

    [Header("战斗UI")]
    public BattleUI battleUI;

    [Header("当前回合信息")]
    public Player currentPlayer;
    public TurnPhase currentPhase;

    [Header("回合设置")]
    public float turnDelay = 1f;

    [Header("响应信息")]
    public Player pendingAttacker;
    public Player pendingDefender;
    public Player dyingPlayer;

    public CardEffectType pendingResponseCardType { get; private set; } = CardEffectType.Defense;

    public string PendingResponseCardName
    {
        get
        {
            switch (pendingResponseCardType)
            {
                case CardEffectType.Attack: return "杀";
                case CardEffectType.Defense: return "闪";
                case CardEffectType.Nullification: return "无懈可击";
                default: return "响应牌";
            }
        }
    }

    private int turnCount = 1;
    private bool gameOver = false;
    private readonly List<string> matchHighlights = new List<string>();

    private bool playerDiscardFinished = false;

    private bool playerResponseFinished = false;
    private bool playerUsedDefense = false;

    private bool playerDyingChoiceFinished = false;
    private bool playerUsedPeach = false;

    void Start()
    {
        //StartCoroutine(GameLoop());
    }

    public void StartGame()
    {
        StopAllCoroutines();

        if (players == null)
        {
            players = new List<Player>();
        }

        // 每次开始游戏都清空重建，避免重新开局时残留旧数据
        players.Clear();

        if (player != null)
            players.Add(player);
        if (enemy != null)
            players.Add(enemy);

        // 三人场：将敌人B也加入角色列表
        if (enemyB != null && GameSelectionData.gameMode == GameMode.ThreePlayer)
        {
            players.Add(enemyB);
        }

        // 五人场：将敌人B/C/D都加入角色列表
        if (GameSelectionData.gameMode == GameMode.FivePlayer)
        {
            if (enemyB != null && !players.Contains(enemyB))
                players.Add(enemyB);
            if (enemyC != null)
                players.Add(enemyC);
            if (enemyD != null)
                players.Add(enemyD);
        }

        currentPlayerIndex = 0;
        turnCount = 1;
        gameOver = false;
        matchHighlights.Clear();

        playerDiscardFinished = false;
        playerResponseFinished = false;
        playerUsedDefense = false;
        playerDyingChoiceFinished = false;
        playerUsedPeach = false;

        pendingAttacker = null;
        pendingDefender = null;
        dyingPlayer = null;
        pendingResponseCardType = CardEffectType.Defense;

        currentPlayer = null;
        currentPhase = TurnPhase.Draw;

        // 开局前所有角色先摸初始手牌
        DrawInitialCards();

        RefreshBattleUI();

        StartCoroutine(GameLoop());
    }

    private IEnumerator GameLoop()
    {
        while (!gameOver)
        {
            if (players == null || players.Count == 0)
            {
                Debug.LogError("没有可参与游戏的角色");
                yield break;
            }

            Player activePlayer = players[currentPlayerIndex];

            if (activePlayer != null && activePlayer.currentHp > 0)
            {
                Debug.Log("========== 第 " + turnCount + " 轮 ==========");
                yield return StartCoroutine(RunTurn(activePlayer));
            }

            if (gameOver)
            {
                yield break;
            }

            yield return new WaitForSeconds(turnDelay);

            GoToNextPlayer();
        }
    }

    private void GoToNextPlayer()
    {
        currentPlayerIndex++;

        if (currentPlayerIndex >= players.Count)
        {
            currentPlayerIndex = 0;
            turnCount++;
        }
    }

    private IEnumerator RunTurn(Player activePlayer)
    {
        currentPlayer = activePlayer;
        activePlayer.ResetTurnState();
        currentPhase = TurnPhase.Draw;
        RefreshBattleUI();

        Debug.Log(activePlayer.playerName + "（" + GetRoleDisplayName(activePlayer) + "）回合开始");

        yield return StartCoroutine(ResolveDelayedTricks(activePlayer));
        if (gameOver || activePlayer.currentHp <= 0 || activePlayer.isDead)
        {
            yield break;
        }

        currentPhase = TurnPhase.Draw;
        if (activePlayer.skipDrawThisTurn)
        {
            Debug.Log(activePlayer.playerName + " 因延时锦囊跳过摸牌阶段");
        }
        else
        {
            DrawPhase(activePlayer);
        }

        yield return new WaitForSeconds(turnDelay);

        if (activePlayer.skipPlayThisTurn)
        {
            currentPhase = TurnPhase.Discard;
            Debug.Log(activePlayer.playerName + " 因延时锦囊跳过出牌阶段");
            RefreshBattleUI();
        }
        else
        {
            currentPhase = TurnPhase.Play;
            if (activePlayer == player)
            {
                GameSfxPlayer.PlayTurnNotice();
            }
            RefreshBattleUI();

            if (activePlayer.isAI)
            {
                Debug.Log(activePlayer.playerName + " 进入出牌阶段，AI 自动行动");

                yield return StartCoroutine(AIPlayPhase(activePlayer));

                if (gameOver)
                {
                    yield break;
                }
            }
            else
            {
                Debug.Log(activePlayer.playerName + " 进入出牌阶段，请点击手牌出牌");

                yield return new WaitUntil(() => currentPhase == TurnPhase.Discard || activePlayer.currentHp <= 0 || gameOver);

                if (gameOver)
                {
                    yield break;
                }
            }
        }

        yield return new WaitForSeconds(turnDelay);

        if (activePlayer.currentHp <= 0 || activePlayer.isDead)
        {
            currentPhase = TurnPhase.End;
            RefreshBattleUI();
            yield break;
        }

        currentPhase = TurnPhase.Discard;
        RefreshBattleUI();

        Debug.Log(activePlayer.playerName + " 进入弃牌阶段");

        if (activePlayer.isAI)
        {
            AutoDiscardPhase(activePlayer);
        }
        else
        {
            playerDiscardFinished = false;
            StartPlayerDiscardPhase();

            yield return new WaitUntil(() => playerDiscardFinished || gameOver);
        }

        if (gameOver)
        {
            yield break;
        }

        yield return new WaitForSeconds(turnDelay);

        currentPhase = TurnPhase.End;
        RefreshBattleUI();

        EndTurn(activePlayer);
    }

    // 开局前所有角色先摸初始手牌（每个角色摸初始手牌数等于2倍摸牌数）
    private void DrawInitialCards()
    {
        foreach (Player p in players)
        {
            if (p == null || p.currentHp <= 0)
            {
                continue;
            }

            int initialCount = GetDrawCount(p) * 2;
            for (int i = 0; i < initialCount; i++)
            {
                CardData card = deckManager.Draw();
                if (card != null)
                {
                    p.DrawCard(card);
                }
            }
            Debug.Log(p.playerName + " 摸了 " + initialCount + " 张初始手牌");
        }
    }

    private void DrawPhase(Player activePlayer)
    {
        // 三人场：霸主摸2张，协战摸1张；二人场：都摸2张
        int drawCount = GetDrawCount(activePlayer);

        Debug.Log(activePlayer.playerName + " 进入摸牌阶段，摸" + drawCount + "张牌");

        for (int i = 0; i < drawCount; i++)
        {
            CardData card = deckManager.Draw();

            if (card != null)
            {
                activePlayer.DrawCard(card);
            }
        }
    }

    // 获取角色每回合的摸牌数
    private int GetDrawCount(Player p)
    {
        if (GameSelectionData.gameMode == GameMode.FivePlayer)
        {
            // 五人场：执令者摸2张，其他人摸2张
            return 2;
        }

        if (GameSelectionData.gameMode == GameMode.ThreePlayer)
        {
            // 三人场：霸主摸2张，协战摸1张
            return p.teamType == TeamType.Overlord ? 2 : 1;
        }

        // 二人场：都摸2张
        return 2;
    }

    public bool CanUseCharacterActiveSkill(Player actor)
    {
        if (
            actor == null
            || actor != currentPlayer
            || actor.currentHp <= 0
            || actor.isDead
            || currentPhase != TurnPhase.Play
            || actor.usedActiveSkillThisTurn
            || actor.characterData == null
            || actor.characterData.activeSkillType != CharacterActiveSkillType.LoseOneHpDrawTwo
            || deckManager == null
        )
        {
            return false;
        }

        return deckManager.deck.Count + deckManager.discardPile.Count >= 2;
    }

    public bool TryUseCharacterActiveSkill(Player actor)
    {
        if (!CanUseCharacterActiveSkill(actor))
        {
            return false;
        }

        actor.usedActiveSkillThisTurn = true;
        StartCoroutine(ResolveLoseOneHpDrawTwoSkill(actor));
        RefreshBattleUI();
        return true;
    }

    private IEnumerator ResolveLoseOneHpDrawTwoSkill(Player actor)
    {
        if (actor == null)
        {
            yield break;
        }

        string skillName = actor.characterData != null
            ? actor.characterData.skillName
            : "燃契";
        actor.LoseHp(1);
        GameSfxPlayer.PlaySkillVoice(actor);

        if (battleUI != null)
        {
            battleUI.ShowDamageEffect(actor, 1);
            battleUI.ShowSkillPopup(actor.playerName + "发动【" + skillName + "】");
        }

        yield return StartCoroutine(CheckDying(actor));

        if (gameOver || actor.currentHp <= 0 || actor.isDead)
        {
            yield break;
        }

        // CheckDying 会临时切换到濒死阶段；角色被救回后继续原本的出牌阶段。
        if (actor == currentPlayer)
        {
            currentPhase = TurnPhase.Play;
        }

        int drawnCount = 0;
        for (int i = 0; i < 2; i++)
        {
            CardData card = deckManager.Draw();
            if (card == null)
            {
                break;
            }

            actor.DrawCard(card);
            drawnCount++;
        }

        if (battleUI != null)
        {
            battleUI.ShowCardAction(
                actor.playerName
                    + "发动【"
                    + skillName
                    + "】，失去1点体力并摸了"
                    + drawnCount
                    + "张牌"
            );
        }

        RefreshBattleUI();
    }

    private IEnumerator AIPlayPhase(Player aiPlayer)
    {
        if (
            CanUseCharacterActiveSkill(aiPlayer)
            && aiPlayer.currentHp >= 3
            && aiPlayer.handCards.Count <= aiPlayer.currentHp + 2
        )
        {
            aiPlayer.usedActiveSkillThisTurn = true;
            yield return StartCoroutine(ResolveLoseOneHpDrawTwoSkill(aiPlayer));

            if (gameOver || aiPlayer.currentHp <= 0 || aiPlayer.isDead)
            {
                yield break;
            }
        }

        int playCount = 0;
        // Let the AI use its hand coherently, with a safety bound for draw-card loops.
        int maxPlayCount = Mathf.Min(aiPlayer.handCards.Count + 2, 10);

        while (playCount < maxPlayCount)
        {
            CardData cardToPlay = FindPlayableCard(aiPlayer);

            if (cardToPlay == null)
            {
                Debug.Log(aiPlayer.playerName + " 没有可以主动使用的牌");
                yield break;
            }

            Player target = FindBestTargetForCard(aiPlayer, cardToPlay);

            if (cardToPlay.needTarget && target == null)
            {
                Debug.Log(aiPlayer.playerName + " 找不到可用目标");
                yield break;
            }

            if (cardToPlay.category == CardCategory.Basic)
            {
                if (cardToPlay.effectType == CardEffectType.Attack)
                {
                    yield return StartCoroutine(UseAttackCard(aiPlayer, target, cardToPlay));
                }
                else if (cardToPlay.effectType == CardEffectType.Heal)
                {
                    UseHealCardOnSelf(aiPlayer, cardToPlay);
                }
                else if (cardToPlay.effectType == CardEffectType.Wine)
                {
                    UseWineCard(aiPlayer, cardToPlay);
                }
            }
            else if (cardToPlay.category == CardCategory.Trick)
            {
                // 必须等待锦囊结算完成，否则会与濒死流程同时改写 currentPhase
                yield return StartCoroutine(UseTrickCard(aiPlayer, target, cardToPlay));
            }
            else if (cardToPlay.category == CardCategory.Equip)
            {
                UseEquipCard(aiPlayer, cardToPlay);
            }

            playCount++;

            RefreshBattleUI();

            if (gameOver)
            {
                yield break;
            }

            yield return new WaitForSeconds(0.5f);
        }

        Debug.Log(aiPlayer.playerName + " 出牌阶段结束");
    }

    private Player FindBestEnemyTarget(Player user, CardData card = null)
    {
        Player result = null;
        float bestScore = float.MinValue;

        foreach (Player target in players)
        {
            if (target == null || target == user || target.currentHp <= 0)
                continue;
            if (!IsKnownEnemyForAI(user, target) && !IsPotentialThreatForAI(user, target))
                continue;

            if (
                card != null
                && card.effectType == CardEffectType.Attack
                && !IsWithinAttackRange(user, target)
                && user.qingLongFollowUpTarget != target
            )
            {
                continue;
            }

            if (card != null
                && card.effectType == CardEffectType.Attack
                && !user.CanUseAttack()
                && user.qingLongFollowUpTarget != target)
            {
                continue;
            }

            if (card != null
                && card.isOfficialPackageCard
                && (card.cardName == "顺手牵羊" || card.cardName == "兵粮寸断")
                && GetLivingDistance(user, target) > 1)
            {
                continue;
            }

            if (card != null && card.effectType == CardEffectType.ForceDiscard && target.handCards.Count == 0)
            {
                continue;
            }

            if (card != null && card.effectType == CardEffectType.DiscardEquip && !target.HasEquipment())
            {
                continue;
            }

            if (card != null && card.effectType == CardEffectType.BorrowWeapon && target.weapon == null)
            {
                continue;
            }

            if (card != null && card.effectType == CardEffectType.FireAttack && target.handCards.Count == 0)
            {
                continue;
            }

            if (
                card != null
                && card.effectType == CardEffectType.DiscardTarget
                && target.handCards.Count == 0
                && !target.HasEquipment()
            )
            {
                continue;
            }

            float score = ScoreTargetForAI(user, target, card);
            if (score > bestScore)
            {
                bestScore = score;
                result = target;
            }
        }

        return result;
    }

    private bool IsPotentialThreatForAI(Player user, Player target)
    {
        if (
            GameSelectionData.gameMode != GameMode.FivePlayer
            || user == null
            || target == null
            || target.isIdentityRevealed
            || target.commanderSuspicion <= 0
        )
        {
            return false;
        }

        return user.identityType == IdentityType.Commander
            || user.identityType == IdentityType.Protector;
    }

    private Player FindBestTargetForCard(Player user, CardData card)
    {
        if (user == null || card == null)
        {
            return null;
        }

        if (card.effectType == CardEffectType.StealTarget)
        {
            Player bestTarget = null;
            float bestScore = float.MinValue;
            foreach (Player candidate in players)
            {
                if (
                    candidate == null
                    || candidate == user
                    || candidate.currentHp <= 0
                    || !IsKnownEnemyForAI(user, candidate)
                    || (candidate.handCards.Count == 0 && !candidate.HasEquipment())
                    || (card.isOfficialPackageCard
                        && card.cardName == "顺手牵羊"
                        && GetLivingDistance(user, candidate) > 1)
                )
                {
                    continue;
                }

                float score = ScoreTargetForAI(user, candidate, card);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestTarget = candidate;
                }
            }
            return bestTarget;
        }

        if (card.effectType == CardEffectType.AidDraw)
        {
            Player bestAlly = null;
            float bestScore = float.MinValue;
            foreach (Player candidate in players)
            {
                if (candidate == null || candidate.currentHp <= 0 || !IsKnownAllyForAI(user, candidate))
                {
                    continue;
                }

                int missingHandCards = Mathf.Max(0, candidate.maxHp - candidate.handCards.Count);
                float score = Mathf.Min(Mathf.Max(1, card.value), missingHandCards) * 12f;
                if (candidate.currentHp <= 2)
                {
                    score += 8f;
                }
                if (candidate == user)
                {
                    score += 2f;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestAlly = candidate;
                }
            }
            return bestAlly;
        }

        if (card.effectType == CardEffectType.Protect || card.effectType == CardEffectType.ReduceSuspicion)
        {
            Player bestAlly = null;
            float bestAllyScore = float.MinValue;
            foreach (Player candidate in players)
            {
                if (candidate == null || candidate.currentHp <= 0 || !IsKnownAllyForAI(user, candidate))
                {
                    continue;
                }

                float score;
                if (card.effectType == CardEffectType.Protect)
                {
                    if (candidate.protectionValue > 0)
                    {
                        continue;
                    }
                    score = 28f + (candidate.maxHp - candidate.currentHp) * 15f;
                    if (candidate.currentHp <= 2)
                    {
                        score += 24f;
                    }
                    if (candidate == user)
                    {
                        score += 4f;
                    }
                }
                else
                {
                    if (
                        candidate.isIdentityRevealed
                        || candidate.commanderSuspicion <= 0
                        || candidate.hasAttackedCommander
                    )
                    {
                        continue;
                    }
                    score = 35f + candidate.commanderSuspicion * 8f;
                    if (!candidate.hasAttackedCommander)
                    {
                        score += 12f;
                    }
                }

                if (score > bestAllyScore)
                {
                    bestAllyScore = score;
                    bestAlly = candidate;
                }
            }
            return bestAlly;
        }

        if (
            GameSelectionData.gameMode == GameMode.FivePlayer
            && (card.effectType == CardEffectType.InvestigateIdentity || card.effectType == CardEffectType.PlantSuspicion)
        )
        {
            Player bestSuspect = null;
            float bestSuspectScore = float.MinValue;
            foreach (Player candidate in players)
            {
                if (
                    candidate == null
                    || candidate == user
                    || candidate.currentHp <= 0
                    || candidate.isIdentityRevealed
                )
                {
                    continue;
                }

                if (card.effectType == CardEffectType.InvestigateIdentity && candidate.commanderSuspicion <= 0)
                {
                    continue;
                }
                if (
                    card.effectType == CardEffectType.PlantSuspicion
                    && candidate.commanderSuspicion >= Player.CommanderSuspicionRevealThreshold
                )
                {
                    continue;
                }

                float score = 20f + candidate.commanderSuspicion * 12f;
                if (IsKnownEnemyForAI(user, candidate))
                {
                    score += 22f;
                }
                else if (card.effectType == CardEffectType.PlantSuspicion)
                {
                    score -= 16f;
                }

                if (card.effectType == CardEffectType.InvestigateIdentity && candidate.commanderSuspicion > 0)
                {
                    score += 25f;
                }

                if (score > bestSuspectScore)
                {
                    bestSuspectScore = score;
                    bestSuspect = candidate;
                }
            }
            return bestSuspect;
        }

        return FindBestEnemyTarget(user, card);
    }

    private float ScoreTargetForAI(Player user, Player target, CardData card)
    {
        float score = 30f - target.currentHp * 7f;

        if (target.currentHp <= 1)
        {
            score += 22f;
        }

        if (target.hasAttackedCommander)
        {
            score += 18f;
        }

        if (
            HasSkill(user, "照谶")
            && !user.usedZhaoChenThisTurn
            && target.handCards.Count > 0
            && (
                target.handCards.Count >= user.handCards.Count
                || (
                    GameSelectionData.gameMode == GameMode.FivePlayer
                    && target.commanderSuspicion > 0
                )
            )
        )
        {
            score += 8f;
        }

        if (GameSelectionData.gameMode == GameMode.FivePlayer)
        {
            if (user.identityType == IdentityType.Rebel && target.identityType == IdentityType.Commander)
            {
                score += 100f;
            }
            else if (
                (user.identityType == IdentityType.Commander || user.identityType == IdentityType.Protector)
                && target.isIdentityRevealed
                && target.identityType == IdentityType.Rebel
            )
            {
                score += 45f;
            }
            else if (
                (user.identityType == IdentityType.Commander || user.identityType == IdentityType.Protector)
                && target.commanderSuspicion > 0
                && !target.isIdentityRevealed
            )
            {
                score += 20f + target.commanderSuspicion * 10f;
            }
        }

        if (card == null)
        {
            return score;
        }

        switch (card.effectType)
        {
            case CardEffectType.Attack:
            case CardEffectType.DirectDamage:
                int damage = Mathf.Max(1, card.value);
                if (target.currentHp <= damage)
                {
                    score += 35f;
                }
                if (
                    target.armor != null
                    && target.armor.cardName == "云纹甲"
                    && !target.armorUsedThisTurn
                )
                {
                    // Yunwenjia now has a 50% judgement instead of guaranteed mitigation.
                    score -= 4f;
                }
                // Opponent hand contents are hidden; estimate response risk only from
                // the public hand count instead of checking whether they hold Shan.
                if (target.currentHp > damage && target.handCards.Count > 0)
                {
                    score -= Mathf.Min(target.handCards.Count, 3) * 2f;
                }
                break;

            case CardEffectType.ForceDiscard:
                score += Mathf.Min(target.handCards.Count, 4) * 9f;
                break;

            case CardEffectType.DiscardEquip:
                if (target.weapon != null)
                {
                    score += 24f;
                }
                else if (target.armor != null)
                {
                    score += 14f;
                }
                else
                {
                    score += 8f;
                }
                break;

            case CardEffectType.DiscardTarget:
                score += Mathf.Min(target.handCards.Count, 3) * 7f;
                if (target.HasEquipment())
                {
                    score += 10f;
                }
                break;

            case CardEffectType.StealTarget:
                score += Mathf.Min(target.handCards.Count, 3) * 9f;
                if (target.weapon != null)
                {
                    score += 12f;
                }
                if (target.armor != null || target.treasure != null)
                {
                    score += 8f;
                }
                break;

            case CardEffectType.AidDraw:
                score = Mathf.Min(Mathf.Max(1, card.value), Mathf.Max(0, target.maxHp - target.handCards.Count)) * 12f;
                if (target.currentHp <= 2)
                {
                    score += 8f;
                }
                if (target == user)
                {
                    score += 2f;
                }
                break;

            case CardEffectType.PlantSuspicion:
                score += target.commanderSuspicion * 14f;
                break;
        }

        return score;
    }

    // 检查是否有拥有手牌的敌人（用于弃牌锦囊判断）
    private bool HasEnemyWithCards(Player user)
    {
        foreach (Player target in players)
        {
            if (target == null || target == user || target.currentHp <= 0)
            {
                continue;
            }

            if (!IsKnownEnemyForAI(user, target))
            {
                continue;
            }

            if (target.handCards.Count > 0)
            {
                return true;
            }
        }

        return false;
    }

    // 检查是否有拥有装备的敌人（用于弃装备锦囊判断）
    private bool HasEnemyWithEquipment(Player user)
    {
        foreach (Player target in players)
        {
            if (target == null || target == user || target.currentHp <= 0)
            {
                continue;
            }

            if (!IsKnownEnemyForAI(user, target))
            {
                continue;
            }

            if (target.HasEquipment())
            {
                return true;
            }
        }

        return false;
    }

    private bool IsEnemy(Player a, Player b)
    {
        if (a == null || b == null)
            return false;
        if (a == b)
            return false;

        // Five-player combat effects use the actual victory alliances. Hidden
        // identities affect what an AI chooses to target, not who is really allied.
        if (GameSelectionData.gameMode == GameMode.FivePlayer)
        {
            bool commanderSidePair =
                (a.identityType == IdentityType.Commander && b.identityType == IdentityType.Protector)
                || (a.identityType == IdentityType.Protector && b.identityType == IdentityType.Commander);
            bool rebelPair =
                a.identityType == IdentityType.Rebel && b.identityType == IdentityType.Rebel;
            return !commanderSidePair && !rebelPair;
        }

        // 二人/三人场：基于阵营判断
        if (a.teamType == TeamType.None || b.teamType == TeamType.None)
        {
            return a != b;
        }

        return a.teamType != b.teamType;
    }

    private bool IsKnownEnemyForAI(Player self, Player other)
    {
        if (self == null || other == null || self == other)
        {
            return false;
        }

        return GameSelectionData.gameMode == GameMode.FivePlayer
            ? IsIdentityEnemy(self, other)
            : IsEnemy(self, other);
    }

    private bool IsKnownAllyForAI(Player self, Player other)
    {
        if (self == null || other == null)
        {
            return false;
        }
        if (self == other)
        {
            return true;
        }

        if (GameSelectionData.gameMode != GameMode.FivePlayer)
        {
            return !IsEnemy(self, other);
        }

        if (self.identityType == IdentityType.Commander || self.identityType == IdentityType.Protector)
        {
            return other.isIdentityRevealed
                && (other.identityType == IdentityType.Commander || other.identityType == IdentityType.Protector);
        }

        return self.identityType == IdentityType.Rebel
            && other.isIdentityRevealed
            && other.identityType == IdentityType.Rebel;
    }

    // 五人场 AI 的可见信息判断：避免 AI 根据隐藏身份直接选中目标。
    private bool IsIdentityEnemy(Player self, Player other)
    {
        IdentityType myIdentity = self.identityType;

        // 执令者：攻击已揭示的逆契者/游离者 + 有嫌疑标记的角色，不攻击已揭示的护令者
        if (myIdentity == IdentityType.Commander)
        {
            if (other.isIdentityRevealed && other.identityType == IdentityType.Protector)
                return false;
            if (other.isIdentityRevealed && other.identityType == IdentityType.Commander)
                return false;
            // 已揭示的逆契者是敌人
            if (other.isIdentityRevealed && other.identityType == IdentityType.Rebel)
                return true;
            // 已揭示的游离者是敌人
            if (other.isIdentityRevealed && other.identityType == IdentityType.Wanderer)
                return true;
            // Confirmed attacks are hostile; an unverified planted clue only affects priority.
            if (other.hasAttackedCommander)
                return true;
            // 未揭示且没有嫌疑的角色暂不攻击（避免误杀护令者）
            return false;
        }

        // 护令者：保护执令者，攻击有嫌疑和已揭示的逆契者/游离者
        if (myIdentity == IdentityType.Protector)
        {
            if (other.isIdentityRevealed && other.identityType == IdentityType.Commander)
                return false;
            if (other.isIdentityRevealed && other.identityType == IdentityType.Protector)
                return false;
            // 已揭示的逆契者是敌人
            if (other.isIdentityRevealed && other.identityType == IdentityType.Rebel)
                return true;
            // 已揭示的游离者是敌人
            if (other.isIdentityRevealed && other.identityType == IdentityType.Wanderer)
                return true;
            // Confirmed attacks are hostile; an unverified planted clue only affects priority.
            if (other.hasAttackedCommander)
                return true;
            // 未揭示且没有嫌疑的角色暂不攻击
            return false;
        }

        // 逆契者：集火执令者，攻击已揭示的护令者
        if (myIdentity == IdentityType.Rebel)
        {
            if (other.isIdentityRevealed && other.identityType == IdentityType.Commander)
                return true;
            if (other.isIdentityRevealed && other.identityType == IdentityType.Rebel)
                return false;
            if (
                other.isIdentityRevealed
                && (other.identityType == IdentityType.Protector || other.identityType == IdentityType.Wanderer)
            )
                return true;
            // 未揭示的角色：逆契者保守，不主动攻击，避免读取隐藏身份。
            return false;
        }

        // 游离者：自保，不主动攻击任何人
        if (myIdentity == IdentityType.Wanderer)
        {
            // The wanderer wins by surviving alone, so it may pressure any rival.
            return true;
        }

        return false;
    }

    // 获取角色显示名称（日志用，5人场不泄露隐藏身份）
    private string GetRoleDisplayName(Player p)
    {
        if (GameSelectionData.gameMode == GameMode.FivePlayer)
        {
            if (p.isIdentityRevealed)
            {
                return GetIdentityName(p.identityType);
            }
            else if (p == player)
            {
                // 玩家能看到自己的身份
                return GetIdentityName(p.identityType);
            }
            else
            {
                return "身份未知";
            }
        }

        return GetTeamName(p.teamType);
    }

    private string GetIdentityName(IdentityType identity)
    {
        switch (identity)
        {
            case IdentityType.Commander:
                return "执令者";
            case IdentityType.Protector:
                return "护令者";
            case IdentityType.Rebel:
                return "逆契者";
            case IdentityType.Wanderer:
                return "游离者";
            default:
                return "无身份";
        }
    }

    private CardData FindPlayableCard(Player activePlayer)
    {
        CardData bestCard = null;
        float bestScore = 0f;

        foreach (CardData card in activePlayer.handCards)
        {
            float score = ScoreCardForAI(activePlayer, card);
            if (score > bestScore)
            {
                bestScore = score;
                bestCard = card;
            }
        }

        return bestCard;
    }

    private float ScoreCardForAI(Player activePlayer, CardData card)
    {
        if (activePlayer == null || card == null)
        {
            return 0f;
        }

        if (card.effectType == CardEffectType.Heal)
        {
            int missingHp = activePlayer.maxHp - activePlayer.currentHp;
            if (missingHp <= 0)
            {
                return 0f;
            }

            return 28f + missingHp * 25f + (activePlayer.currentHp == 1 ? 45f : 0f);
        }

        if (card.effectType == CardEffectType.Wine)
        {
            if (activePlayer.usedWineThisTurn || activePlayer.hasWineBoost)
            {
                return 0f;
            }
            int availableAttacks = CountCardsOfType(activePlayer, CardEffectType.Attack);
            return availableAttacks > 0 ? 49f + Mathf.Min(availableAttacks, 2) * 3f : 0f;
        }

        if (card.category == CardCategory.Equip)
        {
            if (!ShouldEquipCard(activePlayer, card))
            {
                return 0f;
            }

            float equipScore = 44f;
            if (card.effectType == CardEffectType.Weapon)
            {
                int attackCards = CountCardsOfType(activePlayer, CardEffectType.Attack);
                equipScore += Mathf.Min(attackCards, 2) * 10f;
            }
            else if (card.effectType == CardEffectType.Armor && activePlayer.currentHp <= 2)
            {
                equipScore += 18f;
            }
            else if (card.effectType == CardEffectType.Treasure)
            {
                if (activePlayer.currentHp < activePlayer.maxHp && activePlayer.handCards.Count == 0)
                {
                    equipScore += 12f;
                }
                else if (activePlayer.currentHp < activePlayer.maxHp && activePlayer.handCards.Count <= 2)
                {
                    equipScore += 5f;
                }
            }

            return equipScore;
        }

        if (card.effectType == CardEffectType.Attack)
        {
            if (!activePlayer.CanUseAttack() && activePlayer.qingLongFollowUpTarget == null)
            {
                return 0f;
            }

            Player target = FindBestEnemyTarget(activePlayer, card);
            if (target == null)
            {
                return 0f;
            }

            float attackScore = 45f + Mathf.Max(0f, ScoreTargetForAI(activePlayer, target, card) - 15f) * 0.35f;
            if (HasSkill(activePlayer, "烈锋") && activePlayer.usedAttackCountThisTurn == 0)
            {
                attackScore += 14f;
            }
            return attackScore;
        }

        if (card.category == CardCategory.Trick)
        {
            switch (card.effectType)
            {
                case CardEffectType.DirectDamage:
                case CardEffectType.ForceDiscard:
                case CardEffectType.DiscardEquip:
                case CardEffectType.DiscardTarget:
                case CardEffectType.StealTarget:
                {
                    Player target = FindBestTargetForCard(activePlayer, card);
                    if (target == null)
                    {
                        return 0f;
                    }

                    float effectScore = card.effectType == CardEffectType.DirectDamage
                        ? 57f
                        : card.effectType == CardEffectType.DiscardEquip
                            ? 46f
                            : card.effectType == CardEffectType.DiscardTarget
                                ? 43f
                                : card.effectType == CardEffectType.StealTarget ? 52f : 40f;
                    effectScore += Mathf.Max(0f, ScoreTargetForAI(activePlayer, target, card) - 15f) * 0.3f;

                    if (HasSkill(activePlayer, "震策") && !activePlayer.usedZhenCeThisTurn)
                    {
                        effectScore += ScoreZhenCeValue(activePlayer);
                    }

                    return effectScore;
                }

                case CardEffectType.AreaDamage:
                {
                    float impact = 0f;
                    foreach (Player target in players)
                    {
                        if (target == null || target == activePlayer || target.currentHp <= 0)
                        {
                            continue;
                        }

                        bool enemy = IsKnownEnemyForAI(activePlayer, target);
                        impact += enemy ? 11f : -17f;
                        if (target.currentHp <= 1)
                        {
                            impact += enemy ? 18f : -30f;
                        }
                    }
                    return impact > 0f ? 44f + impact : 0f;
                }

                case CardEffectType.Protect:
                {
                    Player target = FindBestTargetForCard(activePlayer, card);
                    if (target == null)
                    {
                        return 0f;
                    }
                    return 42f + (target.maxHp - target.currentHp) * 11f
                        + (target.currentHp <= 2 ? 24f : 0f);
                }

                case CardEffectType.InvestigateIdentity:
                case CardEffectType.PlantSuspicion:
                case CardEffectType.ReduceSuspicion:
                {
                    Player target = FindBestTargetForCard(activePlayer, card);
                    if (target == null)
                    {
                        return 0f;
                    }

                    float score = card.effectType == CardEffectType.InvestigateIdentity
                        ? 62f + target.commanderSuspicion * 8f
                        : card.effectType == CardEffectType.PlantSuspicion
                            ? 40f + target.commanderSuspicion * 12f
                            : 45f + (target.hasAttackedCommander ? 4f : 12f);

                    if (HasSkill(activePlayer, "震策") && !activePlayer.usedZhenCeThisTurn)
                    {
                        score += ScoreZhenCeValue(activePlayer);
                    }
                    return score;
                }

                case CardEffectType.DrawCards:
                    if (activePlayer.handCards.Count >= activePlayer.maxHp + 2)
                    {
                        return 0f;
                    }

                    float drawScore = Mathf.Max(12f, 66f - activePlayer.handCards.Count * 9f);
                    if (HasSkill(activePlayer, "震策"))
                    {
                        drawScore += ScoreZhenCeValue(activePlayer);
                    }
                    return drawScore;

                case CardEffectType.AidDraw:
                {
                    Player target = FindBestTargetForCard(activePlayer, card);
                    if (target == null || target.handCards.Count >= target.maxHp + 2)
                    {
                        return 0f;
                    }

                    float aidScore = 36f + Mathf.Max(0, target.maxHp - target.handCards.Count) * 7f;
                    if (HasSkill(activePlayer, "震策") && !activePlayer.usedZhenCeThisTurn)
                    {
                        aidScore += ScoreZhenCeValue(activePlayer);
                    }
                    return aidScore;
                }

                case CardEffectType.DrawAndDiscard:
                {
                    if (activePlayer.handCards.Count >= activePlayer.maxHp + 2)
                    {
                        return 0f;
                    }

                    float organizeScore = 31f + (activePlayer.handCards.Count <= 2 ? 8f : 0f);
                    if (HasSkill(activePlayer, "震策") && !activePlayer.usedZhenCeThisTurn)
                    {
                        organizeScore += ScoreZhenCeValue(activePlayer);
                    }
                    return organizeScore;
                }

                case CardEffectType.SuitJudgement:
                {
                    float judgementScore = 37f;
                    if (HasSkill(activePlayer, "震策") && !activePlayer.usedZhenCeThisTurn)
                    {
                        judgementScore += ScoreZhenCeValue(activePlayer);
                    }
                    if (HasSkill(activePlayer, "观象") && !activePlayer.usedGuanXiangThisTurn)
                    {
                        judgementScore += 14f;
                    }
                    return judgementScore;
                }

                case CardEffectType.Duel:
                case CardEffectType.BorrowWeapon:
                case CardEffectType.FireAttack:
                case CardEffectType.DelayedSkipPlay:
                case CardEffectType.DelayedSkipDraw:
                {
                    Player target = FindBestTargetForCard(activePlayer, card);
                    if (target == null)
                    {
                        return 0f;
                    }
                    float score = card.effectType == CardEffectType.Duel
                        ? 43f + (target.currentHp <= 2 ? 14f : 0f)
                        : card.effectType == CardEffectType.BorrowWeapon
                            ? 46f
                            : card.effectType == CardEffectType.FireAttack
                                ? 42f
                                : 35f;
                    if (HasSkill(activePlayer, "震策") && !activePlayer.usedZhenCeThisTurn)
                    {
                        score += ScoreZhenCeValue(activePlayer);
                    }
                    return score;
                }

                case CardEffectType.RecoverAll:
                {
                    int injured = 0;
                    foreach (Player target in players)
                    {
                        if (target != null && target.currentHp > 0 && target.currentHp < target.maxHp)
                        {
                            injured++;
                        }
                    }
                    return injured > 0 ? 34f + injured * 11f : 0f;
                }

                case CardEffectType.Harvest:
                    return activePlayer.handCards.Count < activePlayer.maxHp + 1 ? 38f : 24f;

                case CardEffectType.Lightning:
                    return TargetAlreadyHasDelayedCard(activePlayer, "闪电") ? 0f : 22f;

                case CardEffectType.Chain:
                case CardEffectType.Nullification:
                    return 0f;
            }
        }

        return 0f;
    }

    private int CountCardsOfType(Player targetPlayer, CardEffectType effectType)
    {
        int count = 0;
        foreach (CardData card in targetPlayer.handCards)
        {
            if (card != null && card.effectType == effectType)
            {
                count++;
            }
        }

        return count;
    }

    private float ScoreZhenCeValue(Player user)
    {
        if (user == null || user.usedZhenCeThisTurn)
        {
            return 0f;
        }

        float value = 0f;
        foreach (Player target in players)
        {
            if (target == null || target == user || target.currentHp <= 0 || !IsKnownEnemyForAI(user, target))
            {
                continue;
            }

            value += target.handCards.Count == 0 ? 12f : 5f;
        }

        return value;
    }

    private bool ShouldEquipCard(Player activePlayer, CardData equipCard)
    {
        if (activePlayer == null || equipCard == null)
        {
            return false;
        }

        if (equipCard.category != CardCategory.Equip)
        {
            return false;
        }

        switch (equipCard.effectType)
        {
            case CardEffectType.Weapon:
                return activePlayer.weapon != equipCard;

            case CardEffectType.Armor:
                return activePlayer.armor != equipCard;

            case CardEffectType.Treasure:
                return activePlayer.treasure != equipCard;

            case CardEffectType.MountPlus:
                return activePlayer.defensiveHorse != equipCard;

            case CardEffectType.MountMinus:
                return activePlayer.offensiveHorse != equipCard;

            default:
                return false;
        }
    }

    public void PlayerPlayCard(CardData card)
    {
        // 默认寻找最佳敌方目标
        Player defaultTarget = FindBestEnemyTarget(player);

        // 如果找不到敌方目标（如二人场兼容），使用 enemy
        if (defaultTarget == null && enemy != null && enemy.currentHp > 0)
        {
            defaultTarget = enemy;
        }

        PlayerPlayCard(card, defaultTarget);
    }

    public void PlayerPlayCard(CardData card, Player selectedTarget)
    {
        if (gameOver)
        {
            return;
        }

        if (card == null)
        {
            Debug.LogWarning("操作失败：卡牌为空");
            return;
        }

        if (!player.handCards.Contains(card))
        {
            Debug.LogWarning("操作失败：这张牌不在玩家手牌中");
            return;
        }

        if (currentPhase == TurnPhase.Response)
        {
            PlayerUseCardInResponsePhase(card);
            return;
        }

        if (currentPhase == TurnPhase.Dying)
        {
            PlayerUseCardInDyingPhase(card);
            return;
        }

        if (currentPhase == TurnPhase.Discard)
        {
            PlayerDiscardCard(card);
            return;
        }

        if (currentPlayer != player)
        {
            Debug.Log("现在不是玩家的回合，不能出牌");
            return;
        }

        if (currentPhase != TurnPhase.Play)
        {
            Debug.Log("现在不是出牌阶段，不能出牌");
            return;
        }

        if (card.effectType == CardEffectType.Defense)
        {
            Debug.Log("闪不能主动使用，只能在被杀时打出");
            return;
        }

        if (card.effectType == CardEffectType.Attack
            && !player.CanUseAttack()
            && player.qingLongFollowUpTarget != selectedTarget)
        {
            Debug.Log("本回合杀的使用次数已达上限");
            return;
        }

        if (card.effectType == CardEffectType.Heal && player.currentHp >= player.maxHp)
        {
            Debug.Log("玩家满血，不能使用桃");
            return;
        }

        if (card.effectType == CardEffectType.Wine)
        {
            UseWineCard(player, card);
            return;
        }

        if (card.category == CardCategory.Basic)
        {
            if (card.effectType == CardEffectType.Attack)
            {
                if (selectedTarget == null)
                {
                    Debug.Log("请先选择一个目标");
                    return;
                }

                if (!CanUseCardOnTarget(player, selectedTarget, card))
                {
                    Debug.Log("不能对该目标使用这张牌");
                    return;
                }

                StartCoroutine(UseAttackCard(player, selectedTarget, card));
                return;
            }

            if (card.effectType == CardEffectType.Heal)
            {
                UseHealCardOnSelf(player, card);
                return;
            }
        }

        if (card.category == CardCategory.Trick)
        {
            StartCoroutine(UseTrickCard(player, selectedTarget, card));
            return;
        }

        if (card.category == CardCategory.Equip)
        {
            UseEquipCard(player, card);
            return;
        }

        Debug.Log("这张牌当前还不能使用：" + card.cardName);
    }

    private string GetTeamName(TeamType teamType)
    {
        switch (teamType)
        {
            case TeamType.Overlord:
                return "霸主";

            case TeamType.Alliance:
                return "协战";

            default:
                return "无阵营";
        }
    }

    public bool CanUseCardOnTarget(Player user, Player target, CardData card)
    {
        if (user == null || target == null || card == null)
        {
            return false;
        }

        if (target.currentHp <= 0)
        {
            return false;
        }

        if (card.effectType == CardEffectType.Attack
            && !IsWithinAttackRange(user, target)
            && user.qingLongFollowUpTarget != target)
        {
            return false;
        }

        if (card.effectType == CardEffectType.Attack
            && !user.CanUseAttack()
            && user.qingLongFollowUpTarget != target)
        {
            return false;
        }

        if (card.isOfficialPackageCard
            && (card.cardName == "顺手牵羊" || card.cardName == "兵粮寸断")
            && GetLivingDistance(user, target) > 1)
        {
            return false;
        }

        if (card.effectType == CardEffectType.BorrowWeapon && target.weapon == null)
        {
            return false;
        }

        if (card.effectType == CardEffectType.FireAttack && target.handCards.Count == 0)
        {
            return false;
        }

        if ((card.effectType == CardEffectType.DelayedSkipPlay
                || card.effectType == CardEffectType.DelayedSkipDraw)
            && TargetAlreadyHasDelayedCard(target, card.cardName))
        {
            return false;
        }

        if (card.effectType == CardEffectType.Chain)
        {
            return true;
        }

        bool canTargetSelf = card.effectType == CardEffectType.Protect
            || card.effectType == CardEffectType.ReduceSuspicion
            || card.effectType == CardEffectType.AidDraw;
        if (target == user && !canTargetSelf)
        {
            return false;
        }

        if (card.effectType == CardEffectType.InvestigateIdentity)
        {
            return GameSelectionData.gameMode == GameMode.FivePlayer
                && !target.isIdentityRevealed
                && target.commanderSuspicion > 0;
        }

        if (card.effectType == CardEffectType.AidDraw)
        {
            if (GameSelectionData.gameMode == GameMode.FivePlayer && !user.isAI)
            {
                return true;
            }

            return !IsEnemy(user, target);
        }

        if (card.effectType == CardEffectType.StealTarget)
        {
            return target.handCards.Count > 0 || target.HasEquipment();
        }

        if (card.effectType == CardEffectType.BorrowWeapon)
        {
            return target.weapon != null;
        }

        if (card.effectType == CardEffectType.ForceDiscard)
        {
            return target.handCards.Count > 0;
        }

        if (card.effectType == CardEffectType.DiscardEquip)
        {
            return target.HasEquipment();
        }

        if (card.effectType == CardEffectType.DiscardTarget)
        {
            return target.handCards.Count > 0 || target.HasEquipment();
        }

        if (card.effectType == CardEffectType.PlantSuspicion)
        {
            return GameSelectionData.gameMode == GameMode.FivePlayer
                && !target.isIdentityRevealed
                && target.commanderSuspicion < Player.CommanderSuspicionRevealThreshold;
        }

        if (card.effectType == CardEffectType.ReduceSuspicion)
        {
            bool canWashClue =
                GameSelectionData.gameMode == GameMode.FivePlayer
                && !target.isIdentityRevealed
                && target.commanderSuspicion > 0
                && !target.hasAttackedCommander;
            if (!canWashClue)
            {
                return false;
            }

            // Keep hidden identities hidden in the human target picker. AI still
            // selects only allies it can identify from public information.
            return !user.isAI || !IsEnemy(user, target);
        }

        if (card.effectType == CardEffectType.Protect)
        {
            if (target.protectionValue > 0)
            {
                return false;
            }

            if (
                GameSelectionData.gameMode == GameMode.FivePlayer
                && !user.isAI
                && !target.isIdentityRevealed
            )
            {
                return true;
            }

            return !IsEnemy(user, target) && target.protectionValue <= 0;
        }

        if (card.needTarget)
        {
            // In five-player mode the player may not know a target's true side.
            // Keep the target picker open for living opponents; AI selection
            // uses its separate public-information filter.
            if (GameSelectionData.gameMode == GameMode.FivePlayer && !user.isAI)
            {
                return true;
            }

            if (card.effectType == CardEffectType.PlantSuspicion)
            {
                return true;
            }

            return IsEnemy(user, target);
        }

        return true;
    }

    private bool IsWithinAttackRange(Player attacker, Player target)
    {
        int distance = GetLivingDistance(attacker, target);
        if (distance < 0)
        {
            return false;
        }

        int range = attacker.weapon != null ? Mathf.Max(1, attacker.weapon.weaponRange) : 1;
        return distance <= range;
    }

    private int GetLivingDistance(Player from, Player target)
    {
        if (from == null || target == null || players == null || players.Count < 2)
        {
            return -1;
        }

        List<Player> livingPlayers = new List<Player>();
        foreach (Player candidate in players)
        {
            if (candidate != null && candidate.currentHp > 0)
            {
                livingPlayers.Add(candidate);
            }
        }

        int attackerIndex = livingPlayers.IndexOf(from);
        int targetIndex = livingPlayers.IndexOf(target);
        if (attackerIndex < 0 || targetIndex < 0 || attackerIndex == targetIndex)
        {
            return -1;
        }

        int clockwise = Mathf.Abs(attackerIndex - targetIndex);
        int distance = Mathf.Min(clockwise, livingPlayers.Count - clockwise);
        if (from.offensiveHorse != null)
        {
            distance = Mathf.Max(1, distance - 1);
        }
        if (target.defensiveHorse != null)
        {
            distance++;
        }
        return distance;
    }

    private IEnumerator DealDamage(
        Player source,
        Player target,
        int damage,
        string reason,
        CardDamageElement damageElement = CardDamageElement.Normal,
        CardData sourceCard = null,
        bool skipChainPropagation = false
    )
    {
        if (target == null || damage <= 0)
        {
            yield break;
        }

        int finalDamage = damage;
        yield return StartCoroutine(
            ApplyDamageReduction(
                target,
                damage,
                reason,
                damageElement,
                sourceCard,
                source,
                resolvedDamage => finalDamage = resolvedDamage
            )
        );

        if (finalDamage <= 0)
        {
            Debug.Log(target.playerName + " 防止了来自【" + reason + "】的伤害");
            RefreshBattleUI();
            yield break;
        }

        string sourceName = source != null ? source.playerName : "未知来源";

        Debug.Log(
            sourceName
                + " 对 "
                + target.playerName
                + " 造成 "
                + finalDamage
                + " 点伤害，来源：【"
                + reason
                + "】"
        );

        target.TakeDamage(finalDamage);
        if (battleUI != null)
        {
            battleUI.ShowDamageEffect(target, finalDamage);
        }
        TryTriggerZhaoChenSkill(source, target);

        // 受伤提示
        if (battleUI != null)
        {
            if (source != null)
            {
                battleUI.ShowCardAction(
                    target.playerName
                        + " 受到"
                        + finalDamage
                        + "点伤害（来自"
                        + source.playerName
                        + "）"
                );
            }
            else
            {
                battleUI.ShowCardAction(target.playerName + " 受到" + finalDamage + "点伤害");
            }
        }

        yield return StartCoroutine(CheckDying(target));

        if (target.currentHp <= 0)
        {
            RecordMatchHighlight(
                target.playerName + " 阵亡" + (source != null ? "（击倒者：" + source.playerName + "）" : "")
            );
        }

        // 五人场：阵亡时揭示身份 + 击杀奖惩
        if (GameSelectionData.gameMode == GameMode.FivePlayer && target.currentHp <= 0)
        {
            // 揭示阵亡者身份
            if (!target.isIdentityRevealed)
            {
                target.isIdentityRevealed = true;
                Debug.Log(
                    target.playerName + " 的身份揭示：" + GetIdentityName(target.identityType)
                );
                if (battleUI != null)
                {
                    battleUI.ShowSkillPopup(
                        target.playerName
                            + "的身份是【"
                            + GetIdentityName(target.identityType)
                            + "】"
                    );
                }
            }

            // 击杀奖惩
            if (source != null)
            {
                ApplyKillRewardPenalty(source, target);
            }
        }

        if (!skipChainPropagation && damageElement != CardDamageElement.Normal && target.chained)
        {
            List<Player> chainedTargets = new List<Player>();
            foreach (Player candidate in players)
            {
                if (candidate != null && candidate != target && candidate.currentHp > 0 && candidate.chained)
                {
                    chainedTargets.Add(candidate);
                }
            }

            target.chained = false;
            foreach (Player candidate in chainedTargets)
            {
                candidate.chained = false;
            }

            foreach (Player chainedTarget in chainedTargets)
            {
                if (chainedTarget.currentHp <= 0)
                {
                    continue;
                }
                Debug.Log("属性伤害传导至 " + chainedTarget.playerName);
                yield return StartCoroutine(
                    DealDamage(
                        source,
                        chainedTarget,
                        finalDamage,
                        reason + "（铁索传导）",
                        damageElement,
                        sourceCard,
                        true
                    )
                );
                if (gameOver)
                {
                    yield break;
                }
            }
        }

        RefreshBattleUI();
    }

    private IEnumerator ApplyDamageReduction(
        Player target,
        int damage,
        string reason,
        CardDamageElement damageElement,
        CardData sourceCard,
        Player damageSource,
        System.Action<int> onResolved
    )
    {
        if (target == null)
        {
            onResolved?.Invoke(damage);
            yield break;
        }

        if (target.protectionValue > 0)
        {
            int prevented = Mathf.Min(damage, target.protectionValue);
            damage -= prevented;
            target.protectionValue = 0;
            target.protectionTurnsRemaining = 0;
            Debug.Log(target.playerName + " 的护盾抵消了 " + prevented + " 点伤害");
        }

        bool armorIgnored = damageSource != null
            && damageSource.weapon != null
            && damageSource.weapon.cardName == "青釭剑"
            && sourceCard != null
            && sourceCard.effectType == CardEffectType.Attack
            && reason == sourceCard.cardName;
        if (damage > 0 && target.armor != null && !armorIgnored)
        {
            if (target.armor.cardName == "仁王盾"
                && sourceCard != null
                && sourceCard.effectType == CardEffectType.Attack
                && damageElement == CardDamageElement.Normal
                && !sourceCard.IsRedSuit())
            {
                bool blocked = false;
                yield return StartCoroutine(
                    ResolveRedArmorJudgement(target, "仁王盾", result => blocked = result)
                );
                if (blocked)
                {
                    damage = 0;
                    Debug.Log(target.playerName + " 的【仁王盾】判定成功，抵消了黑色普通杀");
                }
            }
            else if (target.armor.cardName == "藤甲")
            {
                if (reason == "南蛮入侵" || reason == "万箭齐发")
                {
                    bool blocked = false;
                    yield return StartCoroutine(
                        ResolveRedArmorJudgement(target, "藤甲", result => blocked = result)
                    );
                    if (blocked)
                    {
                        damage = 0;
                        Debug.Log(target.playerName + " 的【藤甲】判定成功，抵消了【" + reason + "】");
                    }
                }

                if (damage > 0 && damageElement == CardDamageElement.Fire)
                {
                    damage++;
                    Debug.Log(target.playerName + " 的【藤甲】使火焰伤害 +1");
                }
            }
            else if (target.armor.cardName == "白银狮子" && damage > 1)
            {
                bool reduced = false;
                yield return StartCoroutine(
                    ResolveRedArmorJudgement(target, "白银狮子", result => reduced = result)
                );
                if (reduced)
                {
                    damage = 1;
                    Debug.Log(target.playerName + " 的【白银狮子】判定成功，将本次伤害限制为1点");
                }
            }
        }

        if (damage > 0 && !armorIgnored && target.armor != null
            && target.armor.cardName == "云纹甲" && !target.armorUsedThisTurn)
        {
            target.armorUsedThisTurn = true;

            CardData judgementCard = deckManager != null ? deckManager.Draw() : null;
            if (judgementCard != null)
            {
                if (battleUI != null)
                {
                    battleUI.ShowDiscardedCard(target, judgementCard, false, "云纹甲判定");
                }
                deckManager.Discard(judgementCard);

                if (judgementCard.suit == CardSuit.Spade || judgementCard.suit == CardSuit.Club)
                {
                    damage = Mathf.Max(0, damage - 1);
                    GameSfxPlayer.PlayDefenseBlock();
                    Debug.Log(target.playerName + " 的【云纹甲】判定为黑色，伤害 -1");
                    if (battleUI != null)
                    {
                        battleUI.ShowSkillPopup(target.playerName + "【云纹甲】判定成功");
                    }
                }
                else
                {
                    Debug.Log(target.playerName + " 的【云纹甲】判定未成功");
                    if (battleUI != null)
                    {
                        battleUI.ShowSkillPopup(target.playerName + "【云纹甲】未能挡住伤害");
                    }
                }
            }
        }

        onResolved?.Invoke(Mathf.Max(0, damage));
        yield return null;
    }

    private IEnumerator ResolveRedArmorJudgement(
        Player owner,
        string armorName,
        System.Action<bool> onResolved
    )
    {
        CardData judgement = deckManager != null ? deckManager.Draw() : null;
        if (judgement == null)
        {
            onResolved?.Invoke(false);
            yield break;
        }

        if (battleUI != null)
        {
            battleUI.ShowDiscardedCard(owner, judgement, false, armorName + "判定");
        }
        deckManager.Discard(judgement);
        bool succeeded = judgement.IsRedSuit();
        if (succeeded)
        {
            GameSfxPlayer.PlayDefenseBlock();
        }
        if (battleUI != null)
        {
            battleUI.ShowSkillPopup(
                owner.playerName + "【" + armorName + "】判定 " + judgement.GetSuitRankLabel()
                    + (succeeded ? "，触发效果" : "，判定失败")
            );
        }
        onResolved?.Invoke(succeeded);
        yield return null;
    }

    // 五人场击杀奖惩
    private void ApplyKillRewardPenalty(Player killer, Player victim)
    {
        // 击杀护令者：击杀者弃掉所有手牌
        if (victim.identityType == IdentityType.Protector)
        {
            Debug.Log(killer.playerName + " 误杀了护令者，须弃掉所有手牌");
            while (killer.handCards.Count > 0)
            {
                CardData card = killer.handCards[0];
                killer.RemoveCard(card);
                deckManager.Discard(card);
                if (battleUI != null)
                {
                    battleUI.ShowDiscardedCard(killer, card, false, "误杀护令者的惩罚");
                }
            }
            if (battleUI != null)
            {
                battleUI.ShowSkillPopup(killer.playerName + "误杀护令者，弃掉所有手牌");
            }
        }

        // 击杀逆契者：击杀者摸3张牌
        if (victim.identityType == IdentityType.Rebel)
        {
            Debug.Log(killer.playerName + " 击杀了逆契者，摸3张牌");
            for (int i = 0; i < 3; i++)
            {
                CardData card = deckManager.Draw();
                if (card != null)
                    killer.DrawCard(card);
            }
            if (battleUI != null)
            {
                battleUI.ShowSkillPopup(killer.playerName + "击杀逆契者，摸3张牌");
            }
        }
    }

    private void UseEquipCard(Player user, CardData equipCard)
    {
        if (user == null || equipCard == null)
        {
            return;
        }

        if (!user.handCards.Contains(equipCard))
        {
            Debug.LogWarning("装备失败：这张牌不在手牌中");
            return;
        }

        if (equipCard.category != CardCategory.Equip)
        {
            Debug.LogWarning("装备失败：这不是装备牌");
            return;
        }

        CardData oldEquipment = null;

        switch (equipCard.effectType)
        {
            case CardEffectType.Weapon:
                oldEquipment = user.weapon;
                user.weapon = equipCard;
                break;

            case CardEffectType.Armor:
                oldEquipment = user.armor;
                user.armor = equipCard;
                break;

            case CardEffectType.Treasure:
                oldEquipment = user.treasure;
                user.treasure = equipCard;
                break;

            case CardEffectType.MountPlus:
                oldEquipment = user.defensiveHorse;
                user.defensiveHorse = equipCard;
                break;

            case CardEffectType.MountMinus:
                oldEquipment = user.offensiveHorse;
                user.offensiveHorse = equipCard;
                break;

            default:
                Debug.LogWarning("装备失败：未知装备类型 " + equipCard.effectType);
                return;
        }

        user.RemoveCard(equipCard);

        if (oldEquipment != null)
        {
            deckManager.Discard(oldEquipment);
            if (oldEquipment.cardName == "白银狮子" && user.currentHp < user.maxHp)
            {
                StartCoroutine(ResolveSilverLionJudgement(user));
            }
            Debug.Log(
                user.playerName + " 替换装备，原装备【" + oldEquipment.cardName + "】进入弃牌堆"
            );
            if (battleUI != null)
            {
                battleUI.ShowDiscardedCard(user, oldEquipment, true, "被新装备替换");
            }
        }

        Debug.Log(user.playerName + " 装备了【" + equipCard.cardName + "】");

        if (battleUI != null)
        {
            battleUI.ShowEquipmentAction(user, equipCard);
        }

        TryTriggerQiaoZhuSkill(user);

        RefreshBattleUI();
    }

    // 协程：内部会等待伤害与濒死流程完全结算后才返回
    private IEnumerator UseTrickCard(Player user, Player target, CardData trickCard)
    {
        if (user == null || trickCard == null)
        {
            yield break;
        }

        if (!user.handCards.Contains(trickCard))
        {
            Debug.LogWarning("使用锦囊失败：这张牌不在手牌中");
            yield break;
        }

        if (trickCard.effectType == CardEffectType.Nullification)
        {
            Debug.Log("无懈可击只能在响应其他锦囊时使用");
            yield break;
        }

        if (trickCard.needTarget)
        {
            if (!CanUseCardOnTarget(user, target, trickCard))
            {
                Debug.Log("不能对该目标使用这张锦囊");
                yield break;
            }
        }

        if ((trickCard.effectType == CardEffectType.DelayedSkipPlay
                || trickCard.effectType == CardEffectType.DelayedSkipDraw)
            && TargetAlreadyHasDelayedCard(target, trickCard.cardName))
        {
            Debug.Log(target.playerName + " 已经有【" + trickCard.cardName + "】，不能重复放置");
            yield break;
        }
        if (trickCard.effectType == CardEffectType.Lightning
            && TargetAlreadyHasDelayedCard(user, trickCard.cardName))
        {
            Debug.Log(user.playerName + " 已经有【闪电】，不能重复放置");
            yield break;
        }

        CardData targetZoneCard = null;
        if (
            trickCard.effectType == CardEffectType.DiscardTarget
            || trickCard.effectType == CardEffectType.StealTarget
        )
        {
            if (user.isAI || battleUI == null)
            {
                targetZoneCard = ChooseTargetZoneCardForAI(target);
            }
            else
            {
                bool selectionFinished = false;
                string prompt = trickCard.effectType == CardEffectType.DiscardTarget
                    ? "选择要弃置的牌（手牌内容隐藏，装备公开）"
                    : "选择要获得的牌（手牌内容隐藏，装备公开）";
                battleUI.RequestTargetZoneCardChoice(
                    target,
                    prompt,
                    choice =>
                    {
                        targetZoneCard = choice;
                        selectionFinished = true;
                    }
                );
                yield return new WaitUntil(() => selectionFinished || gameOver);
                if (gameOver || !selectionFinished || !TargetStillOwnsCard(target, targetZoneCard))
                {
                    yield break;
                }
            }

            if (!TargetStillOwnsCard(target, targetZoneCard))
            {
                Debug.Log("目标区域已变化，锦囊没有生效");
                yield break;
            }
        }

        if (
            GameSelectionData.gameMode == GameMode.FivePlayer
            && trickCard.needTarget
            && target != null
            && target.identityType == IdentityType.Commander
            && IsHostileTrick(trickCard.effectType)
        )
        {
            AddCommanderSuspicion(user, "对执令者使用敌对锦囊", false);
        }

        user.RemoveCard(trickCard);
        if (trickCard.effectType != CardEffectType.DelayedSkipPlay
            && trickCard.effectType != CardEffectType.DelayedSkipDraw
            && trickCard.effectType != CardEffectType.Lightning)
        {
            deckManager.Discard(trickCard);
        }

        Debug.Log(user.playerName + " 使用锦囊【" + trickCard.cardName + "】");

        // 出牌提示
        if (battleUI != null)
            battleUI.ShowCardAction(user, trickCard.needTarget ? target : null, trickCard);

        bool trickCancelled = false;
        if (trickCard.category == CardCategory.Trick)
        {
            yield return StartCoroutine(
                ResolveNullificationWindow(user, target, trickCard, result => trickCancelled = result)
            );
        }

        if (trickCancelled)
        {
            Debug.Log("【" + trickCard.cardName + "】被无懈可击抵消");
            if (trickCard.effectType == CardEffectType.DelayedSkipPlay
                || trickCard.effectType == CardEffectType.DelayedSkipDraw
                || trickCard.effectType == CardEffectType.Lightning)
            {
                deckManager.Discard(trickCard);
                if (battleUI != null)
                {
                    battleUI.ShowDiscardedCard(user, trickCard, false, "延时锦囊被抵消");
                }
            }
            if (battleUI != null)
            {
                battleUI.ShowSkillPopup("【" + trickCard.cardName + "】的效果被无懈可击抵消");
            }
        }
        else
        {
            switch (trickCard.effectType)
            {
                case CardEffectType.DrawCards:
                    ResolveDrawCardsTrick(user, trickCard);
                    break;

                case CardEffectType.DirectDamage:
                    yield return StartCoroutine(ResolveDirectDamageTrick(user, target, trickCard));
                    break;

                case CardEffectType.AreaDamage:
                    yield return StartCoroutine(ResolveAreaDamageTrick(user, trickCard));
                    break;

            case CardEffectType.Duel:
                yield return StartCoroutine(ResolveDuelTrick(user, target, trickCard));
                break;

            case CardEffectType.BorrowWeapon:
                yield return StartCoroutine(ResolveBorrowWeaponTrick(user, target, trickCard));
                break;

            case CardEffectType.RecoverAll:
                ResolveRecoverAllTrick(user, trickCard);
                break;

            case CardEffectType.Harvest:
                yield return StartCoroutine(ResolveHarvestTrick(user, trickCard));
                break;

            case CardEffectType.DelayedSkipPlay:
            case CardEffectType.DelayedSkipDraw:
                ResolveDelayedTrick(user, target, trickCard);
                break;

            case CardEffectType.Lightning:
                ResolveDelayedTrick(user, user, trickCard);
                break;

            case CardEffectType.FireAttack:
                yield return StartCoroutine(ResolveFireAttackTrick(user, target, trickCard));
                break;

            case CardEffectType.Chain:
                ResolveChainTrick(user, target, trickCard);
                break;

            case CardEffectType.Nullification:
                Debug.Log("无懈可击是响应牌，需要在锦囊响应窗口中使用");
                break;

            case CardEffectType.ForceDiscard:
                ResolveForceDiscardTrick(user, target, trickCard);
                break;

            case CardEffectType.DiscardEquip:
                ResolveDiscardEquipTrick(user, target, trickCard);
                break;

            case CardEffectType.DiscardTarget:
                ResolveDiscardTargetTrick(user, target, trickCard, targetZoneCard);
                break;

            case CardEffectType.StealTarget:
                ResolveStealTargetTrick(user, target, trickCard, targetZoneCard);
                break;

            case CardEffectType.Protect:
                ResolveProtectionTrick(user, target, trickCard);
                break;

            case CardEffectType.InvestigateIdentity:
                ResolveIdentityInvestigation(user, target);
                break;

            case CardEffectType.PlantSuspicion:
                ResolvePlantSuspicion(user, target, trickCard);
                break;

            case CardEffectType.ReduceSuspicion:
                ResolveReduceSuspicion(user, target, trickCard);
                break;

            case CardEffectType.AidDraw:
                ResolveAidDraw(target, trickCard);
                break;

            case CardEffectType.DrawAndDiscard:
                yield return StartCoroutine(ResolveDrawAndDiscardTrick(user, trickCard));
                break;

                case CardEffectType.SuitJudgement:
                    yield return StartCoroutine(ResolveSuitJudgementTrick(user, trickCard));
                    break;

                default:
                    Debug.Log("尚未实现的锦囊效果：" + trickCard.effectType);
                    break;
            }
        }

        if (trickCard.effectType != CardEffectType.SuitJudgement)
        {
            yield return StartCoroutine(TryTriggerGuanXiangFromTopCard(user, trickCard));
        }

        // 玄衡技能【震策】：使用锦囊牌时，每个敌方角色须弃一张牌，无牌则掉一滴血
        yield return StartCoroutine(TryTriggerZhenCeSkill(user));

        RefreshBattleUI();
    }

    private IEnumerator ResolveNullificationWindow(
        Player user,
        Player target,
        CardData trickCard,
        System.Action<bool> onResolved
    )
    {
        if (players == null || players.Count < 2 || user == null || trickCard == null)
        {
            onResolved?.Invoke(false);
            yield break;
        }

        bool cancelled = false;
        int userIndex = players.IndexOf(user);
        for (int offset = 1; offset < players.Count; offset++)
        {
            Player responder = players[(userIndex + offset) % players.Count];
            if (responder == null || responder.currentHp <= 0 || responder == user
                || FindCardByEffectType(responder, CardEffectType.Nullification) == null)
            {
                continue;
            }

            string targetName = target != null ? target.playerName : "全体角色";
            bool usedNullification = false;
            yield return StartCoroutine(
                RequestCardResponse(
                    responder,
                    user,
                    CardEffectType.Nullification,
                    "锦囊响应 · 是否对" + targetName + "使用无懈可击",
                    result => usedNullification = result
                )
            );
            if (usedNullification)
            {
                cancelled = !cancelled;
            }
            if (gameOver)
            {
                break;
            }
        }

        onResolved?.Invoke(cancelled);
    }

    private CardData ChooseTargetZoneCardForAI(Player target)
    {
        if (target == null)
        {
            return null;
        }

        // Equipment is public, so the AI can choose a tactically useful visible card.
        if (target.weapon != null)
        {
            return target.weapon;
        }
        if (target.armor != null)
        {
            return target.armor;
        }
        if (target.treasure != null)
        {
            return target.treasure;
        }

        // Hand contents stay hidden; choose a random slot without inspecting its face.
        if (target.handCards.Count > 0)
        {
            return target.handCards[Random.Range(0, target.handCards.Count)];
        }
        if (target.offensiveHorse != null)
        {
            return target.offensiveHorse;
        }
        if (target.defensiveHorse != null)
        {
            return target.defensiveHorse;
        }

        return null;
    }

    private bool TargetStillOwnsCard(Player target, CardData card)
    {
        return target != null
            && card != null
            && (target.handCards.Contains(card)
                || target.weapon == card
                || target.armor == card
                || target.treasure == card
                || target.offensiveHorse == card
                || target.defensiveHorse == card);
    }

    private void ResolveAidDraw(Player target, CardData trickCard)
    {
        if (target == null || trickCard == null)
        {
            return;
        }

        int drawCount = Mathf.Max(1, trickCard.value);
        for (int i = 0; i < drawCount; i++)
        {
            CardData card = deckManager.Draw();
            if (card != null)
            {
                target.DrawCard(card);
            }
        }

        Debug.Log(target.playerName + " 因【" + trickCard.cardName + "】摸了 " + drawCount + " 张牌");
        RefreshBattleUI();
    }

    private IEnumerator ResolveDrawAndDiscardTrick(Player user, CardData trickCard)
    {
        if (user == null || trickCard == null)
        {
            yield break;
        }

        int requestedDrawCount = Mathf.Max(1, trickCard.value);
        int drawnCount = 0;
        for (int i = 0; i < requestedDrawCount; i++)
        {
            CardData card = deckManager.Draw();
            if (card == null)
            {
                break;
            }

            user.DrawCard(card);
            drawnCount++;
        }

        if (drawnCount == 0 || user.handCards.Count == 0)
        {
            Debug.Log(user.playerName + " 因【" + trickCard.cardName + "】摸不到牌，无需弃牌");
            yield break;
        }

        CardData selectedDiscard = null;
        if (user.isAI || battleUI == null)
        {
            selectedDiscard = FindLeastValuableCard(user);
        }
        else
        {
            bool selectionFinished = false;
            battleUI.RequestOwnHandCardChoice(
                user,
                "整军：选择一张手牌弃置",
                choice =>
                {
                    selectedDiscard = choice;
                    selectionFinished = true;
                }
            );
            yield return new WaitUntil(() => selectionFinished || gameOver);
            if (gameOver || !selectionFinished)
            {
                yield break;
            }
        }

        if (selectedDiscard == null || !user.handCards.Contains(selectedDiscard))
        {
            selectedDiscard = FindLeastValuableCard(user);
            if (selectedDiscard == null)
            {
                yield break;
            }
        }

        user.RemoveCard(selectedDiscard);
        deckManager.Discard(selectedDiscard);
        if (battleUI != null)
        {
            battleUI.ShowDiscardedCard(user, selectedDiscard, false, "整军弃牌");
        }
        Debug.Log(user.playerName + " 因【" + trickCard.cardName + "】摸 " + drawnCount + " 张后弃置一张手牌");
        RefreshBattleUI();
    }

    private IEnumerator ResolveSuitJudgementTrick(Player user, CardData trickCard)
    {
        if (user == null || trickCard == null)
        {
            yield break;
        }

        CardData revealedCard = deckManager.Draw();
        if (revealedCard == null)
        {
            yield break;
        }

        if (battleUI != null)
        {
            battleUI.ShowDiscardedCard(user, revealedCard, false, "牌堆顶判定");
        }
        deckManager.Discard(revealedCard);

        int drawCount = 1;
        if (revealedCard.IsRedSuit() == trickCard.IsRedSuit())
        {
            drawCount++;
        }
        DrawCardsForPlayer(user, drawCount);

        TryTriggerGuanXiangSkill(user, trickCard, revealedCard);
        Debug.Log(user.playerName + " 使用【星占】翻开 " + revealedCard.GetSuitRankLabel());
        RefreshBattleUI();
        yield return null;
    }

    private void DrawCardsForPlayer(Player target, int count)
    {
        if (target == null)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            CardData card = deckManager.Draw();
            if (card == null)
            {
                break;
            }
            target.DrawCard(card);
        }
    }

    private void TryTriggerGuanXiangSkill(Player user, CardData usedCard, CardData revealedCard)
    {
        if (
            !HasSkill(user, "观象")
            || user.usedGuanXiangThisTurn
            || usedCard == null
            || revealedCard == null
        )
        {
            return;
        }

        user.usedGuanXiangThisTurn = true;
        int bonusDraws = 0;
        if (usedCard.suit == revealedCard.suit)
        {
            bonusDraws++;
        }
        if (usedCard.rank == revealedCard.rank)
        {
            bonusDraws++;
        }

        DrawCardsForPlayer(user, bonusDraws);
        GameSfxPlayer.PlaySkillVoice(user);
        if (battleUI != null)
        {
            string resultText = bonusDraws > 0 ? "摸 " + bonusDraws + " 张牌" : "未应验";
            battleUI.ShowSkillPopup(user.playerName + "发动【观象】，" + resultText);
        }
    }

    private IEnumerator TryTriggerGuanXiangFromTopCard(Player user, CardData usedCard)
    {
        if (
            !HasSkill(user, "观象")
            || user.usedGuanXiangThisTurn
            || usedCard == null
        )
        {
            yield break;
        }

        CardData revealedCard = deckManager.Draw();
        if (revealedCard == null)
        {
            yield break;
        }

        if (battleUI != null)
        {
            battleUI.ShowDiscardedCard(user, revealedCard, false, "牌堆顶判定");
        }
        deckManager.Discard(revealedCard);
        TryTriggerGuanXiangSkill(user, usedCard, revealedCard);
        yield return null;
    }

    private void ResolveDrawCardsTrick(Player user, CardData trickCard)
    {
        int drawCount = trickCard.value;

        if (drawCount <= 0)
        {
            drawCount = 1;
        }

        for (int i = 0; i < drawCount; i++)
        {
            CardData drawnCard = deckManager.Draw();

            if (drawnCard != null)
            {
                user.DrawCard(drawnCard);
            }
        }

        Debug.Log(user.playerName + " 因【" + trickCard.cardName + "】摸了 " + drawCount + " 张牌");
    }

    private void TryTriggerQingDeng(Player activePlayer)
    {
        if (activePlayer == null)
        {
            return;
        }

        if (activePlayer.treasure == null)
        {
            return;
        }

        if (activePlayer.treasure.cardName != "青灯")
        {
            return;
        }

        if (activePlayer.handCards.Count > 0 || activePlayer.currentHp >= activePlayer.maxHp)
        {
            return;
        }

        CardData judgementCard = deckManager.Draw();
        if (judgementCard == null)
        {
            return;
        }

        if (battleUI != null)
        {
            battleUI.ShowDiscardedCard(activePlayer, judgementCard, false, "青灯判定");
        }
        deckManager.Discard(judgementCard);

        if (judgementCard.IsRedSuit())
        {
            activePlayer.Heal(1);
            GameSfxPlayer.PlayHeal();
            if (battleUI != null)
            {
                battleUI.ShowHealEffect(activePlayer, 1);
            }
            Debug.Log(activePlayer.playerName + " 的【青灯】判定为红色，回复1点体力");
            if (battleUI != null)
            {
                battleUI.ShowSkillPopup(activePlayer.playerName + "【青灯】判定成功，回复1点体力");
            }
        }
        else
        {
            Debug.Log(activePlayer.playerName + " 的【青灯】判定未成功");
            if (battleUI != null)
            {
                battleUI.ShowSkillPopup(activePlayer.playerName + "【青灯】未能回复体力");
            }
        }

        RefreshBattleUI();
    }

    private IEnumerator ResolveDirectDamageTrick(Player user, Player target, CardData trickCard)
    {
        if (target == null)
        {
            Debug.LogWarning("锦囊伤害失败：目标为空");
            yield break;
        }

        int damage = trickCard.value;

        if (damage <= 0)
        {
            damage = 1;
        }

        Debug.Log(
            user.playerName
                + " 的【"
                + trickCard.cardName
                + "】对 "
                + target.playerName
                + " 造成 "
                + damage
                + " 点伤害"
        );

        // 必须等伤害结算（含濒死选择）走完再返回，
        // 否则父协程会继续出牌，与濒死流程抢改 currentPhase
        yield return StartCoroutine(DealDamage(user, target, damage, trickCard.cardName));
    }

    private IEnumerator RequestCardResponse(
        Player responder,
        Player opposingPlayer,
        CardEffectType requiredCardType,
        string prompt,
        System.Action<bool> onResolved
    )
    {
        if (responder == null || responder.currentHp <= 0 || opposingPlayer == null)
        {
            onResolved?.Invoke(false);
            yield break;
        }

        CardData responseCard = FindCardByEffectType(responder, requiredCardType);
        bool useQingYu = requiredCardType == CardEffectType.Defense
            && responseCard == null
            && HasSkill(responder, "轻羽")
            && responder.handCards.Count > 0;
        if (responseCard == null && !useQingYu)
        {
            onResolved?.Invoke(false);
            yield break;
        }

        if (responder.isAI)
        {
            if (requiredCardType == CardEffectType.Nullification
                && !IsKnownEnemyForAI(responder, opposingPlayer))
            {
                onResolved?.Invoke(false);
                yield break;
            }

            if (requiredCardType == CardEffectType.Defense
                && !useQingYu
                && !ShouldAIUseDefense(responder, opposingPlayer))
            {
                onResolved?.Invoke(false);
                yield break;
            }

            if (useQingYu)
            {
                responseCard = FindAnyCard(responder);
            }
            responder.RemoveCard(responseCard);
            deckManager.Discard(responseCard);
            if (requiredCardType == CardEffectType.Defense && !useQingYu)
            {
                PlayDefenseResponseVoice(responder, responseCard, false);
            }
            else
            {
                GameSfxPlayer.PlayCardAction(responseCard);
                GameSfxPlayer.PlayCardVoice(responder, responseCard);
                if (useQingYu)
                {
                    GameSfxPlayer.PlaySkillVoice(responder);
                }
            }

            if (battleUI != null)
            {
                battleUI.ShowCardResponse(
                    responder,
                    responseCard,
                    responder.playerName + " 打出【" + responseCard.cardName + "】响应 " + prompt
                );
            }
            RefreshBattleUI();
            onResolved?.Invoke(true);
            yield break;
        }

        if (responder != player || battleUI == null)
        {
            onResolved?.Invoke(false);
            yield break;
        }

        pendingAttacker = opposingPlayer;
        pendingDefender = responder;
        pendingResponseCardType = requiredCardType;
        playerResponseFinished = false;
        playerUsedDefense = false;
        currentPhase = TurnPhase.Response;
        battleUI.ShowPendingAttack(opposingPlayer, responder, prompt);
        RefreshBattleUI();

        yield return new WaitUntil(() => playerResponseFinished || gameOver);

        bool responded = playerUsedDefense && !gameOver;
        battleUI.ClearPendingAttack();
        pendingAttacker = null;
        pendingDefender = null;
        pendingResponseCardType = CardEffectType.Defense;
        currentPhase = TurnPhase.Play;
        RefreshBattleUI();
        onResolved?.Invoke(responded);
    }

    private IEnumerator ResolveDuelTrick(Player user, Player target, CardData trickCard)
    {
        if (user == null || target == null)
        {
            yield break;
        }

        Player responder = target;
        Player opponent = user;
        int exchanges = 0;
        const int maxDuelResponses = 20;
        while (responder != null && opponent != null && responder.currentHp > 0 && exchanges < maxDuelResponses)
        {
            bool responded = false;
            yield return StartCoroutine(
                RequestCardResponse(
                    responder,
                    opponent,
                    CardEffectType.Attack,
                    "决斗响应 · 请打出杀",
                    result => responded = result
                )
            );

            if (!responded)
            {
                yield return StartCoroutine(DealDamage(opponent, responder, 1, "决斗"));
                yield break;
            }

            Player previousResponder = responder;
            responder = opponent;
            opponent = previousResponder;
            exchanges++;
        }
    }

    private IEnumerator ResolveAreaDamageTrick(Player user, CardData trickCard)
    {
        if (user == null || trickCard == null)
        {
            yield break;
        }

        int damage = Mathf.Max(1, trickCard.value);
        List<Player> targets = new List<Player>();
        foreach (Player candidate in players)
        {
            if (candidate != null && candidate != user && candidate.currentHp > 0)
            {
                targets.Add(candidate);
            }
        }

        foreach (Player target in targets)
        {
            if (target.currentHp <= 0)
            {
                continue;
            }

            if (
                GameSelectionData.gameMode == GameMode.FivePlayer
                && target.identityType == IdentityType.Commander
            )
            {
                AddCommanderSuspicion(user, "使用【" + trickCard.cardName + "】波及执令者", false);
            }

            CardEffectType responseType = trickCard.cardName == "南蛮入侵"
                ? CardEffectType.Attack
                : CardEffectType.Defense;
            bool responded = false;
            yield return StartCoroutine(
                RequestCardResponse(
                    target,
                    user,
                    responseType,
                    trickCard.cardName + "响应 · 请打出" + (responseType == CardEffectType.Attack ? "杀" : "闪"),
                    result => responded = result
                )
            );
            if (!responded)
            {
                yield return StartCoroutine(DealDamage(user, target, damage, trickCard.cardName));
            }
            if (gameOver)
            {
                yield break;
            }
        }
    }

    private void ResolveRecoverAllTrick(Player user, CardData trickCard)
    {
        foreach (Player target in players)
        {
            if (target == null || target.currentHp <= 0 || target.currentHp >= target.maxHp)
            {
                continue;
            }

            target.Heal(1);
            GameSfxPlayer.PlayHeal();
            if (battleUI != null)
            {
                battleUI.ShowHealEffect(target, 1);
            }
        }
        RefreshBattleUI();
    }

    private IEnumerator ResolveHarvestTrick(Player user, CardData trickCard)
    {
        if (user == null)
        {
            yield break;
        }

        List<CardData> revealed = new List<CardData>();
        int revealCount = 0;
        foreach (Player candidate in players)
        {
            if (candidate != null && candidate.currentHp > 0)
            {
                revealCount++;
            }
        }

        for (int i = 0; i < revealCount; i++)
        {
            CardData card = deckManager.Draw();
            if (card == null)
            {
                break;
            }
            revealed.Add(card);
        }

        if (revealed.Count == 0)
        {
            yield break;
        }

        if (battleUI != null)
        {
            battleUI.ShowCardAction("【五谷丰登】亮出 " + revealed.Count + " 张牌，依次选择");
        }

        int startIndex = Mathf.Max(0, players.IndexOf(user));
        for (int offset = 0; offset < players.Count && revealed.Count > 0; offset++)
        {
            Player recipient = players[(startIndex + offset) % players.Count];
            if (recipient == null || recipient.currentHp <= 0)
            {
                continue;
            }

            CardData selected = null;
            if (recipient == player && battleUI != null)
            {
                bool choiceFinished = false;
                battleUI.RequestRevealedCardChoice(
                    revealed,
                    "五谷丰登：选择一张牌",
                    choice =>
                    {
                        selected = choice;
                        choiceFinished = true;
                    }
                );
                yield return new WaitUntil(() => choiceFinished || gameOver);
                if (gameOver)
                {
                    yield break;
                }
            }
            else
            {
                selected = revealed[0];
            }

            if (selected == null || !revealed.Contains(selected))
            {
                selected = revealed[0];
            }
            revealed.Remove(selected);
            recipient.DrawCard(selected);
            if (battleUI != null)
            {
                battleUI.ShowCardAction(recipient.playerName + " 获得【" + selected.cardName + "】");
            }
        }

        foreach (CardData remaining in revealed)
        {
            deckManager.Discard(remaining);
            if (battleUI != null)
            {
                battleUI.ShowDiscardedCard(user, remaining, false, "五谷丰登未被选中的牌");
            }
        }
        RefreshBattleUI();
    }

    private void ResolveDelayedTrick(Player user, Player target, CardData trickCard)
    {
        if (user == null || target == null || trickCard == null || target.currentHp <= 0)
        {
            return;
        }

        foreach (CardData delayedCard in target.delayedTricks)
        {
            if (delayedCard != null && delayedCard.cardName == trickCard.cardName)
            {
                Debug.Log(target.playerName + " 已经有【" + trickCard.cardName + "】");
                return;
            }
        }

        target.delayedTricks.Add(trickCard);
        Debug.Log(user.playerName + " 将【" + trickCard.cardName + "】放置到 " + target.playerName + " 的判定区");
        if (battleUI != null)
        {
            battleUI.ShowCardAction(user, target, trickCard);
            battleUI.ShowSkillPopup(target.playerName + " 的判定区获得【" + trickCard.cardName + "】");
        }
        RefreshBattleUI();
    }

    private bool TargetAlreadyHasDelayedCard(Player target, string cardName)
    {
        if (target == null || target.delayedTricks == null)
        {
            return false;
        }
        foreach (CardData delayedCard in target.delayedTricks)
        {
            if (delayedCard != null && delayedCard.cardName == cardName)
            {
                return true;
            }
        }
        return false;
    }

    private void ResolveChainTrick(Player user, Player target, CardData trickCard)
    {
        if (target == null || target.currentHp <= 0)
        {
            return;
        }

        target.chained = !target.chained;
        Debug.Log(target.playerName + (target.chained ? " 被横置" : " 重置") + "（【铁索连环】）");
        if (battleUI != null)
        {
            battleUI.ShowSkillPopup(target.playerName + (target.chained ? " 被横置" : " 重置"));
        }
        RefreshBattleUI();
    }

    private void RemoveEquipmentWithFeedback(Player owner, CardData equipment)
    {
        if (owner == null || equipment == null)
        {
            return;
        }

        owner.RemoveEquipment(equipment);
        if (equipment.cardName == "白银狮子" && owner.currentHp > 0 && owner.currentHp < owner.maxHp)
        {
            StartCoroutine(ResolveSilverLionJudgement(owner));
        }
    }

    private IEnumerator ResolveIceSword(Player attacker, Player target)
    {
        for (int i = 0; i < 2; i++)
        {
            int targetCardCount = target.handCards.Count
                + (target.weapon != null ? 1 : 0)
                + (target.armor != null ? 1 : 0)
                + (target.treasure != null ? 1 : 0)
                + (target.offensiveHorse != null ? 1 : 0)
                + (target.defensiveHorse != null ? 1 : 0);
            if (targetCardCount == 0)
            {
                break;
            }

            CardData selected = null;
            if (attacker.isAI || battleUI == null)
            {
                selected = ChooseTargetZoneCardForAI(target);
            }
            else
            {
                bool choiceFinished = false;
                battleUI.RequestTargetZoneCardChoice(
                    target,
                    "寒冰剑：选择要弃置的牌",
                    choice =>
                    {
                        selected = choice;
                        choiceFinished = true;
                    }
                );
                yield return new WaitUntil(() => choiceFinished || gameOver);
                if (gameOver || selected == null)
                {
                    yield break;
                }
            }

            if (!TargetStillOwnsCard(target, selected))
            {
                break;
            }

            bool equipment = selected.category == CardCategory.Equip;
            if (equipment)
            {
                RemoveEquipmentWithFeedback(target, selected);
            }
            else if (attacker.isAI && !target.isAI)
            {
                target.RemoveCardSilently(selected);
            }
            else
            {
                target.RemoveCard(selected);
            }
            deckManager.Discard(selected);
            GameSfxPlayer.PlayCardDiscard();
            if (battleUI != null)
            {
                if (i == 0)
                {
                    battleUI.ShowSkillPopup(attacker.playerName + "发动【寒冰剑】");
                }
                battleUI.ShowDiscardedCard(target, selected, equipment, "寒冰剑弃置");
            }
        }

        Debug.Log(attacker.playerName + " 发动【寒冰剑】，弃置 " + target.playerName + " 最多两张牌");
        RefreshBattleUI();
    }

    private IEnumerator ResolveSilverLionJudgement(Player owner)
    {
        if (owner == null || owner.currentHp <= 0 || owner.currentHp >= owner.maxHp)
        {
            yield break;
        }

        CardData judgement = deckManager != null ? deckManager.Draw() : null;
        if (judgement == null)
        {
            yield break;
        }
        if (battleUI != null)
        {
            battleUI.ShowDiscardedCard(owner, judgement, false, "白银狮子离场判定");
        }
        deckManager.Discard(judgement);

        if (judgement.IsRedSuit())
        {
            owner.Heal(1);
            GameSfxPlayer.PlayHeal();
            if (battleUI != null)
            {
                battleUI.ShowHealEffect(owner, 1);
                battleUI.ShowSkillPopup(owner.playerName + "【白银狮子】判定成功，回复1点体力");
            }
        }
        else if (battleUI != null)
        {
            battleUI.ShowSkillPopup(owner.playerName + "【白银狮子】判定未成功");
        }
        RefreshBattleUI();
    }

    private IEnumerator ResolveFireAttackTrick(Player user, Player target, CardData trickCard)
    {
        if (user == null || target == null || target.handCards.Count == 0)
        {
            yield break;
        }

        CardData revealed = null;
        if (target == player && battleUI != null)
        {
            bool revealChoiceFinished = false;
            battleUI.RequestOwnHandCardChoice(
                target,
                "火攻：选择一张手牌展示",
                choice =>
                {
                    revealed = choice;
                    revealChoiceFinished = true;
                }
            );
            yield return new WaitUntil(() => revealChoiceFinished || gameOver);
            if (gameOver || revealed == null || !target.handCards.Contains(revealed))
            {
                yield break;
            }
        }
        else
        {
            revealed = target.handCards[Random.Range(0, target.handCards.Count)];
        }
        if (battleUI != null)
        {
            battleUI.ShowCardAction(
                target.playerName + " 展示【" + revealed.cardName + "】 " + revealed.GetSuitRankLabel()
            );
        }

        List<CardData> matchingCards = new List<CardData>();
        foreach (CardData card in user.handCards)
        {
            if (card != null && card.suit == revealed.suit)
            {
                matchingCards.Add(card);
            }
        }
        if (matchingCards.Count == 0)
        {
            Debug.Log(user.playerName + " 没有与展示牌同花色的牌，【火攻】未造成伤害");
            yield break;
        }

        CardData discarded = null;
        if (user.isAI || battleUI == null)
        {
            discarded = matchingCards[0];
        }
        else
        {
            bool choiceFinished = false;
            battleUI.RequestRevealedCardChoice(
                matchingCards,
                "火攻：弃置一张与展示牌同花色的手牌",
                choice =>
                {
                    discarded = choice;
                    choiceFinished = true;
                },
                "可弃置的手牌"
            );
            yield return new WaitUntil(() => choiceFinished || gameOver);
            if (gameOver)
            {
                yield break;
            }
        }

        if (discarded == null || !user.handCards.Contains(discarded))
        {
            yield break;
        }
        user.RemoveCard(discarded);
        deckManager.Discard(discarded);
        if (battleUI != null)
        {
            battleUI.ShowDiscardedCard(user, discarded, false, "火攻弃置同花色手牌");
        }
        yield return StartCoroutine(
            DealDamage(user, target, 1, "火攻", CardDamageElement.Fire, trickCard)
        );
    }

    private IEnumerator ResolveBorrowWeaponTrick(Player user, Player target, CardData trickCard)
    {
        if (user == null || target == null || target.weapon == null)
        {
            yield break;
        }

        CardData weaponCard = target.weapon;
        Player victim = target.isAI ? FindBestEnemyTarget(target) : null;
        CardData attackCard = FindCardByEffectType(target, CardEffectType.Attack);
        if (target.isAI
            && victim != null
            && victim != target
            && attackCard != null
            && target.CanUseAttack()
            && IsWithinAttackRange(target, victim))
        {
            yield return StartCoroutine(UseAttackCard(target, victim, attackCard));
            yield break;
        }

        RemoveEquipmentWithFeedback(target, weaponCard);
        user.DrawCard(weaponCard);
        if (battleUI != null)
        {
            battleUI.ShowCardAction(user.playerName + " 获得了 " + target.playerName + " 的武器【" + weaponCard.cardName + "】");
            battleUI.ShowDiscardedCard(target, weaponCard, true, "借刀杀人取走武器");
        }
        Debug.Log(user.playerName + " 因【借刀杀人】获得了 " + target.playerName + " 的武器");
        RefreshBattleUI();
    }

    private IEnumerator ResolveDelayedTricks(Player activePlayer)
    {
        if (activePlayer == null || activePlayer.delayedTricks.Count == 0)
        {
            yield break;
        }

        List<CardData> queued = new List<CardData>(activePlayer.delayedTricks);
        foreach (CardData delayedCard in queued)
        {
            if (gameOver || activePlayer.currentHp <= 0 || delayedCard == null
                || !activePlayer.delayedTricks.Contains(delayedCard))
            {
                continue;
            }

            CardData judgement = deckManager.Draw();
            if (judgement == null)
            {
                continue;
            }
            if (battleUI != null)
            {
                battleUI.ShowDiscardedCard(activePlayer, judgement, false, "延时锦囊判定");
            }
            deckManager.Discard(judgement);
            bool transferredLightning = false;

            if (delayedCard.effectType == CardEffectType.DelayedSkipPlay)
            {
                bool succeeds = judgement.suit == CardSuit.Heart;
                activePlayer.skipPlayThisTurn = !succeeds;
                Debug.Log("【乐不思蜀】判定 " + judgement.GetSuitRankLabel() + (succeeds ? "，判定成功" : "，跳过出牌阶段"));
            }
            else if (delayedCard.effectType == CardEffectType.DelayedSkipDraw)
            {
                bool succeeds = judgement.suit == CardSuit.Club;
                activePlayer.skipDrawThisTurn = !succeeds;
                Debug.Log("【兵粮寸断】判定 " + judgement.GetSuitRankLabel() + (succeeds ? "，判定成功" : "，跳过摸牌阶段"));
            }
            else if (delayedCard.effectType == CardEffectType.Lightning)
            {
                bool hits = judgement.suit == CardSuit.Spade && judgement.rank >= 2 && judgement.rank <= 9;
                if (hits)
                {
                    yield return StartCoroutine(
                        DealDamage(null, activePlayer, 3, "闪电", CardDamageElement.Thunder, delayedCard)
                    );
                }
                else
                {
                    Player nextPlayer = GetNextLivingPlayer(activePlayer);
                    if (nextPlayer != null)
                    {
                        nextPlayer.delayedTricks.Add(delayedCard);
                        transferredLightning = true;
                        Debug.Log("【闪电】未命中，移至 " + nextPlayer.playerName + " 的判定区");
                    }
                }
            }

            activePlayer.delayedTricks.Remove(delayedCard);
            if (!transferredLightning)
            {
                deckManager.Discard(delayedCard);
                if (battleUI != null)
                {
                    battleUI.ShowDiscardedCard(activePlayer, delayedCard, false, "延时锦囊结算");
                }
            }
            yield return new WaitForSeconds(0.2f);
        }
        RefreshBattleUI();
    }

    private Player GetNextLivingPlayer(Player current)
    {
        if (players == null || players.Count < 2)
        {
            return null;
        }

        int currentIndex = players.IndexOf(current);
        for (int offset = 1; offset < players.Count; offset++)
        {
            Player candidate = players[(currentIndex + offset + players.Count) % players.Count];
            if (candidate != null && candidate.currentHp > 0)
            {
                return candidate;
            }
        }
        return null;
    }

    private void ResolveForceDiscardTrick(Player user, Player target, CardData trickCard)
    {
        if (target == null)
        {
            Debug.LogWarning("弃牌锦囊失败：目标为空");
            return;
        }

        int discardCount = trickCard.value;

        if (discardCount <= 0)
        {
            discardCount = 1;
        }

        if (target.handCards.Count == 0)
        {
            Debug.Log(target.playerName + " 没有手牌，无法弃牌");
            return;
        }

        for (int i = 0; i < discardCount; i++)
        {
            if (target.handCards.Count == 0)
            {
                break;
            }

            CardData cardToDiscard = target.handCards[Random.Range(0, target.handCards.Count)];

            target.RemoveCard(cardToDiscard);
            deckManager.Discard(cardToDiscard);
            if (battleUI != null)
            {
                battleUI.ShowDiscardedCard(target, cardToDiscard, false, "被迫弃牌");
            }

            Debug.Log(
                target.playerName
                    + " 因【"
                    + trickCard.cardName
                    + "】弃掉了："
                    + cardToDiscard.cardName
            );
        }
    }

    // 弃装备锦囊：弃掉目标第一张装备牌
    private void ResolveDiscardEquipTrick(Player user, Player target, CardData trickCard)
    {
        if (target == null)
        {
            Debug.LogWarning("弃装备锦囊失败：目标为空");
            return;
        }

        if (!target.HasEquipment())
        {
            Debug.Log(target.playerName + " 没有装备，弃装备锦囊无效");
            return;
        }

        int discardCount = trickCard.value;
        if (discardCount <= 0)
            discardCount = 1;

        for (int i = 0; i < discardCount; i++)
        {
            CardData equipToDiscard = target.GetFirstEquipment();
            if (equipToDiscard == null)
                break;

            RemoveEquipmentWithFeedback(target, equipToDiscard);
            deckManager.Discard(equipToDiscard);
            if (battleUI != null)
            {
                battleUI.ShowDiscardedCard(target, equipToDiscard, true, "装备被拆除");
            }

            Debug.Log(
                user.playerName
                    + " 使用【"
                    + trickCard.cardName
                    + "】弃掉了 "
                    + target.playerName
                    + " 的装备【"
                    + equipToDiscard.cardName
                    + "】"
            );
        }

        RefreshBattleUI();
    }

    private bool IsHostileTrick(CardEffectType effectType)
    {
        return effectType == CardEffectType.DirectDamage
            || effectType == CardEffectType.ForceDiscard
            || effectType == CardEffectType.DiscardEquip
            || effectType == CardEffectType.DiscardTarget
            || effectType == CardEffectType.StealTarget
            || effectType == CardEffectType.AreaDamage
            || effectType == CardEffectType.Duel
            || effectType == CardEffectType.BorrowWeapon
            || effectType == CardEffectType.FireAttack
            || effectType == CardEffectType.DelayedSkipPlay
            || effectType == CardEffectType.DelayedSkipDraw;
    }

    private void ResolveDiscardTargetTrick(
        Player user,
        Player target,
        CardData trickCard,
        CardData selectedCard
    )
    {
        if (target == null || !TargetStillOwnsCard(target, selectedCard))
        {
            return;
        }

        int discardCount = Mathf.Max(1, trickCard.value);
        for (int i = 0; i < discardCount; i++)
        {
            CardData discarded = i == 0 ? selectedCard : ChooseTargetZoneCardForAI(target);
            if (!TargetStillOwnsCard(target, discarded))
            {
                break;
            }

            bool discardedEquipment = discarded.category == CardCategory.Equip;
            if (discardedEquipment)
                RemoveEquipmentWithFeedback(target, discarded);
            else
                target.RemoveCard(discarded);

            deckManager.Discard(discarded);
            if (battleUI != null)
            {
                battleUI.ShowDiscardedCard(target, discarded, discardedEquipment, "区域牌被弃置");
            }
            Debug.Log(
                user.playerName
                    + " 使用【"
                    + trickCard.cardName
                    + "】弃置了 "
                    + target.playerName
                    + " 区域内的【"
                    + discarded.cardName
                    + "】"
            );
        }
    }

    private void ResolveStealTargetTrick(
        Player user,
        Player target,
        CardData trickCard,
        CardData selectedCard
    )
    {
        if (user == null || target == null || !TargetStillOwnsCard(target, selectedCard))
        {
            return;
        }

        CardData stolenCard = selectedCard;
        bool stolenEquipment = stolenCard.category == CardCategory.Equip;
        if (stolenEquipment)
        {
            RemoveEquipmentWithFeedback(target, stolenCard);
        }
        else if (user.isAI && !target.isAI)
        {
            // Keep the identity of a human's stolen face-down card hidden from the public log.
            target.RemoveCardSilently(stolenCard);
        }
        else
        {
            target.RemoveCard(stolenCard);
        }

        user.DrawCard(stolenCard);
        string stolenCardLabel = stolenEquipment
            ? "装备【" + stolenCard.cardName + "】"
            : user.isAI ? "一张暗牌" : "【" + stolenCard.cardName + "】";
        Debug.Log(user.playerName + " 从 " + target.playerName + " 处获得了" + stolenCardLabel);
        if (battleUI != null)
        {
            battleUI.ShowSkillPopup(user.playerName + " 获得" + stolenCardLabel);
            battleUI.RefreshUI();
        }
    }

    private void ResolveProtectionTrick(Player user, Player target, CardData trickCard)
    {
        if (target == null)
        {
            return;
        }

        target.protectionValue = Mathf.Max(target.protectionValue, Mathf.Max(1, trickCard.value));
        // Expiration is counted at the protected character's turn end. A shield
        // placed on oneself must skip the current turn's cleanup, then expire
        // after that character's next turn if it has not absorbed damage.
        int ownerTurnEndsUntilExpiry = target == user && user == currentPlayer ? 2 : 1;
        target.protectionTurnsRemaining = Mathf.Max(
            target.protectionTurnsRemaining,
            ownerTurnEndsUntilExpiry
        );
        Debug.Log(
            user.playerName
                + " 为 "
                + target.playerName
                + " 施加护盾，下次受伤减免 "
                + target.protectionValue
        );
        if (battleUI != null)
        {
            battleUI.ShowSkillPopup(target.playerName + " 获得护盾");
        }
    }

    private void ResolveIdentityInvestigation(Player user, Player target)
    {
        if (target == null || target.isIdentityRevealed || target.commanderSuspicion <= 0)
        {
            Debug.Log("目标没有可查验的疑迹");
            return;
        }

        target.isIdentityRevealed = true;
        string identityName = GetIdentityName(target.identityType);
        RecordMatchHighlight("察微查明 " + target.playerName + " 的身份是" + identityName);
        Debug.Log(user.playerName + " 查验疑迹，揭示 " + target.playerName + " 是【" + identityName + "】");
        if (battleUI != null)
        {
            battleUI.ShowSkillPopup(target.playerName + " 的身份揭示为【" + identityName + "】");
        }
    }

    private void ResolvePlantSuspicion(Player user, Player target, CardData trickCard)
    {
        if (target == null || target.isIdentityRevealed)
        {
            return;
        }

        AddCommanderSuspicion(target, "伪迹（由" + user.playerName + "制造）", false);
        RecordMatchHighlight(user.playerName + " 对 " + target.playerName + " 制造伪迹");
        Debug.Log(user.playerName + " 使用【" + trickCard.cardName + "】将疑迹引向 " + target.playerName);
    }

    private void ResolveReduceSuspicion(Player user, Player target, CardData trickCard)
    {
        if (target == null || target.isIdentityRevealed)
        {
            return;
        }

        int minimumSuspicion = target.hasAttackedCommander ? 1 : 0;
        target.commanderSuspicion = Mathf.Max(
            minimumSuspicion,
            target.commanderSuspicion - Mathf.Max(1, trickCard.value)
        );
        Debug.Log(user.playerName + " 使用【" + trickCard.cardName + "】消除了 " + target.playerName + " 的一条疑迹");
        RefreshBattleUI();
    }

    private void UseHealCardOnSelf(Player user, CardData healCard)
    {
        if (user == null || healCard == null)
        {
            return;
        }

        if (!user.handCards.Contains(healCard))
        {
            Debug.LogWarning("使用桃失败：这张牌不在手牌中");
            return;
        }

        if (user.currentHp >= user.maxHp)
        {
            Debug.Log(user.playerName + " 满血，不能使用桃");
            return;
        }

        user.RemoveCard(healCard);
        deckManager.Discard(healCard);

        user.Heal(1);

        Debug.Log(user.playerName + " 使用桃，回复 1 点体力");

        // 出牌提示
        if (battleUI != null)
            battleUI.ShowCardAction(user, user, healCard);

        TryTriggerQingLanSkill(user);

        RefreshBattleUI();
    }

    private void UseWineCard(Player user, CardData wineCard)
    {
        if (user == null || wineCard == null || !user.handCards.Contains(wineCard))
        {
            return;
        }

        if (user.usedWineThisTurn)
        {
            Debug.Log(user.playerName + " 本回合已经使用过酒");
            return;
        }

        user.RemoveCard(wineCard);
        deckManager.Discard(wineCard);
        user.usedWineThisTurn = true;
        user.hasWineBoost = true;
        GameSfxPlayer.PlayCardAction(wineCard);
        GameSfxPlayer.PlayCardVoice(user, wineCard);

        if (battleUI != null)
        {
            battleUI.ShowCardAction(user, user, wineCard);
            battleUI.ShowSkillPopup(user.playerName + " 使用【酒】，下一张杀伤害 +1");
        }

        Debug.Log(user.playerName + " 使用【酒】，下一张杀伤害 +1");
        RefreshBattleUI();
    }

    private IEnumerator ResolveBaguaJudgement(Player defender, System.Action<bool> onResolved)
    {
        CardData judgement = deckManager != null ? deckManager.Draw() : null;
        if (judgement == null)
        {
            onResolved?.Invoke(false);
            yield break;
        }

        if (battleUI != null)
        {
            battleUI.ShowDiscardedCard(defender, judgement, false, "八卦阵判定");
        }
        deckManager.Discard(judgement);

        bool dodged = judgement.IsRedSuit();
        if (dodged)
        {
            GameSfxPlayer.PlayDefenseBlock();
        }
        if (battleUI != null)
        {
            battleUI.ShowSkillPopup(
                defender.playerName + "【八卦阵】判定 " + judgement.GetSuitRankLabel()
                    + (dodged ? "，视为打出闪" : "，判定失败")
            );
        }
        Debug.Log(defender.playerName + " 的【八卦阵】判定 " + judgement.GetSuitRankLabel() + (dodged ? "，成功" : "，失败"));
        onResolved?.Invoke(dodged);
        yield return null;
    }

    private IEnumerator UseAttackCard(Player attacker, Player defender, CardData attackCard)
    {
        if (attackCard == null)
        {
            yield break;
        }

        if (!attacker.handCards.Contains(attackCard))
        {
            yield break;
        }

        if (attacker.qingLongFollowUpTarget == defender)
        {
            attacker.qingLongFollowUpTarget = null;
        }

        bool wineBoost = attacker.hasWineBoost;
        attacker.hasWineBoost = false;
        int attackDamage = attackCard.value + (wineBoost ? 1 : 0);
        CardDamageElement attackDamageElement = attackCard.damageElement;
        if (attacker.weapon != null
            && attacker.weapon.cardName == "朱雀羽扇"
            && attackDamageElement == CardDamageElement.Normal)
        {
            attackDamageElement = CardDamageElement.Fire;
            if (battleUI != null)
            {
                battleUI.ShowSkillPopup(attacker.playerName + "发动【朱雀羽扇】，此杀视为火杀");
            }
        }
        if (attacker.weapon != null
            && attacker.weapon.cardName == "古锭刀"
            && defender.handCards.Count == 0)
        {
            attackDamage++;
        }

        Debug.Log(
            attacker.playerName + " 对 " + defender.playerName + " 使用了 " + attackCard.cardName
        );

        attacker.RemoveCard(attackCard);
        deckManager.Discard(attackCard);

        // 出牌提示：谁对谁使用了杀
        if (battleUI != null)
            battleUI.ShowCardAction(attacker, defender, attackCard);

        attacker.RecordUseAttack();

        // A committed attack is a confirmed clue; other hostile tricks leave an uncertain clue.
        if (GameSelectionData.gameMode == GameMode.FivePlayer && defender.identityType == IdentityType.Commander)
        {
            AddCommanderSuspicion(attacker, "攻击执令者", true);
        }

        pendingAttacker = attacker;
        pendingDefender = defender;

        bool attackAvoided = false;
        bool attackAvoidedByFlash = false;

        if (defender == player)
        {
            // 玩家被攻击：查找闪牌，白鸢可用任意牌当闪
            CardData defenseCard = FindCardByEffectType(player, CardEffectType.Defense);

            if (defenseCard == null && HasSkill(player, "轻羽"))
            {
                // 白鸢技能：任意牌当闪
                defenseCard = FindAnyCard(player);
            }

            if (defenseCard != null)
            {
                pendingResponseCardType = CardEffectType.Defense;
                currentPhase = TurnPhase.Response;
                playerResponseFinished = false;
                playerUsedDefense = false;

                Debug.Log("玩家被杀，可以选择是否使用闪");
                if (battleUI != null)
                {
                    battleUI.ShowPendingAttack(attacker, defender, "需要响应 · 请出闪");
                }
                RefreshBattleUI();

                yield return new WaitUntil(() => playerResponseFinished || gameOver);
                if (battleUI != null)
                {
                    battleUI.ClearPendingAttack();
                }

                attackAvoided = playerUsedDefense;
                attackAvoidedByFlash = playerUsedDefense
                    && defenseCard.effectType == CardEffectType.Defense;
            }
            else
            {
                Debug.Log("玩家没有闪，无法响应");
            }

            if (!attackAvoided && !gameOver && player.armor != null && player.armor.cardName == "八卦阵")
            {
                yield return StartCoroutine(
                    ResolveBaguaJudgement(player, result => attackAvoided = result)
                );
            }
        }
        else
        {
            // AI被攻击：查找闪牌，白鸢可用任意牌当闪
            CardData defenseCard = FindCardByEffectType(defender, CardEffectType.Defense);

            if (defenseCard == null && HasSkill(defender, "轻羽"))
            {
                // 白鸢技能：任意牌当闪
                defenseCard = FindAnyCard(defender);
            }

            if (defenseCard != null && ShouldAIUseDefense(defender, attacker))
            {
                if (battleUI != null)
                {
                    battleUI.ShowPendingAttack(
                        attacker,
                        defender,
                        defender.playerName + " 正在响应 · 闪"
                    );
                    yield return new WaitForSeconds(0.32f);
                }

                defender.RemoveCard(defenseCard);
                deckManager.Discard(defenseCard);
                PlayDefenseResponseVoice(defender, defenseCard, false);
                if (battleUI != null)
                {
                    battleUI.ClearPendingAttack();
                }

                attackAvoided = true;
                attackAvoidedByFlash = defenseCard.effectType == CardEffectType.Defense;

                if (defenseCard.effectType == CardEffectType.Defense)
                {
                    Debug.Log(defender.playerName + " 自动使用了闪，抵消了攻击");

                    if (battleUI != null)
                    {
                        battleUI.ShowCardResponse(
                            defender,
                            defenseCard,
                            defender.playerName + " 使用【闪】抵消攻击"
                        );
                    }
                }
                else
                {
                    Debug.Log(
                        defender.playerName
                            + " 发动【轻羽】，将【"
                            + defenseCard.cardName
                            + "】当闪使用"
                    );

                    if (battleUI != null)
                    {
                        battleUI.ShowCardResponse(
                            defender,
                            defenseCard,
                            defender.playerName + " 发动【轻羽】将【"
                                + defenseCard.cardName + "】当闪"
                        );
                    }
                }
            }
            else if (defender.armor != null && defender.armor.cardName == "八卦阵")
            {
                yield return StartCoroutine(
                    ResolveBaguaJudgement(defender, result => attackAvoided = result)
                );
            }
            else if (defenseCard != null)
            {
                Debug.Log(defender.playerName + " 判断这次攻击风险较低，保留了防御牌");
            }
        }

        // 白鸢技能【轻羽】延伸：如果被攻击者没有闪，白鸢队友可以帮忙出闪
        if (!attackAvoided)
        {
            foreach (Player ally in players)
            {
                if (ally == null || ally == defender || ally.currentHp <= 0)
                {
                    continue;
                }

                // 只允许同阵营帮忙
                if (IsEnemy(ally, defender))
                {
                    continue;
                }

                // 只有白鸢（拥有轻羽技能）才能帮队友出闪
                if (!HasSkill(ally, "轻羽"))
                {
                    continue;
                }

                CardData allyDefenseCard = FindCardByEffectType(ally, CardEffectType.Defense);

                if (allyDefenseCard == null)
                {
                    // 白鸢可以用任意牌当闪帮队友
                    allyDefenseCard = FindAnyCard(ally);
                }

                if (allyDefenseCard != null)
                {
                    if (ally.isAI)
                    {
                        // AI白鸢自动帮队友出闪
                        if (battleUI != null)
                        {
                            battleUI.ShowPendingAttack(
                                attacker,
                                defender,
                                ally.playerName + " 正在代队友出闪"
                            );
                            yield return new WaitForSeconds(0.32f);
                        }

                        ally.RemoveCard(allyDefenseCard);
                        deckManager.Discard(allyDefenseCard);
                        PlayDefenseResponseVoice(ally, allyDefenseCard, true);
                        if (battleUI != null)
                        {
                            battleUI.ClearPendingAttack();
                        }
                        attackAvoided = true;
                        attackAvoidedByFlash = allyDefenseCard.effectType == CardEffectType.Defense;

                        if (allyDefenseCard.effectType == CardEffectType.Defense)
                        {
                            Debug.Log(
                                ally.playerName
                                    + " 帮 "
                                    + defender.playerName
                                    + " 使用了闪，抵消了攻击"
                            );
                            if (battleUI != null)
                            {
                                battleUI.ShowCardResponse(
                                    ally,
                                    allyDefenseCard,
                                    ally.playerName + " 为 " + defender.playerName + " 打出【闪】"
                                );
                            }
                        }
                        else
                        {
                            Debug.Log(
                                ally.playerName
                                    + " 发动【轻羽】，将【"
                                    + allyDefenseCard.cardName
                                    + "】当闪帮 "
                                    + defender.playerName
                                    + " 抵消了攻击"
                            );

                            if (battleUI != null)
                            {
                                battleUI.ShowCardResponse(
                                    ally,
                                    allyDefenseCard,
                                    ally.playerName + " 发动【轻羽】代队友响应"
                                );
                                battleUI.ShowSkillPopup(ally.playerName + "发动【轻羽】帮队友出闪");
                            }
                        }

                        break;
                    }
                    // 玩家白鸢帮AI队友出闪：进入响应阶段让玩家选牌
                    else if (ally == player)
                    {
                        // 检查玩家是否有闪牌或任意牌（轻羽）
                        bool hasDefense =
                            FindCardByEffectType(player, CardEffectType.Defense) != null;
                        CardData playerFlashBeforeResponse = FindCardByEffectType(
                            player,
                            CardEffectType.Defense
                        );
                        bool hasAnyCard = player.handCards.Count > 0;

                        if (hasDefense || hasAnyCard)
                        {
                            pendingAttacker = attacker;
                            pendingDefender = defender;
                            pendingResponseCardType = CardEffectType.Defense;
                            playerResponseFinished = false;
                            playerUsedDefense = false;
                            currentPhase = TurnPhase.Response;
                            if (battleUI != null)
                            {
                                battleUI.ShowPendingAttack(attacker, defender, "队友受杀 · 请帮出闪");
                            }
                            RefreshBattleUI();
                            yield return new WaitUntil(() => playerResponseFinished || gameOver);
                            if (battleUI != null)
                            {
                                battleUI.ClearPendingAttack();
                            }

                            if (gameOver)
                                yield break;

                            if (playerUsedDefense)
                            {
                                attackAvoided = true;
                                attackAvoidedByFlash = playerFlashBeforeResponse != null
                                    && !player.handCards.Contains(playerFlashBeforeResponse);
                                Debug.Log(
                                    player.playerName
                                        + " 帮 "
                                        + defender.playerName
                                        + " 出闪，抵消了攻击"
                                );
                            }
                        }
                    }
                }
            }
        }

        if (battleUI != null)
        {
            battleUI.ClearPendingAttack();
        }
        pendingAttacker = null;
        pendingDefender = null;
        pendingResponseCardType = CardEffectType.Defense;

        if (attackAvoided)
        {
            GameSfxPlayer.PlayDefenseBlock();
            TryTriggerJieFengSkill(defender);
        }

        if (attackAvoidedByFlash
            && attacker.weapon != null
            && attacker.weapon.cardName == "寒冰剑"
            && defender.handCards.Count
                + (defender.weapon != null ? 1 : 0)
                + (defender.armor != null ? 1 : 0)
                + (defender.treasure != null ? 1 : 0)
                + (defender.offensiveHorse != null ? 1 : 0)
                + (defender.defensiveHorse != null ? 1 : 0) >= 2)
        {
            bool useIceSword = attacker.isAI
                ? IsKnownEnemyForAI(attacker, defender)
                : false;
            if (!attacker.isAI && battleUI != null)
            {
                bool choiceFinished = false;
                Canvas ownerCanvas = battleUI.GetComponentInParent<Canvas>();
                if (ownerCanvas == null)
                {
                    ownerCanvas = FindObjectOfType<Canvas>();
                }
                GameConfirmationDialog.Show(
                    ownerCanvas,
                    "寒冰剑",
                    "目标已闪避。是否弃置其最多两张牌，代替本次杀造成伤害？",
                    "弃置牌",
                    "保留伤害",
                    () =>
                    {
                        useIceSword = true;
                        choiceFinished = true;
                    },
                    GameUIButtonTone.PrimaryAction
                );
                GameConfirmationDialog dialog = ownerCanvas != null
                    ? ownerCanvas.GetComponentInChildren<GameConfirmationDialog>(true)
                    : null;
                yield return new WaitUntil(
                    () => choiceFinished || dialog == null || !dialog.gameObject.activeSelf || gameOver
                );
            }

            if (useIceSword && !gameOver)
            {
                yield return StartCoroutine(ResolveIceSword(attacker, defender));
            }
        }

        if (attackAvoidedByFlash
            && attacker.weapon != null
            && attacker.weapon.cardName == "青龙偃月刀")
        {
            attacker.qingLongFollowUpTarget = defender;
            if (battleUI != null)
            {
                battleUI.ShowSkillPopup(attacker.playerName + "【青龙偃月刀】可对同一目标继续出杀");
            }
        }

        if (!attackAvoided)
        {
            int beforeHp = defender.currentHp;

            yield return StartCoroutine(
                DealDamage(
                    attacker,
                    defender,
                    attackDamage,
                    attackCard.cardName,
                    attackDamageElement,
                    attackCard
                )
            );

            if (defender.currentHp < beforeHp)
            {
                if (attacker.weapon != null && attacker.weapon.cardName == "麒麟弓")
                {
                    List<CardData> mounts = new List<CardData>();
                    if (defender.offensiveHorse != null) mounts.Add(defender.offensiveHorse);
                    if (defender.defensiveHorse != null) mounts.Add(defender.defensiveHorse);
                    CardData mountToDiscard = mounts.Count > 0 ? mounts[0] : null;
                    if (defender == player && mounts.Count > 0 && battleUI != null)
                    {
                        bool mountChoiceFinished = false;
                        battleUI.RequestRevealedCardChoice(
                            mounts,
                            "麒麟弓：选择弃置目标的一匹坐骑",
                            choice =>
                            {
                                mountToDiscard = choice;
                                mountChoiceFinished = true;
                            },
                            "目标坐骑"
                        );
                        yield return new WaitUntil(() => mountChoiceFinished || gameOver);
                    }

                    if (mountToDiscard != null
                        && (defender.offensiveHorse == mountToDiscard || defender.defensiveHorse == mountToDiscard))
                    {
                        RemoveEquipmentWithFeedback(defender, mountToDiscard);
                        deckManager.Discard(mountToDiscard);
                        if (battleUI != null)
                        {
                            battleUI.ShowDiscardedCard(defender, mountToDiscard, true, "麒麟弓弃置坐骑");
                        }
                    }
                }
                TryTriggerChiXiaoSkill(attacker);
            }
        }

        if (!gameOver)
        {
            currentPhase = TurnPhase.Play;
        }

        RefreshBattleUI();
    }

    private void PlayDefenseResponseVoice(Player responder, CardData card, bool helpingAlly)
    {
        if (responder == null || card == null)
        {
            return;
        }

        if (card.effectType == CardEffectType.Defense)
        {
            GameSfxPlayer.PlayCardVoice(responder, card);
        }

        if (helpingAlly || card.effectType != CardEffectType.Defense)
        {
            GameSfxPlayer.PlaySkillVoice(responder);
        }
    }

    private void PlayerUseCardInResponsePhase(CardData card)
    {
        bool isPlayerDefending = pendingDefender == player;
        bool isPlayerHelpingAlly = pendingResponseCardType == CardEffectType.Defense
            && HasSkill(player, "轻羽")
            && pendingDefender != null
            && pendingDefender != player
            && !IsEnemy(player, pendingDefender);

        if (!isPlayerDefending && !isPlayerHelpingAlly)
        {
            Debug.Log("当前不是玩家需要响应");
            return;
        }

        bool usesRequiredCard = card.effectType == pendingResponseCardType;
        bool usesQingYuAsDodge = pendingResponseCardType == CardEffectType.Defense
            && !usesRequiredCard
            && HasSkill(player, "轻羽");
        if (!usesRequiredCard && !usesQingYuAsDodge)
        {
            Debug.Log("当前需要使用【" + PendingResponseCardName + "】响应");
            return;
        }

        player.RemoveCard(card);
        deckManager.Discard(card);

        if (usesRequiredCard && pendingResponseCardType == CardEffectType.Defense)
        {
            PlayDefenseResponseVoice(player, card, isPlayerHelpingAlly);
        }
        else
        {
            GameSfxPlayer.PlayCardAction(card);
            GameSfxPlayer.PlayCardVoice(player, card);
            if (usesQingYuAsDodge)
            {
                GameSfxPlayer.PlaySkillVoice(player);
            }
        }

        playerUsedDefense = true;
        playerResponseFinished = true;

        if (usesQingYuAsDodge)
        {
            if (isPlayerHelpingAlly)
            {
                Debug.Log(
                    player.playerName + " 使用轻羽，将【" + card.cardName + "】当闪帮队友出闪"
                );
                if (battleUI != null)
                    battleUI.ShowSkillPopup(
                        player.playerName + "发动【轻羽】将" + card.cardName + "当闪帮队友出闪"
                    );
            }
            else
            {
                Debug.Log(player.playerName + " 使用轻羽，将【" + card.cardName + "】当闪使用");
                if (battleUI != null)
                    battleUI.ShowSkillPopup(
                        player.playerName + "发动【轻羽】将" + card.cardName + "当闪使用"
                    );
            }
        }
        else
        {
            if (pendingResponseCardType == CardEffectType.Attack)
            {
                Debug.Log("玩家在【决斗】中打出杀响应");
            }
            else if (isPlayerHelpingAlly)
            {
                Debug.Log(
                    player.playerName + " 帮 " + pendingDefender.playerName + " 出闪，抵消了攻击"
                );
            }
            else
            {
                Debug.Log("玩家使用了闪，抵消了攻击");
            }
        }

        if (battleUI != null)
        {
            string responseMessage = isPlayerHelpingAlly
                ? player.playerName + " 为 " + pendingDefender.playerName + " 打出【" + card.cardName + "】"
                : player.playerName + " 打出【" + card.cardName + "】响应";
            battleUI.ShowCardResponse(player, card, responseMessage);
        }

        RefreshBattleUI();
    }

    private IEnumerator CheckDying(Player targetPlayer)
    {
        if (targetPlayer.currentHp > 0)
        {
            yield break;
        }

        // 濒死选择是一个临时子阶段；拒绝用桃或被救回后必须回到触发濒死的阶段。
        TurnPhase resumePhase = currentPhase == TurnPhase.Dying ? TurnPhase.Play : currentPhase;
        dyingPlayer = targetPlayer;
        currentPhase = TurnPhase.Dying;
        RefreshBattleUI();

        Debug.Log(targetPlayer.playerName + " 进入濒死状态");

        // 第一步：濒死者自己有桃则自动使用
        if (targetPlayer.currentHp <= 0 && targetPlayer.isAI)
        {
            CardData aiPeach = FindCardByEffectType(targetPlayer, CardEffectType.Heal);
            if (aiPeach == null)
            {
                aiPeach = FindCardByEffectType(targetPlayer, CardEffectType.Wine);
            }

            if (aiPeach != null)
            {
                targetPlayer.RemoveCard(aiPeach);
                deckManager.Discard(aiPeach);
                targetPlayer.Heal(1);
                if (battleUI != null)
                {
                    battleUI.ShowCardAction(targetPlayer, targetPlayer, aiPeach);
                }

                if (aiPeach.effectType == CardEffectType.Heal)
                {
                    TryTriggerQingLanSkill(targetPlayer);
                }

                Debug.Log(targetPlayer.playerName + " 自动使用【" + aiPeach.cardName + "】，脱离濒死");
            }
        }

        // 第二步：AI队友自动帮忙使用桃（无论谁濒死都可以帮忙）
        if (targetPlayer.currentHp <= 0)
        {
            foreach (Player ally in players)
            {
                if (ally == null || ally == targetPlayer || ally.currentHp <= 0)
                {
                    continue;
                }

                // 只允许同阵营角色帮忙
                if (IsEnemy(ally, targetPlayer))
                {
                    continue;
                }

                // 只让 AI 队友自动帮忙，玩家是否救人走后面的玩家选择逻辑
                if (!ally.isAI)
                {
                    continue;
                }

                CardData allyPeach = FindCardByEffectType(ally, CardEffectType.Heal);

                if (allyPeach != null)
                {
                    ally.RemoveCard(allyPeach);
                    deckManager.Discard(allyPeach);
                    targetPlayer.Heal(1);
                    if (battleUI != null)
                    {
                        battleUI.ShowCardAction(ally, targetPlayer, allyPeach);
                    }

                    TryTriggerQingLanSkill(ally);

                    Debug.Log(
                        ally.playerName
                            + " 对 "
                            + targetPlayer.playerName
                            + " 自动使用桃，帮助脱离濒死"
                    );
                    break;
                }
            }
        }

        // 第三步：玩家选择是否使用桃帮忙（只帮自己或同阵营）
        if (targetPlayer.currentHp <= 0 && player != null && player.currentHp > 0)
        {
            // 二人场：玩家只救自己；三人场：玩家只救同阵营
            bool canPlayerHelp = (targetPlayer == player) || !IsEnemy(player, targetPlayer);

            if (canPlayerHelp)
            {
                CardData playerPeach = FindCardByEffectType(player, CardEffectType.Heal);

                if (playerPeach != null)
                {
                    playerDyingChoiceFinished = false;
                    playerUsedPeach = false;

                    Debug.Log(targetPlayer.playerName + " 濒死，玩家可以选择是否使用桃");

                    RefreshBattleUI();

                    yield return new WaitUntil(() => playerDyingChoiceFinished || gameOver);

                    if (playerUsedPeach)
                    {
                        Debug.Log("玩家使用了桃");
                    }
                    else
                    {
                        Debug.Log("玩家选择不使用桃");
                    }
                }
            }
        }

        if (targetPlayer.currentHp <= 0)
        {
            targetPlayer.isDead = true;
            Debug.Log(targetPlayer.playerName + " 阵亡");

            CheckGameOver();
        }
        else
        {
            targetPlayer.isDead = false;
        }

        dyingPlayer = null;
        if (!gameOver)
        {
            currentPhase = resumePhase;
        }
        RefreshBattleUI();
    }

    private void PlayerUseCardInDyingPhase(CardData card)
    {
        if (dyingPlayer == null)
        {
            Debug.Log("当前没有濒死角色");
            return;
        }

        bool isPeach = card.effectType == CardEffectType.Heal;
        bool isSelfWine = card.effectType == CardEffectType.Wine && dyingPlayer == player;
        if (!isPeach && !isSelfWine)
        {
            Debug.Log("当前濒死响应只能使用桃；酒只能用于自救");
            return;
        }

        player.RemoveCard(card);
        deckManager.Discard(card);

        dyingPlayer.Heal(1);
        if (battleUI != null)
        {
            battleUI.ShowCardAction(player, dyingPlayer, card);
        }

        if (card.effectType == CardEffectType.Heal)
        {
            TryTriggerQingLanSkill(player);
        }

        playerUsedPeach = true;
        playerDyingChoiceFinished = true;

        Debug.Log("玩家对 " + dyingPlayer.playerName + " 使用了【" + card.cardName + "】");

        RefreshBattleUI();
    }

    public void EndPlayerTurn()
    {
        if (gameOver)
        {
            return;
        }

        if (currentPhase == TurnPhase.Response)
        {
            Debug.Log("玩家选择不打出【" + PendingResponseCardName + "】");
            playerUsedDefense = false;
            playerResponseFinished = true;
            RefreshBattleUI();
            return;
        }

        if (currentPhase == TurnPhase.Dying)
        {
            Debug.Log("玩家选择不使用桃");
            playerUsedPeach = false;
            playerDyingChoiceFinished = true;
            RefreshBattleUI();
            return;
        }

        if (currentPlayer != player)
        {
            Debug.Log("现在不是玩家的回合");
            return;
        }

        if (currentPhase != TurnPhase.Play)
        {
            Debug.Log("现在还不能结束回合");
            return;
        }

        currentPhase = TurnPhase.Discard;
        RefreshBattleUI();
    }

    private void StartPlayerDiscardPhase()
    {
        int handLimit = GetHandLimit(player);

        Debug.Log("玩家的手牌上限是：" + handLimit);

        if (player.handCards.Count <= handLimit)
        {
            Debug.Log("玩家不需要弃牌");
            playerDiscardFinished = true;
            return;
        }

        int needDiscardCount = player.handCards.Count - handLimit;
        Debug.Log("玩家需要弃掉 " + needDiscardCount + " 张牌，请点击手牌弃牌");

        RefreshBattleUI();
    }

    private void PlayerDiscardCard(CardData card)
    {
        if (card == null)
        {
            Debug.LogWarning("弃牌失败：卡牌为空");
            return;
        }

        if (!player.handCards.Contains(card))
        {
            Debug.LogWarning("弃牌失败：这张牌不在玩家手牌中");
            return;
        }

        int handLimit = GetHandLimit(player);

        if (player.handCards.Count <= handLimit)
        {
            Debug.Log("当前不需要弃牌");
            playerDiscardFinished = true;
            RefreshBattleUI();
            return;
        }

        player.RemoveCard(card);
        deckManager.Discard(card);
        if (battleUI != null)
        {
            battleUI.ShowDiscardedCard(player, card, false, "回合弃牌");
        }

        Debug.Log("玩家弃掉了：" + card.cardName);

        RefreshBattleUI();

        if (player.handCards.Count <= handLimit)
        {
            Debug.Log("玩家弃牌完成");
            playerDiscardFinished = true;
        }
        else
        {
            int needDiscardCount = player.handCards.Count - handLimit;
            Debug.Log("还需要弃掉 " + needDiscardCount + " 张牌");
        }
    }

    private void AutoDiscardPhase(Player activePlayer)
    {
        int handLimit = GetHandLimit(activePlayer);

        Debug.Log(activePlayer.playerName + " 的手牌上限是：" + handLimit);

        while (activePlayer.handCards.Count > handLimit)
        {
            CardData cardToDiscard = FindLeastValuableCard(activePlayer);
            if (cardToDiscard == null)
            {
                break;
            }

            activePlayer.RemoveCard(cardToDiscard);
            deckManager.Discard(cardToDiscard);
            if (battleUI != null)
            {
                battleUI.ShowDiscardedCard(activePlayer, cardToDiscard, false, "超出手牌上限");
            }

            Debug.Log(activePlayer.playerName + " 弃掉了：" + cardToDiscard.cardName);
        }

        RefreshBattleUI();
    }

    private CardData FindLeastValuableCard(Player activePlayer)
    {
        CardData result = null;
        float lowestValue = float.MaxValue;

        foreach (CardData card in activePlayer.handCards)
        {
            float value = ScoreCardRetention(activePlayer, card);
            if (value < lowestValue)
            {
                lowestValue = value;
                result = card;
            }
        }

        return result;
    }

    private float ScoreCardRetention(Player activePlayer, CardData card)
    {
        if (card == null)
        {
            return 0f;
        }

        switch (card.effectType)
        {
            case CardEffectType.Heal:
                return activePlayer.currentHp < activePlayer.maxHp
                    ? 70f + (activePlayer.currentHp <= 2 ? 30f : 0f)
                    : 16f;

            case CardEffectType.Defense:
                return 42f + (activePlayer.currentHp <= 2 ? 24f : 0f);

            case CardEffectType.Attack:
                return 36f + (activePlayer.CanUseAttack() ? 16f : 0f);

            case CardEffectType.DrawCards:
                return 32f + (activePlayer.handCards.Count <= 2 ? 18f : 0f);

            case CardEffectType.Weapon:
            case CardEffectType.Armor:
            case CardEffectType.Treasure:
                return ShouldEquipCard(activePlayer, card) ? 48f : 12f;

            case CardEffectType.DirectDamage:
                return FindBestEnemyTarget(activePlayer, card) != null ? 48f : 20f;

            case CardEffectType.ForceDiscard:
            case CardEffectType.DiscardEquip:
            case CardEffectType.DiscardTarget:
            case CardEffectType.StealTarget:
            case CardEffectType.AreaDamage:
                return FindBestEnemyTarget(activePlayer, card) != null ? 40f : 18f;

            case CardEffectType.Protect:
                return FindBestTargetForCard(activePlayer, card) != null ? 46f : 14f;

            case CardEffectType.InvestigateIdentity:
            case CardEffectType.PlantSuspicion:
            case CardEffectType.ReduceSuspicion:
                return FindBestTargetForCard(activePlayer, card) != null ? 44f : 16f;

            default:
                return 20f;
        }
    }

    private void AddCommanderSuspicion(Player suspect, string clue, bool confirmedAttack)
    {
        if (
            GameSelectionData.gameMode != GameMode.FivePlayer
            || suspect == null
            || suspect.isIdentityRevealed
        )
        {
            return;
        }

        if (confirmedAttack)
        {
            suspect.hasAttackedCommander = true;
        }

        suspect.commanderSuspicion = Mathf.Min(
            suspect.commanderSuspicion + 1,
            Player.CommanderSuspicionRevealThreshold
        );
        Debug.Log(
            suspect.playerName
                + " 留下疑迹（"
                + clue
                + "）："
                + suspect.commanderSuspicion
                + "/"
                + Player.CommanderSuspicionRevealThreshold
        );
        RecordMatchHighlight(
            suspect.playerName
                + " 疑迹 "
                + suspect.commanderSuspicion
                + "/"
                + Player.CommanderSuspicionRevealThreshold
                + "（"
                + clue
                + "）"
        );

        RevealIdentityAtSuspicionThreshold(suspect);
        RefreshBattleUI();
    }

    private void RevealIdentityAtSuspicionThreshold(Player suspect)
    {
        if (
            GameSelectionData.gameMode != GameMode.FivePlayer
            || suspect == null
            || suspect.isIdentityRevealed
            || suspect.commanderSuspicion < Player.CommanderSuspicionRevealThreshold
        )
        {
            return;
        }

        suspect.isIdentityRevealed = true;
        string identityName = GetIdentityName(suspect.identityType);
        RecordMatchHighlight(suspect.playerName + " 身份公开为" + identityName);
        Debug.Log("【疑令】" + suspect.playerName + " 的身份揭示为【" + identityName + "】");
        if (battleUI != null)
        {
            battleUI.ShowSkillPopup(suspect.playerName + " 的身份是【" + identityName + "】");
        }
    }

    private void ExpireTemporaryProtection(Player target)
    {
        if (target == null || target.protectionTurnsRemaining <= 0)
        {
            return;
        }

        target.protectionTurnsRemaining--;
        if (target.protectionTurnsRemaining <= 0)
        {
            target.protectionValue = 0;
            Debug.Log(target.playerName + " 的护盾效果消失");
        }
    }

    private bool HasSkill(Player targetPlayer, string skillName)
    {
        if (targetPlayer == null || targetPlayer.characterData == null)
        {
            return false;
        }

        return targetPlayer.characterData.skillName == skillName;
    }

    private bool ShouldAIUseDefense(Player defender, Player attacker)
    {
        if (defender == null)
        {
            return false;
        }

        int incomingDamage = 1;
        if (defender.protectionValue > 0)
        {
            incomingDamage = Mathf.Max(0, incomingDamage - defender.protectionValue);
        }
        if (incomingDamage <= 0)
        {
            return false;
        }

        if (HasSkill(defender, "借风") && !defender.usedJieFengThisTurn)
        {
            return true;
        }

        if (defender.currentHp <= incomingDamage || defender.currentHp < defender.maxHp)
        {
            return true;
        }

        if (GameSelectionData.gameMode == GameMode.FivePlayer)
        {
            if (defender.identityType == IdentityType.Commander)
            {
                return true;
            }

            bool knownThreat = attacker != null
                && attacker.isIdentityRevealed
                && attacker.identityType == IdentityType.Rebel;
            if (
                knownThreat
                && (defender.identityType == IdentityType.Protector || defender.identityType == IdentityType.Commander)
            )
            {
                return true;
            }
        }

        // If the AI must discard at turn end, spending a response card can avoid
        // taking damage without increasing its final discard count.
        return defender.handCards.Count > GetHandLimit(defender);
    }

    private void TryTriggerChiXiaoSkill(Player attacker)
    {
        if (!HasSkill(attacker, "烈锋"))
        {
            return;
        }

        if (attacker.usedAttackCountThisTurn != 1)
        {
            return;
        }

        CardData card = deckManager.Draw();

        if (card != null)
        {
            attacker.DrawCard(card);
            Debug.Log(attacker.playerName + " 触发技能【烈锋】，摸一张牌");
            GameSfxPlayer.PlaySkillVoice(attacker);

            if (battleUI != null)
            {
                battleUI.ShowSkillPopup(attacker.playerName + "发动【烈锋】");
            }
        }

        RefreshBattleUI();
    }

    private void TryTriggerQiaoZhuSkill(Player owner)
    {
        if (!HasSkill(owner, "巧铸") || owner.usedQiaoZhuThisTurn)
        {
            return;
        }

        owner.usedQiaoZhuThisTurn = true;
        CardData card = deckManager.Draw();
        if (card != null)
        {
            owner.DrawCard(card);
        }

        Debug.Log(owner.playerName + " 首次装备后触发【巧铸】，摸一张牌");
        GameSfxPlayer.PlaySkillVoice(owner);
        if (battleUI != null)
        {
            battleUI.ShowSkillPopup(owner.playerName + "发动【巧铸】");
        }
        RefreshBattleUI();
    }

    private void TryTriggerJieFengSkill(Player defender)
    {
        if (!HasSkill(defender, "借风") || defender.usedJieFengThisTurn)
        {
            return;
        }

        defender.usedJieFengThisTurn = true;
        CardData card = deckManager.Draw();
        if (card != null)
        {
            defender.DrawCard(card);
        }

        Debug.Log(defender.playerName + " 首次闪避成功后触发【借风】，摸一张牌");
        GameSfxPlayer.PlaySkillVoice(defender);
        if (battleUI != null)
        {
            battleUI.ShowSkillPopup(defender.playerName + "发动【借风】");
        }
        RefreshBattleUI();
    }

    private void TryTriggerZhaoChenSkill(Player source, Player target)
    {
        if (
            source == null
            || target == null
            || source == target
            || source.usedZhaoChenThisTurn
            || !HasSkill(source, "照谶")
        )
        {
            return;
        }

        source.usedZhaoChenThisTurn = true;
        GameSfxPlayer.PlaySkillVoice(source);
        bool targetHasSuspicionBonus =
            GameSelectionData.gameMode == GameMode.FivePlayer
            && target.commanderSuspicion > 0;
        bool shouldDiscardTargetHand =
            target.handCards.Count > 0
            && (target.handCards.Count >= source.handCards.Count || targetHasSuspicionBonus);

        CardData card = deckManager.Draw();
        if (card != null)
        {
            source.DrawCard(card);
            Debug.Log(source.playerName + " 首次造成伤害，发动【照谶】摸一张牌");
        }

        if (shouldDiscardTargetHand)
        {
            int discardIndex = Random.Range(0, target.handCards.Count);
            CardData discardedCard = target.handCards[discardIndex];
            target.RemoveCard(discardedCard);
            deckManager.Discard(discardedCard);
            if (battleUI != null)
            {
                battleUI.ShowDiscardedCard(target, discardedCard, false, "照谶弃牌");
            }
            Debug.Log(target.playerName + " 因【照谶】随机弃置手牌：" + discardedCard.cardName);
        }

        if (battleUI != null)
        {
            battleUI.ShowSkillPopup(source.playerName + "发动【照谶】");
        }

        RefreshBattleUI();
    }

    private void TryTriggerQingLanSkill(Player peachUser)
    {
        if (!HasSkill(peachUser, "回澜"))
        {
            return;
        }

        if (peachUser.handCards.Count >= peachUser.maxHp)
        {
            Debug.Log(peachUser.playerName + " 触发【回澜】条件不足：手牌数不小于体力上限");
            return;
        }

        CardData card = deckManager.Draw();

        if (card != null)
        {
            peachUser.DrawCard(card);
            Debug.Log(peachUser.playerName + " 触发技能【回澜】，摸一张牌");
            GameSfxPlayer.PlaySkillVoice(peachUser);

            if (battleUI != null)
            {
                battleUI.ShowSkillPopup(peachUser.playerName + "发动【回澜】");
            }
        }

        RefreshBattleUI();
    }

    // 玄衡技能【震策】：使用锦囊牌时，每个敌方角色须弃一张牌，无牌则掉一滴血
    private IEnumerator TryTriggerZhenCeSkill(Player user)
    {
        if (!HasSkill(user, "震策") || user.usedZhenCeThisTurn)
        {
            yield break;
        }

        // 先固定本次受影响的目标：结算过程中可能有人濒死/阵亡，
        // 直接遍历 players 会有歧义，也避免多个濒死流程交叉
        List<Player> targets = new List<Player>();

        foreach (Player target in players)
        {
            if (target == null || target == user || target.currentHp <= 0)
            {
                continue;
            }

            // 只对敌方生效
            if (!IsEnemy(user, target))
            {
                continue;
            }

            targets.Add(target);
        }

        if (targets.Count == 0)
        {
            yield break;
        }

        user.usedZhenCeThisTurn = true;
        GameSfxPlayer.PlaySkillVoice(user);

        foreach (Player target in targets)
        {
            // 前一个目标结算后可能已经阵亡，跳过
            if (target == null || target.currentHp <= 0)
            {
                continue;
            }

            if (target.handCards.Count > 0)
            {
                // 有牌则弃一张（AI弃第一张，玩家也自动弃第一张）
                CardData discardCard = target.handCards[0];
                target.RemoveCard(discardCard);
                deckManager.Discard(discardCard);
                if (battleUI != null)
                {
                    battleUI.ShowDiscardedCard(target, discardCard, false, "震策弃牌");
                }
                Debug.Log(target.playerName + " 因【震策】被迫弃掉一张牌：" + discardCard.cardName);
            }
            else
            {
                // 震策造成的是普通伤害，统一经过护盾、防具、伤害特效和濒死结算。
                Debug.Log(target.playerName + " 因【震策】无牌可弃，受到1点伤害");
                yield return StartCoroutine(DealDamage(user, target, 1, "震策"));
                if (gameOver)
                {
                    yield break;
                }
            }

            RefreshBattleUI();
        }

        if (battleUI != null)
        {
            battleUI.ShowSkillPopup(user.playerName + "发动【震策】");
        }

        RefreshBattleUI();
    }

    private CardData FindCardByEffectType(Player targetPlayer, CardEffectType effectType)
    {
        foreach (CardData card in targetPlayer.handCards)
        {
            if (card.effectType == effectType)
            {
                return card;
            }
        }

        return null;
    }

    // 查找角色手中的任意一张牌（用于白鸢【轻羽】技能：任意牌当闪）
    private CardData FindAnyCard(Player targetPlayer)
    {
        if (targetPlayer.handCards.Count > 0)
        {
            return targetPlayer.handCards[0];
        }

        return null;
    }

    private int GetHandLimit(Player targetPlayer)
    {
        return targetPlayer.currentHp;
    }

    private void EndTurn(Player activePlayer)
    {
        TryTriggerQingDeng(activePlayer);
        ExpireTemporaryProtection(activePlayer);

        Debug.Log(activePlayer.playerName + " 回合结束");

        RefreshBattleUI();
    }

    private void CheckGameOver()
    {
        if (players == null || players.Count == 0)
            return;

        if (GameSelectionData.gameMode == GameMode.FivePlayer)
        {
            CheckFivePlayerGameOver();
        }
        else if (GameSelectionData.gameMode == GameMode.ThreePlayer)
        {
            CheckThreePlayerGameOver();
        }
        else
        {
            CheckTwoPlayerGameOver();
        }
    }

    private void EndGame(string result)
    {
        if (gameOver)
        {
            return;
        }

        gameOver = true;
        currentPhase = TurnPhase.End;
        RecordMatchHighlight(result);
        Debug.Log("游戏结束：" + result);
        RefreshBattleUI();
        if (battleUI != null)
        {
            battleUI.ShowMatchSummary(result, turnCount, players);
        }
    }

    private void CheckTwoPlayerGameOver()
    {
        if (player != null && player.currentHp <= 0)
        {
            EndGame("玩家失败");
            return;
        }

        if (enemy != null && enemy.currentHp <= 0)
        {
            EndGame("玩家胜利");
            return;
        }
    }

    // 五人场胜负判定：灵契之争
    private void CheckFivePlayerGameOver()
    {
        bool commanderAlive = false;
        bool rebelAlive = false;
        int aliveCount = 0;

        foreach (Player p in players)
        {
            if (p == null || p.currentHp <= 0)
                continue;
            aliveCount++;

            if (p.identityType == IdentityType.Commander)
                commanderAlive = true;
            if (p.identityType == IdentityType.Rebel)
                rebelAlive = true;
        }

        // 游离者胜利条件优先判断，否则执令者阵亡分支会提前结束游戏。
        if (aliveCount == 1)
        {
            foreach (Player p in players)
            {
                if (p == null || p.currentHp <= 0 || p.identityType != IdentityType.Wanderer)
                    continue;

                EndGame("游离者 " + p.playerName + " 胜利！存活到最后");
                return;
            }
        }

        // 执令者/护令者胜利条件：逆契者全部出局，且执令者存活
        if (!rebelAlive && commanderAlive)
        {
            EndGame("执令者阵营胜利！逆契者全部出局，执令者存活");
            return;
        }

        // 逆契者胜利条件：执令者出局
        if (!commanderAlive)
        {
            // 检查是否有篡令者（游离者）存活 — 目前游离者先按基础规则
            EndGame("逆契者阵营胜利！执令者出局");
            return;
        }
    }

    private void CheckThreePlayerGameOver()
    {
        bool overlordAlive = false;
        bool allianceAlive = false;

        foreach (Player p in players)
        {
            if (p == null)
            {
                continue;
            }

            if (p.currentHp <= 0)
            {
                continue;
            }

            if (p.teamType == TeamType.Overlord)
            {
                overlordAlive = true;
            }

            if (p.teamType == TeamType.Alliance)
            {
                allianceAlive = true;
            }
        }

        if (!overlordAlive)
        {
            EndGame("协战阵营胜利");
            return;
        }

        if (!allianceAlive)
        {
            EndGame("霸主胜利");
            return;
        }
    }

    private void RefreshBattleUI()
    {
        if (battleUI != null)
        {
            battleUI.RefreshUI();
        }
    }

    public List<string> GetMatchHighlights()
    {
        return new List<string>(matchHighlights);
    }

    private void RecordMatchHighlight(string entry)
    {
        if (string.IsNullOrWhiteSpace(entry))
        {
            return;
        }

        matchHighlights.Add(entry);
        const int maxHighlights = 6;
        if (matchHighlights.Count > maxHighlights)
        {
            matchHighlights.RemoveAt(0);
        }
    }
}
