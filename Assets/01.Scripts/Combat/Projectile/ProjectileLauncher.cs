using Cysharp.Threading.Tasks;
using UnityEngine;
using Mirror;
using UnityEngine.SceneManagement;

namespace MySecondWorld.Combat
{
    // 이 파일이 "화살을 새로 창조하지 않는다"는 규칙이 사는 유일한 자리
    // "투사체 한 발 쏘기"의 단일 진입점. 플레이어·몬스터·(향후) Companion이 전부 여기로 모인다.
    public static class ProjectileLauncher
    {
        public static UniTask<IDamageable> Launch(in ProjectileRequest request, Scene scene)
        {
            var projectile = Acquire(request, scene);
            if (projectile == null)
                return UniTask.FromResult<IDamageable>(null); // 발사 실패 — 대기하는 쪽이 매달리지 않게 즉시 완결

            // 이 한 줄이 "한 피어에 화살 하나"를 구조적으로 보장한다.
            // 서버는 권위 화살만, 순수 클라는 표현 화살만 만든다 —
            // 호스트에서 둘이 이중으로 날아가는 사고가 원리적으로 불가능해진다.
            var mode = NetworkServer.active ? ProjectileMode.Authoritative : ProjectileMode.Presentation;

            return projectile.Launch(request, mode); // ③ 발사
        }

        // ① 화살 확보 — 손에 노킹된 게 있으면 그걸 쓰고, 없을 때만 풀에서 새로 대여한다.
        //    이 순서가 "발사 프레임에 새 화살을 만들지 않는다"의 전부다.
        //    전용 서버처럼 손 화살이 아예 없는 환경에서는 자연히 풀 경로로 떨어진다.
        private static Projectile Acquire(in ProjectileRequest request, Scene scene)
        {
            var nocked = request.NockSource?.TakeNocked();
            if (nocked != null)
                return nocked;

            var instance = ProjectilePool.Rent(request.Prefab, scene);
            if (instance == null)
                return null;

            if (!instance.TryGetComponent(out Projectile projectile))
            {
                Debug.LogError($"[ProjectileLauncher] {request.Prefab.name}에 Projectile 컴포넌트가 없다", request.Prefab);
                return null;
            }
            return projectile;
        }
    }
}