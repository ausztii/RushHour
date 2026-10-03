using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The start menu's behaviour: PLAY leaves for the run, HOW TO PLAY swaps the title card for the
/// briefing, and BACK returns to it.
///
/// It owns only the menu's own presentation state. Nothing in the run exists until the gameplay
/// scene loads, so this never touches a HUD, a GameManager or a battery - it just opens a scene.
/// </summary>
public class StartScreenController : MonoBehaviour
{
    /// <summary>
    /// The how-to-play overlay. Held inactive so the title card is the first thing on screen;
    /// the builder leaves it disabled.
    /// </summary>
    public GameObject howToPlayPanel;

    /// <summary>Scene name of the playable run, as it appears in the build list.</summary>
    public string gameplaySceneName = "RushHour";

    /// <summary>Leaves the menu for the run.</summary>
    public void PlayGame()
    {
        SceneManager.LoadScene(gameplaySceneName);
    }

    /// <summary>Shows the briefing over the title card.</summary>
    public void ShowHowToPlay()
    {
        if (howToPlayPanel != null) howToPlayPanel.SetActive(true);
    }

    /// <summary>Returns to the title card.</summary>
    public void HideHowToPlay()
    {
        if (howToPlayPanel != null) howToPlayPanel.SetActive(false);
    }
}
