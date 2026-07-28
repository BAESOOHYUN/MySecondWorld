namespace MySecondWorld.Combat.Monster
{
    // 몬스터 AI가 스킬 선택 시점에 selector에게 넘겨주는 상황 정보.
    // TODO : 지금은 필드가 없지만, 원거리몹/보스 selector가 생기면 거리/체력비율 등을 여기 추가한다.
    public class AIContext
    {
        // RangedMobSelector가 후퇴/사격 여부를 판단하는 기준 (MonsterAI.UpdateAttack()에서 매 프레임 갱신)
        public float DistanceToTarget { get; set; }
    }
}