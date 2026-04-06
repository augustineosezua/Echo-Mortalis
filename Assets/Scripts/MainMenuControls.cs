using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuControls : MonoBehaviour
{
    public void PlayGame()
    {
        CheckpointSystem.Reset();
        GamePersistence.Reset();
        AudioManager.TryPlaySfx("ui_accept");
        SceneManager.LoadScene("Zone1_Test");
    }

    public void QuitGame()
    {
        AudioManager.TryPlaySfx("ui_back");
        Application.Quit();
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }

    public void OptionsPlaceholder()
    {
        AudioManager.TryPlaySfx("ui_back");
        // Options not yet implemented
        Debug.Log("Options: not yet implemented");
    }
}
