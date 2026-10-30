#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Debug = UnityEngine.Debug;

namespace MySecondWorld.Editor
{
    public static class ServerBuildTool
    {
        private const string ServerBuildPath = "Builds/Server/MySecondWorld.x86_64";
        private const string AndroidBuildPath = "Builds/Android/MySecondWorld.apk";
        private const string KeyPath = "Builds/m2w-key.pem";
        private const string EC2Host = "32.236.54.192";
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

            DeployToEC2();
        }

        // =========================================================================
        // 1-b. [Upload Only] 빌드 없이 직전 빌드 결과물만 EC2에 올린다
        //      (업로드 실패 후 재시도용 — 코드를 고쳤다면 반드시 1-Click으로 다시 빌드할 것)
        // =========================================================================
        [MenuItem("Tools/Build/📤 [Upload Only] 빌드 없이 EC2 업로드 & 재시작", priority = 4)]
        public static void DeployOnly()
        {
            if (!Directory.Exists("ServerData/StandaloneLinux64") || !Directory.Exists(Path.GetDirectoryName(ServerBuildPath)))
            {
                Debug.LogError("[Deploy] ❌ 빌드 결과물이 없다 — 먼저 [1-Click]으로 빌드하라.");
                return;
            }

            Debug.Log("[Deploy] ========================================================");
            DeployToEC2();
        }

        // 빌드 결과물(번들 + 서버 바이너리)을 EC2에 올리고 서비스를 재시작한다. 빌드는 하지 않는다.
        private static bool DeployToEC2()
        {
            Debug.Log("[Deploy] 🚀 [2/3] EC2 기존 서버 안전 중지 및 파일 업로드 중 (잠시 대기)...");
            // 실행 중인 바이너리 덮어쓰기 에러 방지를 위해 서비스 먼저 중지
            // + scp -r는 목적지에 같은 이름의 디렉터리가 이미 있으면 그 "안으로" 복사한다
            //   (ServerData/StandaloneLinux64/StandaloneLinux64/ 처럼 중첩되고, 클라/서버는 계속 옛 번들을 받는다)
            //   → 업로드 전에 원격 디렉터리를 지워서 항상 새 번들이 올라가게 만든다.
            // df까지 같이 찍는다 — 디스크가 꽉 차면 scp가 "write remote ...: Failure"로 죽는데
            // 에러 메시지만 봐서는 용량 문제인 줄 모른다.
            if (!RunSsh($"sudo systemctl stop mysecondworld; rm -rf {EC2RemoteDir}/ServerData/StandaloneLinux64; mkdir -p {EC2RemoteDir}/ServerData; df -h /"))
            {
                Debug.LogError("[Deploy] ❌ 서버 중지/정리 실패 — 배포 중단.");
                return false;
            }

            // A: 리눅스 에셋번들 업로드
            if (!RunScp(new[] { "ServerData/StandaloneLinux64" }, $"{EC2RemoteDir}/ServerData/"))
            {
                Debug.LogError("[Deploy] ❌ 에셋번들 업로드 실패 — 배포 중단.");
                return false;
            }
            // B: 리눅스 서버 바이너리 업로드 (Builds/Server 하위 항목을 직접 펼쳐서 넘긴다)
            string serverBuildDir = Path.GetDirectoryName(ServerBuildPath);
            string[] serverBuildEntries = Directory.GetFileSystemEntries(serverBuildDir);
            if (!RunScp(serverBuildEntries, $"{EC2RemoteDir}/"))
            {
                Debug.LogError("[Deploy] ❌ 서버 바이너리 업로드 실패 — 배포 중단.");
                return false;
            }

            Debug.Log("[Deploy] 🚀 [3/3] 실행 권한 부여 및 systemd 서비스 재시작...");
            RunSsh($"chmod +x {EC2RemoteDir}/MySecondWorld.x86_64; sudo systemctl restart mysecondworld");

            // 업로드 결과 확인 — aa/ 카탈로그와 번들이 실제로 올라갔는지 눈으로 확인할 수 있게 찍는다
            RunSsh($"ls {EC2RemoteDir}/MySecondWorld_Data/StreamingAssets/aa; ls {EC2RemoteDir}/ServerData/StandaloneLinux64; df -h /");

            Debug.Log("🎉 [Deploy] Linux 전용 서버가 EC2에 배포 및 재시작되었습니다! 위 ls 출력에 aa/ 와 catalog_*.json 이 보이는지 확인하세요.");
            Debug.Log("[Deploy] ========================================================");
            return true;
        }

