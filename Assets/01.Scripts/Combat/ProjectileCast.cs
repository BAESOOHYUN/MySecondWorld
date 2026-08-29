using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using MySecondWorld.Pool;

namespace MySecondWorld.Combat
{
    // 축2 구현체 — 투사체를 실제로 날려 "물리적으로 충돌하는 순간"까지 대기하는 발동 타이밍
    public class ProjectileCast : ICastTiming
    {
        private readonly Transform _spawnPoint;
        private readonly float _speed;
        private readonly IGameObjectPool _pool;
        private GameObject _pendingInstance; // Spawn()으로 미리 배치해둔 뒤 ResolveTargets(발사)를 기다리는 인스턴스

        public ProjectileCast(GameObject projectilePrefab, Transform spawnPoint,
            float speed, Vector3 colliderCenter, float colliderRadius, LayerMask targetLayerMask
            )
        {
            _spawnPoint = spawnPoint;
            _speed = speed;
            _pool = new GameObjectPool(
                () => ProjectileFactory.CreatePooled(
                    projectilePrefab, colliderCenter, colliderRadius, targetLayerMask, _pool, spawnPoint.gameObject.scene),
                    defaultCapacity: 4, maxSize: 10);
        }

        // 투사체를 스폰 위치에 미리 배치만 해둔다(아직 안 날아감, "노킹"에 해당하는 비주얼 단계).
        // 호출 안 해도 ResolveTargets가 알아서 그 자리에서 새로 Rent하므로, Spawn 없이 바로 쏘는 기존 호출부는 영향 없음.
        public void Spawn(Vector3 direction)
        {
            if (_pendingInstance != null) // 발사 없이 재스폰된 경우(연타 등) — 누수 방지로 먼저 반납
                _pendingInstance.GetComponent<PooledObject>().ReturnToPool();

            _pendingInstance = _pool.Rent();
            var projectile = _pendingInstance.GetComponent<Projectile>();
            var t = _pendingInstance.transform;
            t.SetParent(_spawnPoint, false); // 노킹 상태 — 손(스폰 포인트)의 애니메이션 움직임을 그대로 따라감
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity; // 스폰 포인트(활) 본래 방향 그대로 — 조준 방향은 발사 시점에만 적용

            // 부모(스폰 포인트) 본의 스케일이 원본 프리팹 스케일을 왜곡시키지 않도록 보정
            // — NetworkWeaponEquip.ResetSocketTransform과 동일한 방식(부모 스케일 역산)
            var parentScale = _spawnPoint.lossyScale;
            var original = projectile.OriginalLocalScale;
            t.localScale = new Vector3(original.x / parentScale.x, original.y / parentScale.y, original.z / parentScale.z);
        }

        public async UniTask<IReadOnlyList<IDamageable>> ResolveTargets(SkillContext ctx)
        {
            if (ctx.AimPoint == null) // 조준 지점 없음 — 발사 자체가 불가능한 상황
                return Array.Empty<IDamageable>();
            var direction = (ctx.AimPoint.Value - _spawnPoint.position).normalized;

            // Spawn()으로 미리 준비된 인스턴스가 있으면 재사용(방향만 발사 시점 최신 조준으로 갱신), 없으면 지금 바로 Rent
            var instance = _pendingInstance ?? _pool.Rent();
            _pendingInstance = null;

            var projectile = instance.GetComponent<Projectile>();
            var t = instance.transform;
            t.SetParent(null); // 발사 순간 손(스폰 포인트)에서 분리 — 이제부터 월드 기준 독립 비행
            t.SetPositionAndRotation(_spawnPoint.position, Quaternion.LookRotation(direction));
            t.localScale = projectile.OriginalLocalScale; // 부모 분리 후엔 로컬=월드라 원본 스케일 그대로 복원

            var completionSource = new UniTaskCompletionSource<IDamageable>();
            projectile.Launch(direction, _speed, completionSource);

            // 투사체가 실제로 맞히거나(OnTriggerEnter) 타임아웃(OnDestroy)될 때까지 대기
            var hitTarget = await completionSource.Task;
            return hitTarget == null ? Array.Empty<IDamageable>() : new List<IDamageable> { hitTarget };
        }

        public void Dispose()
        {
            _pool.Dispose();
        }

    }
}