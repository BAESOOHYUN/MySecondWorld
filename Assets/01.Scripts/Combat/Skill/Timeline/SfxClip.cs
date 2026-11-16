using UnityEngine;

namespace MySecondWorld.Combat
{
    [System.Serializable]
    public class SfxClip : SkillClip
    {
        [SerializeField] private AudioClip _sound;
        [SerializeField, Range(0f, 1f)] private float _volume = 1f;

        public AudioClip Sound => _sound; // 스킬 툴의 미리듣기용(읽기 전용)

        public override bool IsCosmetic => true;

        public override void Execute(SkillContext ctx)
        {
            if (_sound == null) return;
            AudioSource.PlayClipAtPoint(_sound, ctx.Caster.transform.position, _volume);
        }
    }
}