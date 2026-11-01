using UnityEngine;

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

        // 인벤토리
        public InventorySlotSave[] inventorySlots;   // null이면 인벤토리를 저장한 적 없는 옛 세이브

        // 장착 무기 (아이템 dbKey)
        public int mainHandDbKey;

        // 장착한 인벤토리 칸 번호(저장 당시 배치 기준). 미장착은 -1
        // 이 필드가 없는 파일은 0으로 읽히므로,
        // 복원(RestoreInventory)은 그 칸에 그 무기가 없으면 같은 무기가 든 첫 칸으로 폴백한다.
        public int mainHandInventorySlotIndex;
    }

    [System.Serializable]
    public struct InventorySlotSave
    {
        public int dbKey;   // 아이템 id. 빈 슬롯은 -1
        public int count;   // 이 슬롯의 스택 수
    }
}
