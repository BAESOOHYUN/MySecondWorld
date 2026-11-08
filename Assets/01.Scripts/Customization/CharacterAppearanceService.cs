using UnityEngine;
using System.Collections.Generic;
using VContainer;
using MessagePipe;
using MySecondWorld.Inventory.Items;

namespace MySecondWorld.Customization
{
    using Inventory = Cleverous.VaultInventory.Inventory;

    // 캐릭터 외형 장착, 좌우 대칭 파츠 페어링, 소유권 검증,
    // 세이브 파일 저장 및 네트워크 동기화를 총괄하는 전담 서비스.
    // InventoryUI와 CharacterCustomizeUI가 이 서비스를 공통으로 호출하여 중복과 불일치 버그를 방지한다.
    // 구역 구성은 무기(NetworkWeaponEquip)와 짝을 맞췄다.
    // 무기(NetworkWeaponEquip)의 "서버: 장착 처리 · 검증"에 해당하는 구역이 여기의 "④ 입은 칸 검증"이다.
    //
    // [클라이언트 전용] MonoBehaviour·NetworkBehaviour가 아닌 일반 클래스(ProjectLifetimeScope 싱글톤)이고 서버에서는 호출되지 않는다.
    // - 서버로는 직접 보내지 않는다. CharacterAppearanceSyncRequestedEvent를 발행하면
    //   로컬 플레이어의 NetworkCharacterAppearance가 받아 Command(CmdSetAppearance, CmdSetEquippedPartSlots)로 보낸다.
    // - 여기 들고 있는 값은 클라의 기억이다. 서버가 확정한 진짜 값은 NetworkCharacterAppearance의 _serverAppearance, _equippedPartSlots다.
    //   _serverEquippedPartSlots는 서버가 TargetRpc로 알려 준 값을 후보로 받아 둔 사본일 뿐이고, 서버가 요청을 다시 검증한다.
    public class CharacterAppearanceService
    {
        #region 상태

        // 입은 파츠 기록 : 파츠 종류 → 입은 인벤토리 칸 번호 (입지 않았으면 키 없음)
        // 무기(NetworkWeaponEquip)의 _mainHandInventorySlotIndex에 해당.
        // 서버가 아니라 클라 메모리에만 있어서 앱을 다시 시작하면 비는 점이 다르다.
        private readonly Dictionary<PartType, int> _equippedInventorySlotIndexByPart = new();
        // 직전 외형 기억
        private readonly Dictionary<(PartType part, string subtype), int> _lastSelectedIndexByPartAndSubtype = new();

        // 서버가 접속 때 알려 준 칸 번호 "후보" (배열 순서는 PartType 순서, 없음·모름은 -1)
        // null이면 서버가 알려 준 값이 없다는 뜻 (옛 세이브 등) → 복원은 첫 칸 폴백으로 간다
        // 인벤토리가 아직 다 도착하지 않았을 수 있어서,
        // 인벤토리를 열 때 그 칸에 실제로 그 아이템이 있는지 확인한 뒤에 위 딕셔너리로 옮긴다.
        private int[] _serverEquippedPartSlots;

        #endregion

        #region 공개 프로퍼티 · 이벤트

        public event System.Action OnAppearanceChanged;

        // 이 파츠를 입은 인벤토리 칸 번호.
        // 입지 않았으면 -1.
        // (무기(NetworkWeaponEquip)의 CurrentMainHandInventorySlotIndex에 해당)
        public int GetEquippedInventorySlotIndex(PartType part)
            => _equippedInventorySlotIndexByPart.TryGetValue(part, out var slot) ? slot : -1;

        #endregion

        #region DI

        // 주입받는 참조 — 생성자에서만 채워진다
        private readonly LocalAppearanceCache _appearanceCache;
        private readonly AvatarPartKeyTableSO _avatarPartKeyTable;
        private readonly IPublisher<CharacterAppearanceSyncRequestedEvent> _syncRequestPub;

