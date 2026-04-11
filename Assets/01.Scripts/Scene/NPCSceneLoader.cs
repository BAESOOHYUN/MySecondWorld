using UnityEngine;
using UnityEngine.SceneManagement;

namespace MySecondWorld.SceneManagement
{
    /// <summary>
    /// FieldScene 시작 시 Gemma.unity를 Additive로 로드합니다.
    /// </summary>
    public class NPCSceneLoader : MonoBehaviour
    {
        [SerializeField] private string _npcSceneName = "Gemma";

        private void Start()
        {
            if (!SceneManager.GetSceneByName(_npcSceneName).isLoaded)
            {
                SceneManager.LoadSceneAsync(_npcSceneName, LoadSceneMode.Additive);
            }
        }
    }
}
