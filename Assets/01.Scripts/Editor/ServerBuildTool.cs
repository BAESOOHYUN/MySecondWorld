#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace MySecondWorld.Editor
{
    public static class ServerBuildTool
    {
        private const string ServerBuildPath = "Builds/Server/MySecondWorld.x86_64";
        private const string AndroidBuildPath = "Builds/Android/MySecondWorld.apk";
        private const string KeyPath = "Builds/m2w-key.pem";
        private const string EC2Host = "54.79.190.139";
        private const string EC2User = "ubuntu";
        private const string EC2RemoteDir = "/home/ubuntu/game-server";

        // 빌드에 포함할 필수 스타트업 씬 (나머지 존/던전은 Addressables로 로드됨)
        private static readonly string[] StartupScenes = new[]
        {
            "Assets/00.Scenes/OpenWorldRobby.unity",
            "Assets/00.Scenes/MainScene.unity"
        };

        // =========================================================================
        // 1. [1-Click] 리눅스 서버 완전 자동 배포
        // =========================================================================
        [MenuItem("Tools/Build/🚀 [1-Click] Linux 서버 빌드 + EC2 자동 배포 & 재시작", priority = 1)]
        public static void BuildAndDeployLinuxServer()
        {
            Debug.Log("[Deploy] ========================================================");
            Debug.Log("[Deploy] 🚀 [1/3] Linux 번들 및 서버 빌드 시작...");
            if (!BuildAllInternal())
            {
                Debug.LogError("[Deploy] ❌ 빌드 실패로 인해 배포를 중단합니다.");
                return;
            }

            string fullKeyPath = Path.GetFullPath(KeyPath).Replace("\\", "/");
            string fullServerData = Path.GetFullPath("ServerData/StandaloneLinux64").Replace("\\", "/");
            string fullServerBuild = Path.GetFullPath("Builds/Server").Replace("\\", "/") + "/*";

            Debug.Log("[Deploy] 🚀 [2/3] EC2 기존 서버 안전 중지 및 파일 업로드 중 (잠시 대기)...");
            // 실행 중인 바이너리 덮어쓰기 에러 방지를 위해 서비스 먼저 중지
            RunPowerShell($"ssh -i \"{fullKeyPath}\" {EC2User}@{EC2Host} \"sudo systemctl stop mysecondworld\"");

            // A: 리눅스 에셋번들 업로드
            RunPowerShell($"scp -i \"{fullKeyPath}\" -r \"{fullServerData}\" {EC2User}@{EC2Host}:{EC2RemoteDir}/ServerData/");
            // B: 리눅스 서버 바이너리 업로드
            RunPowerShell($"scp -i \"{fullKeyPath}\" -r \"{fullServerBuild}\" {EC2User}@{EC2Host}:{EC2RemoteDir}/");

            Debug.Log("[Deploy] 🚀 [3/3] 실행 권한 부여 및 systemd 서비스 재시작...");
            RunPowerShell($"ssh -i \"{fullKeyPath}\" {EC2User}@{EC2Host} \"chmod +x {EC2RemoteDir}/MySecondWorld.x86_64; sudo systemctl restart mysecondworld\"");

            Debug.Log("🎉 [Deploy] Linux 전용 서버가 EC2에 완벽하게 배포 및 재시작되었습니다! 이제 접속 테스트를 진행하세요.");
            Debug.Log("[Deploy] ========================================================");
        }

        // =========================================================================
        // 2. [Android] 안드로이드 테스트 대비용 버튼
        // =========================================================================
        [MenuItem("Tools/Build/📱 [Android] 번들 빌드 + EC2 자동 업로드 (원격 리소스)", priority = 2)]
        public static void BuildAndDeployAndroidBundles()
        {
            Debug.Log("[Deploy-Android] ========================================================");
            Debug.Log("[Deploy-Android] 📱 Android 타겟으로 전환 중...");
            var androidProfile = AssetDatabase.LoadAssetAtPath<UnityEditor.Build.Profile.BuildProfile>("Assets/Settings/Build Profiles/Android™.asset");
            if (androidProfile != null)
                UnityEditor.Build.Profile.BuildProfile.SetActiveBuildProfile(androidProfile);
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

            Debug.Log("[Deploy-Android] 📱 Android용 Addressables 번들 빌드 시작...");
            AddressableAssetSettings.BuildPlayerContent();
            Debug.Log("[Deploy-Android] 📱 Android 번들 빌드 완료! (ServerData/Android/)");

            string fullKeyPath = Path.GetFullPath(KeyPath).Replace("\\", "/");
            string fullAndroidData = Path.GetFullPath("ServerData/Android").Replace("\\", "/");

            Debug.Log("[Deploy-Android] 🚀 EC2 웹서버로 Android 번들 업로드 중...");
            RunPowerShell($"scp -i \"{fullKeyPath}\" -r \"{fullAndroidData}\" {EC2User}@{EC2Host}:{EC2RemoteDir}/ServerData/");

            Debug.Log("🎉 [Deploy-Android] Android 에셋번들이 EC2에 업로드되었습니다! 안드로이드 기기에서 접속 테스트가 가능합니다.");
            Debug.Log("[Deploy-Android] ========================================================");
        }

        [MenuItem("Tools/Build/📱 [Android] 번들 빌드 + EC2 업로드 + APK 빌드 (ALL-IN-ONE)", priority = 3)]
        public static void BuildAndDeployAndroidAll()
        {
            BuildAndDeployAndroidBundles();

            Debug.Log("[Deploy-Android] 📱 Android APK 빌드 시작...");
            string outputDir = Path.GetDirectoryName(AndroidBuildPath);
            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = StartupScenes,
                locationPathName = AndroidBuildPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"🎉 [Deploy-Android] APK 빌드 성공! 위치: {AndroidBuildPath}");
                EditorUtility.RevealInFinder(outputDir);
            }
            else
            {
                Debug.LogError("[Deploy-Android] ❌ APK 빌드 실패!");
            }
        }

        // =========================================================================
        // 3. 서버 퀵 재시작 및 개별 빌드 메뉴
        // =========================================================================
        [MenuItem("Tools/Build/🔄 [EC2] 서버 재시작만 실행 (Quick Restart)", priority = 10)]
        public static void RestartEC2ServerOnly()
        {
            string fullKeyPath = Path.GetFullPath(KeyPath).Replace("\\", "/");
            Debug.Log("[Deploy] 🔄 EC2 서버 서비스 재시작 중...");
            RunPowerShell($"ssh -i \"{fullKeyPath}\" {EC2User}@{EC2Host} \"sudo systemctl restart mysecondworld\"");
            Debug.Log("🎉 [Deploy] EC2 서버 서비스 재시작 완료!");
        }

        [MenuItem("Tools/Build/1. Linux 전용 Addressables 번들 빌드 (Remote)", priority = 20)]
        public static void BuildAddressablesRemote()
        {
            Debug.Log("[Build] StandaloneLinux64 타겟으로 전환 중...");
            var linuxProfile = AssetDatabase.LoadAssetAtPath<UnityEditor.Build.Profile.BuildProfile>("Assets/Settings/Build Profiles/Linux Server.asset");
            if (linuxProfile != null)
                UnityEditor.Build.Profile.BuildProfile.SetActiveBuildProfile(linuxProfile);
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64);

            Debug.Log("[Build] Linux용 Addressables 번들 빌드 시작...");
            AddressableAssetSettings.BuildPlayerContent();
            Debug.Log("[Build] Linux용 Addressables 번들 빌드 완료! (ServerData/StandaloneLinux64/ 생성됨)");
        }

        [MenuItem("Tools/Build/2. Linux Dedicated Server 빌드 (Headless)", priority = 21)]
        public static bool BuildLinuxServer()
        {
            Debug.Log("[Build] StandaloneLinux64 타겟 강제 확인 중...");
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64);

            Debug.Log("[Build] Linux Dedicated Server 빌드 시작...");
            var addressableSettings = AddressableAssetSettingsDefaultObject.Settings;
            if (addressableSettings != null)
                addressableSettings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;

            string outputDir = Path.GetDirectoryName(ServerBuildPath);
            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = StartupScenes,
                locationPathName = ServerBuildPath,
                target = BuildTarget.StandaloneLinux64,
                subtarget = (int)StandaloneBuildSubtarget.Server,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[Build] 🎉 서버 빌드 대성공! (크기: {summary.totalSize / (1024 * 1024):F1} MB, 위치: {ServerBuildPath})");
                return true;
            }
            else
            {
                Debug.LogError($"[Build] ❌ 서버 빌드 실패! 에러 수: {summary.totalErrors}");
                return false;
            }
        }

        [MenuItem("Tools/Build/3. [ALL-IN-ONE] Linux 번들 + Linux 서버 일괄 빌드", priority = 22)]
        public static void BuildAll() => BuildAllInternal();

        private static bool BuildAllInternal()
        {
            BuildAddressablesRemote();
            return BuildLinuxServer();
        }

        private static bool RunPowerShell(string command)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            try
            {
                using var p = Process.Start(psi);
                string stdout = p.StandardOutput.ReadToEnd();
                string stderr = p.StandardError.ReadToEnd();
                p.WaitForExit();

                if (!string.IsNullOrEmpty(stdout)) Debug.Log($"[Deploy] {stdout.Trim()}");
                if (p.ExitCode != 0)
                {
                    Debug.LogError($"[Deploy 실패 - Code {p.ExitCode}] {command}\n{stderr}");
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Deploy 예외] {ex.Message}");
                return false;
            }
        }
    }
}
#endif

