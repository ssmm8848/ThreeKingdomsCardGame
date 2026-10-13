using UnityEngine;

public enum GameMode
{
    TwoPlayer,
    ThreePlayer,
    FivePlayer,
}

public static class GameSelectionData
{
    private const string CustomExpansionPreferenceKey = "Deck.CustomExpansionEnabled";

    public static GameMode gameMode { get; private set; } = GameMode.TwoPlayer;

    public static bool customExpansionEnabled
    {
        get { return PlayerPrefs.GetInt(CustomExpansionPreferenceKey, 1) != 0; }
    }

    public static void SetCustomExpansionEnabled(bool enabled)
    {
        PlayerPrefs.SetInt(CustomExpansionPreferenceKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static void SetGameMode(GameMode mode)
    {
        gameMode = mode;

        // 切换模式时顺手清空上一局的残留选择：
        // 例如先打五人场再打二人场，enemyB/C/D 的角色数据不应被带过来。
        // 各选角控制器都是先调用本方法、再写入本局选择，所以顺序是安全的。
        playerCharacter = null;
        enemyCharacter = null;
        enemyACharacter = null;
        enemyBCharacter = null;
        enemyCCharacter = null;
        enemyDCharacter = null;

        playerTeamType = TeamType.Overlord;

        if (fivePlayerIdentities == null || fivePlayerIdentities.Length != 5)
        {
            fivePlayerIdentities = new IdentityType[5];
        }

        for (int i = 0; i < fivePlayerIdentities.Length; i++)
        {
            fivePlayerIdentities[i] = IdentityType.None;
        }
    }

    public static CharacterData playerCharacter;
    public static CharacterData enemyCharacter;

    public static CharacterData enemyACharacter;
    public static CharacterData enemyBCharacter;

    // 5人场角色数据
    public static CharacterData enemyCCharacter;
    public static CharacterData enemyDCharacter;

    // 5人场身份分配（按顺序：player, enemyA, enemyB, enemyC, enemyD）
    public static IdentityType[] fivePlayerIdentities = new IdentityType[5];

    // 三人场玩家选择的阵营：霸主(1v2) 或 协战(2v1)
    public static TeamType playerTeamType = TeamType.Overlord;
}
