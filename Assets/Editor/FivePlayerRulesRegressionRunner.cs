using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Deterministic editor-side checks for the five-player identity and suspicion rules.
/// Run from Tools > 灵契之争 > Run Five-Player Rules Regression.
/// </summary>
public static class FivePlayerRulesRegressionRunner
{
    private sealed class Fixture : IDisposable
    {
        public readonly Player[] players = new Player[5];
        public readonly TurnManager turnManager;
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly List<UnityEngine.Object> assets = new List<UnityEngine.Object>();

        public Fixture()
        {
            for (int i = 0; i < players.Length; i++)
            {
                GameObject playerObject = new GameObject("RegressionPlayer" + i);
                objects.Add(playerObject);
                players[i] = playerObject.AddComponent<Player>();
                players[i].playerName = "角色" + i;
                players[i].currentHp = 4;
                players[i].maxHp = 4;
            }

            GameObject managerObject = new GameObject("RegressionTurnManager");
            objects.Add(managerObject);
            turnManager = managerObject.AddComponent<TurnManager>();
            turnManager.player = players[0];
            turnManager.enemy = players[1];
            turnManager.enemyB = players[2];
            turnManager.enemyC = players[3];
            turnManager.enemyD = players[4];
            turnManager.players.AddRange(players);
        }

        public BattleGameManager CreateBattleGameManager()
        {
            GameObject managerObject = new GameObject("RegressionBattleGameManager");
            objects.Add(managerObject);
            BattleGameManager manager = managerObject.AddComponent<BattleGameManager>();
            manager.player = players[0];
            manager.enemy = players[1];
            manager.enemyB = players[2];
            manager.enemyC = players[3];
            manager.enemyD = players[4];
            return manager;
        }

        public CardData CreateCard(string name, CardEffectType effectType)
        {
            CardData card = ScriptableObject.CreateInstance<CardData>();
            card.cardName = name;
            card.effectType = effectType;
            card.category = CardCategory.Trick;
            card.value = 1;
            card.needTarget = true;
            assets.Add(card);
            return card;
        }

        public void Dispose()
        {
            for (int i = assets.Count - 1; i >= 0; i--)
            {
                if (assets[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(assets[i]);
                }
            }

            for (int i = objects.Count - 1; i >= 0; i--)
            {
                if (objects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(objects[i]);
                }
            }
        }
    }

    [MenuItem("Tools/灵契之争/Run Five-Player Rules Regression")]
    public static void Run()
    {
        GameMode previousMode = GameSelectionData.gameMode;
        List<string> failures = new List<string>();
        int passed = 0;

        RunCase("身份分配比例", TestIdentityAllocation, failures, ref passed);
        RunCase("五人场公开身份与阵营设置", TestRoleSetup, failures, ref passed);
        RunCase("实证疑迹递增、阈值揭示与封顶", TestConfirmedClues, failures, ref passed);
        RunCase("伪迹只增加目标疑迹", TestPlantedClue, failures, ref passed);
        RunCase("洗迹只消除未证实疑迹", TestWashRules, failures, ref passed);
        RunCase("察微只查验未公开的疑迹角色", TestInvestigationRules, failures, ref passed);
        RunCase("五人场真实阵营与 AI 可见信息", TestFactionAndAiKnowledge, failures, ref passed);
        RunCase("五人场三种胜利条件", TestVictoryConditions, failures, ref passed);
        RunCase("非五人场不累积疑迹", TestSuspicionModeGuard, failures, ref passed);
        RunCase("玩家拒绝用桃后恢复原阶段", TestDyingDeclineRestoresPhase, failures, ref passed);
        RunCase("角色专属杀语音映射", TestCharacterCardVoiceMappings, failures, ref passed);
        RunCase("AI 不读取敌方隐藏手牌", TestAINoHiddenHandLeak, failures, ref passed);

        GameSelectionData.SetGameMode(previousMode);

        string summary = "五人场规则回归：通过 " + passed + " 项，失败 " + failures.Count + " 项。";
        if (failures.Count > 0)
        {
            foreach (string failure in failures)
            {
                Debug.LogError("[五人场回归失败] " + failure);
            }

            Debug.LogError(summary);
            throw new InvalidOperationException(summary + " " + string.Join(" | ", failures));
        }

        Debug.Log("[五人场回归通过] " + summary);
    }

