using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Playables;

public class SceneLoader : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "GameScene";
    [SerializeField] private PlayableDirector timelineDirector;

    public void LoadGame()
    {
        if (timelineDirector != null)
        {
            timelineDirector.Stop();
        }

        SceneManager.LoadScene(gameSceneName);
    }
}