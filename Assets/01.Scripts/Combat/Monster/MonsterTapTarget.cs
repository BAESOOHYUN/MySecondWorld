using UnityEngine;
using MySecondWorld.Interaction;

namespace MySecondWorld.Combat.Monster
{
    /// <summary>
    /// 몬스터를 탭 대상으로 만드는 컴포넌트. 순수 프리팹에 부착한다.
    ///
    /// 판정은 이 컴포넌트가 런타임에 만드는 TapTarget 레이어 트리거 캡슐로 한다.
    /// 프리팹마다 손으로 캡슐을 재지 않는 이유: 몸통 치수는 MonsterStatsSO에만 있고,
    /// 그 값은 MonsterServerState가 동기화해준다.
    /// </summary>
    [DisallowMultipleComponent]
    public class MonsterTapTarget : MonoBehaviour, ITapTargetable
    {
        // Domain Reload가 꺼져 있어 static은 플레이 세션을 넘어 살아남는다 — 반드시 리셋한다.
        private static int _tapLayer = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetLayerCache() => _tapLayer = -1;

        private MonsterServerState _serverState;
        private CapsuleCollider _tapCollider;

        public Transform Anchor => transform;

        // 판정 캡슐을 끄는 것만으로 이미 걸러지지만, 이중 안전장치로 남긴다.
        public bool IsSelectable => isActiveAndEnabled && (_serverState == null || _serverState.IsAlive);

        public void OnTapped() => Debug.Log($"[MonsterTapTarget] 탭 승자: {name}", this);

        private void Awake() => _serverState = GetComponent<MonsterServerState>();

        /// <summary>MonsterServerState.OnStartClient가 동기화된 몸통 치수로 호출한다.</summary>
        public void BuildTapCollider(Vector3 center, float radius, float height)
        {
            if (_tapLayer < 0)
            {
                _tapLayer = LayerMask.NameToLayer("TapTarget");
                if (_tapLayer < 0)
                {
                    Debug.LogError("[MonsterTapTarget] 'TapTarget' 레이어가 없습니다.", this);
                    return;
                }
            }

            if (_tapCollider == null)
            {
                // 서버 전용 몸통 콜라이더(실체)와 섞이지 않게 별도 자식으로 만든다.
                // 트리거 + 전용 레이어라 이동·화살·공격 판정 어디에도 걸리지 않는다.
                var go = new GameObject("TapCollider");
                go.transform.SetParent(transform, false);
                go.layer = _tapLayer;
                _tapCollider = go.AddComponent<CapsuleCollider>();
                _tapCollider.isTrigger = true;
            }

            // 풀 재사용으로 다시 스폰되면 값만 갱신한다.
            _tapCollider.center = center;
            _tapCollider.radius = radius > 0f ? radius : 0.3f;
            _tapCollider.height = height > 0f ? height : 1.8f;
        }

        /// <summary>죽은 몸이 뒤의 살아있는 몬스터를 가리지 않게 판정에서 뺀다.</summary>
        public void SetTappable(bool value)
        {
            if (_tapCollider != null)
                _tapCollider.enabled = value;
        }
    }
}