using System;
using UnityEngine;

namespace MySecondWorld.Combat
{
    /// <summary>
    /// 애니메이션 클립에서 발동하는 Animation Event를 수신하는 컴포넌트.
    /// Dedicated Server 환경에서 클라이언트 측 프리팹의 "has no receiver" 에러를 방지하고,
    /// 필요 시 사운드/이펙트 연출 또는 서버 넉백/타격 로직에 이벤트를 중계한다.
    /// </summary>
    public class AnimationEventReceiver : MonoBehaviour
    {
        #region Events

        public event Action OnAttackHitEvent;
        public event Action OnAttackNockEvent;
        public event Action OnAttackReleaseEvent;
        public event Action OnAttackShootEvent; // 기타 클립용 예비 훅
        public event Action OnSlamImpactEvent;

        #endregion

        #region Animation Event Handlers

        // 근접 몬스터 이벤트 (MonsterHitbox 대응)
        public void OnAttackHit() { }

        // 원거리 몬스터 이벤트 (RangedMonsterHitbox 대응)
        public void OnAttackNock() { }
        public void OnAttackRelease() { }
        public void OnAttackShoot() { }

        // 보스 광역 이벤트 (BossAoEHitbox 대응)
        public void OnSlamImpact() { }

        // 사망 몬스터 이벤트 (클라이언트에서 has no receiver 방지용)
        public void OnDeathAnimationEnd() { }

        #endregion
    }
}