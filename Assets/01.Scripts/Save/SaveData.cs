using UnityEngine;
using MySecondWorld.Customization;

namespace MySecondWorld.Save
{
    [System.Serializable]
    public struct SaveData
    {
        public string[] lastSceneNames;
        public Vector3 lastPosition;

        // 컴패니언
        public string[] recruitedCompanionIds;
        public bool companionAlive;   // 마지막으로 기록된 생존 여부
        public bool companionPresent; // 마지막으로 기록된 소환/소환해제 여부

        // 인벤토리: null이면 인벤토리를 저장한 적 없는 옛 세이브
        public InventorySlotSave[] inventorySlots;

        // 서버 보관 외형. hasAppearance가 false면 서버에 외형 기록이 없는 계정
        public bool hasAppearance;
        public CharacterAppearanceData appearance;

        // 장착 무기 (아이템 dbKey)
        public int mainHandDbKey;

        // 장착한 인벤토리 칸 번호(저장 당시 배치 기준). 미장착은 -1
        // 이 필드가 없는 파일은 0으로 읽히므로,
        // 복원(RestoreInventory)은 그 칸에 그 무기가 없으면 같은 무기가 든 첫 칸으로 폴백한다.
        public int mainHandInventorySlotIndex;

        // 장착한 파츠별 인벤토리 칸 번호 (배열 순서는 PartType 순서, 미장착·모름은 -1)
        // 무기는 위의 mainHandInventorySlotIndex가 따로 맡는다
        // null이면 이 필드가 생기기 전의 옛 세이브
        public int[] equippedPartSlots;

        // 도감 해금: 한 번이라도 인벤토리에 들어온 적 있는 아이템 dbKey 목록
        // null이면 이 필드가 생기기 전의 옛 세이브 (복원 때 지금 인벤토리로 채워진다)
        public int[] unlockedItemDbKeys;

        // NPC 기억: null이면 저장된 NPC 기억이 없음 (한 번도 대화하지 않았거나 이 필드가 생기기 전 세이브)
        public NpcMemorySave[] npcMemories;
    }

    [System.Serializable]
    public struct InventorySlotSave
    {
        public int dbKey;   // 아이템 id. 빈 슬롯은 -1
        public int count;   // 이 슬롯의 스택 수
    }

    // NPC 대화 한 쌍 (나 + NPC)
    [System.Serializable]
    public struct NpcDialoguePairSave
    {
        public string pairId;     // 재전송 중복 제거용 고유값
        public string timestamp;
        public string userText;
        public string npcText;
    }

    /// NPC 한 명에 대한 기억 (플레이어 한 명 기준)
    [System.Serializable]
    public struct NpcMemorySave
    {
        public string npcKey;                    // NPC 고유 id (계정은 서버가 conn으로 앎)
        public NpcDialoguePairSave[] dialogue;   // 최대 10쌍, 오래된 것이 앞

        // 요약 필드
        public float relationScore;
        public int questStage;
        public bool questGiven;
        public string firstMetDate;
        public string firstMetLocation;
        public int conversationCount;
        public string[] knownFacts;
        public float factionRelation;   // NPC가 이 플레이어를 보는 호감도(-100~100). 서버 코드만 바꾼다 — 요약 업로드로 온 값은 서버가 복사하지 않고 무시
    }
}
