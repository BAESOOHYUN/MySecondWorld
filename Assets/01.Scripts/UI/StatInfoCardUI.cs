using UnityEngine;
using TMPro;
using MySecondWorld.Inventory.Items;
using MySecondWorld.Inventory;
using System.Collections.Generic;

namespace MySecondWorld.UI
{
    /// <summary>
    /// 캐릭터 프로필, 종합 전투력, 5대 핵심 스탯(공격력, 방어력, 체력, 이동속도, 행운)을 표시하며,
    /// 아이템 선택 시 실시간 증감치(+35 🟢 / -15 🔴)를 시각적으로 미리보기합니다.
    /// </summary>
    public class StatInfoCardUI : MonoBehaviour
    {
        [Header("프로필 & 전투력")]
        [SerializeField] private TMP_Text _characterNameText;
        [SerializeField] private TMP_Text _characterClassText;
        [SerializeField] private TMP_Text _combatPowerText;

        [Header("상세 스탯 텍스트")]
        [SerializeField] private TMP_Text _attackText;
        [SerializeField] private TMP_Text _defenseText;
        [SerializeField] private TMP_Text _maxHpText;
        [SerializeField] private TMP_Text _moveSpeedText;
        [SerializeField] private TMP_Text _luckText;

        // 현재 적용 중인 기본 스탯
        private float _baseAtk = 50f;
        private float _baseDef = 30f;
        private float _baseHp = 1000f;
        private float _baseSpeed = 100f;
        private float _baseLuck = 10f;

        // 장비로 인한 보정치 합산
        private float _equipAtk;
        private float _equipDef;
        private float _equipHp;
        private float _equipSpeed;
        private float _equipLuck;

        // 미리보기 델타
        private float _deltaAtk;
        private float _deltaDef;
        private float _deltaHp;
        private float _deltaSpeed;
        private float _deltaLuck;
        private bool _hasDelta;

        private void Awake()
        {
            UpdateDisplay();
        }

        public void SetProfile(string characterName, string className)
        {
            if (_characterNameText != null) _characterNameText.text = characterName;
            if (_characterClassText != null) _characterClassText.text = className;
        }

        /// <summary>
        /// 장착 중인 모든 장비 리스트를 전달받아 스탯을 갱신합니다.
        /// </summary>
        public void UpdateEquippedStats(IEnumerable<EquipmentItem> equippedItems)
        {
            _equipAtk = 0;
            _equipDef = 0;
            _equipHp = 0;
            _equipSpeed = 0;
            _equipLuck = 0;

            if (equippedItems != null)
            {
                foreach (var eq in equippedItems)
                {
                    if (eq == null || eq.Stats == null) continue;
                    foreach (var sm in eq.Stats)
                    {
                        ApplyStatModifier(sm, ref _equipAtk, ref _equipDef, ref _equipHp, ref _equipSpeed, ref _equipLuck);
                    }
                }
            }

            _hasDelta = false;
            UpdateDisplay();
        }

        /// <summary>
        /// 특정 아이템을 장착했을 때 스탯 증감치(+delta / -delta)를 계산하여 표시합니다.
        /// </summary>
        public void SetPreviewDelta(EquipmentItem newItem, EquipmentItem currentItem)
        {
            _deltaAtk = 0;
            _deltaDef = 0;
            _deltaHp = 0;
            _deltaSpeed = 0;
            _deltaLuck = 0;
            _hasDelta = false;

            if (newItem == null && currentItem == null)
            {
                UpdateDisplay();
                return;
            }

            // 기존 아이템 차감
            if (currentItem != null && currentItem.Stats != null)
            {
                foreach (var sm in currentItem.Stats)
                {
                    ApplyStatModifier(new StatModifier { Type = sm.Type, Value = -sm.Value },
                        ref _deltaAtk, ref _deltaDef, ref _deltaHp, ref _deltaSpeed, ref _deltaLuck);
                }
            }

            // 새 아이템 가산
            if (newItem != null && newItem.Stats != null)
            {
                foreach (var sm in newItem.Stats)
                {
                    ApplyStatModifier(sm, ref _deltaAtk, ref _deltaDef, ref _deltaHp, ref _deltaSpeed, ref _deltaLuck);
                }
            }

            _hasDelta = true;
            UpdateDisplay();
        }

