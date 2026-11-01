using UnityEngine;
using UnityEngine.SceneManagement;
using Mirror;
using VContainer;
using Cleverous.VaultSystem;
using Cleverous.VaultInventory;
using MySecondWorld.Customization;
using MySecondWorld.Inventory.Items;

namespace MySecondWorld.Network
{
    // 인벤토리 UI/상점/퀘스트 보상이 사용하는 서버 Command 모음.
    // NetworkWeaponEquip/NetworkCharacterAppearance와 마찬가지로
    // Inventory와 같은 GameObject(플레이어 프리팹)에 부착한다.
    // Phase가 늘어날 때마다 이 파일에 Command를 추가한다.
    [RequireComponent(typeof(Cleverous.VaultInventory.Inventory))]
    public class NetworkInventoryActions : NetworkBehaviour, IInventoryActionsProvider
    {
        #region 필드

        [SerializeField] private GameObject _floorItemPrefab; // 버리기 시 바닥에 스폰할 순수 프리팹 (스크립트는 FloorItemFactory가 스폰 시점에 부착)

        private LocalPlayerInventoryProvider _localInventoryProvider;
        private Cleverous.VaultInventory.Inventory _inventory;
        private IUseInventory _useInventory;

        #endregion

        #region 생명주기

        [Inject]
        public void Construct(
            LocalPlayerInventoryProvider localInventoryProvider)
        {
            // ProjectLifetimeScope가 초기화될 때 딱 한 번, LocalPlayerInventoryProvider가 DI 컨테이너에 "빈 상자"로 등록된다.
            // 로그인 후 Mirror가 캐릭터 프리팹을 Instantiate할 때마다 PlayerScope(자식)가 초기화되면서
            // 이 Construct가 호출되고, 그 "빈 상자"가 주입되어 들어온다.
            _localInventoryProvider = localInventoryProvider;
        }

        private void Awake()
        {
            _inventory = GetComponent<Cleverous.VaultInventory.Inventory>();
            _useInventory = GetComponent<IUseInventory>(); // NetworkCharacterAppearance가 같은 오브젝트에서 구현 중
        }

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            _localInventoryProvider.SetActions(this); // 로컬 플레이어일 때만 UI가 쓸 수 있게 "빈 상자"에 자기 자신을 채운다
        }

        #endregion

        #region 클라이언트 진입점 (IInventoryActionsProvider)

        // UI는 이 메서드들만 부르고, [Command] 호출은 여기서 감싼다.
        public void DiscardItem(int slotIndex) => CmdDiscardItem(slotIndex);
        public void TradeItem(int payItemDbKey, int receiveItemDbKey, int payInventorySlotIndex)
            => CmdTradeItem(payItemDbKey, receiveItemDbKey, payInventorySlotIndex);
        public void UseItem(int slotIndex) => CmdUseItem(slotIndex);

        #endregion

        #region 서버 Command

