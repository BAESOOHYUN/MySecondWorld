using System.Collections.Generic;
using UnityEngine;

namespace MySecondWorld.Combat.Monster
{
    [CreateAssetMenu(fileName = "MonsterWaveDefinitionSO", menuName = "Combat/Monster Wave Definition")]
    public class MonsterWaveDefinitionSO : ScriptableObject
    {
        [System.Serializable]
        public struct MonsterEntry
        {
            public GameObject PurePrefab;
            public MonsterStatsSO Stats;
        }

        [System.Serializable]
        public struct Wave
        {
            public MonsterEntry[] Monsters;
        }

        // 리스트 순서대로 진행. 마지막 파수에 보스(BossStatsSO)를 넣어서 마무리한다.
        [SerializeField] private Wave[] _waves;

        public IReadOnlyList<Wave> Waves => _waves;
    }
}
