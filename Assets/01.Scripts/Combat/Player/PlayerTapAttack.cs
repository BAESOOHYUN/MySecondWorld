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


            resolver.OnTargetTapped
                .Subscribe(HandleTargetTapped)
                .AddTo(destroyCancellationToken);
        }

        private void HandleTargetTapped(ITapTargetable target)
        {
            if (target == null) return; // 빈 곳 탭 → 유지 (카메라 조작 중 오인 해제 방지)

            // 다른 대상(NPC 등)을 고르면 몬스터 선택은 푼다.
            // 안 그러면 대화하러 간 사이에도 조준이 남아, 공격 입력이 엉뚱한 데로 나간다.
            if (target is not MonsterTapTarget monster)
            {
                ClearSelection();
                return;
            }

            Select(monster);

            // 사거리 밖이면 선택만 하고 공격은 걸지 않는다 — 헛스윙 방지.
            // 다가가서 공격하면 이미 선택돼 있으니 그놈이 맞는다.
            if (!_attackController.IsInAttackRange(monster.Anchor)) return;

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