    private static void RunCase(string name, Action test, List<string> failures, ref int passed)
    {
        try
        {
            test();
            passed++;
            Debug.Log("[五人场回归通过] " + name);
        }
        catch (Exception exception)
        {
            failures.Add(name + "：" + exception.Message);
        }
    }

    private static void TestIdentityAllocation()
    {
        for (int run = 0; run < 100; run++)
        {
            IdentityType[] identities = IdentityAllocator.AllocateFivePlayer();
            Require(identities.Length == 5, "每局必须分配五个身份");
            Require(Count(identities, IdentityType.Commander) == 1, "必须恰有一名执令者");
            Require(Count(identities, IdentityType.Protector) == 1, "必须恰有一名护令者");
            Require(Count(identities, IdentityType.Rebel) == 2, "必须恰有两名逆契者");
            Require(Count(identities, IdentityType.Wanderer) == 1, "必须恰有一名游离者");
        }
    }

    private static void TestRoleSetup()
    {
        GameSelectionData.SetGameMode(GameMode.FivePlayer);
        GameSelectionData.fivePlayerIdentities = StandardIdentities();
        using (Fixture fixture = new Fixture())
        {
            fixture.players[0].isAI = false;
            BattleGameManager manager = fixture.CreateBattleGameManager();
            Invoke(manager, "SetupFivePlayerIdentities");

            Require(fixture.players[0].identityType == IdentityType.Commander, "玩家身份顺序应与分配数组一致");
            Require(fixture.players[0].isIdentityRevealed, "执令者开局应公开");
            Require(!fixture.players[1].isIdentityRevealed, "护令者开局应隐藏");
            Require(!fixture.players[2].isIdentityRevealed, "逆契者开局应隐藏");
            Require(fixture.players[0].maxHp == 5 && fixture.players[0].currentHp == 5, "执令者应获得一点体力上限");
            Require(fixture.players[1].isAI && fixture.players[4].isAI, "其余四名角色应由 AI 控制");
        }
    }

    private static void TestConfirmedClues()
    {
        GameSelectionData.SetGameMode(GameMode.FivePlayer);
        using (Fixture fixture = new Fixture())
        {
            Player suspect = fixture.players[2];
            suspect.identityType = IdentityType.Rebel;

            Invoke(fixture.turnManager, "AddCommanderSuspicion", suspect, "回归测试：出杀", true);
            Require(suspect.commanderSuspicion == 1, "第一次实证应增加一点疑迹");
            Require(suspect.hasAttackedCommander, "实证应记录为攻击过执令者");
            Require(!suspect.isIdentityRevealed, "一点疑迹不应立即公开身份");

            Invoke(fixture.turnManager, "AddCommanderSuspicion", suspect, "回归测试：再次出杀", true);
            Require(suspect.commanderSuspicion == 2, "第二次实证应达到两点");
            Require(suspect.isIdentityRevealed, "两点疑迹应自动公开身份");

            Invoke(fixture.turnManager, "AddCommanderSuspicion", suspect, "回归测试：额外疑迹", false);
            Require(suspect.commanderSuspicion == 2, "身份公开后疑迹不得继续累加");
        }
    }

    private static void TestPlantedClue()
    {
        GameSelectionData.SetGameMode(GameMode.FivePlayer);
        using (Fixture fixture = new Fixture())
        {
            Player user = fixture.players[0];
            Player target = fixture.players[2];
            CardData card = fixture.CreateCard("伪迹", CardEffectType.PlantSuspicion);
            target.identityType = IdentityType.Rebel;

            Invoke(fixture.turnManager, "ResolvePlantSuspicion", user, target, card);
            Require(target.commanderSuspicion == 1, "伪迹应将疑迹加在指定目标身上");
            Require(!target.hasAttackedCommander, "伪迹不得伪造实证标记");
            Require(user.commanderSuspicion == 0, "伪迹不应把疑迹加到使用者身上");
        }
    }

