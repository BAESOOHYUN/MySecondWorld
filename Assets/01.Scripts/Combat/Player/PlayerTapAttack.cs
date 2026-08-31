using Cysharp.Threading.Tasks;   // AddTo(destroyCancellationToken)
using Mirror;
using R3;
using UnityEngine;
using MySecondWorld.Combat.Monster;
using MySecondWorld.Interaction;

namespace MySecondWorld.Combat.Player
{
    /// <summary>
    /// 탭한 몬스터를 선택 대상으로 삼고 즉시 공격까지 건다. 로컬 플레이어에서만 동작한다.
    /// </summary>
    [RequireComponent(typeof(PlayerAttackController))]
    public class PlayerTapAttack : NetworkBehaviour
    {
        private PlayerAttackController _attackController;
        private MonsterServerState _selectedState;   // 사망 신호 구독 해제용

        private void Awake() => _attackController = GetComponent<PlayerAttackController>();

        public override void OnStartLocalPlayer()
        {
            var resolver = FindAnyObjectByType<TapTargetResolver>();
            if (resolver == null)
            {
                Debug.LogError("[PlayerTapAttack] TapTargetResolver를 찾지 못했습니다.", this);
                return;
            }

            //Debug.Log("[PlayerTapAttack] 탭 구독 시작", this);

            resolver.OnTargetTapped
                .Subscribe(HandleTargetTapped)
                .AddTo(destroyCancellationToken);
        }

        private void HandleTargetTapped(ITapTargetable target)
        {
            if (target is not MonsterTapTarget monster)
            {
                //Debug.Log($"[PlayerTapAttack] 몬스터 아님 — 무시 ({(target == null ? "빈 곳" : target.Anchor.name)})", this);
                return;
            }

            Select(monster);

            bool inRange = _attackController.IsInAttackRange(monster.Anchor);

            if (!inRange) return;

            _attackController.TryAttack();
        }
        private void OnDisable() => ClearSelection();

        private void Select(MonsterTapTarget monster)
        {
            ClearSelection();
            _attackController.SelectedTarget = monster.Anchor;

            if (monster.TryGetComponent(out MonsterServerState state))
            {
                _selectedState = state;
                _selectedState.Died += ClearSelection;   // 죽는 즉시 자동 해제
            }
        }

        private void ClearSelection()
        {
            if (_selectedState != null)
            {
                _selectedState.Died -= ClearSelection;
                _selectedState = null;
            }

            if (_attackController != null)
                _attackController.SelectedTarget = null;
        }
    }
}