using Cysharp.Threading.Tasks;
using UnityEngine;
using R3;
using MySecondWorld.Combat.Monster;
using MySecondWorld.Combat.Player;
using MySecondWorld.Interaction;

namespace MySecondWorld.UI
{
    /// <summary>
    /// 탭으로 고른 몬스터 또는 조준/공격 중인 자동 타겟 몬스터 위에 조준점(인디케이터)을 띄운다.
    /// 선택은 항상 한 명이므로 인스턴스도 하나면 된다.
    /// HP바는 MonsterHealthBarSpawner가 몬스터마다 따로 붙인다 — 여기서 다루지 않는다.
    /// </summary>
    [RequireComponent(typeof(ScreenFollowUI))]
    public class MonsterTargetUI : MonoBehaviour
    {
        private ScreenFollowUI _follow;
        private MonsterServerState _state;
        private PlayerAttackController _attackController;
        private Transform _currentTarget;

        private void Awake() => _follow = GetComponent<ScreenFollowUI>();

        private void Start()
        {
            var resolver = FindAnyObjectByType<TapTargetResolver>();
            if (resolver == null)
            {
                Debug.LogError("[MonsterTargetUI] TapTargetResolver를 찾지 못했습니다.", this);
            }
            else
            {
                resolver.OnTargetTapped
                    .Subscribe(HandleTargetTapped)
                    .AddTo(destroyCancellationToken);
            }

            if (PlayerAttackController.Local != null)
                SubscribeToAttackController(PlayerAttackController.Local);

            PlayerAttackController.LocalReady += SubscribeToAttackController;
        }

        private void OnDestroy()
        {
            PlayerAttackController.LocalReady -= SubscribeToAttackController;
            UnsubscribeFromAttackController();
        }

        private void OnDisable() => Clear();

        private void SubscribeToAttackController(PlayerAttackController controller)
        {
            UnsubscribeFromAttackController();
            _attackController = controller;
            if (_attackController != null)
            {
                _attackController.OnCurrentTargetChanged += HandleCurrentTargetChanged;
            }
        }

        private void UnsubscribeFromAttackController()
        {
            if (_attackController != null)
            {
                _attackController.OnCurrentTargetChanged -= HandleCurrentTargetChanged;
                _attackController = null;
            }
        }

        private void HandleTargetTapped(ITapTargetable target)
        {
            // PlayerTapAttack의 선택 규칙과 같아야 조준점과 실제 선택이 어긋나지 않는다.
            // 빈 곳 탭은 유지, 다른 대상 선택은 해제(결정 ②).
            if (target == null) return;
            if (target is not MonsterTapTarget monster)
            {
                Clear();
                return;
            }

            SetTargetTransform(monster.Anchor);
        }

        private void HandleCurrentTargetChanged(Transform target)
        {
            if (target != null)
            {
                SetTargetTransform(target);
            }
            else
            {
                // 실시간 타겟이 사라졌을 때: 수동 탭 선택(SelectedTarget)이 남아있고 살아있으면 복귀, 아니면 숨김
                var selected = _attackController != null ? _attackController.SelectedTarget : null;
                if (selected != null && selected.gameObject.activeInHierarchy)
                {
                    SetTargetTransform(selected);
                }
                else
                {
                    Clear();
                }
            }
        }

        private void SetTargetTransform(Transform targetTransform)
        {
            if (targetTransform == null)
            {
                Clear();
                return;
            }

            if (_currentTarget == targetTransform) return;

            Clear();
            _currentTarget = targetTransform;
            _follow.SetTarget(targetTransform);

            _state = targetTransform.GetComponent<MonsterServerState>() ?? targetTransform.GetComponentInParent<MonsterServerState>();
            if (_state != null)
                _state.Died += Clear;     // 죽으면 조준점만 걷는다 (HP바는 Layer가 따로 정리)
        }

        private void Clear()
        {
            if (_state != null)
            {
                _state.Died -= Clear;
                _state = null;
            }
            _currentTarget = null;
            _follow.SetTarget(null);
        }
    }
}