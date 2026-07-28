namespace MySecondWorld.Combat.Monster
{
    // 몬스터 스킬 ID 모음 — 지금은 근접 공격 하나뿐이지만, 매직넘버 대신 이름으로 참조하기 위한 용도.
    public static class MonsterSkillId
    {
        public const int MeleeAttack = 0;   // 근거리 공격
        public const int RangedShoot = 1;   // 원거리 공격
        public const int Retreat = 2;       // 후퇴
    }
}