        [Inject]
        public CharacterAppearanceService(
            LocalAppearanceCache appearanceCache,
            AvatarPartKeyTableSO avatarPartKeyTable,
            IPublisher<CharacterAppearanceSyncRequestedEvent> syncRequestPub)
        {
            _appearanceCache = appearanceCache;
            _avatarPartKeyTable = avatarPartKeyTable;
            _syncRequestPub = syncRequestPub;
        }

        #endregion

        // ═══════════════════════════════════════════════════════════════════════
        // 동작 흐름: ① 인벤토리를 열면 입은 칸 기록을 복원한다 → ② 목록을 그릴 때 칸마다 "입은 칸인가"를 묻는다
        // → ③ 장착·해제 버튼을 누르면 외형과 기록을 바꾼다 → ④ 인벤토리가 바뀌면 입은 칸이 비었는지 검증한다
        // ═══════════════════════════════════════════════════════════════════════

        #region ① 입은 칸 기록 복원 (인벤토리를 열 때)

        // 서버가 접속 때 알려 준 칸 번호를 받아 둔다. 예전 기록은 모두 지우고 서버 값으로 다시 시작한다.
        // (로그아웃했다가 다른 계정으로 들어와도 앞 계정의 칸 번호가 남지 않게 하려는 것)
        public void SetServerEquippedPartSlots(int[] slots)
        {
            _serverEquippedPartSlots = slots;
            _equippedInventorySlotIndexByPart.Clear();
        }

        // 서버가 칸 번호를 알려 줬을 때(접속 직후, 인벤토리 정리 뒤) 받는 곳.
        // 후보를 받아 두고, 인벤토리가 열려 있든 아니든 바로 기록을 채운 뒤 화면을 갱신한다.
        public void ApplyServerEquippedPartSlots(int[] slots, Inventory inventory)
        {
            SetServerEquippedPartSlots(slots);
            RestoreEquippedInventorySlots(inventory);
            OnAppearanceChanged?.Invoke();
        }

        // 입고 있는 옷이 가방 몇 번 칸 것인지 딕셔너리에 적는다. 인벤토리를 열 때 부른다.
        // 서버가 알려 준 칸이 맞으면 그 칸을, 아니면 가방을 0번부터 훑어 처음 만난 같은 옷의 칸을 적는다.
        // 무기(NetworkWeaponEquip)의 FindInventorySlotOf(칸을 모를 때 첫 칸을 찾는 부분)에 해당
        public void RestoreEquippedInventorySlots(Inventory inventory)
        {
            if (inventory == null) return;

            foreach (PartType part in System.Enum.GetValues(typeof(PartType)))      // 파츠 25개를 하나씩
            {
                if (_equippedInventorySlotIndexByPart.ContainsKey(part)) continue;  // 이미 적혀 있으면 다음 파츠로

                var item = GetRequiredItemForCurrentLook(part);                     // 지금 이 파츠에 입은 옷의 아이템 (맨몸·무료 파츠면 null)
                if (item == null) continue;                                         // 찾을 게 없으니 다음 파츠로

                int serverSlot = GetServerEquippedPartSlot(part);                   // 서버가 알려 준 칸 (없으면 -1)

                if (serverSlot >= 0 && serverSlot < inventory.MaxSlots 
                    && inventory.Get(serverSlot)?.Source == item)                   // 서버가 알려 준 칸 하나만 열어서, 그 옷이 정말 있는지 확인한다
                {
                    _equippedInventorySlotIndexByPart[part] = serverSlot;           // 있으면 딕셔너리에 적는다
                    continue;                                                       // 이 파츠는 끝, 다음 파츠로
                }

                // 서버 칸이 없거나 틀렸으면, 가방을 0번부터 한 칸씩 열어 '첫번째로 만나는 같은 옷'을 찾는다
                for (int i = 0; i < inventory.MaxSlots; i++)
                {
                    // 빈 칸이거나 다른 옷이면 다음 칸으로
                    if (inventory.Get(i)?.Source != item) continue;

                    _equippedInventorySlotIndexByPart[part] = i;
                    // 찾았으니 칸 훑기는 끝 (같은 옷이 여러 칸이면 첫 칸만 선택)
                    break;
                }
            }
        }

