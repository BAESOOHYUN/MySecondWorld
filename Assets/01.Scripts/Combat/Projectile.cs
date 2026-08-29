using System.Threading;
using Cysharp.Threading.Tasks;
using MySecondWorld.Pool;
using UnityEngine;

namespace MySecondWorld.Combat
{
    // 투사체 자체 — 등속 직선 이동 + 실제 물리 충돌(Trigger) 시점에 대상을 확정한다.
    // 몬스터/플레이어 공용(누가 쐈는지는 이 클래스가 알 필요 없음) — 대상 레이어만 Initialize로 주입받는다.
    // 순수 프리팹엔 Rigidbody/SphereCollider가 없어서(팩토리가 동적 부착) 이동/판정에 필요한 값은 전부 코드로 채운다.
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    [RequireComponent(typeof(PooledObject))]
    public class Projectile : MonoBehaviour
    {
        [SerializeField] private float _lifetime = 5f;  // 아무것도 못 맞히고 이 시간 지나면 자동 소멸
        [SerializeField] private LayerMask _targetLayerMask; // Initialize에서 주입 — 몬스터용이면 Player 레이어, 플레이어용이면 Monster 레이어
        private Rigidbody _rigidbody;
        private Vector3 _direction;
        private float _speed;
        private bool _resolved;
        private UniTaskCompletionSource<IDamageable> _completionSource;
        private CancellationTokenSource _lifetimeCts; // Launch마다 재발급 — 명중 시 타임아웃 취소용

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _rigidbody.isKinematic = true; // MovePosition 이동 방식 전제 — RequireComponent 기본값(비-Kinematic)은 안 맞음
            // 프리팹이 순수화되며 Inspector로 못 넣게 된 값 — MonsterHitbox/BossAoEHitbox와 동일한 방식으로 코드에서 채움
        }

        private void FixedUpdate()
        {
            // Kinematic Rigidbody는 MovePosition으로 이동해야 Trigger 판정이 물리 스텝과 어긋나지 않는다
            _rigidbody.MovePosition(_rigidbody.position + _direction * _speed * Time.fixedDeltaTime);
        }

        // 경로상에서 처음 부딪힌 IDamageable한테 꽂히는 로직
        private void OnTriggerEnter(Collider other)
        {
            // _resolved : 같은 프레임 중복 호출과,
            // OnDestroy의 오판정(맞았는데 타임아웃으로 잘못 처리)을 둘 다 막는 안전장치
            if (_resolved) return;
            // 대상 레이어가 아니면 무시하고 계속 비행
            if ((_targetLayerMask.value & (1 << other.gameObject.layer)) == 0) return;
            var target = other.GetComponentInParent<IDamageable>();
            if (target == null) return; // IDamageable 없는 콜라이더(지형 등)는 무시하고 계속 비행
            // TODO : 벽 통과 문제 분기

            _resolved = true;
            _completionSource.TrySetResult(target);
            _lifetimeCts?.Cancel();  // 타임아웃 쪽 Return과 중복 반납되는 것 방지 (collectionCheck: true라 이중 Return은 즉시 예외)
            ReturnToPool();
        }

        private void OnDestroy()
        {
            // 풀 Dispose(에디터 종료, 씬 전환 등) 때 호출되거나,
            // 동시 비활성 대기 인스턴스가 GameObjectPool의 maxSize(10)를 넘어 ObjectPool이 강제 Destroy할 때도 호출될 수 있음
            // — 두 경우 다 _resolved 가드로 안전(ReturnToPool은 _resolved=true 이후에만 호출됨)
            if (!_resolved)
                _completionSource?.TrySetResult(null);

            _lifetimeCts?.Dispose();
        }

        // ProjectileFactory가 AddComponent 직후 호출 — 화살촉 위치 등 판정 위치/크기, 그리고 '대상 레이어'를 데이터로 주입
        public void Initialize(Vector3 colliderCenter, float colliderRadius, LayerMask targetLayerMask)
        {
            var collider = GetComponent<SphereCollider>();
            collider.center = colliderCenter;
            collider.radius = colliderRadius;
            collider.isTrigger = true; // RequireComponent 기본값(비-Trigger)은 안 맞음
            _targetLayerMask = targetLayerMask;
        }

        // ProjectileCast가 스폰 직후 호출 — 방향/속도/결과 통지 채널을 주입
        public void Launch(Vector3 direction, float speed, UniTaskCompletionSource<IDamageable> completionSource)
        {
            _direction = direction;
            _speed = speed;
            _completionSource = completionSource;
            _resolved = false; // 재사용 대비 리셋

            _lifetimeCts?.Dispose(); // 직전 발사분 토큰 정리
            _lifetimeCts = new CancellationTokenSource();
            LifetimeTimeoutAsync(_lifetimeCts.Token).Forget();
        }

        // Destroy(gameObject, _lifetime) 예약 파괴를 대체 — 풀링 대상은 Destroy 대신 시간 기반 반납이 필요
        private async UniTaskVoid LifetimeTimeoutAsync(CancellationToken ct)
        {
            bool canceled = await UniTask.Delay(
                System.TimeSpan.FromSeconds(_lifetime), cancellationToken: ct)
                .SuppressCancellationThrow(); // 예외를 잡아서 bool로 바꿔주는 UniTask 확장 메서드.
                                              // 취소됐으면 → 예외 대신 true, 정상적으로 시간이 다 찼으면 → false 반환
            if (canceled) return; // 명중해서 취소된 경우 — 여기선 아무것도 안 함

            _resolved = true;
            _completionSource.TrySetResult(null); // 아무도 못 맞힘으로 확정
            ReturnToPool();
        }

        private void ReturnToPool()
        {
            GetComponent<PooledObject>().ReturnToPool();
        }
    }
}
