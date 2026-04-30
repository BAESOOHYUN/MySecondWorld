using System.IO;
using LLMUnity;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

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

        private bool _cleanedUp;

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

        private void OnEnable()
        {
            Application.quitting += CleanupNative;
#if UNITY_EDITOR
            // Unity Editor 전용 — Play Mode ↔ Edit Mode 전환을 감지하는 이벤트
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif
        }

        private void OnDisable()
        {
            Application.quitting -= CleanupNative;
#if UNITY_EDITOR
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
#endif
        }

#if UNITY_EDITOR
        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)    // ← Play Mode 종료 감지
                CleanupNative();                                 // ← 프로세스 + C# 객체 정리
        }
#endif

        // 도메인 리로드 OFF 환경에서 이전 세션이 남긴 llamalib subprocess가 누적되지 않도록
        // managed Destroy (_llm?.Destroy();) 전에 OS 프로세스를 먼저 죽여 LLM_Delete가 즉시 리턴되게 한다.

        // llamalib subprocess : LLMUnity 라이브러리가 OS 레벨에서 실행하는 별도 프로세스
        // Unity Main Process (C#)
        //        ↓ (IPC 통신)
        // llamalib_win-x64_server(Native C++)  ← 실제 AI 추론 실행
        private void CleanupNative()
        {
            if (_cleanedUp) return; // 중복 실행 방지
            _cleanedUp = true;

            // Step 1: 프로세스 강제 종료
            // Windows의 llamalib_win-x64_server.exe 프로세스를 찾아서 proc.Kill() → 강제 종료
            foreach (var proc in System.Diagnostics.Process.GetProcessesByName("llamalib_win-x64_server"))
            {
                try 
                { 
                    proc.Kill();            // ← OS 프로세스 즉시 죽임 (해결책)
                    proc.WaitForExit(500);  // ← 500ms 대기해서 정말 죽을 때까지 기다림
                }
                catch { }
            }
            // Step 2: C# 객체 정리
            try 
            {
                _llm?.Destroy(); // C++ 메모리 (native handle) 정리
            }
            catch { }
        }
    }
}

//Domain Reload ON vs OFF

//┌─ Play Mode 1
//│  LLMInitializer 실행
//│  → llamalib_win-x64_server 프로세스 시작 (PID 1234)
//│
//└─ Edit Mode로 복귀

//   ✅ Domain Reload ON (기본값)
//   │ C# 클래스 인스턴스 전부 메모리에서 제거
//   │ → _llm 인스턴스 소멸
//   │ → LLMInitializer 인스턴스 소멸
//   │ → (C#이 정리되면서 llamalib 프로세스도 자동 정리)
//   │
//   ❌ Domain Reload OFF
//     C# 클래스 인스턴스들이 메모리에 **남아있음**
//     → _llm 인스턴스 여전히 존재
//     → LLMInitializer 인스턴스 여전히 존재
//     → llamalib_win-x64_server 프로세스도 여전히 실행 중 (PID 1234)

//┌─ Play Mode 2
//│ LLMInitializer.Start() 또 실행
//│ → llamalib_win-x64_server 프로세스 **또 띄우려고 함** (PID 5678)
//│    ❌ 문제: PID 1234가 이미 같은 포트 점유 중
//│    → 포트 충돌 → 실패
//└─


//작동 흐름

//[에디터에서 ▶️ 누름]
//   → Play Mode 시작
//   → ... (게임 플레이) ...
//   → [■ 누름 또는 ESC]
//   → ExitingPlayMode 감지
//   → OnPlayModeStateChanged 호출
//   → CleanupNative()
//   → llamalib_win-x64_server 강제 종료
//   → _llm.Destroy()