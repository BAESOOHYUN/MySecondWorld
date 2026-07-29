using UnityEngine;
using Mirror;
using Cleverous.VaultInventory;

namespace MySecondWorld.Network
{
    // NetworkBehaviour(NetworkMatch)는 Instantiate 이후 런타임에 AddComponent로 못 붙인다 —
    // NetworkIdentity.Awake()가 그 시점에 존재하는 NetworkBehaviour만 한 번 스캔해서 netIdentity를 채워주기 때문
    // (Assets/Plugins/Mirror/Core/NetworkIdentity.cs:297 InitializeNetworkBehaviours 참고, 재스캔 API는 internal이라 접근 불가).
    // 그래서 RequireComponent는 "런타임 동적 부착"이 아니라 "프리팹 에디터 작업 시 빠뜨리지 않게 하는 안전장치"로만 기능한다.
    [RequireComponent(typeof(SphereCollider))]
    [RequireComponent(typeof(NetworkMatch))]
    public class NetworkFloorItem : NetworkBehaviour
    {
        [SerializeField] private RootItem _item;
        [SerializeField] private int _stackSize = 1;
        [SerializeField] private float _pickupDelay = 1f; // 스폰 직후 이 시간(초) 동안은 못 주움 — 버리자마자 다시 줍히는 것 방지

        // "이 바닥 아이템이 이미 누군가에게 지급됐는가"를 기억하는 스위치
        // _isPickedUp 플래그는 서버가 NetworkServer.Destroy 처리 전 네트워크 왕복 시간 동안
        // 중복 CmdPickup 호출이 들어와도 인벤토리에 '중복 지급되지 않도록' 막는 서버측 가드
        private bool _isPickedUp;
        private double _spawnServerTime;
        public override void OnStartServer()
        {
            base.OnStartServer();
            _spawnServerTime = NetworkTime.time; // 서버 기준 스폰 시각 기록(씬 배치든 런타임 Instantiate든 공통)
        }

        private void OnTriggerEnter(Collider other)
        {
            // identity.isLocalPlayer 체크로 '자기 자신의 플레이어가 닿았을 때'만 Command 호출
            if (!other.TryGetComponent(out NetworkIdentity identity) || !identity.isLocalPlayer) return;
            CmdPickup();
        }
        
        // 런타임 스폰(예: 버리기)으로 생성될 때 씬 배치와 달리 Inspector로 값을 못 넣으므로 코드로 채워줌
        public void Initialize(RootItem item, int stackSize)
        {
            _item = item;
            _stackSize = stackSize;
        }

        [Command(requiresAuthority = false)]
        private void CmdPickup(NetworkConnectionToClient sender = null)
        {
            // 아직 아무도 주워가지 않았다면 처리 시작
            if (_isPickedUp) return;
            if (_item == null) return;
            if (sender?.identity == null) return;
            if (NetworkTime.time - _spawnServerTime < _pickupDelay) return; // 방금 스폰됨 → 아직 못 주움

            // 호출자의 NetworkIdentity에서 Inventory 컴포넌트 찾기
            if (!sender.identity.TryGetComponent(out Cleverous.VaultInventory.Inventory inventory)) return;
            
            // leftover = 요청한 개수 중 인벤토리에 못 들어간 수량
            int leftover = inventory.DoAdd(new RootItemStack(_item, _stackSize));
            
            // 못 들어간 게 있으면 아이템을 그대로 남겨둔다 (재시도 가능)
            if (leftover > 0) return;

            // 모두 들어갔으면 지급 완료 → 필드 아이템 제거
            _isPickedUp = true;
            NetworkServer.Destroy(gameObject);
        }
    }
}