    private static void TestWashRules()
    {
        GameSelectionData.SetGameMode(GameMode.FivePlayer);
        using (Fixture fixture = new Fixture())
        {
            Player user = fixture.players[0];
            Player unconfirmed = fixture.players[2];
            CardData wash = fixture.CreateCard("洗迹", CardEffectType.ReduceSuspicion);
            unconfirmed.commanderSuspicion = 1;
            unconfirmed.hasAttackedCommander = false;

            Require(fixture.turnManager.CanUseCardOnTarget(user, unconfirmed, wash), "洗迹应可指定有未证实疑迹的角色");
            Invoke(fixture.turnManager, "ResolveReduceSuspicion", user, unconfirmed, wash);
            Require(unconfirmed.commanderSuspicion == 0, "洗迹应清除一点未证实疑迹");

            Player confirmed = fixture.players[3];
            confirmed.commanderSuspicion = 1;
            confirmed.hasAttackedCommander = true;
            Require(!fixture.turnManager.CanUseCardOnTarget(user, confirmed, wash), "洗迹不得移除攻击执令者的实证");
            Invoke(fixture.turnManager, "ResolveReduceSuspicion", user, confirmed, wash);
            Require(confirmed.commanderSuspicion == 1, "实证疑迹应至少保留一点");
        }
    }

    private static void TestInvestigationRules()
    {
        GameSelectionData.SetGameMode(GameMode.FivePlayer);
        using (Fixture fixture = new Fixture())
        {
            Player user = fixture.players[0];
            Player target = fixture.players[2];
            target.identityType = IdentityType.Rebel;
            target.commanderSuspicion = 1;
            CardData investigate = fixture.CreateCard("察微", CardEffectType.InvestigateIdentity);

            Require(fixture.turnManager.CanUseCardOnTarget(user, target, investigate), "察微应能指定有疑迹的隐藏角色");
            Invoke(fixture.turnManager, "ResolveIdentityInvestigation", user, target);
            Require(target.isIdentityRevealed, "察微应公开目标身份");
            Require(!fixture.turnManager.CanUseCardOnTarget(user, target, investigate), "已经公开的角色不能重复查验");

            Player clean = fixture.players[3];
            Require(!fixture.turnManager.CanUseCardOnTarget(user, clean, investigate), "没有疑迹的角色不能被察微");
        }
    }

    private static void TestFactionAndAiKnowledge()
    {
        GameSelectionData.SetGameMode(GameMode.FivePlayer);
        using (Fixture fixture = new Fixture())
        {
            Player commander = fixture.players[0];
            Player protector = fixture.players[1];
            Player rebelA = fixture.players[2];
            Player rebelB = fixture.players[3];
            Player wanderer = fixture.players[4];
            commander.identityType = IdentityType.Commander;
            commander.isIdentityRevealed = true;
            protector.identityType = IdentityType.Protector;
            rebelA.identityType = IdentityType.Rebel;
            rebelB.identityType = IdentityType.Rebel;
            wanderer.identityType = IdentityType.Wanderer;

            Require(!(bool)Invoke(fixture.turnManager, "IsEnemy", commander, protector), "执令者与护令者应为队友");
            Require(!(bool)Invoke(fixture.turnManager, "IsEnemy", rebelA, rebelB), "两名逆契者应为队友");
            Require((bool)Invoke(fixture.turnManager, "IsEnemy", wanderer, commander), "游离者应与执令者为敌");
            Require((bool)Invoke(fixture.turnManager, "IsKnownEnemyForAI", rebelA, commander), "逆契者 AI 应识别公开的执令者");
            Require(!(bool)Invoke(fixture.turnManager, "IsKnownEnemyForAI", rebelA, rebelB), "逆契者 AI 不应读取隐藏队友身份");

            Player suspected = fixture.players[4];
            suspected.commanderSuspicion = 1;
            suspected.isIdentityRevealed = false;
            Require((bool)Invoke(fixture.turnManager, "IsPotentialThreatForAI", commander, suspected), "执令者阵营 AI 应把疑迹作为公开的潜在威胁");
        }
    }

