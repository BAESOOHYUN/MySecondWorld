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
    }
}