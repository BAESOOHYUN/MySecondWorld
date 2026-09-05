#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MySecondWorld.Editor
{
    public static class ServerBuildTool
    {
        private const string ServerBuildPath = "Builds/Server/MySecondWorld.x86_64";

        // 빌드에 포함할 필수 스타트업 씬 (나머지 존/던전은 Addressables로 로드됨)
        private static readonly string[] StartupScenes = new[]
        {
            "Assets/00.Scenes/OpenWorldRobby.unity",
            "Assets/00.Scenes/MainScene.unity"
        };

        [MenuItem("Tools/Build/1. Linux 전용 Addressables 번들 빌드 (Remote)")]
        public static void BuildAddressablesRemote()
        {
            Debug.Log("[Build] StandaloneLinux64 타겟으로 전환 중...");
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64);

            Debug.Log("[Build] Linux용 Addressables 번들 빌드 시작...");
            AddressableAssetSettings.BuildPlayerContent();
            Debug.Log("[Build] Linux용 Addressables 번들 빌드 완료! (ServerData/StandaloneLinux64/ 생성됨)");
        }

        [MenuItem("Tools/Build/2. Linux Dedicated Server 빌드 (Headless)")]
        public static void BuildLinuxServer()
        {
            Debug.Log("[Build] Linux Dedicated Server 빌드 시작...");
            // 이미 BuildAddressablesRemote()로 번들을 만들어놨는데, BuildPipeline.BuildPlayer()가
            // Addressables "Build on Player Build" 설정 때문에 또 한 번 자동으로 빌드를 시도한다.
            // 이 자동 빌드가 Server subtarget을 못 다뤄서 실패하므로 여기서는 끈다.
            var addressableSettings = AddressableAssetSettingsDefaultObject.Settings;
            if (addressableSettings != null)
                addressableSettings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;

            // 빌드 디렉토리 확인 및 생성
            string outputDir = Path.GetDirectoryName(ServerBuildPath);
            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            // 빌드 옵션 구성
            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                // ① 빌드에 직접 집어넣을 씬 (스타트업 씬 2개만!)
                // 나머지 존/던전 씬은 Addressables로 원격 로드하므로 빌드에 안 넣어서 서버 실행 파일 용량이 확 줄어듬.
                scenes = StartupScenes,
                // ② 빌드 결과물이 나올 위치 (Builds/Server/MySecondWorld.x86_64)
                locationPathName = ServerBuildPath,
                // ③ 타겟 OS: 64비트 리눅스 실행 파일 형식으로 컴파일
                target = BuildTarget.StandaloneLinux64,
                // ④ ★ 가장 핵심: 'Headless Server' 모드로 빌드 지정
                // 모니터 화면 그리기(DirectX/Vulkan), 스피커 사운드 재생 등을 엔진 레벨에서 완전히 도려내고,
                // 오직 "물리 충돌 계산 + 네트워크 통신"만 돌아가는 초경량 서버로 만듬
                subtarget = (int)StandaloneBuildSubtarget.Server,
                options = BuildOptions.None
            };

            // ⑤ 유니티의 실제 빌드 엔진을 돌려서 컴파일 시작!
            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            // ⑥ 빌드가 성공했는지, 용량은 얼마인지, 에러는 없는지 결과표(Summary)를 받아서 콘솔에 출력
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[Build] 🎉 서버 빌드 대성공! (크기: {summary.totalSize / (1024 * 1024):F1} MB, 위치: {ServerBuildPath})");
                EditorUtility.RevealInFinder(outputDir);
            }
            else if (summary.result == BuildResult.Failed)
            {
                Debug.LogError($"[Build] ❌ 서버 빌드 실패! 에러 수: {summary.totalErrors}");
            }
        }

        [MenuItem("Tools/Build/3. [ALL-IN-ONE] Linux 번들 + Linux 서버 일괄 빌드")]
        public static void BuildAll()
        {
            BuildAddressablesRemote();
            BuildLinuxServer();
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