        #endregion

        #region ② 입은 칸 판정 (목록을 그릴 때)

        // 이 칸(inventorySlotIndex)이 입은 칸인가.
        // 종류(변형 번호)와 칸 번호가 모두 같아야 true.
        // 무기(NetworkWeaponEquip)의 "CurrentMainHandInventorySlotIndex == 칸" 비교에 해당.
        public bool IsPartEquipped(EquipmentItem item, int inventorySlotIndex)
        {
            if (item == null || _avatarPartKeyTable == null) return false;
            var data = _appearanceCache.Get();

            // "아이템 → 파츠 종류" 조회
            if (_avatarPartKeyTable.TryFindPartIndex(item, data.Gender, out var part, out var index))
            {
                return data[part] == index
                       && GetEquippedInventorySlotIndex(part) == inventorySlotIndex;
            }
            return false;
        }

        #endregion

        #region ③ 장착 · 해제 (버튼을 눌렀을 때)

        // 인벤토리에서 착용 가능한 파츠 아이템을 단일 장착한다.
        // (무기(NetworkWeaponEquip)의 EquipMainHand에 해당)
        // 좌우 대칭 파츠(팔, 다리, 장갑, 어깨 등)는 MirroredPartPairs를 통해 자동으로 한 쌍이 함께 장착된다.
        public bool EquipWearableItem(EquipmentItem item, int inventorySlotIndex)
        {
            if (item == null) return false;

            var data = _appearanceCache.Get();
            if (_avatarPartKeyTable == null || !_avatarPartKeyTable.TryFindPartIndex(item, data.Gender, out var part, out var index))
            {
                Debug.LogWarning($"[CharacterAppearanceService] {item.name}에 대응하는 파츠 인덱스를 찾지 못했습니다. (현재 캐릭터 성별: {data.Gender})");
                return false;
            }

            // 투구 장식은 Head가 No_Elements(투구 타입)일 때만 장착할 수 있다
            if (part == PartType.HelmetAttachment && !IsHeadNoElements(data))
            {
                Debug.LogWarning("[CharacterAppearanceService] 투구 장식은 Head No_Elements 장착 시에만 입을 수 있습니다.");
                return false;
            }
            
            // 폴백을 위한 준비단계
            var before = data;                                         // 규칙 적용 전 값 (struct 복사)
            if (data.Head >= 0)
                RecordLastSelected(PartType.Head, data.Head, data.Gender); // 입기 전 현재 얼굴을 기억
            
            // 1. 해당 파츠 장착
            data[part] = index;                                             // 이 옷이 어떤 옷인가
            _equippedInventorySlotIndexByPart[part] = inventorySlotIndex;   // 그 옷이 가방 어디에 있는가

            // 2. 좌우 대칭 파츠(팔, 다리, 장갑,
            // 어깨 등)면 반대쪽도 세트로 함께 착용
            if (MirroredPartPairs.TryGetMirror(part, out var mirrorPart))
            {
                data[mirrorPart] = index;
                _equippedInventorySlotIndexByPart[mirrorPart] = inventorySlotIndex;
            }

            // 3. 상호배제 규칙 적용 - 커스터마이징과 같은 함수
            RecordLastSelected(part, index, data.Gender);
            MutualExclusionResolver.ApplyDirectSelectionRules(
                ref data, part, index, data.Gender, 
                _avatarPartKeyTable, _lastSelectedIndexByPartAndSubtype);
            RemoveChangedSlots(before, data, part);

            // 로컬 세이브 저장 & 네트워크 동기화 브로드캐스트
            _appearanceCache.Set(data);
            _syncRequestPub.Publish(new CharacterAppearanceSyncRequestedEvent(data, BuildEquippedPartSlots()));
            OnAppearanceChanged?.Invoke();

            Debug.Log($"[CharacterAppearanceService] 파츠 장착 완료: {item.Title} (Part={part}, Index={index})");
            return true;
        }

