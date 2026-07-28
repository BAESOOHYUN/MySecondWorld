
using UnityEngine;
using MySecondWorld.Combat.Monster;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System;

namespace MySecondWorld.Combat
{
    // 축2 구현체 — 투사체를 실제로 날려 "물리적으로 충돌하는 순간"까지 대기하는 발동 타이밍
    public class ProjectileCast : ICastTiming
    {
        private readonly EnemyProjectile _projectilePrefab;
        private readonly Transform _spawnPoint;
        private readonly float _speed;
        
        public ProjectileCast(EnemyProjectile projectilePrefab, Transform spawnPoint, float speed)
        {
            _projectilePrefab = projectilePrefab;
            _spawnPoint = spawnPoint;
            _speed = speed;
        }

        public async UniTask<IReadOnlyList<IDamageable>> ResolveTargets(SkillContext ctx)
        {
            if (ctx.AimPoint == null)
                return Array.Empty<IDamageable>(); // 조준 지점 없음 — 발사 자체가 불가능한 상황

            var direction = (ctx.AimPoint.Value - _spawnPoint.position).normalized;
            var projectile = UnityEngine.Object.Instantiate(_projectilePrefab, _spawnPoint.position, Quaternion.LookRotation(direction));
            var completionSource = new UniTaskCompletionSource<IDamageable>();
            projectile.Launch(direction, _speed, completionSource);
            // 투사체가 실제로 맞히거나(OnTriggerEnter) 타임아웃(OnDestroy)될 때까지 대기
            var hitTarget = await completionSource.Task;
            return hitTarget == null ? Array.Empty<IDamageable>() : new List<IDamageable> { hitTarget };
        }
    }
}