namespace MySecondWorld.AI
{
    /// <summary>
    /// MessagePipe로 주고받는 이벤트 메시지 타입 모음.
    /// readonly struct = MessagePipe는 메시지를 값 타입으로 받는다. 힙 할당 없음 (GC 부담 없음).
    /// NpcGuid 필드로 복수 NPC가 자신의 메시지만 필터링한다.
    /// </summary>

    public readonly struct PlayerMessageSentEvent
    {
        public readonly string NpcGuid;
        public readonly string Text;
        public PlayerMessageSentEvent(string npcGuid, string text)
        {
            NpcGuid = npcGuid;
            Text = text;
        }
    }
    public readonly struct NPCResponseReceivedEvent
    {
        public readonly string NpcGuid;
        public readonly string NpcName;
        public readonly string Text;
        public NPCResponseReceivedEvent(string npcGuid, string npcName, string text)
        {
            NpcGuid = npcGuid;
            NpcName = npcName;
            Text = text;
        }
    }
    public readonly struct NPCThinkingStartedEvent
    {
        public readonly string NpcGuid;
        public NPCThinkingStartedEvent(string npcGuid)
        {
            NpcGuid = npcGuid;
        }
    }
}