        // 특정 파츠 슬롯의 장착을 해제하고 기본 상태(0)로 되돌린다.
        // 좌우 대칭 파츠도 함께 해제된다.
        // (무기(NetworkWeaponEquip)의 UnequipMainHand에 해당)
        public bool UnequipWearablePart(PartType part)
        {
            var data = _appearanceCache.Get();
            data[part] = 0;
            _equippedInventorySlotIndexByPart.Remove(part);

            // 좌우 대칭 파츠면 함께 해제
            if (MirroredPartPairs.TryGetMirror(part, out var mirrorPart))
            {
                data[mirrorPart] = 0;
                _equippedInventorySlotIndexByPart.Remove(mirrorPart);
            }
            _appearanceCache.Set(data);
            _syncRequestPub.Publish(new CharacterAppearanceSyncRequestedEvent(data, BuildEquippedPartSlots()));
            OnAppearanceChanged?.Invoke();
            Debug.Log($"[CharacterAppearanceService] 파츠 해제 완료: Part={part}");
            return true;
        }

        // 장착 중인 아이템을 찾아 해제한다.
        // (아이템 → 파츠 종류를 찾은 뒤 UnequipWearablePart에 맡긴다)
        public bool UnequipWearableItem(EquipmentItem item)
        {
            if (item == null || _avatarPartKeyTable == null) return false;
            var data = _appearanceCache.Get();
            if (_avatarPartKeyTable.TryFindPartIndex(item, data.Gender, out var part, out _))
            {
                return UnequipWearablePart(part);
            }
            return false;
        }

        // 커스터마이징 UI 등에서 변경된 전체 외형 데이터를 저장 및 네트워크 동기화한다.
        public void SaveAndSyncAppearance(CharacterAppearanceData data)
        {
            _appearanceCache.Set(data);
            _syncRequestPub.Publish(new CharacterAppearanceSyncRequestedEvent(data, BuildEquippedPartSlots()));
            OnAppearanceChanged?.Invoke();
            Debug.Log($"[CharacterAppearanceService] 전체 외형 저장 및 동기화 요청 완료 (Gender={data.Gender}, Head={data.Head}, Torso={data.Torso})");
        }

        #endregion

        #region 헬퍼 메서드 (조회)

        // 서버가 알려준 이 파츠의 칸 번호. 없거나 모르면 -1
        private int GetServerEquippedPartSlot(PartType part)
            => _serverEquippedPartSlots != null && (int)part < _serverEquippedPartSlots.Length
            ? _serverEquippedPartSlots[(int)part] : -1;

        // 커스터마이징 저장 뒤 호출: 외형이 바뀌어 더는 안 맞는 입은 칸 기록을 지우고,
        // 지워진 파츠는 가방에서 새 외형의 옷을 찾아 다시 적은 뒤 서버로 보낼 배열을 만든다
        // (Reconcile은 "서로 어긋난 두 값을 맞춰서 일치시킨다"는 뜻)
        public int[] ReconcileEquippedSlots(Inventory inventory)
        {
            if (inventory == null) return BuildEquippedPartSlots();
            foreach (var part in new List<PartType>(_equippedInventorySlotIndexByPart.Keys))
            {
                var required = GetRequiredItemForCurrentLook(part); // 새 외형을 요구하는 아이템
                int slot = _equippedInventorySlotIndexByPart[part];
                if (required == null || inventory.Get(slot)?.Source != required)
                    _equippedInventorySlotIndexByPart.Remove(part); // 그 칸의 옷이 더는 이 외형이 아니다
            }
            RestoreEquippedInventorySlots(inventory);               // 지워진 파츠만 첫 칸을 찾아 채운다 (이미 적힌 파츠는 건너뜀)
            return BuildEquippedPartSlots();
        }

