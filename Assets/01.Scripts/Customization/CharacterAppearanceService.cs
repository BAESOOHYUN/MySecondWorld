using UnityEngine;
using System.Collections.Generic;
using VContainer;
using MessagePipe;
using Cleverous.VaultInventory;
using MySecondWorld.Inventory.Items;

namespace MySecondWorld.Customization
{
    using Inventory = Cleverous.VaultInventory.Inventory;

    /// <summary>
    /// 캐릭터 외형 장착, 좌우 대칭 파츠 페어링, 소유권 검증, 세이브 파일 저장 및 네트워크 동기화를 총괄하는 전담 서비스.
    /// InventoryUI와 CharacterCustomizeUI가 이 서비스를 공통으로 호출하여 중복과 불일치 버그를 방지합니다.
    /// </summary>
    public class CharacterAppearanceService
    {
        private readonly CharacterSaveService _saveService;
        private readonly AvatarPartKeyTableSO _avatarPartKeyTable;
        private readonly IPublisher<CharacterAppearanceSyncRequestedEvent> _syncRequestPub;
        //입은 파츠 기록 : 파츠 종류 → 입은 인벤토리 칸 번호 (입지 않았으면 키 없음)
        private readonly Dictionary<PartType, int> _equippedInventorySlotIndexByPart = new();
        public event System.Action OnAppearanceChanged;

        [Inject]
        public CharacterAppearanceService(
            CharacterSaveService saveService,
            AvatarPartKeyTableSO avatarPartKeyTable,
            IPublisher<CharacterAppearanceSyncRequestedEvent> syncRequestPub)
        {
            _saveService = saveService;
            _avatarPartKeyTable = avatarPartKeyTable;
            _syncRequestPub = syncRequestPub;
        }

        /// <summary>
        /// 현재 세이브된 외형 데이터를 반환합니다.
        /// </summary>
        public CharacterAppearanceData GetCurrentAppearance()
        {
            return _saveService.Load();
        }

        public int GetEquippedInventorySlotIndex(PartType part)
            => _equippedInventorySlotIndexByPart.TryGetValue(part, out var slot) ? slot : -1;
        /// <summary>
        /// 인벤토리에서 착용 가능한 파츠 아이템을 단일 장착합니다.
        /// 좌우 대칭 파츠(팔, 다리, 장갑, 어깨 등)는 MirroredPartPairs를 통해 자동으로 한 쌍이 함께 장착됩니다.
        /// </summary>
        public bool EquipWearableItem(EquipmentItem item, int inventorySlotIndex)
        {
            if (item == null) return false;

            var data = _saveService.Load();
            if (_avatarPartKeyTable == null || !_avatarPartKeyTable.TryFindPartIndex(item, data.Gender, out var part, out var index))
            {
                Debug.LogWarning($"[CharacterAppearanceService] {item.name}에 대응하는 파츠 인덱스를 찾지 못했습니다. (현재 캐릭터 성별: {data.Gender})");
                return false;
            }

            // 1. 해당 파츠 장착
            data[part] = index;
            _equippedInventorySlotIndexByPart[part] = inventorySlotIndex;

            // 2. 좌우 대칭 파츠(팔, 다리, 장갑, 어깨 등)면 반대쪽도 세트로 함께 착용
            if (MirroredPartPairs.TryGetMirror(part, out var mirrorPart))
            {
                data[mirrorPart] = index;
                _equippedInventorySlotIndexByPart[mirrorPart] = inventorySlotIndex;
            }

            // 3. 로컬 세이브 저장 & 네트워크 동기화 브로드캐스트
            _saveService.Save(data);
            _syncRequestPub.Publish(new CharacterAppearanceSyncRequestedEvent(data));
            OnAppearanceChanged?.Invoke();

            Debug.Log($"[CharacterAppearanceService] 파츠 장착 완료: {item.Title} (Part={part}, Index={index})");
            return true;
        }

