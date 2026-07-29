using UnityEngine;

namespace MySecondWorld.Combat.Monster
{
    // 순수 시각 프리팹(메시+리그+Animator만, AI/Hitbox 스크립트 없음)에
    // 스탯 SO 타입에 따라 AI + Hitbox 컴포넌트 조합을 스폰 시점에 붙인다.
    public static class MonsterFactory
    {
        public static GameObject Create(GameObject purePrefab, MonsterStatsSO stats, Vector3 position, Quaternion rotation)
        {
            if (purePrefab == null || stats == null)
            {
                Debug.LogError($"[MonsterFactory] purePrefab 또는 stats가 비어있어 스폰을 건너뜀 (purePrefab={purePrefab}, stats={stats})");
                return null;
            }

            var instance = Object.Instantiate(purePrefab, position, rotation);

            // 비활성화 후 조립 — AddComponent는 오브젝트가 활성 상태면 그 자리에서 Awake를 실행시켜버려서,
            // Awake가 필요로 하는 데이터(Initialize)를 미리 못 채우면 각 컴포넌트가 참조 누락으로 자기 자신을 꺼버린다.
            instance.SetActive(false);

            // 소켓 이름은 스탯 SO가 결정 — 리그마다 본 이름이 달라서 하드코딩 대신 데이터로 관리(예: SkeletonWarriorLP는 "ShieldBoneR")
            var attackPoint = FindSocket(instance, stats.AttackPointSocketName);

            MonsterAI monsterAI = stats is BossStatsSO
                ? instance.AddComponent<BossAI>()
                : instance.AddComponent<MonsterAI>();
            monsterAI.Initialize(stats);

            switch (stats)
            {
                case BossStatsSO bossStats:
                    // 보스는 근접 모션(MonsterHitbox)과 광역 슬램(BossAoEHitbox)을 둘 다 씀
                    instance.AddComponent<MonsterHitbox>().Initialize(attackPoint, bossStats);
                    instance.AddComponent<BossAoEHitbox>().Initialize(attackPoint, bossStats);
                    break;
                case RangedMonsterStatsSO rangedStats:
                    instance.AddComponent<RangedMonsterHitbox>().Initialize(attackPoint, rangedStats);
                    break;
                default:
                    instance.AddComponent<MonsterHitbox>().Initialize(attackPoint, stats);
                    break;
            }

            instance.SetActive(true); // 여기서 전부 Awake — 필요한 데이터는 이미 다 채워진 상태
            return instance;
        }

        private static Transform FindSocket(GameObject root, string socketName)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == socketName)
                    return t;
            }

            Debug.LogWarning($"[MonsterFactory] {root.name}에서 '{socketName}' 소켓을 못 찾음 — Hitbox의 attackPoint가 비어있는 상태로 남음", root);
            return null;
        }
    }
}
