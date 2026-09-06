using UnityEngine;

namespace MySecondWorld.Combat.Vfx
{
    // 공격 스윙/피격/사망 VFX 공용 Instantiate 헬퍼.
    public static class CombatVfxSpawner
    {
        // 사용하는 VFX 프리팹 중 Particle System Stop Action=Destroy가 안 된 것이 섞여 있을 수 있어,
        // 일정 시간 후 강제 Destroy로 폴백한다. 이미 자체적으로 파괴된 인스턴스에 다시 걸어도
        // Unity가 조용히 무시하므로 안전하다.
        private const float FallbackLifetimeSeconds = 5f;

        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return null;

            var instance = Object.Instantiate(prefab, position, rotation);
            Object.Destroy(instance, FallbackLifetimeSeconds);
            return instance;
        }
    }
}
