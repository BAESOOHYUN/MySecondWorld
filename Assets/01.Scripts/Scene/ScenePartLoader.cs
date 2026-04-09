using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ScenePartLoader : MonoBehaviour
{
    public enum CheckMethod
    { 
        Distance,
        Trigger
    }

    public CheckMethod checkMethod;
    public Transform player;
    public float loadRange;

    private bool isLoaded; // 장면을 두번 로드하는 것을 방지함
    private bool shouldLoad; // Trigger 메서드에서 사용자가 콜라이더에 들어왔는지 판단함

    private void Start()
    {
        // 씬(scene)이 이미 열려 있는지 확인하여 씬(scene)을 두 번 열지 않도록 한다.
        // SceneManager.sceneCount는 '현재 로드된' Scene 수를 반환한다.
        if (SceneManager.sceneCount > 0)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.name == this.gameObject.name)
                {
                    isLoaded = true;
                }
            }
        }
    }

    private void Update()
    {
        if (checkMethod == CheckMethod.Distance)
            DistanceCheck();
        else if (checkMethod == CheckMethod.Trigger)
            TriggerCheck();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            shouldLoad = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {

        if (other.CompareTag("Player"))
        {
            shouldLoad = false;
        }
    }

    private void DistanceCheck()
    {
        // loadRange보다 거리가 좁으면 씬 로드
        if (Vector3.Distance(player.position, this.transform.position) < loadRange)
            LoadScene();
        else
            UnLoadScene();
    }

    private void TriggerCheck()
    {
        if (shouldLoad)
            LoadScene();
        else
            UnLoadScene();
    }

    private void LoadScene()
    {
        if (!isLoaded)
        {
            SceneManager.LoadSceneAsync(this.gameObject.name, LoadSceneMode.Additive);
            isLoaded = true;
        }
    }

    private void UnLoadScene()
    {
        if (isLoaded)
        {
            SceneManager.UnloadSceneAsync(this.gameObject.name);
            isLoaded = false;
        }
    }
}