/*
 
Windows Server도 선택할 수는 있다.
하지만 인디 개발자부터 대형 게임사까지 게임 서버의 99%가 Linux(Ubuntu)를 쓰는 데는 치명적인 3가지 이유가 있다.

① 비용 (Windows 라이선스 비)
Linux (Ubuntu): 오픈소스라** OS 비용이 0 (완전무료)** Lightsail월3.5 ~ $10
Windows Server: 마이크로소프트에 내는 OS 라이선스비가 붙어서, 똑같은 사양인데도 서버 비용이 2배~3배 비싸다. (Lightsail 기준 최소 월 $20 이상부터 시작)

② 서버 자원(RAM) 낭비
Windows Server: 윈도우 바탕화면, 창 애니메이션, 탐색기 등이 켜져 있는 것만으로도 RAM 1.5GB ~ 2GB를 OS 혼자 다 먹어버린다. (저렴한 2GB 램 서버를 빌리면 게임 서버를 켤 램조차 안 남음)
Linux: 화면(GUI)이 없는 텍스트 기반이라 OS가 먹는 RAM이 단 150MB에 불과합니다. 남은 1.85GB 램을 오롯이 유니티 게임 서버가 쓸 수 있다

③ 24시간 안정성 & 자동 복구
리눅스는 몇 달 동안 켜두어도 메모리 누수가 적고 매우 안정적.
무엇보다 서버가 불의의 사고로 다운되어도 5초 만에 혼자 다시 살아나게 하는 systemd 자동 복구 데몬 세팅이 리눅스에서 가장 완벽하게 돌아간다.

*/