        /// <summary>
        /// 미리보기 증감치를 초기화하고 정상 스탯으로 복귀합니다.
        /// </summary>
        public void ClearPreviewDelta()
        {
            _hasDelta = false;
            _deltaAtk = 0;
            _deltaDef = 0;
            _deltaHp = 0;
            _deltaSpeed = 0;
            _deltaLuck = 0;
            UpdateDisplay();
        }

        private void ApplyStatModifier(StatModifier sm, ref float atk, ref float def, ref float hp, ref float spd, ref float luck)
        {
            switch (sm.Type)
            {
                case StatType.Attack:
                case StatType.MagicAttack:
                    atk += sm.Value;
                    break;
                case StatType.Defense:
                case StatType.MagicDefense:
                    def += sm.Value;
                    break;
                case StatType.MaxHP:
                    hp += sm.Value;
                    break;
                case StatType.MoveSpeed:
                    spd += sm.Value;
                    break;
                case StatType.Luck:
                    luck += sm.Value;
                    break;
            }
        }

        private void UpdateDisplay()
        {
            float curAtk = _baseAtk + _equipAtk;
            float curDef = _baseDef + _equipDef;
            float curHp = _baseHp + _equipHp;
            float curSpeed = _baseSpeed + _equipSpeed;
            float curLuck = _baseLuck + _equipLuck;

            float curPower = CalculatePower(curAtk, curDef, curHp, curSpeed, curLuck);

            if (_hasDelta)
            {
                float nextPower = CalculatePower(curAtk + _deltaAtk, curDef + _deltaDef, curHp + _deltaHp, curSpeed + _deltaSpeed, curLuck + _deltaLuck);
                float deltaPower = nextPower - curPower;

                if (_combatPowerText != null) _combatPowerText.text = FormatValueWithDelta(curPower, deltaPower, false);
                if (_attackText != null) _attackText.text = FormatValueWithDelta(curAtk, _deltaAtk, false);
                if (_defenseText != null) _defenseText.text = FormatValueWithDelta(curDef, _deltaDef, false);
                if (_maxHpText != null) _maxHpText.text = FormatValueWithDelta(curHp, _deltaHp, false);
                if (_moveSpeedText != null) _moveSpeedText.text = FormatValueWithDelta(curSpeed, _deltaSpeed, false);
                if (_luckText != null) _luckText.text = FormatValueWithDelta(curLuck, _deltaLuck, false);
            }
            else
            {
                if (_combatPowerText != null) _combatPowerText.text = $"{curPower:N0}";
                if (_attackText != null) _attackText.text = $"{curAtk:0}";
                if (_defenseText != null) _defenseText.text = $"{curDef:0}";
                if (_maxHpText != null) _maxHpText.text = $"{curHp:N0}";
                if (_moveSpeedText != null) _moveSpeedText.text = $"{curSpeed:0}";
                if (_luckText != null) _luckText.text = $"{curLuck:0}";
            }
        }

        private float CalculatePower(float atk, float def, float hp, float speed, float luck)
        {
            return (atk * 2.5f) + (def * 2.0f) + (hp * 0.5f) + (speed * 1.0f) + (luck * 3.0f);
        }

        private string FormatValueWithDelta(float current, float delta, bool isPercentage)
        {
            string unit = isPercentage ? "%" : "";
            if (Mathf.Abs(delta) < 0.01f)
            {
                return $"{current:0}{unit}";
            }

            if (delta > 0)
            {
                return $"{current:0}{unit} <color=#4ade80>(+{delta:0}{unit})</color>";
            }
            else
            {
                return $"{current:0}{unit} <color=#f87171>({delta:0}{unit})</color>";
            }
        }
    }
}
