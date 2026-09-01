using UnityEngine;

namespace MySecondWorld.Combat
{
    // 캐스터가 축2(ProjectileCastSO)에 넘기는 발사 정보 일체.
    // 이 값들이 SO 필드가 아니라 여기 있는 이유가 4축 설계의 핵심 제약이다 —
    // ProjectileCastSO 에셋 하나를 모든 몬스터와 플레이어가 공유하므로,
    // SO가 화살·속도를 필드로 들면 캐스터끼리 서로 덮어쓴다.
    public struct ProjectileRequest
    {
        public GameObject Prefab;                   // 어떤 투사체를 쓸지 (풀을 나누는 키이기도 하다)
        public INockedProjectileSource NockSource;  // 손에 든 화살 공급자. 없으면 null → 풀에서 대여
        public Vector3 Origin;
        public Vector3 Direction;
        public float Speed;
        public float SweepRadius;
        public float MaxLifetime;
        public LayerMask HitMask;                   // 데미지 대상 (몬스터가 쏘면 Player, 반대면 Monster)
        public LayerMask BlockMask;                 // 벽·지형

        // 유도(homing). Target이 null이거나 TurnRate가 0이면 유도 없이 직선 비행(게이지 0%와 동일).
        public Transform HomingTarget;
        public float HomingTurnRateDegPerSec;

        // 명중한 순간 호출된다(빗나감·벽은 호출 안 함). 인자는 화살촉이 닿은 지점.
        // Projectile이 Mirror를 직접 알 필요가 없도록 캐스터가 여기에 Rpc 호출을 꽂는다.
        // (서버가 명중한 순간을 클라에 알려야 표현 화살을 같은 지점에서 정리할 수 있다.
        //  그런데 Projectile은 네트워크를 몰라야 하므로, 콜백으로 뒤집는다.)
        // 델리게이트를 이벤트가 아니라 필드로 둔 이유: 풀에서 재사용될 때 Launch가 매번 덮어써서
        // 구독이 쌓이는 사고(이벤트의 고전적 누수)가 원리적으로 안 생긴다.
        public System.Action<Vector3> OnResolved;
    }
}