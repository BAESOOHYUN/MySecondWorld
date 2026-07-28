using Cysharp.Threading.Tasks;
using UnityEngine;

namespace MySecondWorld.Combat.Monster
{
    // 투사체 자체 — 등속 직선 이동 + 실제 물리 충돌(Trigger) 시점에 대상을 확정한다.
    // Rigidbody(Kinematic)+SphereCollider(Trigger) 전제 (프리팹 세팅, 씬 배치 항목 참고).
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    public class EnemyProjectile : MonoBehaviour
    {
        [SerializeField] private float _lifetime = 5f;  // 아무것도 못 맞히고 이 시간 지나면 자동 소멸
        [SerializeField] private LayerMask _targetLayerMask; // "Player" 레이어만 지정 — Monster는 무시
        private Rigidbody _rigidbody;
        private Vector3 _direction;
        private float _speed; 
        private bool _resolved;
        private UniTaskCompletionSource<IDamageable> _completionSource;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
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
            Destroy(this.gameObject); // 실제로 맞혀서 파괴된 경우
        }

        private void OnDestroy()
        {
            // 실제로 맞혀서 파괴된 경우(_resolved == true)는 이미 결과가 확정돼 있으니 아무것도 안 함.
            // 타임아웃으로 파괴된 경우(_resolved == false)만 "아무도 못 맞혔다"로 확정.
            if (!_resolved)
                _completionSource?.TrySetResult(null);
        }

        // ProjectileCast가 스폰 직후 호출 — 방향/속도/결과 통지 채널을 주입
        public void Launch(Vector3 direction, float speed, UniTaskCompletionSource<IDamageable> completionSource)
        {
            _direction = direction;
            _speed = speed;
            _completionSource = completionSource;
            Destroy(this.gameObject, _lifetime); // 타임아웃으로 파괴된 경우
            // TODO : 오브젝트 풀링
        }

    }
}
