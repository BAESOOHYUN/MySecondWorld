using UnityEngine;

namespace MySecondWorld.Combat
{
    [System.Serializable]
    public class SfxClip : SkillClip
    {
        [SerializeField] private AudioClip _sound;
        [SerializeField, Range(0f, 1f)] private float _volume = 1f;

        public override void Execute(SkillContext ctx)
        {
            if (_sound == null) return;
            AudioSource.PlayClipAtPoint(_sound, ctx.Caster.transform.position, _volume);
        }
    }
}