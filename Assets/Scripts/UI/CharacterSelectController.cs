using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CharacterSelectController : MonoBehaviour
{
    [Header("可选角色")]
    public CharacterData chiXiao;
    public CharacterData qingLan;
    public CharacterData xuanHeng;
    public CharacterData baiYuan;
    public CharacterData yanZhiWei;

    [Header("当前选择")]
    public CharacterData selectedCharacter;

    [Header("UI")]
    public TMP_Text selectedInfoText;
    public Button startBattleButton;

    void Start()
    {
        StyleNavigationButtons();
        CharacterSelectCard.PopulateResourceCards(this);
        selectedCharacter = null;
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

    public void SelectChiXiao()
    {
        SelectCharacter(chiXiao);
    }

    public void SelectQingLan()
    {
        SelectCharacter(qingLan);
    }

    public void SelectXuanHeng()
    {
        SelectCharacter(xuanHeng);
    }

    public void SelectBaiYuan()
    {
        SelectCharacter(baiYuan);
    }

    public bool IsSelected(CharacterData character)
    {
        return selectedCharacter == character;
    }

    public void StartBattle()
    {
        if (selectedCharacter == null)
        {
            Debug.Log("请先选择角色");
            return;
        }

        GameSelectionData.SetGameMode(GameMode.TwoPlayer);
        GameSelectionData.playerCharacter = selectedCharacter;

        // 从所有角色中随机选一个不是玩家选的作为敌人
        List<CharacterData> allCharacters = GetAllCharacters();
        List<CharacterData> enemyOptions = new List<CharacterData>();

        foreach (CharacterData c in allCharacters)
        {
            if (c != null && c != selectedCharacter)
            {
                enemyOptions.Add(c);
            }
        }

        if (enemyOptions.Count > 0)
        {
            int randomIndex = Random.Range(0, enemyOptions.Count);
            GameSelectionData.enemyCharacter = enemyOptions[randomIndex];
        }

        SceneManager.LoadScene("Battle2P");
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
                selectedInfoText.text =
                    "当前选择："
                    + selectedCharacter.characterName
                    + "    体力："
                    + selectedCharacter.maxHp
                    + "    技能："
                    + selectedCharacter.skillName;
            }
        }

        if (startBattleButton != null)
        {
            startBattleButton.interactable = selectedCharacter != null;
        }
    }
}
