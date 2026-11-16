using MySecondWorld.Inventory;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MySecondWorld.Combat.Vfx
{
    // WeaponCategory → 공격 스윙 VFX 프리팹 매핑. WeaponAnimationMapSO(Network 폴더)와 동일한
    // "리스트 순회 조회 + 매핑 안 되면 null" 구조를 그대로 따른다.
    [CreateAssetMenu(fileName = "WeaponAttackVfxMapSO", menuName = "Combat/Weapon Attack Vfx Map")]
    public class WeaponAttackVfxMapSO : ScriptableObject
    {
        [Serializable]
        private struct Entry
        {
            public WeaponCategory Category;
            public GameObject VfxPrefab;
        }

        [SerializeField] private List<Entry> _entries = new();

        // 매핑 안 된 Category(Mace/Hammer/Staff 등 MainHand 장착 대상이 아닌 값)나
        // 등록 안 한 값이 들어오면 null 반환 — 호출부에서 null 체크로 "재생 안 함" 처리.
        public GameObject GetVfxPrefab(WeaponCategory category)
        {
            foreach (var entry in _entries)
            {
                if (entry.Category == category)
                    return entry.VfxPrefab;
            }
            return null;
        }
    }
}
