namespace MySecondWorld.AI
{
    // 서버 모드 여부를 VContainer 주입 없이 동기적으로 조회하기 위한 정적 플래그.
    // ProjectLifetimeScope.Configure()에서 가장 먼저 설정되며(= MainScene 로드 시점),
    // 그 이후 Additive로 로드되는 모든 NPC/Companion 프리팹의 Awake()보다 항상 먼저 채워진다.
    public static class ServerModeFlag
    {
        public static bool UseServerMode { get; private set; }
        public static void Set(bool value) => UseServerMode = value;
    }
}
