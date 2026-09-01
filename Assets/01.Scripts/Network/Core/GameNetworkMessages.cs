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
    //       └→ 클라이언트: 번들 다운로드(AddressableLabelLoader)
    //            └→ BundleReadyMessage 전송(다운로드 완료 ACK)
    //                 └→ 서버: spawn 허용

    /// <summary>
    /// 서버 → 클라이언트 : "이 라벨들 지금 다운로드해"
    /// </summary>
    public struct BundleRequiredMessage : NetworkMessage
    {
        public string[] labels;
    }

    /// <summary>
    /// 클라이언트 → 서버 : "라벨들 다운로드 완료, spawn 허용해도 됨"
    /// </summary>
    public struct BundleReadyMessage : NetworkMessage
    {
        public string[] labels;
    }

    /// <summary>
    /// 서버 → 클라이언트 : "내 catalog 버전이 이거야, 다르면 업데이트해"
    /// </summary>
    public struct CatalogHashMessage : NetworkMessage
    {
        public string serverCatalogHash;
    }


    // 던전 포탈 전환: 트리거 진입 → FadeOut 지시 → 씬스왑 → 로드완료 ack 대기 → 재스냅 → FadeIn 지시

    /// <summary>
    /// 서버 → 클라이언트 : 포탈 전환 화면 페이드 지시 (아웃/인)
    /// </summary>
    public struct ScreenFadeMessage : NetworkMessage
    {
        public bool fadeOut;   // true=페이드아웃(어두워짐), false=페이드인(밝아짐)
        public float duration;
    }

    /// <summary>
    /// 클라이언트 → 서버 : 포탈 전환 중 특정 씬 로드 완료 ack.
    /// 기존 SceneLoadedAckMessage(스폰 시 카운터 기반)와는 용도가 달라 별도 메시지로 분리.
    /// </summary>
    public struct PortalSceneLoadedAckMessage : NetworkMessage
    {
        public string sceneName;
    }
}