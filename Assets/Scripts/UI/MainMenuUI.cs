using UnityEngine;

public class MainMenuUI : MonoBehaviour
{
    [SerializeField] private GameObject mainMenuCanvas;
    [SerializeField] private GameObject lobbyCanvas;

    private void Start()
    {
        mainMenuCanvas.SetActive(true);
        lobbyCanvas.SetActive(false);
    }

    public void PressStart()
    {
        mainMenuCanvas.SetActive(false);
        lobbyCanvas.SetActive(true);
    }
}
