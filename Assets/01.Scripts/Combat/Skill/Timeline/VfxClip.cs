
using UnityEngine;

namespace MySecondWorld.Combat
{
    // 이펙트를 어디에 붙일지 정하는 enum
    public enum VfxAnchor
    {
        Caster,     // 시전자 위치 (버프, 기모으기)
        Epicenter,  // 폭심지 (폭발, 스플래시)
    }

    // 이펙트를 생성하는 클립 (Visual Effect (시각))

    [System.Serializable]
    public class VfxClip : SkillClip
    {
        [SerializeField] private GameObject _prefab;
        [SerializeField] private VfxAnchor _anchor;
        [SerializeField] private Vector3 _offset;
        [SerializeField] private float _lifetime = 2f;

        public override float Duration => _lifetime;

        // 스킬 툴의 미리보기가 읽는 통로(읽기 전용).
        public GameObject Prefab => _prefab;
        public VfxAnchor Anchor => _anchor;
        public Vector3 Offset => _offset;

        public override bool IsCosmetic => true;

        public override void Execute(SkillContext ctx)
        {
            if (_prefab == null) return;

            var position = ResolvePosition(ctx);
            var instance = Object.Instantiate(_prefab, position, ctx.Caster.transform.rotation);
            Object.Destroy(instance, _lifetime);
        }

        private Vector3 ResolvePosition(SkillContext ctx)
        {
            Vector3 basePos = _anchor == VfxAnchor.Epicenter && ctx.Epicenter.HasValue
                ? ctx.Epicenter.Value
                : ctx.Caster.transform.position;

            return basePos + _offset;
        }
    }
}