        /// <summary>
        /// 특정 아이템이 현재 캐릭터에 장착되어 있는지 여부를 확인합니다.
        /// </summary>
        public bool IsPartEquipped(EquipmentItem item, int inventorySlotIndex)
        {
            if (item == null || _avatarPartKeyTable == null) return false;
            var data = _saveService.Load();

            // "아이템 → 파츠 종류" 조회
            if (_avatarPartKeyTable.TryFindPartIndex(item, data.Gender, out var part, out var index))
            {
                return data[part] == index
                                    && GetEquippedInventorySlotIndex(part) == inventorySlotIndex;
            }
            return false;
        }


        /// <summary>
        /// 이 아이템이 어느 파츠(PartType)에 속하는지 알려 준다 (장착 여부와 무관)
        /// </summary>
        public bool TryGetPartType(EquipmentItem item, out PartType part)
        {
            part = default;
            if (item == null || _avatarPartKeyTable == null) return false;
            var data = _saveService.Load();

            // "아이템 → 파츠 종류" 조회
            return _avatarPartKeyTable.TryFindPartIndex(item, data.Gender, out part, out _);
        }

        /// <summary>
        /// 해당 아이템이 현재 캐릭터의 성별에 적합한지 확인합니다.
        /// </summary>
        public bool IsItemMatchingGender(EquipmentItem item)
        {
            if (item == null || _avatarPartKeyTable == null) return true;
            var data = _saveService.Load();
            return _avatarPartKeyTable.IsItemMatchingGender(item, data.Gender);
        }

        /// <summary>
        /// 특정 파츠 슬롯의 장착을 해제하고 기본 상태(0)로 되돌립니다.
        /// 좌우 대칭 파츠도 함께 해제됩니다.
        /// </summary>
        public bool UnequipWearablePart(PartType part)
        {
            var data = _saveService.Load();
            data[part] = 0;
            _equippedInventorySlotIndexByPart.Remove(part);

            // 좌우 대칭 파츠면 함께 해제
            if (MirroredPartPairs.TryGetMirror(part, out var mirrorPart))
            {
                data[mirrorPart] = 0;
                _equippedInventorySlotIndexByPart.Remove(mirrorPart);
            }
            _saveService.Save(data);
            _syncRequestPub.Publish(new CharacterAppearanceSyncRequestedEvent(data));
            OnAppearanceChanged?.Invoke();
            Debug.Log($"[CharacterAppearanceService] 파츠 해제 완료: Part={part}");
            return true;
        }

        /// <summary>
        /// 장착 중인 아이템을 찾아 해제합니다.
        /// </summary>
        public bool UnequipWearableItem(EquipmentItem item)
        {
            if (item == null || _avatarPartKeyTable == null) return false;
            var data = _saveService.Load();
            if (_avatarPartKeyTable.TryFindPartIndex(item, data.Gender, out var part, out _))
            {
                return UnequipWearablePart(part);
            }
            return false;
        }

        /// <summary>
        /// 입은 파츠가 버리기·판매 등의 행위로 그 인벤토리 슬롯이 비게 되면, 그 파츠를 벗겨 기본 파츠(맨몸)로 되돌린다
        /// 기본 파츠(맨몸)"에서 맨몸은 파츠 값 0(data[part] = 0)
        /// = 무기의 ServerValidateMainHand에 해당
        /// </summary>
        public void ReleasePartsWithEmptiedSlot(Inventory inventory)
        {
            if (inventory == null) return;

            List<PartType> emptied = null;
            // 입은 파츠 기록을 훑어본다.
            foreach (var kv in _equippedInventorySlotIndexByPart)
            {
                bool hasItem = kv.Value >= 0 && kv.Value < inventory.MaxSlots
                    && inventory.Get(kv.Value)?.Source != null;
                if (hasItem) continue;
                (emptied ??= new List<PartType>()).Add(kv.Key);
            }
            if (emptied == null) return;
            foreach (var part in emptied)
            {
                if (_equippedInventorySlotIndexByPart.ContainsKey(part)) // 좌우 쌍은 앞에서 같이 벗겨졌을 수 있다
                    UnequipWearablePart(part);
            }
        }