    private static void TestVictoryConditions()
    {
        GameSelectionData.SetGameMode(GameMode.FivePlayer);
        using (Fixture fixture = new Fixture())
        {
            fixture.players[0].identityType = IdentityType.Commander;
            fixture.players[1].identityType = IdentityType.Protector;
            fixture.players[2].identityType = IdentityType.Rebel;
            fixture.players[3].identityType = IdentityType.Rebel;
            fixture.players[4].identityType = IdentityType.Wanderer;
            fixture.players[0].currentHp = 1;
            fixture.players[1].currentHp = 1;
            fixture.players[2].currentHp = 0;
            fixture.players[3].currentHp = 0;
            fixture.players[4].currentHp = 1;

            Invoke(fixture.turnManager, "CheckFivePlayerGameOver");
            Require(IsGameOver(fixture.turnManager), "逆契者全部出局且执令者存活时应结束");
            Require(LastHighlight(fixture.turnManager).Contains("执令者阵营胜利"), "应判执令者阵营获胜");
        }

        using (Fixture fixture = new Fixture())
        {
            fixture.players[0].identityType = IdentityType.Commander;
            fixture.players[2].identityType = IdentityType.Rebel;
            fixture.players[0].currentHp = 0;
            fixture.players[2].currentHp = 1;
            Invoke(fixture.turnManager, "CheckFivePlayerGameOver");
            Require(IsGameOver(fixture.turnManager), "执令者出局时应结束");
            Require(LastHighlight(fixture.turnManager).Contains("逆契者阵营胜利"), "应判逆契者阵营获胜");
        }

        using (Fixture fixture = new Fixture())
        {
            fixture.players[0].identityType = IdentityType.Commander;
            fixture.players[1].identityType = IdentityType.Protector;
            fixture.players[2].identityType = IdentityType.Rebel;
            fixture.players[3].identityType = IdentityType.Rebel;
            fixture.players[4].identityType = IdentityType.Wanderer;
            for (int i = 0; i < fixture.players.Length; i++)
            {
                fixture.players[i].currentHp = i == 4 ? 1 : 0;
            }

            Invoke(fixture.turnManager, "CheckFivePlayerGameOver");
            Require(IsGameOver(fixture.turnManager), "游离者独自存活时应结束");
            Require(LastHighlight(fixture.turnManager).Contains("游离者"), "应判游离者获胜");
        }
    }

    private static void TestSuspicionModeGuard()
    {
        GameSelectionData.SetGameMode(GameMode.TwoPlayer);
        using (Fixture fixture = new Fixture())
        {
            Player suspect = fixture.players[1];
            Invoke(fixture.turnManager, "AddCommanderSuspicion", suspect, "回归测试：二人场", true);
            Require(suspect.commanderSuspicion == 0, "非五人场不应累积疑迹");
            Require(!suspect.hasAttackedCommander, "非五人场不应留下攻主实证标记");
        }
    }

    private static void TestDyingDeclineRestoresPhase()
    {
        GameSelectionData.SetGameMode(GameMode.FivePlayer);
        GameSelectionData.fivePlayerIdentities = StandardIdentities();
        using (Fixture fixture = new Fixture())
        {
            Player player = fixture.players[0];
            Player dyingAlly = fixture.players[1];
            IdentityType[] identities = StandardIdentities();
            for (int i = 0; i < fixture.players.Length; i++)
            {
                fixture.players[i].identityType = identities[i];
            }

            player.identityType = IdentityType.Commander;
            player.isIdentityRevealed = true;
            player.currentHp = 4;
            player.handCards.Add(fixture.CreateCard("桃", CardEffectType.Heal));

            dyingAlly.identityType = IdentityType.Protector;
            dyingAlly.isAI = true;
            dyingAlly.currentHp = 0;
            dyingAlly.isDead = false;

            fixture.turnManager.currentPlayer = player;
            fixture.turnManager.currentPhase = TurnPhase.Play;

            IEnumerator dyingRoutine = (IEnumerator)Invoke(fixture.turnManager, "CheckDying", dyingAlly);
            Require(dyingRoutine.MoveNext(), "有桃时应等待玩家选择救援或拒绝");
            Require(fixture.turnManager.currentPhase == TurnPhase.Dying, "等待期间应处于濒死阶段");

            fixture.turnManager.EndPlayerTurn();
            Require(!dyingRoutine.MoveNext(), "玩家拒绝后濒死流程应完成");
            Require(dyingAlly.isDead, "无人救援时濒死角色应阵亡");
            Require(fixture.turnManager.dyingPlayer == null, "濒死目标应清空");
            Require(fixture.turnManager.currentPhase == TurnPhase.Play, "濒死结束后应回到原出牌阶段");
        }
    }

