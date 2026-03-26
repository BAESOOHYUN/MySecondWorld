using Mirror;

namespace MySecondWorld.Network.OpenWorld
{
    /// <summary>서버 → 클라이언트: 씬 N개 로드 완료 후 ACK 보내달라는 신호</summary>
    public struct SpawnReadyMessage : NetworkMessage
    {
        public int sceneCount;
    }

    /// <summary>클라이언트 → 서버: 씬 전부 로드 완료, 이제 스폰해도 됨</summary>
    public struct SceneLoadedMessage : NetworkMessage { }
}