        /// <summary>
        /// 재접속 등으로 기록이 비었을 때, 입은 파츠에 해당하는 아이템이 든 '첫 칸'을 칸 번호로 채운다
        /// </summary>
        /// TODO : 서버에 파츠 칸 번호 저장으로 변경 복원을 칸 위치 보존으로
        public void RestoreEquippedInventorySlots(Inventory inventory)
        {
            if (inventory == null) return;
            foreach (PartType part in System.Enum.GetValues(typeof(PartType)))
            {
                if (_equippedInventorySlotIndexByPart.ContainsKey((PartType)part)) continue; // 이미 기억중이면 건드리지 않는다.
                var item = GetEquippedItem(part); // 입은 값 → 그 값에 필요한 아이템. 미착용(0)이면 null
                if (item == null) continue;
                for (int i = 0; i < inventory.MaxSlots; i++)
                {
                    // i번 칸의 아이템이 우리가 찾는 item이 아니면 이 칸은 건너뛰고 다음 칸으로
                    // 빈 칸이면 Get(i)가 null이라 ?. 때문에 결과도 null
                    if (inventory.Get(i)?.Source != item) continue;
                    // 같으면(찾았다) 이 파츠는 i번 칸의 것이라고 딕셔너리에 기록
                    _equippedInventorySlotIndexByPart[part] = i;
                    break; // 찾았으니 반복을 끝낸다. 뒤쪽 칸은 보지 않는다. 같은 아이템이 여러 칸이면 첫칸만 봄
                }
            }
        }

        /// <summary>
        /// 특정 파츠 슬롯에 현재 착용 중인 장비 아이템을 반환합니다. 미착용(0)이면 null.
        /// </summary>
        public EquipmentItem GetEquippedItem(PartType part)
        {
            if (_avatarPartKeyTable == null) return null;
            var data = _saveService.Load();
            int index = data[part];
            if (index <= 0) return null;
            return _avatarPartKeyTable.GetRequiredItem(part, data.Gender, index);
        }

        /// <summary>
        /// 커스터마이징 UI 등에서 변경된 전체 외형 데이터를 저장 및 네트워크 동기화합니다.
        /// </summary>
        public void SaveAndSyncAppearance(CharacterAppearanceData data)
        {
            _saveService.Save(data);
            _syncRequestPub.Publish(new CharacterAppearanceSyncRequestedEvent(data));
            OnAppearanceChanged?.Invoke();
            Debug.Log($"[CharacterAppearanceService] 전체 외형 저장 및 동기화 요청 완료 (Gender={data.Gender}, Head={data.Head}, Torso={data.Torso})");
        }

        /// <summary>
        /// 인벤토리 소유권을 검증하여 미보유 파츠는 이전 외형으로 롤백합니다.
        /// </summary>
        public CharacterAppearanceData ValidateOwnership(
            CharacterAppearanceData requested, 
            CharacterAppearanceData fallback, 
            Cleverous.VaultInventory.Inventory inventory)
        {
            if (_avatarPartKeyTable == null || inventory == null) return requested;

            foreach (PartType part in System.Enum.GetValues(typeof(PartType)))
            {
                int index = requested[part];
                if (index <= 0) continue; // 기본 파츠(0)나 미착용(-1)은 항상 허용

                var requiredItem = _avatarPartKeyTable.GetRequiredItem(part, requested.Gender, index);
                if (requiredItem != null && !inventory.Contains(requiredItem, 1))
                {
                    requested[part] = fallback[part]; // 미소유 → 롤백
                }
            }

            return requested;
        }
    }
}
