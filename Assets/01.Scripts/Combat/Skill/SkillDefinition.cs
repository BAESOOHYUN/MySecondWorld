using Cysharp.Threading.Tasks;
using UnityEngine;

namespace MySecondWorld.Combat
{
    // 스킬 하나 = 축1(발동 타이밍) + 타임라인(시퀀스).
    // 축2(타겟팅)·축3(효과)는 타임라인의 효과 클립(EffectClip) 안에 있다 — 때리는 순간마다 한 벌씩.
    // 새 스킬을 만들 때 코드가 늘어나지 않는다 — 에셋에서 부품을 골라 끼우고 시간에 배치하면 끝.
    [CreateAssetMenu(fileName = "Skill_", menuName = "Combat/Skill Definition")]
    public class SkillDefinition : ScriptableObject, ISkill
    {
        [Header("슬롯 표시·제한")]
        [SerializeField] private string _displayName;   // 슬롯 UI에 보일 이름. 비우면 에셋 이름을 쓴다
        [SerializeField] private Sprite _icon;          // 슬롯 UI 아이콘
        [Min(0f)] [SerializeField] private float _cooldown = 1f; // 재사용 대기(초). 서버가 슬롯 시전 때 검증한다

        [Header("구성")]
        [SerializeField] private CastTimingSO _castTiming; // 축1
        [SerializeReference] private System.Collections.Generic.List<SkillClip> _timeline = new(); // 시퀀스: 스킬 안의 시간 배치

        public string DisplayName => string.IsNullOrEmpty(_displayName) ? name : _displayName;
        public Sprite Icon => _icon;
        public float Cooldown => _cooldown;

        // 타임라인 툴이 클립 종류·시작 시간·길이를 읽는 통로(읽기 전용).
        public System.Collections.Generic.IReadOnlyList<SkillClip> Timeline => _timeline;

        public async UniTask Execute(SkillContext ctx)
        {
            // ★ 축1이 반드시 먼저다.
            //   투사체처럼 "도착하는 순간에야 대상이 정해지는" 스킬은,
            //   타겟팅을 먼저 하면 발사 시점의 낡은 후보를 때리게 된다.
            //   즉시발동(InstantCast)은 대기가 없어 순서가 결과에 영향을 주지 않는다.
            await _castTiming.WaitBeforeExecute(ctx);

            // 타격(축2+축3)은 타임라인의 효과 클립이 자기 시각에 실행한다.
            await SkillTimelinePlayer.Play(_timeline, ctx);
        }
    }
}