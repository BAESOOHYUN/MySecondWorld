using UnityEngine;
using VContainer;
using MessagePipe;
using Cleverous.VaultInventory;
using MySecondWorld.Inventory.Items;

namespace MySecondWorld.Customization
{
    /// <summary>
    /// 캐릭터 외형 장착, 좌우 대칭 파츠 페어링, 소유권 검증, 세이브 파일 저장 및 네트워크 동기화를 총괄하는 전담 서비스.
    /// InventoryUI와 CharacterCustomizeUI가 이 서비스를 공통으로 호출하여 중복과 불일치 버그를 방지합니다.
    /// </summary>
    public class CharacterAppearanceService
    {
        private readonly CharacterSaveService _saveService;
        private readonly AvatarPartKeyTableSO _avatarPartKeyTable;
        private readonly IPublisher<CharacterAppearanceSyncRequestedEvent> _syncRequestPub;
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

        /// <summary>
        /// 인벤토리에서 착용 가능한 파츠 아이템을 단일 장착합니다.
        /// 좌우 대칭 파츠(팔, 다리, 장갑, 어깨 등)는 MirroredPartPairs를 통해 자동으로 한 쌍이 함께 장착됩니다.
        /// </summary>
        public bool EquipWearableItem(EquipmentItem item)
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

            // 2. 🎽 좌우 대칭 파츠(팔, 다리, 장갑, 어깨 등)면 반대쪽도 세트로 함께 착용!
            if (MirroredPartPairs.TryGetMirror(part, out var mirrorPart))
            {
                data[mirrorPart] = index;
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
        public bool IsPartEquipped(EquipmentItem item)
        {
            if (item == null || _avatarPartKeyTable == null) return false;
            var data = _saveService.Load();
            if (_avatarPartKeyTable.TryFindPartIndex(item, data.Gender, out var part, out var index))
            {
                return data[part] == index;
            }
            return false;
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
            if (MirroredPartPairs.TryGetMirror(part, out var mirrorPart))
            {
                data[mirrorPart] = 0;
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
