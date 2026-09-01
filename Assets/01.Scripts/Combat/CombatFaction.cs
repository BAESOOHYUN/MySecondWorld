namespace MySecondWorld.Combat
{
    // 공격 대상 필터링에 쓰는 진영 구분. None을 0번으로 두는 이유는
    // 배선을 깜빡했을 때 "눈에 띄는 방향"으로 틀어지게 하기 위함
    // (아무 진영도 아니므로 아군 필터에 걸리지 않아 데미지는 계속 들어간다).
    public enum CombatFaction
    {
        None,
        Player,
        Monster,
    }
}