        // 아이템 삭제 + 바닥에 스폰. slotIndex는 버릴 인벤토리 칸 번호.
        [Command]
        private void CmdDiscardItem(int slotIndex)
        {
            if (_inventory == null) return;
            if (slotIndex < 0 || slotIndex >= _inventory.MaxSlots) return; // 조작된 인덱스 방어
            var stack = _inventory.Get(slotIndex);
            if (stack?.Source == null) return; // 빈 슬롯 방어
            if (_floorItemPrefab == null)
            {
                Debug.LogWarning("[NetworkInventoryActions] _floorItemPrefab이 연결되지 않아 버리기를 취소합니다.");
                return;
            }

            var item = stack.Source;
            int stackSize = stack.StackSize;

            // 인벤토리에서 빼기 전에 먼저 스폰 성공 여부부터 확인 — 실패 시 아이템이 증발하는 것 방지
            var floorItem = FloorItemFactory.Create(_floorItemPrefab, transform.position, Quaternion.identity);
            if (floorItem == null) return; // 프리팹 구성 오류 — FloorItemFactory가 이미 에러 로그를 남김
            floorItem.Initialize(item, stackSize);

            // 전량 버리기(간단 구현 — 부분 버리기는 범위 밖)
            _inventory.DoTake(slotIndex, stack.StackSize);

            // 버린 칸이 장착한 칸이면 손에서도 내린다 — 안 그러면 인벤토리엔 없는데 계속 들고 있게 됨.
            // 장착한 칸만 보므로, 같은 무기가 다른 칸에 있어도 그쪽으로 옮겨 가지 않고 해제된다.
            if (TryGetComponent(out NetworkWeaponEquip weaponEquip))
                weaponEquip.ServerValidateMainHand();

            // 런타임 스폰 오브젝트는 GameNetworkManager의 자동 matchId 세팅을 안 거치므로 직접 복사
            if (TryGetComponent(out NetworkMatch myMatch) && floorItem.TryGetComponent(out NetworkMatch floorMatch))
                floorMatch.matchId = myMatch.matchId;

            // Instantiate는 기본적으로 Active Scene에 생성하므로, 플레이어와 같은 씬(FieldScene 등)으로 옮겨줌
            SceneManager.MoveGameObjectToScene(floorItem.gameObject, gameObject.scene);
            NetworkServer.Spawn(floorItem.gameObject);
        }

        // 물물교환(구매·판매 공용). payInventorySlotIndex >= 0이면 그 칸에서 대가를 내고(판매), -1이면 종류만 본다(구매).
        [Command]
        private void CmdTradeItem(int payItemDbKey, int receiveItemDbKey, int payInventorySlotIndex)
        {
            if (_inventory == null) return;
            if (Vault.Get(payItemDbKey) is not Cleverous.VaultInventory.RootItem payItem) return;
            if (Vault.Get(receiveItemDbKey) is not Cleverous.VaultInventory.RootItem receiveItem) return;

            // 1) 대가 확인 — 판매는 지정한 칸에 그 아이템이 있어야 하고, 구매는 가방 어딘가에 있으면 된다
            bool hasPayItem
                = payInventorySlotIndex >= 0
                ? payInventorySlotIndex < _inventory.MaxSlots
                    && _inventory.Get(payInventorySlotIndex)?.Source == payItem // 판매: 칸 지정
                : _inventory.Contains(payItem, 1);                              // 구매: 어디든 있으면 됨
            if (!hasPayItem) return; // 미보유 → 거부

            // 2) 상품 지급 — 자리가 있을 때만. DoAdd(1)는 전부 들어가거나 전혀 안 들어가므로,
            //    먼저 넣어 보고 성공했을 때만 대가를 차감하면 "지불했는데 못 받는" 일이 없다.
            int leftover = _inventory.DoAdd(new RootItemStack(receiveItem, 1));
            if (leftover > 0) return; // 자리 없음 → 아무것도 바뀌지 않은 채 거부

            // 3) 대가 차감 — 칸을 지정했으면 그 칸에서, 아니면 Vault가 고른 칸에서
            if (payInventorySlotIndex >= 0)
                _inventory.DoTake(payInventorySlotIndex, 1);
            else
                _inventory.DoTake(payItem, 1);

            // 4) 장착 정리 — 판 칸이 장착한 칸이었으면 손에서도 내린다
            if (TryGetComponent(out NetworkWeaponEquip weaponEquip))
                weaponEquip.ServerValidateMainHand();
        }

        // 소모품 사용. slotIndex는 사용할 인벤토리 칸 번호.
        [Command]
        private void CmdUseItem(int slotIndex)
        {
            if (_inventory == null || _useInventory == null) return;
            if (slotIndex < 0 || slotIndex >= _inventory.MaxSlots) return; // 조작된 인덱스 방어
            var stack = _inventory.Get(slotIndex);
            if (stack?.Source is not ConsumableItem consumable) return;

            consumable.UseBegin(_useInventory);
        }

        #endregion
    }
}
