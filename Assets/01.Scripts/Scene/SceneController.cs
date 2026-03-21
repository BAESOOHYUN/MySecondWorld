using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneController : MonoBehaviour
{
    public GameObject menu;
    public GameObject lodingInterface;
    public Image loadingProgressBar;

    List<AsyncOperation> scenesToLoad = new List<AsyncOperation>();


    public void StartGame()
    {
        HideMenu();
        ShowLoadingScreen();
        //LoadSceneAsync을 통해 백그라운드에서 Scene을 비동기적으로 로드
        scenesToLoad.Add(SceneManager.LoadSceneAsync($"")); //전체 씬
        scenesToLoad.Add(SceneManager.LoadSceneAsync($"", LoadSceneMode.Additive)); //전체 씬의 부분 씬
        StartCoroutine(LoadingScreen());
    }

    public void HideMenu()
    {
        menu.SetActive(false);
    }

    public void ShowLoadingScreen()
    {
        lodingInterface.SetActive(true);
    }

    private IEnumerator LoadingScreen()
    {
        float totalProgress = 0;
        for (int i = 0; i < scenesToLoad.Count; i++)
        {
            // 장면이 아직 로드되지 않은 한,
            while (!scenesToLoad[i].isDone)
            {
                // AsyncOperation를 사용하면 로딩 프로세스의 progress 값에 액세스 할 수 있다.
                // 각 씬의 로딩 진행률을 totalProgress 값에 추가한다.
                totalProgress += scenesToLoad[i].progress;
                // 그런 다음 totalProgress 값을 로드할 장면 수로 나누어, 프로그래스 바를 채운다.
                loadingProgressBar.fillAmount = totalProgress / scenesToLoad.Count;
                yield return null;
            }
        }
    }

    public void ExitGame()
    {
        Application.Quit();
    }
}
