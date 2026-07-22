using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MySecondWorld.UI
{
    public class InventorySlotButton : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _stackSizeText;
        [SerializeField] private Button _discardButton;
        [SerializeField] private Button _equipButton; // 파츠 아이템일 때만 노출

        public void Setup(Sprite icon, int stackSize, bool isPart, Action onDiscard, Action onEquip)
        { 
            _icon.sprite = icon;
            _icon.enabled = icon != null;
            _stackSizeText.text = stackSize > 1 ? stackSize.ToString() : string.Empty;
            _discardButton.onClick.RemoveAllListeners();
            _discardButton.onClick.AddListener(() => onDiscard?.Invoke());
            _equipButton.onClick.RemoveAllListeners();
            if(isPart)
                _equipButton.onClick.AddListener(() => onEquip?.Invoke());
        }
    }
}