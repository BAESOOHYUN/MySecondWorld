using UnityEngine;
using UnityEngine.AddressableAssets;
using Mirror;

namespace MySecondWorld.Network
{
    /// <summary>
    /// "이 좌표 범위가 이 씬이다"라는 지도 데이터.
    /// </summary>
    [CreateAssetMenu(fileName = "NewZone", menuName = "Scene/Zone Definition")]
    public class ZoneDefinitionSO : ScriptableObject
    {
        [Scene] public string sceneName;    // 이 존에 대응하는 씬 경로
        public Bounds bounds;               // 씬의 정확한 월드 영역 (플레이어 소속 판정용)
        public float preloadMargin = 20f;   // bounds 바깥으로 이만큼 확장한 영역 = 미리 로드

        [SerializeField] private AssetLabelReference _zoneAssetLabel;
        public AssetLabelReference ZoneAssetLabel => _zoneAssetLabel;

        /// <summary>
        /// 이 존이 속한 월드. 같은 월드끼리만 소속 판정/스트리밍 후보가 되고,
        /// 월드 간 이동은 좌표가 아니라 포탈로만 일어난다.
        ///
        /// 번들 라벨을 그대로 쓴다 — 번들 경계와 포탈 경계는 같은 개념이기 때문이다.
        /// 둘 다 "여기서부터는 심리스 스트리밍이 안 되고 명시적 로딩이 필요하다"는 지점을 가리킨다.
        /// (예: zone_field / zone_dungeon_1)
        ///
        /// 주의: 다운로드 크기 때문에 한 월드의 번들을 쪼개면(zone_dungeon_1 → _a/_b)
        /// 연속된 던전 한가운데에 월드 경계가 생겨 포탈 없이는 못 건너가는 벽이 된다.
        /// 그때는 이 속성을 별도 필드로 분리할 것.
        /// </summary>
        public string WorldId => _zoneAssetLabel?.labelString;
    }
}