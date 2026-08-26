using UnityEditor;
using UnityEngine;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace MySecondWorld.EditorTools
{
    // Unity 6부터 Addressables Hosting Services 창이 사라져서, 그 역할을 대신함.
    // 에디터 로드/스크립트 컴파일마다 자동 실행 — ServerData 폴더를 8080 포트로 서빙.
    [InitializeOnLoad]
    public static class LocalBundleServerLauncher
    {
        private const int Port = 8080;
        private static Process _serverProcess;

        static LocalBundleServerLauncher()
        {
            if (IsPortInUse(Port))
            {
                UnityEngine.Debug.Log($"[LocalBundleServer] 포트 {Port} 이미 사용 중 — 기존 서버 그대로 사용");
                return;
            }

            var serverDataPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "ServerData");
            if (!Directory.Exists(serverDataPath))
            {
                UnityEngine.Debug.LogWarning($"[LocalBundleServer] {serverDataPath} 없음 — Addressables 빌드 먼저 필요");
                return;
            }

            _serverProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"-m http.server {Port} --bind 0.0.0.0",
                    WorkingDirectory = serverDataPath,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };

            _serverProcess.Start();
            UnityEngine.Debug.Log($"[LocalBundleServer] 로컬 번들 서버 시작 (포트 {Port}, PID {_serverProcess.Id})");

            AssemblyReloadEvents.beforeAssemblyReload += StopServer;
            EditorApplication.quitting += StopServer;
        }
        
        private static bool IsPortInUse(int port)
        {
            var listener = new TcpListener(IPAddress.Loopback, port);
            try
            {
                listener.Start();
                return false;
            }
            catch (SocketException)
            {
                return true;
            }
            finally
            {
                listener.Stop();
            }
        }

        private static void StopServer()
        {
            if (_serverProcess != null && !_serverProcess.HasExited)
            {
                _serverProcess.Kill();
                UnityEngine.Debug.Log("[LocalBundleServer] 로컬 번들 서버 종료");
            }
            AssemblyReloadEvents.beforeAssemblyReload -= StopServer;
            EditorApplication.quitting -= StopServer;
        }
    }

}
