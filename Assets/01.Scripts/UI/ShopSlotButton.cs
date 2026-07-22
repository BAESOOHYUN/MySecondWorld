using System;
using UnityEngine;
using UnityEngine.UI;

namespace MySecondWorld.UI
{
    public class ShopSlotButton : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private Button _buyButton;

        public void Setup(Sprite icon, Action onBuy)
        {
            _icon.sprite = icon;
            _icon.enabled = icon != null;
            _buyButton.onClick.RemoveAllListeners();
            _buyButton.onClick.AddListener(() => onBuy?.Invoke());
        }
    }
}