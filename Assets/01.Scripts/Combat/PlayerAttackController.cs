using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using MySecondWorld.Network;
using MySecondWorld.Move;

namespace MySecondWorld.Combat
{
    // 입력 발생 → 쿨다운 체크 → 애니메이션 트리거 → (애니메이션의 타격 프레임에서) 범위 안 후보 탐지 → SkillContext 구성 → SkillExecutor.Execute()
    public class PlayerAttackController : MonoBehaviour
    {
        #region Fields

        [Header("참조")]
        [SerializeField] private NetworkWeaponEquip _weaponEquip;
        [SerializeField] private PlayerAttackAuthority _attackAuthority;
        [SerializeField] private PlayerAnimationController _animationController;
        [SerializeField] private InputActionReference _attackAction;

        [Header("판정")]
        [SerializeField] private Transform _attackPoint;
        [SerializeField] private float _attackRadius = 1.5f;
        [SerializeField] private float _unarmedAttack = 10f; // 무기 미장착(맨주먹) 시 기본 공격력
        [SerializeField] private LayerMask _targetLayerMask;

        [Header("전투 상태")]
        [SerializeField] private float _combatStateDuration = 3f; // 마지막 공격 후 이 시간 동안 "전투 중"으로 간주
        private float _lastAttackTime = float.NegativeInfinity;
        public bool IsInCombat => Time.time - _lastAttackTime < _combatStateDuration;

        [Header("쿨다운")]
        [SerializeField] private float _attackCooldown = 0.8f;

        private SkillExecutor _skillExecutor;
        private float _nextAttackTime;

        #endregion

        #region Unity 생명주기

        private void Awake()
        {
            _skillExecutor = new SkillExecutor(
                new SingleTargetSelector(),
                new InstantCast(),
                new List<ISkillEffect> { new DamageEffect() }
                );
        }

        private void OnEnable()
        {
            if (_weaponEquip == null || _attackAuthority == null || _attackAction == null || _animationController == null || _attackPoint == null)
            {
                Debug.LogError($"PlayerAttackController 참조 누락 on {this.gameObject.name}", this);
                enabled = false;
                return;
            }

            _attackAction.action.Enable();
            _attackAction.action.performed += OnAttackInput;
        }

        private void OnDisable()
        {
            if (_attackAction != null)
                _attackAction.action.performed -= OnAttackInput;
        }

        #endregion

        #region 입력 처리

        private void OnAttackInput(InputAction.CallbackContext ctx)
        {
            if (!_weaponEquip.isLocalPlayer)
                return; // 원격 플레이어 — 내 입력에 반응하면 안 됨

            if (Time.time < _nextAttackTime) return;

            _nextAttackTime = Time.time + _attackCooldown;
            _lastAttackTime = Time.time;
            _animationController.HandleAttack();
        }

        #endregion

        #region 공격 판정

        // Attack 애니메이션 클립의 타격 프레임에 Animation Event로 연결
        public void OnAttackHit()
        {
            if (!_weaponEquip.isLocalPlayer)
                return;

            // 무기 미장착 시 맨주먹 공격력 사용
            float power = _weaponEquip.GetMainHandAttack();
            if (power <= 0f)
                power = _unarmedAttack;

            _attackAuthority.CmdResolveAttackHit(_attackPoint.position, _attackRadius, _unarmedAttack);
        }

        #endregion
    }
}