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
}
