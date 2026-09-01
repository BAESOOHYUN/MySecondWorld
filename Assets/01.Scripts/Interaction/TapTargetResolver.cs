using Cysharp.Threading.Tasks;   // AddTo(destroyCancellationToken)
using UnityEngine;
using R3;
using VContainer;

namespace MySecondWorld.Interaction
{
    /// <summary>
    /// 탭 판정의 유일한 주인. 한 번의 탭에 승자는 한 명이라는 게 구조로 보장된다.
    ///
    /// 등록 목록이 없어진 이유: 레이캐스트가 맞은 콜라이더에서 대상을 직접 찾으므로
    /// 누가 살아있는지 목록으로 관리할 필요가 사라졌다(풀 반납 누락 사고도 같이 사라진다).
    /// </summary>
    public class TapTargetResolver : MonoBehaviour
    {
        [Inject] private TouchEventListener _touchEventListener;

        [SerializeField, Tooltip("탭 광선 최대 사거리(m)")]
        private float _maxDistance = 200f;

        private int _tapLayerMask;

        private readonly Subject<ITapTargetable> _onTargetTapped = new();
        public Observable<ITapTargetable> OnTargetTapped => _onTargetTapped;

        private void Awake()
        {
            int layer = LayerMask.NameToLayer("TapTarget");
            if (layer < 0)
            {
                Debug.LogError("[TapTargetResolver] 'TapTarget' 레이어가 없습니다. " +
                               "Project Settings > Tags and Layers에서 추가하세요.", this);
                enabled = false;
                return;
            }
            _tapLayerMask = 1 << layer;
        }

        private void Start()
        {
            if (_touchEventListener == null)
            {
                Debug.LogError("[TapTargetResolver] TouchEventListener 주입 실패", this);
                enabled = false;
                return;
            }

            _touchEventListener.OnTap
                .Subscribe(HandleTap)
                .AddTo(destroyCancellationToken);
        }

        private void OnDestroy() => _onTargetTapped.Dispose();

        private void HandleTap(Vector2 screenPos)
        {
            // 던전 전환 등으로 카메라가 갈릴 수 있어 캐시하지 않는다.
            Camera cam = Camera.main;
            if (cam == null) return;

            ITapTargetable winner = null;

            // TapTarget 레이어만 쏜다 — 다른 레이어의 감지존/포탈 트리거가 끼어들 수 없다.
            // 판정용 콜라이더는 전부 트리거라 Collide를 명시해야 잡힌다.
            if (Physics.Raycast(cam.ScreenPointToRay(screenPos), out RaycastHit hit,
                                _maxDistance, _tapLayerMask, QueryTriggerInteraction.Collide))
            {
                // 판정 콜라이더는 자식이므로 부모까지 거슬러 올라가 대상을 찾는다.
                var candidate = hit.collider.GetComponentInParent<ITapTargetable>();
                if (candidate != null && candidate.IsSelectable)
                    winner = candidate;
            }

            winner?.OnTapped();
            _onTargetTapped.OnNext(winner);   // 못 맞혔으면 null
        }
    }
}