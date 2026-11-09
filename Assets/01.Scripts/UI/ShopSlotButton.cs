using System;
using UnityEngine;
using UnityEngine.UI;
using MySecondWorld.Customization;

namespace MySecondWorld.UI
{
    public class ShopSlotButton : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private Button _buyButton;
        [SerializeField] private Button _slotButton; 
        [SerializeField] private GenderBadge _genderBadge;

        public void Setup(Sprite icon, Action onBuy)
        {
            _icon.sprite = icon;
            _icon.enabled = icon != null;

            if (_buyButton != null)
            {
                _buyButton.onClick.RemoveAllListeners();
                _buyButton.onClick.AddListener(() => onBuy?.Invoke());
            }

            if (_slotButton != null)
            {
                _slotButton.onClick.RemoveAllListeners();
                _slotButton.onClick.AddListener(() => onBuy?.Invoke());
            }
        }

        public void SetGenderBadge(Gender? gender) => _genderBadge?.Set(gender);
    }
}