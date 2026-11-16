using UnityEngine;

namespace MySecondWorld.Combat
{
    [System.Serializable]
    public class AnimClip : SkillClip
    {
        [SerializeField] private string _trigger;

        public override bool IsCosmetic => true;

        public override void Execute(SkillContext ctx)
        {
            if (string.IsNullOrEmpty(_trigger)) return;
            if (ctx.PlayAnimation != null) { ctx.PlayAnimation(_trigger); return; }
            if (ctx.CasterAnimator != null) ctx.CasterAnimator.SetTrigger(_trigger);
        }
    }
}