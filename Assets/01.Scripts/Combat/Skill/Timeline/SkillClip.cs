using UnityEngine;

namespace MySecondWorld.Combat
{
    // 모든 클립의 공통 부모. "몇 초에 무엇을 한다"만 정의

    [System.Serializable]
    public abstract class SkillClip
    {
        [SerializeField] private float _startTime;
        public float StartTime => _startTime;

        // 타임라인 툴에서 클립 블록의 폭으로 보여줄 길이(초). 순간 동작(효과·SFX·애니)은 0.
        public virtual float Duration => 0f;

        // true면 보이고 들리는 연출(이펙트·사운드·애니). 서버는 건너뛰고 클라이언트가 각자 재생한다.
        public virtual bool IsCosmetic => false;

        public abstract void Execute(SkillContext ctx);
    }
}