        // =========================================================================
        // 2. [Android] 안드로이드 테스트 대비용 버튼
        // =========================================================================
        [MenuItem("Tools/Build/📱 [Android] 번들 빌드 + EC2 자동 업로드 (원격 리소스)", priority = 2)]
        public static void BuildAndDeployAndroidBundles()
        {
            Debug.Log("[Deploy-Android] ========================================================");

            // 타겟 전환 직후 BuildPlayerContent()를 부르면 이전 타겟의 콘텐츠가 만들어진다.
            // 전환만 하고 중단 → 재임포트가 끝난 뒤 사용자가 한 번 더 실행하는 방식으로 나눈다.
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                Debug.LogWarning("[Deploy-Android] 📱 Android 타겟으로 전환을 시작한다.");
                var androidProfile = AssetDatabase.LoadAssetAtPath<UnityEditor.Build.Profile.BuildProfile>("Assets/Settings/Build Profiles/Android™.asset");
                if (androidProfile != null)
                    UnityEditor.Build.Profile.BuildProfile.SetActiveBuildProfile(androidProfile);
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

                Debug.LogError("[Deploy-Android] ⛔ 전환/재임포트가 끝난 뒤 메뉴를 '한 번 더' 실행하라. 지금은 여기서 중단한다.");
                return;
            }

            Debug.Log("[Deploy-Android] 📱 Android용 Addressables 번들 빌드 시작...");
            AddressableAssetSettings.BuildPlayerContent(out var buildResult);
            if (!string.IsNullOrEmpty(buildResult.Error))
            {
                Debug.LogError($"[Deploy-Android] ❌ Addressables 빌드 실패: {buildResult.Error}");
                return;
            }

            string androidDataDir = Path.GetFullPath("ServerData/Android");

            // 빈 폴더를 올려서 "업로드 성공"으로 착각하는 걸 막는다
            if (!Directory.Exists(androidDataDir) || Directory.GetFiles(androidDataDir, "catalog_*.json").Length == 0)
            {
                Debug.LogError($"[Deploy-Android] ❌ {androidDataDir} 에 카탈로그가 없다 — 업로드 중단. Addressables 프로파일의 Remote 경로를 확인하라.");
                return;
            }

            PruneStaleBundles(androidDataDir, Directory.GetFiles(androidDataDir, "catalog_*.json").OrderByDescending(File.GetLastWriteTime).First());

            Debug.Log("[Deploy-Android] 🚀 EC2 웹서버로 Android 번들 업로드 중...");
            // scp -r 중첩 방지 — 원격 디렉터리를 먼저 지운다 (Linux 배포와 동일한 이유)
            if (!RunSsh($"rm -rf {EC2RemoteDir}/ServerData/Android; mkdir -p {EC2RemoteDir}/ServerData; df -h /"))
                return;
            if (!RunScp(new[] { "ServerData/Android" }, $"{EC2RemoteDir}/ServerData/"))
                return;

