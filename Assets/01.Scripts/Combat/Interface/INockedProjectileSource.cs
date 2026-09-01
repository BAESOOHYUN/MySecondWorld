namespace MySecondWorld.Combat
{
    // "손에 이미 노킹된 투사체"의 공급자.
    //
    // Phase 5의 ArrowNockPoint가 이걸 구현한다. 지금 인터페이스로 끊어두는 이유는
    // 발사 규칙("손에 든 게 있으면 그걸 쓴다")을 ProjectileLauncher에 먼저 넣어두기 위해서다.
    // 몬스터처럼 손 화살 개념이 없는 캐스터는 그냥 null을 넘긴다.
    public interface INockedProjectileSource
    {
        // 노킹된 투사체를 넘기고 자기 손은 비운다. 없으면 null.
        // 여기서 재노킹하지 않는다 — 같은 프레임에 새 화살이 붙으면 "화살 두 개"가 재발한다.
        Projectile TakeNocked();
    }
}