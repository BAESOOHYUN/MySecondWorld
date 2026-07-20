using MySecondWorld.Inventory;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MySecondWorld.Network
{
    [CreateAssetMenu(fileName = "WeaponAnimationMapSO", menuName = "Animation/Weapon Animation Map")]
    public class WeaponAnimationMapSO : ScriptableObject
    {
        [Serializable]
        private struct Entry
        {
            public WeaponCategory Category;
            public RuntimeAnimatorController Controller;
        }

        [SerializeField] private List<Entry> _entries = new();


        // 매핑 안 된 Category(Mace/Hammer/Staff/Sceptre 등, 애초에 MainHand 장착이 안 되는
        // 카테고리)나 등록 안 한 값이 들어오면 null 반환 — 호출부에서 null 체크로 "교체 안 함" 처리
        public RuntimeAnimatorController GetController(WeaponCategory category)
        {
            foreach (var entry in _entries)
            {
                if (entry.Category == category)
                    return entry.Controller;
            }
            return null;
        }
    }
}