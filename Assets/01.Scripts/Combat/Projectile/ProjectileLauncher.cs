using UnityEngine;
using Mirror;
using UnityEngine.SceneManagement;

namespace MySecondWorld.Combat
{
    // 이 파일이 "화살을 새로 창조하지 않는다"는 규칙이 사는 유일한 자리
    // "투사체 한 발 쏘기"의 단일 진입점. 플레이어·몬스터·(향후) Companion이 전부 여기로 모인다.
    public static class ProjectileLauncher
    {
        // 발사하고 그 투사체를 돌려준다.
        // 결과를 기다릴 쪽(서버 판정)은 반환값의 HitTask를,
        // 나중에 조작할 쪽(표현 화살)은 반환값 자체를 들고 있으면 된다.
        public static Projectile Launch(in ProjectileRequest request, Scene scene)
        {
            var projectile = Acquire(request, scene);
            if (projectile == null) return null;

            // 이 한 줄이 "한 피어에 화살 하나"를 구조적으로 보장한다.
            // 서버(NetworkServer.active == true)라면 → Authoritative 모드 → 서버가 판정(충돌, 데미지 등)을 책임짐.
            // 클라이언트라면 → Presentation 모드   → 단순히 시각적 표현(화살 날아가는 연출)만 담당.
            var mode = NetworkServer.active ? ProjectileMode.Authoritative : ProjectileMode.Presentation;
            projectile.Launch(request, mode);
            return projectile;
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