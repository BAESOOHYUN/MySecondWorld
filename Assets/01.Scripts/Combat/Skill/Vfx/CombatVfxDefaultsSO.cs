using UnityEngine;

namespace MySecondWorld.Combat.Vfx
{
    // Player/Monster/Companion이 공유하는 공용 피격·사망 VFX 프리팹 1개씩.
    // 몬스터 37종 개별 프리팹 드래그를 피하려고 SO 하나로 묶었다(WeaponAnimationMapSO와 같은 이유).
    [CreateAssetMenu(fileName = "CombatVfxDefaultsSO", menuName = "Combat/Combat Vfx Defaults")]
    public class CombatVfxDefaultsSO : ScriptableObject
    {
        [SerializeField] private GameObject _hitVfxPrefab;
        [SerializeField] private GameObject _deathVfxPrefab;

        public GameObject HitVfxPrefab => _hitVfxPrefab;
        public GameObject DeathVfxPrefab => _deathVfxPrefab;
    }
}
