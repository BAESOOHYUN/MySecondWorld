using MySecondWorld.Inventory;
using MySecondWorld.Network;
using UnityEngine;

namespace MySecondWorld.Combat.Player
{
    // 손에 든 화살의 유일한 관리자.
    //
    // 이 컴포넌트는 모든 피어의 플레이어 사본에서 동작한다(로컬/원격 구분 없음) —
    // 남의 캐릭터도 내 화면에서 손에 화살을 들고 있어야 하기 때문.
    // 장비 시각화가 SyncVar로 각 클라에서 각자 재현되는 것과 같은 구조다.
    public class ArrowNockPoint : MonoBehaviour, INockedProjectileSource
    {
        [Header("참조")]
        [Tooltip("활 장착/해제 신호를 받는다. 같은 오브젝트의 NetworkWeaponEquip을 넣으면 된다.")]
        [SerializeField] private NetworkWeaponEquip _weaponEquip;

        [Tooltip("화살 프리팹의 소유자. 스탯 SO가 아니라 릴레이가 들고 있는 이유는 " +
                 "클라이언트 사본이 스탯 SO를 거치지 않고 프리팹 authoring된 것만 갖기 때문이다.")]
        [SerializeField] private NetworkProjectileRelay _relay;

        [Header("손에 쥐는 자세")]
        [Tooltip("시위를 당기는 손 본. 화살 피벗(깃)이 이 본의 원점에 정확히 놓인다. " +
                 "활을 쥐는 왼손이 아니라 화살을 잡아당기는 오른손이다.")]
        [SerializeField] private Transform _handSocket;

        [Tooltip("손 본 원점 기준 위치 보정. 본의 원점과 실제로 손가락이 쥐는 지점이 다를 때 쓴다.")]
        [SerializeField] private Vector3 _nockPositionOffset;

        [Tooltip("손에 쥔 화살의 각도 보정. 비행 방향과는 무관하다 — " +
                 "발사 순간 LookRotation이 회전을 통째로 덮어쓰므로 이 값은 노킹 자세에만 영향을 준다.\n" +
                 "※ Nock() 시점에 적용되므로, 플레이 중 값을 바꾸면 활을 벗었다 다시 껴야 반영된다.")]
        [SerializeField] private Vector3 _nockRotationOffset;

        private Projectile _nocked;

        public bool HasNocked => _nocked != null;

        // 발사 원점 = 화살이 실제로 놓여 있는 자리.
        // 위치 보정을 준 만큼 소켓 원점에서 떨어져 있으므로 그것까지 반영한다.
        public Vector3 MuzzlePosition => _handSocket != null
            ? _handSocket.TransformPoint(_nockPositionOffset)
            : transform.position;

        private void OnEnable()
        {
            if (_weaponEquip == null || _relay == null || _handSocket == null)
            {
                Debug.LogError($"ArrowNockPoint 참조 누락(_weaponEquip / _relay / _handSocket) on {name}", this);
                enabled = false;
                return;
            }

            _weaponEquip.OnMainHandVisualApplied += HandleMainHandChanged;
        }

        private void OnDisable()
        {
            if (_weaponEquip != null)
                _weaponEquip.OnMainHandVisualApplied -= HandleMainHandChanged;

            ReleaseNocked(); // 사망 / 씬 전환 / 비활성화 — 손 화살을 풀에 돌려준다
        }

        private void HandleMainHandChanged(WeaponCategory category)
        {
            ReleaseNocked(); // 무기가 바뀌면 들고 있던 화살부터 정리

            if (category == WeaponCategory.Bow)
                Nock(); // 활을 들면 대기 자세에서도 화살이 보여야 한다
        }

        // 손이 비어 있으면 화살을 하나 끼운다. 이미 있으면 아무것도 하지 않는다.
        // 멱등성 : "여러 번 호출해도 한 번 호출한 것과 결과가 같다"
        // ★ 이 멱등성이 "화살 두 개" 버그의 차단점이다.
        //   애니메이션을 끊고 다시 시작하면 Draw 이벤트가 연속으로 두 번 올 수 있는데,
        //   두 번째 호출이 화살을 또 붙이면 예전 버그가 그대로 재발한다.
        public void Nock()
        {
            if (_nocked != null) return; // 멱등성의 실체

            var prefab = _relay.ProjectilePrefab;
            if (prefab == null)
            {
                Debug.LogError($"ArrowNockPoint: 릴레이에 투사체 프리팹이 없다 on {name}", this);
                return;
            }

            var instance = ProjectilePool.Rent(prefab, gameObject.scene);
            if (instance == null) return;

            if (!instance.TryGetComponent(out Projectile projectile))
            {
                Debug.LogError($"ArrowNockPoint: {prefab.name}에 Projectile 컴포넌트가 없다", prefab);
                return;
            }

            projectile.Nock(_handSocket, _nockPositionOffset, Quaternion.Euler(_nockRotationOffset));
            _nocked = projectile;
        }

        // INockedProjectileSource — 발사 순간 ProjectileLauncher가 호출한다.
        //
        // ★ 여기서 재노킹하지 않는다. 이게 옛 TakeCurrentArrowVisual()과의 결정적 차이다.
        //   그쪽은 화살을 넘겨준 바로 그 줄에서 새 화살을 손에 다시 붙였고,
        //   그래서 같은 프레임에 "날아가는 화살 + 손의 새 화살"이 동시에 존재했다.
        //   다음 화살은 다음 사격의 Draw 프레임에 Nock()이 붙인다.
        public Projectile TakeNocked()
        {
            var taken = _nocked;
            _nocked = null;
            return taken;
        }

        private void ReleaseNocked()
        {
            if (_nocked == null) return;

            _nocked.DespawnWithoutHit();
            _nocked = null;
        }
    }
}