using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CharacterSelect5PController : MonoBehaviour
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

        GameSelectionData.SetGameMode(GameMode.FivePlayer);

        GameSelectionData.playerCharacter = selectedCharacter;

        // 分配身份
        IdentityType[] identities = IdentityAllocator.AllocateFivePlayer();
        GameSelectionData.fivePlayerIdentities = identities;

        // 从所有角色中随机选4个不同的角色作为其他玩家
        List<CharacterData> allCharacters = GetAllCharacters();
        List<CharacterData> enemyOptions = new List<CharacterData>();

        foreach (CharacterData c in allCharacters)
        {
            if (c != null && c != selectedCharacter)
            {
                enemyOptions.Add(c);
            }

        }

        // 随机选4个不同的AI角色。
        GameSelectionData.enemyACharacter = PickRandom(enemyOptions);
        GameSelectionData.enemyBCharacter = PickRandom(enemyOptions);
        GameSelectionData.enemyCCharacter = PickRandom(enemyOptions);
        GameSelectionData.enemyDCharacter = PickRandom(enemyOptions);

        SceneManager.LoadScene("Battle5P");
    }

    private CharacterData PickRandom(List<CharacterData> options)
    {
        if (options == null || options.Count == 0)
        {
            return null;
        }

        int index = Random.Range(0, options.Count);
        CharacterData selected = options[index];
        options.RemoveAt(index);
        return selected;
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

    public List<CharacterData> GetAllCharacters()
    {
        List<CharacterData> list = new List<CharacterData>();

        if (chiXiao != null) list.Add(chiXiao);
        if (qingLan != null) list.Add(qingLan);
        if (xuanHeng != null) list.Add(xuanHeng);
        if (baiYuan != null) list.Add(baiYuan);
        if (yanZhiWei != null) list.Add(yanZhiWei);

        CharacterSelectCard.AppendResourceCharacters(list);

        return list;
    }

    private void RefreshUI()
    {
        if (selectedInfoText != null)
        {
            if (selectedCharacter == null)
            {
                selectedInfoText.text = "当前选择：未选择\n执令者公开；出杀记实证，敌对锦囊记未证实；伪迹可栽赃；两点公开身份";
            }
            else
            {
                selectedInfoText.text =
                    "当前选择：" + selectedCharacter.characterName +
                    "    体力：" + selectedCharacter.maxHp +
                    "    技能：" + selectedCharacter.skillName +
                    "\n执令者公开；出杀记实证，敌对锦囊记未证实；伪迹可栽赃；两点公开身份";
            }
        }

        if (startBattleButton != null)
        {
            startBattleButton.interactable = selectedCharacter != null;
        }
    }
}
