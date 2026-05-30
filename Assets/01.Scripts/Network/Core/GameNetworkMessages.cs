using Mirror;

namespace MySecondWorld.Network
{
    // 서버↔클라이언트가 주고받는 편지 양식

    /// <summary>
    /// 서버 → 클라이언트: additive 씬 로드/언로드 지시
    /// </summary>
    public struct AdditiveSceneMessage : NetworkMessage
    {
        public string sceneName;
        public bool load; // true=로드, false=언로드
    }

    /// <summary>
    /// 서버 → 클라이언트: "N개 씬 로드 후 ACK 보내라"
    /// </summary>
    public struct SpawnReadyMessage : NetworkMessage
    {
        public int sceneCount;
    }

    /// <summary>
    /// 클라이언트 → 서버: "모든 씬 로드 완료"
    /// </summary>
    public struct SceneLoadedAckMessage : NetworkMessage { }


    //서버: 플레이어 Zone 진입 감지
    //  └→ BundleRequiredMessage 전송(어떤 번들 받아야 하는지)
    //       └→ 클라이언트: 번들 다운로드(ZoneAddressableLoader)
    //            └→ BundleReadyMessage 전송(다운로드 완료 ACK)
    //                 └→ 서버: spawn 허용

    /// <summary>
    /// 서버 → 클라이언트 : "이 Zone 번들 지금 다운로드해"
    /// </summary>
    public struct BundleRequiredMessage : NetworkMessage
    {
        public string zoneLabel;
    }

    /// <summary>
    /// 클라이언트 → 서버 : "번들 다운로드 완료, spawn 허용해도 됨"
    /// </summary>
    public struct BundleReadyMessage : NetworkMessage
    {
        public string zoneLabel;
    }

    /// <summary>
    /// 서버 → 클라이언트 : "내 catalog 버전이 이거야, 다르면 업데이트해"
    /// </summary>
    public struct CatalogHashMessage : NetworkMessage
    {
        public string serverCatalogHash;
    }
}