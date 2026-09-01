using UnityEngine;

namespace MySecondWorld.Network.Player
{
    // Item(FloorItemFactory)/Monster(MonsterFactory)와 동일한 "카테고리별 Factory 진입점" 규칙을 맞추기 위한 래퍼.
    // MovementController/PlayerAnimationController/ThirdPersonCameraController/ObservableCharacterCustomizer는
    // 동적 AddComponent 후보로 검토했으나, Mirror는 각 클라이언트가 원본 프리팹에서 독립적으로 Instantiate하므로
    // 서버 팩토리가 붙인 컴포넌트가 클라이언트 로컬 사본에는 전달되지 않는다 — 그래서 전부 프리팹 사전 부착 유지.
    // (자세한 근거는 .claude/memory/20260730-player-factory-PLAN.md 참고)
    public static class PlayerFactory
    {
        public static GameObject Create(GameObject purePrefab, Vector3 position, Quaternion rotation)
        {
            if (purePrefab == null)
            {
                Debug.LogError("[PlayerFactory] purePrefab이 비어있어 스폰을 건너뜀");
                return null;
            }

            return Object.Instantiate(purePrefab, position, rotation);
        }
    }
}
