using Mirror;
using MySecondWorld.Save;

namespace MySecondWorld.Network
{
    // GameNetworkMessages.cs
    // 서버↔클라이언트가 주고받는 편지 양식

    #region 씬 로드 / 스폰
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

    /// <summary>
    /// 서버 → 클라이언트 : 이 플레이어의 소속 씬이 확정됐다.
    ///
    /// 서브씬 사이를 걸어서 이동할 때 클라의 ActiveScene을 따라오게 하는 유일한 경로다.
    /// 이 통보가 없으면 ActiveScene이 최초로 로드된 씬에 잠긴 채 남고,
    /// Mirror가 스폰하는 모든 오브젝트가 엉뚱한 씬에 들어간 뒤 그 씬과 함께 파괴된다.
    /// </summary>
    public struct PlayerSceneChangedMessage : NetworkMessage
    {
        public string sceneName;
    }

    #endregion

    #region 번들 / 카탈로그

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

    #endregion

    #region 던전 포탈 전환

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

    #endregion

    #region NPC 기억

    /// <summary>클라이언트 → 서버: 이 NPC와의 대화 기록 줘</summary>
    public struct NpcMemoryRequestMessage : NetworkMessage
    {
        public string npcKey;
    }

    /// <summary>서버 → 클라이언트: 이 NPC의 기억 (요약 + 대화 기록)</summary>
    public struct NpcMemoryResponseMessage : NetworkMessage
    {
        public NpcMemorySave memory;
    }

    /// <summary>클라이언트 → 서버: 새 대화 한 쌍 추가</summary>
    public struct NpcMemoryAppendMessage : NetworkMessage
    {
        public string npcKey;
        public NpcDialoguePairSave pair;
    }

    /// <summary>서버 → 클라이언트: 이 pairId 받았음</summary>
    public struct NpcMemoryAckMessage : NetworkMessage
    {
        public string pairId;
    }

    /// <summary>
    /// 클라이언트 → 서버: 요약 업로드.
    /// NpcMemorySave는 대화 기록(dialogue)과 요약 필드(relationScore 등)를 함께 들고 있는 구조체라
    /// 이 메시지에 dialogue 칸도 있지만, 클라는 보내기 전에 memory.dialogue = null로 비워서 보내고 
    /// 서버(OnNpcMemorySummary)도 dialogue를 읽지 않는다. dialogue 무시하고 요약 필드만 반영한다.
    /// 대화 기록은 Append로만 쌓는다 — 옛 복사본이 서버의 최신 기록을 덮지 않게
    /// </summary>
    public struct NpcMemorySummaryMessage : NetworkMessage
    {
        public NpcMemorySave memory;
        public int seq;   // 클라가 매기는 번호 — Ack와 짝을 맞춘다
    }

    /// <summary>서버 → 클라이언트: 이 NPC의 seq번 요약을 받았음</summary>
    public struct NpcMemorySummaryAckMessage : NetworkMessage
    {
        public string npcKey;
        public int seq;
    }
    #endregion
}