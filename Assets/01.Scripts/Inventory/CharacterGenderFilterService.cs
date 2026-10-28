using System;
using UnityEngine;
using R3;
using VContainer;

namespace MySecondWorld.Inventory
{
    public enum CharacterGenderFilter
    {
        All,
        Female,
        Male
    }

    /// <summary>
    /// 인벤토리와 상점에서 공유하는 성별 필터 상태.
    /// 한쪽에서 성별을 변경하면 다른 쪽도 자동으로 동기화된다.
    /// </summary>
    public class CharacterGenderFilterService
    {
        private readonly Subject<CharacterGenderFilter> _onGenderFilterChanged = new();
        private CharacterGenderFilter _currentFilter = CharacterGenderFilter.All;

        public CharacterGenderFilter CurrentFilter => _currentFilter;
        public Observable<CharacterGenderFilter> OnGenderFilterChanged => _onGenderFilterChanged;

        public void SetGenderFilter(CharacterGenderFilter filter)
        {
            if (_currentFilter == filter) return;

            _currentFilter = filter;
            _onGenderFilterChanged.OnNext(_currentFilter);
        }
    }
}