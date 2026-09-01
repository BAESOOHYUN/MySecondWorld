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
            [SerializeField] private GameObject _purePrefab;
            [SerializeField] private MonsterStatsSO _stats;

            public GameObject PurePrefab => _purePrefab;
            public MonsterStatsSO Stats => _stats;
        }

        [System.Serializable]
        public struct Wave
        {
            [SerializeField] private MonsterEntry[] _monsters;

            public IReadOnlyList<MonsterEntry> Monsters => _monsters;
        }

        // 리스트 순서대로 진행. 마지막 웨이브에 보스(BossStatsSO)를 넣어서 마무리한다.
        [SerializeField] private Wave[] _waves;

        public IReadOnlyList<Wave> Waves => _waves;
    }
}