        // 클라: 옷을 입고 벗을 때 칸 번호를 서버로 보낸다  
        // 입은 파츠 기록을 서버로 보낼 배열로 바꾼다. 배열 순서는 PartType 순서이고, 기록이 없는 파츠는 -1
        private int[] BuildEquippedPartSlots()
        {
            var slots = new int[System.Enum.GetValues(typeof(PartType)).Length];
            for (int i = 0; i < slots.Length; i++)
                slots[i] = -1;
            foreach (var kv in _equippedInventorySlotIndexByPart)
                slots[(int)kv.Key] = kv.Value;

            return slots;
        }

        // 현재 세이브된 외형 데이터를 반환한다.
        public CharacterAppearanceData GetCurrentAppearance()
        {
            return _appearanceCache.Get();
        }

        // "이 파츠 자리에 지금 입은 외형이 있으면, 그 외형에 대응하는 아이템이 무엇인가?"를 돌려줌
        // 미착용(0)이면 null.
        public EquipmentItem GetRequiredItemForCurrentLook(PartType part)
        {
            if (_avatarPartKeyTable == null) return null;
            var data = _appearanceCache.Get(); // 클라가 기억하는 지금 이 캐릭터가 입고 있는 외형을 뜻함. (옛날에 저장해 둔 파일을 뜻하는게 아님)
            int index = data[part];
            if (index <= 0) return null; // 0은 맨몸(기본 파츠), -1은 안 입음
            return _avatarPartKeyTable.GetRequiredItem(part, data.Gender, index);
        }

        // 직전 값 저장용 기록 헬퍼.
        private void RecordLastSelected(PartType part, int index, Gender gender)
        {
            var subtype = _avatarPartKeyTable.GetSubType(part, gender, index);
            _lastSelectedIndexByPartAndSubtype[(part, subtype)] = index;
        }

        // 칸 기록 정리 헬퍼
        // 규칙이 투구나 Head를 바꾸면 그 파츠의 "입은 칸" 기록도 지워야 UI와 서버 검증이 어긋나지 않는다.
        // 파츠별 if를 쓰지 않고 "전후로 값이 바뀐 파츠"를 일괄 처리하면 앞으로 규칙이 늘어나도 손댈 곳이 없다.
        private void RemoveChangedSlots(CharacterAppearanceData before, CharacterAppearanceData after, PartType equipped)
        {
            bool hasMirror = MirroredPartPairs.TryGetMirror(equipped, out var mirror);
            foreach (PartType part in System.Enum.GetValues(typeof(PartType)))
            {
                if (part == equipped || (hasMirror && part == mirror)) continue; // 지금 입은 파츠(와 짝)는 제외
                if (before[part] != after[part])
                    _equippedInventorySlotIndexByPart.Remove(part);
            }
        }

        // Head 중 헬멧을 착용한 파츠. (NoElements는 눈코입,머리,수염 등이 없음을 의미)
        private bool IsHeadNoElements(CharacterAppearanceData data)
            => _avatarPartKeyTable.GetSubType(PartType.Head, data.Gender, data.Head) == "No_Elements";

        // 이 아이템이 어느 파츠(PartType)에 속하는지 알려 준다 (장착 여부와 무관)
        public bool TryGetPartType(EquipmentItem item, out PartType part)
        {
            part = default;
            if (item == null || _avatarPartKeyTable == null) return false;
            var data = _appearanceCache.Get();

            // "아이템 → 파츠 종류" 조회
            return _avatarPartKeyTable.TryFindPartIndex(item, data.Gender, out part, out _);
        }

        // 해당 아이템이 현재 캐릭터의 성별에 적합한지 확인한다.
        public bool IsItemMatchingGender(EquipmentItem item)
        {
            if (item == null || _avatarPartKeyTable == null) return true;
            var data = _appearanceCache.Get();
            return _avatarPartKeyTable.IsItemMatchingGender(item, data.Gender);
        }

        #endregion
    }
}
