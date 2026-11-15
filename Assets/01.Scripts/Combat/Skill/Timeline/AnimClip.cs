using UnityEngine;

namespace MySecondWorld.Combat
{
    [System.Serializable]
    public class AnimClip : SkillClip
    {
        [SerializeField] private string _trigger;

        public override void Execute(SkillContext ctx)
        {
            if (string.IsNullOrEmpty(_trigger) || ctx.CasterAnimator == null) return;
            ctx.CasterAnimator.SetTrigger(_trigger);
        }
    }
}