    private static void TestCharacterCardVoiceMappings()
    {
        CardData sha = AssetDatabase.LoadAssetAtPath<CardData>("Assets/Scripts/Cards/sha.asset");
        Require(sha != null && sha.voiceClip != null, "杀牌通用语音资源应存在");

        string[] characterPaths =
        {
            "Assets/ScriptableObjects/Characters/BaiYuan.asset",
            "Assets/ScriptableObjects/Characters/ChiXiao.asset",
            "Assets/ScriptableObjects/Characters/QingLan.asset",
            "Assets/ScriptableObjects/Characters/XuanHeng.asset",
            "Assets/ScriptableObjects/Characters/YanZhiWei.asset",
            "Assets/Resources/Characters/GuHanchuan.asset",
            "Assets/Resources/Characters/LiuQingwu.asset",
            "Assets/Resources/Characters/ShenZhaoye.asset",
        };
        HashSet<AudioClip> uniqueVoices = new HashSet<AudioClip>();
        foreach (string path in characterPaths)
        {
            CharacterData character = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
            Require(character != null, "角色数据缺失：" + path);
            AudioClip voice = character.GetCardVoice(sha);
            Require(voice != null, character.characterName + " 缺少角色专属杀语音");
            Require(voice != sha.voiceClip, character.characterName + " 错用了杀牌通用语音");
            Require(uniqueVoices.Add(voice), character.characterName + " 与其他角色共用了杀语音资源");
        }
    }

    private static void TestAINoHiddenHandLeak()
    {
        GameSelectionData.SetGameMode(GameMode.TwoPlayer);
        using (Fixture fixture = new Fixture())
        {
            Player ai = fixture.players[0];
            Player target = fixture.players[1];
            CardData attack = fixture.CreateCard("杀", CardEffectType.Attack);
            attack.category = CardCategory.Basic;
            attack.value = 1;

            target.currentHp = 1;
            target.handCards.Add(fixture.CreateCard("桃", CardEffectType.Heal));
            float scoreWhenTargetMayHavePeach = (float)Invoke(
                fixture.turnManager,
                "ScoreTargetForAI",
                ai,
                target,
                attack
            );
            target.handCards.Clear();
            target.handCards.Add(fixture.CreateCard("杀", CardEffectType.Attack));
            float scoreWithSamePublicHandCount = (float)Invoke(
                fixture.turnManager,
                "ScoreTargetForAI",
                ai,
                target,
                attack
            );
            Require(
                Mathf.Approximately(scoreWhenTargetMayHavePeach, scoreWithSamePublicHandCount),
                "AI 不应通过未知手牌判断目标是否有桃"
            );

            target.currentHp = 4;
            target.handCards.Clear();
            target.handCards.Add(fixture.CreateCard("闪", CardEffectType.Defense));
            float scoreWhenTargetMayHaveShan = (float)Invoke(
                fixture.turnManager,
                "ScoreTargetForAI",
                ai,
                target,
                attack
            );
            target.handCards.Clear();
            target.handCards.Add(fixture.CreateCard("杀", CardEffectType.Attack));
            float scoreWithoutKnownResponse = (float)Invoke(
                fixture.turnManager,
                "ScoreTargetForAI",
                ai,
                target,
                attack
            );
            Require(
                Mathf.Approximately(scoreWhenTargetMayHaveShan, scoreWithoutKnownResponse),
                "AI 不应读取目标隐藏的闪牌"
            );
        }
    }

    private static IdentityType[] StandardIdentities()
    {
        return new[]
        {
            IdentityType.Commander,
            IdentityType.Protector,
            IdentityType.Rebel,
            IdentityType.Rebel,
            IdentityType.Wanderer,
        };
    }

    private static int Count(IdentityType[] values, IdentityType target)
    {
        int count = 0;
        foreach (IdentityType value in values)
        {
            if (value == target)
            {
                count++;
            }
        }

        return count;
    }

    private static object Invoke(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );
        if (method == null)
        {
            throw new InvalidOperationException("找不到方法：" + methodName);
        }

        return method.Invoke(target, arguments);
    }

    private static bool IsGameOver(TurnManager turnManager)
    {
        FieldInfo field = typeof(TurnManager).GetField("gameOver", BindingFlags.Instance | BindingFlags.NonPublic);
        return (bool)field.GetValue(turnManager);
    }

    private static string LastHighlight(TurnManager turnManager)
    {
        FieldInfo field = typeof(TurnManager).GetField("matchHighlights", BindingFlags.Instance | BindingFlags.NonPublic);
        List<string> highlights = (List<string>)field.GetValue(turnManager);
        return highlights[highlights.Count - 1];
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
