using System;
using Cysharp.Threading.Tasks;
using Mirror;
using MySecondWorld.Network;
using UnityEngine;

namespace MySecondWorld.Combat.Player
{
    // 스킬 슬롯: 평타(PlayerAttackAuthority)와 별개로, 슬롯 번호로 발동하는 추가 스킬.
    // 클라는 "몇 번 슬롯을 누를지"만 보내고, 스킬 내용·공격력·쿨다운은 전부 서버가 자기 데이터로 정한다.
    public class PlayerSkillSlots : NetworkBehaviour
    {
        [Header("슬롯 (배열 순서 = 슬롯 번호. PC 숫자키 1~N)")]
        [SerializeField] private SlotSkill[] _slots = new SlotSkill[4];

        [Header("판정")]
        [Tooltip("슬롯 스킬이 대상 후보를 찾는 반경. 중심은 클라가 보낸 조준 지점(서버가 거리 검증).")]
        [SerializeField] private float _castRadius = 3f;
        [Tooltip("공격력 폴백용. 무기가 없을 때 쓴다.")]
        [SerializeField] private PlayerCombatStatsSO _stats;

        private const float MaxCastReach = 6f;     // 시전자와 조준 지점의 최대 거리(조작 방어)
        private const float CooldownTolerance = 0.9f; // 네트워크 지연을 감안해 서버 쿨다운을 10% 느슨하게

        private NetworkWeaponEquip _weaponEquip;
        private Animator _animator;
        private static int _monsterLayerMask = -1;
        private static int MonsterLayerMask => _monsterLayerMask != -1 ? _monsterLayerMask : (_monsterLayerMask = LayerMask.GetMask("Monster"));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => _monsterLayerMask = -1;

        // 슬롯별 진행 상태(콤보 단계·쿨다운). Host는 클라와 서버가 같은 오브젝트라서, 한 배열을 같이 쓰면
        // 클라가 갱신한 상태를 서버가 보고 거부한다. 반드시 분리.
        private SlotCursor[] _clientCursor;  // 클라(UI·입력 차단용)
        private SlotCursor[] _serverCursor;  // 서버(검증용)

        public int SlotCount => _slots.Length;
        public SlotSkill GetSkill(int slot) => (slot >= 0 && slot < _slots.Length) ? _slots[slot] : null;

        // 슬롯 UI가 쿨다운 오버레이를 그릴 때 쓴다. (슬롯 번호, 쿨다운 길이)
        public event Action<int, float> SlotCast;

        private void Awake()
        {
            _weaponEquip = GetComponent<NetworkWeaponEquip>();
            _animator = GetComponentInChildren<Animator>();
            _clientCursor = new SlotCursor[_slots.Length];
            _serverCursor = new SlotCursor[_slots.Length];
        }

        public float GetCooldownRemaining(int slot)
        {
            if (slot < 0 || slot >= _clientCursor.Length) return 0f;
            return Mathf.Max(0f, _clientCursor[slot].ReadyAt - Time.time);
        }

        // 입력(숫자키/아이콘 터치)이 부르는 클라 진입점. 로컬 플레이어만.
        // 로컬에서 먼저 쿨다운을 걸어 연타를 막지만, 진짜 검증은 서버가 한다.
        public void TryCastSlot(int slot, Vector3 aimPoint, uint preferredNetId = 0)
        {
            if (!isLocalPlayer) { Debug.LogWarning($"[Slot] 클라 차단: 로컬 플레이어가 아님 ({name})"); return; }
            var skill = GetSkill(slot);
            if (skill == null) { Debug.LogWarning($"[Slot] 클라 차단: 슬롯 {slot}이 비어 있음(_slots 연결 확인)"); return; }
            // 콤보면 단계가 넘어가고 쿨다운이 걸린다. null이면 아직 입력을 받을 수 없는 상태.
            var step = skill.TryAdvance(ref _clientCursor[slot], Time.time, 1f);
            if (step == null) { Debug.Log($"[Slot] 클라 차단: 쿨다운 {GetCooldownRemaining(slot):F2}초 남음"); return; }
            Debug.Log($"[Slot] 클라 전송: 슬롯 {slot} ({step.DisplayName}), 조준 지점 {aimPoint}");

            SlotCast?.Invoke(slot, skill.Cooldown);
            CmdCastSlot(slot, aimPoint, preferredNetId);
        }

        [Command]
        private void CmdCastSlot(int slot, Vector3 aimPoint, uint preferredNetId)
        {
            // Command에서 예외가 나면 Mirror가 연결을 끊는다 — 범위 밖 값은 조용히 무시.
            var entry = GetSkill(slot);
            if (entry == null) { Debug.LogWarning($"[Slot] 거부: 슬롯 {slot} 비어 있음/범위 밖"); return; }
            float dist = Vector3.Distance(transform.position, aimPoint);
            if (dist > MaxCastReach) { Debug.LogWarning($"[Slot] 거부: 조준 지점이 {dist:F1}m (최대 {MaxCastReach}m)"); return; }

            // 서버가 자기 커서로 따로 판정한다. 콤보 몇 단계인지도 클라가 보낸 값이 아니라 서버 상태가 정한다.
            var skill = entry.TryAdvance(ref _serverCursor[slot], Time.time, CooldownTolerance);
            if (skill == null) { Debug.LogWarning($"[Slot] 거부: 슬롯 {slot} 쿨다운 {_serverCursor[slot].ReadyAt - Time.time:F2}초 남음"); return; }

            float power = _weaponEquip != null ? _weaponEquip.GetMainHandAttack() : 0f;
            if (power <= 0f && _stats != null) power = _stats.UnarmedAttack;

            Transform preferred = null;
            if (preferredNetId != 0 && NetworkServer.spawned.TryGetValue(preferredNetId, out var identity))
                preferred = identity.transform;

            var candidates = SphereAttackResolver.QueryCandidates(gameObject, aimPoint, _castRadius, MonsterLayerMask, preferred);
            var ctx = new SkillContext(gameObject, candidates, power, null, _animator);
            ctx.SetEpicenter(aimPoint, _castRadius);
            Debug.Log($"[Slot] 발동: 슬롯 {slot} {skill.DisplayName}, power={power}, 후보 {candidates.Count}명");
            skill.Execute(ctx).Forget();
        }
    }
}
