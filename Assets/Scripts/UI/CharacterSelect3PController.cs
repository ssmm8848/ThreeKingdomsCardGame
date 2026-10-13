using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CharacterSelect3PController : MonoBehaviour
{
    [Header("可选角色")]
    public CharacterData chiXiao;
    public CharacterData qingLan;
    public CharacterData xuanHeng;
    public CharacterData baiYuan;
    public CharacterData yanZhiWei;

    [Header("当前选择")]
    public CharacterData selectedCharacter;

    [Header("阵营选择")]
    public TeamType selectedTeamType = TeamType.Overlord;

    [Header("阵营按钮")]
    public Button overlordButton;
    public Button allianceButton;

    // 阵营按钮高亮颜色
    public Color selectedColor = new Color(1f, 0.85f, 0.3f);
    public Color normalColor = Color.white;

    [Header("UI")]
    public TMP_Text selectedInfoText;
    public Button startBattleButton;

    void Start()
    {
        StyleNavigationButtons();
        CharacterSelectCard.PopulateResourceCards(this);
        selectedCharacter = null;
        selectedTeamType = TeamType.Overlord;
        RefreshUI();
    }

    private void StyleNavigationButtons()
    {
        GameObject backObject = GameObject.Find("BackButton");
        GameUIButtonStyler.Apply(
            backObject != null ? backObject.GetComponent<Button>() : null,
            GameUIButtonTone.Navigation
        );
        GameUIButtonStyler.Apply(startBattleButton, GameUIButtonTone.PrimaryAction);
    }

    public void SelectCharacter(CharacterData character)
    {
        selectedCharacter = character;
        RefreshUI();
    }

    // 选择霸主阵营
    public void SelectOverlord()
    {
        selectedTeamType = TeamType.Overlord;
        RefreshUI();
    }

    // 选择协战阵营
    public void SelectAlliance()
    {
        selectedTeamType = TeamType.Alliance;
        RefreshUI();
    }

    public bool IsSelected(CharacterData character)
    {
        return selectedCharacter == character;
    }

    public bool IsTeamSelected(TeamType teamType)
    {
        return selectedTeamType == teamType;
    }

    public void StartBattle()
    {
        if (selectedCharacter == null)
        {
            Debug.Log("请先选择角色");
            return;
        }

        GameSelectionData.SetGameMode(GameMode.ThreePlayer);

        GameSelectionData.playerCharacter = selectedCharacter;
        GameSelectionData.playerTeamType = selectedTeamType;

        // 从所有角色中随机选两个不同的角色作为敌方
        List<CharacterData> allCharacters = GetAllCharacters();
        List<CharacterData> enemyOptions = new List<CharacterData>();

        foreach (CharacterData c in allCharacters)
        {
            if (c != null && c != selectedCharacter)
            {
                enemyOptions.Add(c);
            }
        }

        // 随机选敌方A
        if (enemyOptions.Count >= 2)
        {
            int indexA = Random.Range(0, enemyOptions.Count);
            GameSelectionData.enemyACharacter = enemyOptions[indexA];
            enemyOptions.RemoveAt(indexA);

            // 随机选敌方B（从剩余角色中选，可能和A重复阵营）
            int indexB = Random.Range(0, enemyOptions.Count);
            GameSelectionData.enemyBCharacter = enemyOptions[indexB];
        }
        else if (enemyOptions.Count == 1)
        {
            // 只剩一个角色，两个AI都用它
            GameSelectionData.enemyACharacter = enemyOptions[0];
            GameSelectionData.enemyBCharacter = enemyOptions[0];
        }

        SceneManager.LoadScene("Battle3P");
    }

    public void BackToMainMenu()
    {
        GameConfirmationDialog.Show(
            FindObjectOfType<Canvas>(),
            "返回主菜单",
            "返回后，当前选角将不会保留。确定继续吗？",
            "返回主菜单",
            "继续选角",
            () => SceneManager.LoadScene("MainMenu"),
            GameUIButtonTone.Navigation
        );
    }

    // 获取所有可选角色列表
    public List<CharacterData> GetAllCharacters()
    {
        List<CharacterData> list = new List<CharacterData>();

        if (chiXiao != null)
            list.Add(chiXiao);
        if (qingLan != null)
            list.Add(qingLan);
        if (xuanHeng != null)
            list.Add(xuanHeng);
        if (baiYuan != null)
            list.Add(baiYuan);
        if (yanZhiWei != null)
            list.Add(yanZhiWei);

        CharacterSelectCard.AppendResourceCharacters(list);

        return list;
    }

    private void RefreshUI()
    {
        if (selectedInfoText != null)
        {
            if (selectedCharacter == null)
            {
                selectedInfoText.text = "当前选择：未选择";
            }
            else
            {
                string teamName =
                    selectedTeamType == TeamType.Overlord ? "霸主（1v2）" : "协战（2v1）";
                string extraInfo =
                    selectedTeamType == TeamType.Overlord
                        ? "    体力：" + (selectedCharacter.maxHp + 1) + "（霸主+1）"
                        : "    体力：" + selectedCharacter.maxHp;

                selectedInfoText.text =
                    "当前选择："
                    + selectedCharacter.characterName
                    + "    阵营："
                    + teamName
                    + extraInfo
                    + "    技能："
                    + selectedCharacter.skillName;
            }
        }

        if (startBattleButton != null)
        {
            startBattleButton.interactable = selectedCharacter != null;
        }

        // 更新阵营按钮高亮
        if (overlordButton != null)
        {
            overlordButton.image.color =
                selectedTeamType == TeamType.Overlord ? selectedColor : normalColor;
        }

        if (allianceButton != null)
        {
            allianceButton.image.color =
                selectedTeamType == TeamType.Alliance ? selectedColor : normalColor;
        }
    }
}
