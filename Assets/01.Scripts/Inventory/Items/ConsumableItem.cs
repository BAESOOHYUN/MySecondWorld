using System.Collections.Generic;
using Cleverous.NetworkImposter;
using Cleverous.VaultInventory;
using UnityEngine;

// 소모 아이템 
namespace MySecondWorld.Inventory.Items
{
    public class ConsumableItem : UseableItem
    {
        public AudioClip ConsumeSound;
        public float HealAmount; // (즉시 회복, 스탯 아님)
        public List<StatModifier> BuffStats = new List<StatModifier>();
        public float BuffDuration; // (사용 시 일정 시간 버프)

        public override void UseBegin(IUseInventory user)
        {
            // TODO: 사운드 재생은 전용 오디오 시스템 구축 후 ConsumeSound를 연결.
            // PlayClipAtPoint는 소비마다 GameObject 생성/파괴 + 서버(헤드리스)에서도 동일 실행되어 낭비됨.
            if (ConsumeSound != null)
                AudioSource.PlayClipAtPoint(ConsumeSound, user.MyTransform.position, 0.7f);

            if (NetworkPipeline.StaticIsServer())
            {
                user.Inventory.DoTake(this, 1);
            }
        }

        public override void UseFinish(IUseInventory user)
        {
            throw new System.NotImplementedException();
        }

        public override void UseCancel(IUseInventory user)
        {
            throw new System.NotImplementedException();
        }
    }
}