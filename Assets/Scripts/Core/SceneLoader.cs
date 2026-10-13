using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public void LoadMainMenu()
    {
        GameConfirmationDialog.Show(
            FindObjectOfType<Canvas>(),
            "返回主菜单",
            "返回后，本局进度将丢失。确定继续吗？",
            "返回主菜单",
            "继续对局",
            () => SceneManager.LoadScene("MainMenu"),
            GameUIButtonTone.Navigation
        );
    }

    public void LoadBattle2P()
    {
        SceneManager.LoadScene("Battle2P");
    }
}