            Debug.Log("🎉 [Deploy-Android] Android 에셋번들이 EC2에 업로드되었습니다!");
            Debug.LogWarning("[Deploy-Android] ⚠️ 지금 에디터 빌드 타겟은 Android다. 서버를 다시 구우려면 Linux Server 프로파일로 되돌려라.");
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
            Debug.Log("[Deploy] 🔄 EC2 서버 서비스 재시작 중...");
            RunSsh("sudo systemctl restart mysecondworld");
            Debug.Log("🎉 [Deploy] EC2 서버 서비스 재시작 완료!");
        }

        [MenuItem("Tools/Build/1. Linux 전용 Addressables 번들 빌드 (Remote)", priority = 20)]
        public static void BuildAddressablesRemoteMenu() => BuildAddressablesRemote();

        // 타겟 전환은 스크립트 재컴파일을 유발해 이 메서드 뒤쪽이 실행되지 않을 수 있다.
        // 그래서 "전환만 하고 중단 → 사용자가 다시 실행"으로 나눈다.
        // 전환 직후 BuildPlayerContent()를 부르면 이전 타겟(Android 등)의 콘텐츠가 만들어져
        // aa/Linux 가 비는 채로 서버가 배포된다 — 오늘 사고의 원인.
        public static bool BuildAddressablesRemote()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneLinux64)
            {
                Debug.LogWarning("[Build] 현재 빌드 타겟이 StandaloneLinux64가 아니다 — 전환을 시작한다.");
                var linuxProfile = AssetDatabase.LoadAssetAtPath<UnityEditor.Build.Profile.BuildProfile>("Assets/Settings/Build Profiles/Linux Server.asset");
                if (linuxProfile != null)
                    UnityEditor.Build.Profile.BuildProfile.SetActiveBuildProfile(linuxProfile);
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64);

                Debug.LogError("[Build] ⛔ 타겟 전환/재임포트가 끝난 뒤 메뉴를 '한 번 더' 실행하라. 지금은 여기서 중단한다.");
                return false;
            }

            EditorUserBuildSettings.standaloneBuildSubtarget = StandaloneBuildSubtarget.Server;

            DateTime buildStartedAt = DateTime.Now;
            Debug.Log("[Build] Linux용 Addressables 번들 빌드 시작...");
            AddressableAssetSettings.BuildPlayerContent(out var buildResult);

            if (!string.IsNullOrEmpty(buildResult.Error))
            {
                Debug.LogError($"[Build] ❌ Addressables 빌드 실패: {buildResult.Error}");
                return false;
            }

            // 검증 1: StreamingAssets로 복사될 로컬 카탈로그(aa/Linux)가 실제로 생겼는가
            // (이게 없으면 서버가 Addressables.InitializeAsync()에서 죽어 ServerReady가 영원히 false다)
            string localCatalogDir = Addressables.BuildPath;
            if (!Directory.Exists(localCatalogDir) || Directory.GetFiles(localCatalogDir, "settings.json", SearchOption.AllDirectories).Length == 0)
            {
                Debug.LogError($"[Build] ❌ 로컬 카탈로그가 없다: {localCatalogDir}\n" +
                               "Addressables Groups 창에서 Build > New Build > Default Build Script를 직접 한 번 돌려라.");
                return false;
            }

            // 검증 2: 원격 번들(ServerData/StandaloneLinux64)이 이번 실행에서 갱신됐는가
            string remoteDir = Path.GetFullPath("ServerData/StandaloneLinux64");
            var catalogs = Directory.Exists(remoteDir) ? Directory.GetFiles(remoteDir, "catalog_*.json") : Array.Empty<string>();
            if (catalogs.Length == 0 || !catalogs.Any(c => File.GetLastWriteTime(c) >= buildStartedAt))
            {
                Debug.LogError($"[Build] ❌ 원격 카탈로그가 갱신되지 않았다: {remoteDir}\n" +
                               "Addressables 프로파일이 Remote(ServerData/[BuildTarget])를 가리키는지 확인하라.");
                return false;
            }

            PruneStaleBundles(remoteDir, catalogs.OrderByDescending(File.GetLastWriteTime).First());

            Debug.Log($"[Build] ✅ Linux Addressables 빌드 완료 — 로컬: {localCatalogDir} / 원격: {remoteDir}");
            return true;
        }

        // Addressables는 재빌드해도 구버전 .bundle을 지우지 않고 계속 쌓는다.
        // 그대로 두면 업로드 용량이 눈덩이처럼 불어나 EC2 디스크가 100%가 되고,
        // scp가 "write remote ...: Failure"로 죽는다(오늘 겪은 그 에러).
        // 현재 카탈로그가 참조하지 않는 번들은 죽은 파일이므로 지운다.
        private static void PruneStaleBundles(string remoteDir, string catalogPath)
        {
            if (!Directory.Exists(remoteDir)) return;

            string catalogText = File.ReadAllText(catalogPath);
            long freed = 0;
            int count = 0;

            foreach (string bundlePath in Directory.GetFiles(remoteDir, "*.bundle"))
            {
                if (catalogText.Contains(Path.GetFileName(bundlePath))) continue;

                freed += new FileInfo(bundlePath).Length;
                File.Delete(bundlePath);
                count++;
            }

            if (count > 0)
                Debug.Log($"[Build] 🧹 카탈로그 미참조 구버전 번들 {count}개 삭제 ({freed / (1024 * 1024)} MB 확보)");
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

            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[Build] ❌ 서버 빌드 실패! 에러 수: {summary.totalErrors}");
                return false;
            }

            // Addressables를 DoNotBuildWithPlayer로 꺼놨기 때문에, 플레이어 빌드는
            // Library/com.unity.addressables/aa/<플랫폼> 을 StreamingAssets로 "복사만" 한다.
            // 그게 비어 있으면 빌드는 성공으로 끝나지만 서버는 Addressables.InitializeAsync()에서 죽고
            // GameNetworkManager.ServerReady가 영원히 false → 클라는 로그인만 되고 씬/스폰이 멈춘다.
            string catalogDir = Path.Combine(Path.GetDirectoryName(ServerBuildPath), "MySecondWorld_Data/StreamingAssets/aa");
            if (!Directory.Exists(catalogDir))
            {
                Debug.LogError($"[Build] ❌ StreamingAssets/aa 가 없다 ({catalogDir}) — Linux용 Addressables 로컬 콘텐츠 미빌드.\n" +
                               "메뉴 '1. Linux 전용 Addressables 번들 빌드'를 먼저 성공시켜라. 이 상태로 배포하면 접속만 되고 씬이 안 열린다.");
                return false;
            }

            // 서버는 로컬 LLM(LlamaLib)을 쓰지 않는다. LLMUnity가 빌드 때 복사해 넣은 것을 결과물에서 제거한다 (약 538MB)
            string streamingDir = Path.Combine(outputDir, "MySecondWorld_Data/StreamingAssets");
            long freed = 0;
            foreach (string dir in Directory.GetDirectories(streamingDir, "LlamaLib-*"))
            {
                // 지우기 전에 폴더 안 모든 파일(EnumerateFiles = 파일을 하나씩 꺼냄)의 크기를 합산해 둔다
                freed += new DirectoryInfo(dir).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
                Directory.Delete(dir, true);
            }
            if (freed > 0)
                Debug.Log($"[Build] 🧹 서버 결과물에서 LlamaLib 제거 ({freed / (1024 * 1024)} MB 확보)");

            Debug.Log($"[Build] 🎉 서버 빌드 대성공! (크기: {summary.totalSize / (1024 * 1024):F1} MB, 위치: {ServerBuildPath})");
            return true;
        }

        [MenuItem("Tools/Build/3. [ALL-IN-ONE] Linux 번들 + Linux 서버 일괄 빌드", priority = 22)]
        public static void BuildAll() => BuildAllInternal();

        private static bool BuildAllInternal()
        {
            // 번들 빌드가 실패했는데 서버만 굽는 걸 막는다 — 그게 오늘 사고의 형태였다.
            if (!BuildAddressablesRemote()) return false;
            return BuildLinuxServer();
        }

        private static string KeyArg => $"-i \"{Path.GetFullPath(KeyPath)}\" -o StrictHostKeyChecking=no -o BatchMode=yes";

        // 원격에서 셸 명령 실행.
        // powershell.exe를 거치지 않는다 — -Command "ssh ... \"a; b; c\"" 형태로 넘기면
        // PowerShell이 중첩 따옴표를 벗겨내면서 ';' 뒤 명령들을 '로컬' 문장으로 쪼개 실행해버린다
        // (원격을 지우려던 rm -rf가 로컬에서 돌 뻔한, 조용하고 위험한 버그).
        // ssh.exe를 직접 띄우면 따옴표가 그대로 보존된다.
        public static bool RunSsh(string remoteCommand)
            => RunProcess("ssh", $"{KeyArg} {EC2User}@{EC2Host} \"{remoteCommand}\"");

        // localPaths를 원격 remoteDir로 업로드. 와일드카드(*)는 쓰지 않는다 —
        // CreateProcess는 글롭을 확장해주지 않아서 '*'가 파일명 그대로 전달된다.
        // 대신 C#에서 미리 펼쳐 여러 소스 인자로 넘긴다(scp가 지원하는 형식).
        private static bool RunScp(string[] localPaths, string remoteDir)
        {
            string sources = string.Join(" ", localPaths.Select(p => $"\"{Path.GetFullPath(p)}\""));
            return RunProcess("scp", $"{KeyArg} -r {sources} {EC2User}@{EC2Host}:{remoteDir}");
        }

        private static bool RunProcess(string fileName, string arguments)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            try
            {
                using var p = Process.Start(psi);
                // ReadToEnd를 순차로 부르면 한쪽 파이프가 가득 찰 때 교착된다 → 비동기로 받는다
                var stdoutTask = p.StandardOutput.ReadToEndAsync();
                var stderrTask = p.StandardError.ReadToEndAsync();
                p.WaitForExit();
                string stdout = stdoutTask.Result;
                string stderr = stderrTask.Result;

                if (!string.IsNullOrWhiteSpace(stdout)) Debug.Log($"[Deploy] {stdout.Trim()}");
                if (p.ExitCode != 0)
                {
                    Debug.LogError($"[Deploy 실패 - Code {p.ExitCode}] {fileName} {arguments}\n{stderr}");
                    return false;
                }
                if (!string.IsNullOrWhiteSpace(stderr)) Debug.LogWarning($"[Deploy] {stderr.Trim()}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Deploy 예외] {fileName}: {ex.Message}");
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