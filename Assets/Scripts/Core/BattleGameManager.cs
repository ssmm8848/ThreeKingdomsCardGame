using UnityEngine;

public class BattleGameManager : MonoBehaviour
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

    [Header("默认角色")]
    public CharacterData defaultPlayerCharacter;
    public CharacterData defaultEnemyCharacter;
    public CharacterData defaultEnemyBCharacter;

    [Header("牌堆管理器")]
    public DeckManager deckManager;

    [Header("回合管理器")]
    public TurnManager turnManager;

    [Header("战斗UI")]
    public BattleUI battleUI;

    [Header("日志")]
    public GameLogger gameLogger;

    void Start()
    {
        StartNewGame();
    }

    public void StartNewGame()
    {
        if (battleUI != null)
        {
            battleUI.ClearPendingAttack();
        }

        if (gameLogger != null)
        {
            gameLogger.ClearLog();
        }

        Debug.Log("========== 新的一局开始 ==========");

        ApplySelectedCharacters();

        // 初始化所有角色
        if (player != null)
            player.InitializePlayer();
        if (enemy != null)
            enemy.InitializePlayer();

        if (
            enemyB != null
            && GameSelectionData.gameMode != GameMode.TwoPlayer
        )
        {
            enemyB.InitializePlayer();
        }

        if (enemyC != null && GameSelectionData.gameMode == GameMode.FivePlayer)
        {
            enemyC.InitializePlayer();
        }

        if (enemyD != null && GameSelectionData.gameMode == GameMode.FivePlayer)
        {
            enemyD.InitializePlayer();
        }

        // 在所有角色初始化完成后再设置阵营/身份和血量加成
        SetupPlayerRoles();

        if (deckManager != null)
        {
            deckManager.InitializeDeck();
        }

        if (battleUI != null)
        {
            battleUI.RefreshUI();
        }

        if (turnManager != null)
        {
            turnManager.StartGame();
        }
    }

    public void ConfirmRestart()
    {
        GameConfirmationDialog.Show(
            FindObjectOfType<Canvas>(),
            "重新开始",
            "当前对局进度将丢失。确定重新开始吗？",
            "重新开始",
            "继续对局",
            StartNewGame,
            GameUIButtonTone.Destructive
        );
    }

    private void ApplySelectedCharacters()
    {
        CharacterData playerCharacter = GameSelectionData.playerCharacter;
        CharacterData enemyCharacter = GameSelectionData.enemyCharacter;
        CharacterData enemyACharacter = GameSelectionData.enemyACharacter;
        CharacterData enemyBCharacter = GameSelectionData.enemyBCharacter;
        CharacterData enemyCCharacter = GameSelectionData.enemyCCharacter;
        CharacterData enemyDCharacter = GameSelectionData.enemyDCharacter;

        if (playerCharacter == null)
            playerCharacter = defaultPlayerCharacter;
        if (enemyCharacter == null)
            enemyCharacter = defaultEnemyCharacter;
        if (enemyACharacter == null)
            enemyACharacter = defaultEnemyCharacter;
        if (enemyBCharacter == null)
            enemyBCharacter = defaultEnemyBCharacter;
        if (enemyBCharacter == null)
            enemyBCharacter = defaultEnemyCharacter;
        if (enemyCCharacter == null)
            enemyCCharacter = defaultEnemyCharacter;
        if (enemyDCharacter == null)
            enemyDCharacter = defaultEnemyCharacter;

        if (player != null && playerCharacter != null)
        {
            player.SetCharacter(playerCharacter);
        }

        // 二人场旧字段 enemy 仍然可以用
        if (enemy != null)
        {
            CharacterData finalEnemyCharacter =
                GameSelectionData.gameMode != GameMode.TwoPlayer
                    ? enemyACharacter
                    : enemyCharacter;

            if (finalEnemyCharacter != null)
            {
                enemy.SetCharacter(finalEnemyCharacter);
            }
        }

        if (enemyB != null && enemyBCharacter != null)
        {
            enemyB.SetCharacter(enemyBCharacter);
        }

        if (enemyC != null && enemyCCharacter != null)
        {
            enemyC.SetCharacter(enemyCCharacter);
        }

        if (enemyD != null && enemyDCharacter != null)
        {
            enemyD.SetCharacter(enemyDCharacter);
        }
    }

    // 根据游戏模式设置 isAI 和阵营/身份
    private void SetupPlayerRoles()
    {
        // 玩家始终由人类控制
        if (player != null)
        {
            player.isAI = false;
        }

        if (GameSelectionData.gameMode == GameMode.FivePlayer)
        {
            SetupFivePlayerIdentities();
        }
        else if (GameSelectionData.gameMode == GameMode.ThreePlayer)
        {
            SetupThreePlayerTeams();
        }
        else
        {
            // 二人场：无阵营，简单 1v1
            if (player != null)
                player.teamType = TeamType.None;
            if (enemy != null)
            {
                enemy.isAI = true;
                enemy.teamType = TeamType.None;
            }
        }
    }

    // 五人场：灵契之争身份分配
    private void SetupFivePlayerIdentities()
    {
        IdentityType[] identities = GameSelectionData.fivePlayerIdentities;
        Player[] allPlayers = new Player[] { player, enemy, enemyB, enemyC, enemyD };

        for (int i = 0; i < allPlayers.Length && i < identities.Length; i++)
        {
            if (allPlayers[i] == null)
                continue;

            allPlayers[i].identityType = identities[i];

            // 执令者身份公开
            if (identities[i] == IdentityType.Commander)
            {
                allPlayers[i].isIdentityRevealed = true;
                // 执令者+1血量
                allPlayers[i].maxHp += 1;
                allPlayers[i].currentHp = allPlayers[i].maxHp;
            }

            // 设置AI控制
            if (allPlayers[i] != player)
            {
                allPlayers[i].isAI = true;
            }

            // 五人场不使用TeamType阵营系统
            allPlayers[i].teamType = TeamType.None;
        }

        Debug.Log("灵契之争身份分配完成");
        Debug.Log("执令者：" + FindCommander()?.playerName);
    }

    private Player FindCommander()
    {
        Player[] allPlayers = new Player[] { player, enemy, enemyB, enemyC, enemyD };

        foreach (Player p in allPlayers)
        {
            if (p != null && p.identityType == IdentityType.Commander)
            {
                return p;
            }
        }

        return null;
    }

    // 三人场阵营设置
    private void SetupThreePlayerTeams()
    {
        TeamType playerTeam = GameSelectionData.playerTeamType;

        if (player != null)
        {
            player.teamType = playerTeam;
            if (playerTeam == TeamType.Overlord)
            {
                player.maxHp += 1;
                player.currentHp = player.maxHp;
            }
        }

        if (playerTeam == TeamType.Overlord)
        {
            if (enemy != null)
            {
                enemy.isAI = true;
                enemy.teamType = TeamType.Alliance;
            }

            if (enemyB != null)
            {
                enemyB.isAI = true;
                enemyB.teamType = TeamType.Alliance;
            }
        }
        else
        {
            if (enemy != null)
            {
                enemy.isAI = true;
                enemy.teamType = TeamType.Overlord;
                enemy.maxHp += 1;
                enemy.currentHp = enemy.maxHp;
            }

            if (enemyB != null)
            {
                enemyB.isAI = true;
                enemyB.teamType = TeamType.Alliance;
            }
        }
    }
}
