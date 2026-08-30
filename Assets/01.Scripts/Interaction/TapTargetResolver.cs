using System.Collections.Generic;
using Cysharp.Threading.Tasks;   // AddTo(destroyCancellationToken) — R3가 아니라 UniTask의 확장이다
using UnityEngine;
using R3;
using VContainer;

namespace MySecondWorld.Interaction
{
    /// <summary>
    /// 탭 판정의 유일한 주인. 씬에 하나만 둔다.
    ///
    /// 대상들이 각자 OnTap을 구독하던 기존 구조를 폐지한다 —
    /// N명이면 탭 한 번에 같은 계산이 N번 돌고, 각자가 각자의 승자를 정해 각자 실행했다.
    /// (NPC 클릭이 랜덤하던 원인 ③, CONTEXT §1-③)
    ///
    /// 이제 "한 번의 탭에 승자는 한 명"이 코드 규율이 아니라 구조로 보장된다.
    /// </summary>
    public class TapTargetResolver : MonoBehaviour
    {
        [Inject] private TouchEventListener _touchEventListener;

        private readonly List<ITapTargetable> _registered = new();

        // 손가락 여유(미터). 0이면 보이는 몸에 정확히 닿아야 잡힌다.
        private const float TapTolerance = 0.1f;

        // 탭 판정 결과. 아무것도 못 맞혔으면 null이 흐른다(빈 곳 탭).
        // 4단계의 PlayerTargetSelector가 이걸 구독해 선택 상태를 관리한다.
        private readonly Subject<ITapTargetable> _onTargetTapped = new();
        public Observable<ITapTargetable> OnTargetTapped => _onTargetTapped;

        #region 유니티 생명주기
        private void Start()
        {
            if (_touchEventListener == null)
            {
                Debug.LogError("[TapTargetResolver] TouchEventListener 주입 실패 — " +
                               "ProjectLifetimeScope에 RegisterComponentInHierarchy<TapTargetResolver>()가 있는지 확인하세요.", this);
                enabled = false;
                return;
            }

            // 씬 전체에서 이 구독 하나뿐이다.
            _touchEventListener.OnTap
                .Subscribe(HandleTap)
                .AddTo(destroyCancellationToken);
        }

        private void OnDestroy() => _onTargetTapped.Dispose();
        #endregion

        #region 등록 / 해제
        public void Register(ITapTargetable target)
        {
            if (target == null || _registered.Contains(target))
                return;
            _registered.Add(target);
        }

        public void Unregister(ITapTargetable target)
        {
            if (target == null)
                return;
            _registered.Remove(target);
        }
        #endregion

        #region 판정
        private void HandleTap(Vector2 screenPos)
        {
            // 던전 전환 등으로 카메라가 갈릴 수 있어 캐시하지 않는다.
            // 탭당 1회 조회라 비용은 무시할 수준.
            Camera cam = Camera.main;
            if (cam == null)
                return;

            ITapTargetable winner = null;
            float bestDepth = float.MaxValue;

            // 역순 순회 — 파괴된 대상을 도중에 걷어내도 인덱스가 안 밀린다.
            for (int i = _registered.Count - 1; i >= 0; i--)
            {
                ITapTargetable candidate = _registered[i];

                // 해제를 깜빡한 대상 자동 청소(풀 반납/파괴 후 남은 찌꺼기).
                //
                // 구현체가 두 종류라 검사도 두 갈래다.
                //  · MonoBehaviour 구현체(NPC): 인터페이스 타입 참조에는 Unity가 오버로드한 ==(파괴 여부까지
                //    보는 것)가 안 걸리고 순수 참조 비교가 쓰인다. UnityEngine.Object로 캐스팅해야 파괴를 잡는다.
                //    이걸 먼저 걸러야 한다 — 파괴된 뒤 Anchor(내부적으로 transform)에 손대면 예외가 난다.
                //  · 순수 C# 어댑터(몬스터): 애초에 UnityEngine.Object가 아니므로 참조 null만 보면 되고,
                //    대상이 사라졌는지는 바로 아래 Anchor 검사가 판별한다.
                bool destroyed = candidate is Object unityObject ? unityObject == null : candidate is null;
                if (destroyed || candidate.Anchor == null)
                {
                    _registered.RemoveAt(i);
                    continue;
                }

                if (!candidate.IsSelectable)
                    continue;

                if (!TryGetHitDepth(cam, candidate, screenPos, out float depth))
                    continue;

                // 겹쳐 있으면 앞에 있는 놈이 이긴다.
                // 기존 NPC 코드는 '피벗의 화면 거리'로 승자를 정했는데, 둘이 가까우면 거의 동률이라
                // 카메라 미세 이동이나 Idle 모션만으로 매번 결과가 뒤집혔다(랜덤의 진짜 원인).
                // 카메라 깊이는 그런 식으로 흔들리지 않는다.
                if (depth < bestDepth)
                {
                    bestDepth = depth;
                    winner = candidate;
                }
            }

            winner?.OnTapped();
            _onTargetTapped.OnNext(winner);
        }

        /// <summary>
        /// 탭 지점에서 쏜 광선이 대상의 바운드를 맞혔는지 검사한다.
        /// depth는 실제로 맞은 지점의 카메라 거리(작을수록 앞).
        /// </summary>
        private static bool TryGetHitDepth(Camera cam, ITapTargetable target, Vector2 screenPos, out float depth)
        {
            depth = 0f;
            Transform anchor = target.Anchor;

            Ray worldRay = cam.ScreenPointToRay(screenPos);

            // 상자를 화면으로 끌고 오는 대신 광선을 상자의 로컬 공간으로 끌고 간다.
            // 이러면 Bounds가 자기 공간에서 계산하므로 min/max 두 점만으로 충분해지고,
            // 회전/스케일은 이 변환이 이미 흡수한 상태가 된다.
            // 방향은 InverseTransformDirection이 아니라 InverseTransformVector를 쓴다 —
            // 전자는 스케일을 무시해서, 원점만 스케일 보정된 광선이 만들어져 방향이 틀어진다.
            Ray localRay = new(
                anchor.InverseTransformPoint(worldRay.origin),
                anchor.InverseTransformVector(worldRay.direction));

            Bounds local = target.LocalBounds;
            local.Expand(TapTolerance * 2f);   // Expand는 각 축을 양쪽으로 절반씩 늘린다

            if (!local.IntersectRay(localRay, out float localDistance))
                return false;

            // localDistance는 로컬 스케일 기준이라 크기가 다른 대상끼리 비교하면 안 된다.
            // 맞은 지점을 월드로 되돌려 카메라와의 실제 거리를 잰다.
            Vector3 worldHit = anchor.TransformPoint(localRay.GetPoint(localDistance));
            depth = Vector3.Distance(cam.transform.position, worldHit);
            return true;
        }

        #endregion
    }
}