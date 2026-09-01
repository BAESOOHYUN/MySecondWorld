using UnityEngine;
using System.Linq;
using Mirror;
using MySecondWorld.Combat.Companion;

namespace MySecondWorld.Network.Companion
{
    // PlayerFactory와 같은 위치 규칙(Network 어셈블리)
    // CompanionAI를 직접 참조 안 하고 인터페이스(ICompanionServerState)로만 접근해서 순환참조를 피함
    public static class CompanionFactory
    {
        public static GameObject Create(GameObject speciesPrefab, Vector3 position, Quaternion rotation, Transform ownerTransform)
        {
            if (speciesPrefab == null)
            {
                Debug.LogError("[CompanionFactory] speciesPrefab이 비어있어 스폰을 건너뜀");
                return null;
            }

            var instance = Object.Instantiate(speciesPrefab, position, rotation);
            NetworkServer.Spawn(instance);

            if (instance.TryGetComponent(out ICompanionServerState serverState))
                serverState.Initialize(ownerTransform);
            else
                Debug.LogError($"[CompanionFactory] {speciesPrefab.name}에 ICompanionServerState 구현체(CompanionServerState)가 없음", instance);

            return instance;
        }
    }
}
