using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace MySecondWorld.Combat
{
    // 투사체 발사 시점에 공통으로 반복되는 "빈 후보 목록 + 조준점으로 SkillContext 구성 → Execute" 마무리 단계를 추출.
    // 조준점을 어떻게 찾는지(플레이어: 즉석 레이캐스트, 몬스터: AI가 추적 중인 타겟)와
    // 공격력 출처(플레이어: 장착 무기, 몬스터: StatsSO)는 호출부마다 달라 그대로 유지.
    public static class ProjectileSpawnResolver
    {
        public static void SpawnAndExecute(GameObject caster, Vector3 aimPoint, float power, SkillExecutor skillExecutor)
        {
            var ctx = new SkillContext(caster, Array.Empty<IDamageable>(), power, aimPoint);
            skillExecutor.Execute(ctx).Forget();
        }
    }
}