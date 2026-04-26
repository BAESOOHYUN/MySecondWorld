using System.IO;
using Cysharp.Threading.Tasks;
using LLMUnity;
using UnityEngine;

namespace MySecondWorld.AI
{
    /// <summary>
    /// MainScene의 별도 활성 GameObject에 부착.
    /// OpenWorldRobbyScene에서 PlayerPrefs에 저장해둔 모델 파일명을 읽어
    /// persistentDataPath 절대경로를 llm.model에 주입한 뒤 LLM GameObject를 활성화한다.
    /// LLM GameObject는 Inspector에서 비활성 상태로 시작해야 한다.
    /// </summary>
    public class LLMInitializer : MonoBehaviour
    {
        [SerializeField] private LLM _llm; // Inspector에서 비활성 LLM GameObject 연결

        private async void Start()
        {
            string fileName = PlayerPrefs.GetString("SelectedModelFileName", "");
            if (string.IsNullOrEmpty(fileName))
            {
                Debug.LogError("[LLMInitializer] PlayerPrefs에 모델 파일명이 없습니다. " +
                               "OpenWorldRobbyScene에서 다운로드가 완료됐는지 확인하세요.");
                return;
            }

            string fullPath = Path.Combine(Application.persistentDataPath, fileName);
            if (!File.Exists(fullPath))
            {
                Debug.LogError($"[LLMInitializer] 모델 파일이 없습니다: {fullPath}");
                return;
            }

            _llm.model = fullPath;
            _llm.gameObject.SetActive(true);
            await _llm.WaitUntilReady();

            Debug.Log($"[LLMInitializer] LLM 준비 완료: {fullPath}");
        }
    }
}
