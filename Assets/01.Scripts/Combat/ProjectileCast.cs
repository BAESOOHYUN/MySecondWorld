using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using MySecondWorld.Combat.Monster;

namespace MySecondWorld.Combat
{
    // 축2 구현체 — 투사체를 실제로 날려 "물리적으로 충돌하는 순간"까지 대기하는 발동 타이밍
    public class ProjectileCast : ICastTiming
    {
        private readonly GameObject _projectilePrefab; // 순수 프리팹(스크립트 없음) — ProjectileFactory가 스폰 시점에 EnemyProjectile을 붙임
        private readonly Transform _spawnPoint;
        private readonly float _speed;
        private readonly Vector3 _colliderCenter;
        private readonly float _colliderRadius;

        public ProjectileCast(GameObject projectilePrefab, Transform spawnPoint, float speed, Vector3 colliderCenter, float colliderRadius)
        {
            _projectilePrefab = projectilePrefab;
            _spawnPoint = spawnPoint;
            _speed = speed;
            _colliderCenter = colliderCenter;
            _colliderRadius = colliderRadius;
        }

        public async UniTask<IReadOnlyList<IDamageable>> ResolveTargets(SkillContext ctx)
        {
            if (ctx.AimPoint == null)
                return Array.Empty<IDamageable>(); // 조준 지점 없음 — 발사 자체가 불가능한 상황

            var direction = (ctx.AimPoint.Value - _spawnPoint.position).normalized;
            var projectile = ProjectileFactory.Create(_projectilePrefab, _spawnPoint.position, Quaternion.LookRotation(direction), _colliderCenter, _colliderRadius);
            if (projectile == null) return Array.Empty<IDamageable>(); // 프리팹 구성 오류 — ProjectileFactory가 이미 에러 로그를 남김

            // Instantiate는 기본적으로 Active Scene(MainScene)에 생성하므로, 몬스터와 같은 씬(FieldScene 등)으로 옮겨줌
            SceneManager.MoveGameObjectToScene(projectile.gameObject, _spawnPoint.gameObject.scene);

            var completionSource = new UniTaskCompletionSource<IDamageable>();
            projectile.Launch(direction, _speed, completionSource);
            // 투사체가 실제로 맞히거나(OnTriggerEnter) 타임아웃(OnDestroy)될 때까지 대기
            var hitTarget = await completionSource.Task;
            return hitTarget == null ? Array.Empty<IDamageable>() : new List<IDamageable> { hitTarget };
        }
    }
}