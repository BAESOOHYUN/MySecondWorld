using MySecondWorld.Pool;
using UnityEngine;

namespace MySecondWorld.Combat
{
    // 순수 시각 이펙트 전용 — 물리 판정 없이 정해진 시간 동안 등속 직선 이동만 하다가 사라진다.
    // 몬스터 원거리 공격의 클라 시각 브로드캐스트(MonsterProjectileVisualRelay)와
    // 플레이어 로컬 예측(PlayerAttackController) 양쪽 공용 — 둘 다 "보여주기 전용"이라
    // 물리 충돌 판정이 필요 없음(실제 데미지는 서버가 별도 경로로 확정).
    public class ProjectileVisualMover : MonoBehaviour
    {
        private Vector3 _direction;
        private float _speed;
        private float _lifetime;
        private float _timer;
        private Vector3 _originalLocalScale; // 부모 본 스케일 보정 기준값(노킹 단계에서 손에 붙일 때 필요)

        private void Awake()
        {
            _originalLocalScale = transform.localScale;
        }

        public Vector3 OriginalLocalScale => _originalLocalScale;

        public void Setup(Vector3 direction, float speed, float lifetime)
        {
            _direction = direction;
            _speed = speed;
            _lifetime = lifetime;
            _timer = 0f;
        }

        private void Update()
        {
            transform.position += _direction * _speed * Time.deltaTime;
            _timer += Time.deltaTime;
            if (_timer >= _lifetime)
            {
                // 풀에서 대여된 인스턴스(몬스터 쪽)면 반납, 아니면(장비 시스템에서 넘어온 화살) 그냥 파괴
                if (TryGetComponent(out PooledObject pooled))
                    pooled.ReturnToPool();
                else
                    Destroy(gameObject);
            }
        }

    }
}
