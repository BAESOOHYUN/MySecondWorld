namespace MySecondWorld.Network
{
    // Mirror가 연결되고 이름 중복 검사까지 통과하면 "인증 성공/실패" 결과를 다른 시스템에 알리는 메시지 타입
    // UIManager가 이걸 받아서 게임 UI로 전환하거나 로비 UI로 복귀
    public readonly struct AuthStateChangedEvent
    {
        public readonly bool IsAuthenticated;
        public AuthStateChangedEvent(bool isAuthenticated) => IsAuthenticated = isAuthenticated;
    }
}
