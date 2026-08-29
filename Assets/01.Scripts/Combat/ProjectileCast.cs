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

        public async UniTask<IReadOnlyList<IDamageable>> ResolveTargets(SkillContext ctx)
        {
            if (ctx.AimPoint == null) // 조준 지점 없음 — 발사 자체가 불가능한 상황
                return Array.Empty<IDamageable>();
            var direction = (ctx.AimPoint.Value - _spawnPoint.position).normalized;
            var instance = _pool.Rent();
            instance.transform.SetPositionAndRotation(_spawnPoint.position, Quaternion.LookRotation(direction));

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