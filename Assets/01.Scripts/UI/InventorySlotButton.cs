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
        [SerializeField] private Button _actionButton; // 파츠면 "장착", 소모품이면 "사용"
        [SerializeField] private TMP_Text _actionButtonText;

        public void Setup(Sprite icon, int stackSize, bool isPart, bool isConsumable, Action onDiscard, Action onAction)
        {
            _icon.sprite = icon;
            _icon.enabled = icon != null;
            _stackSizeText.text = stackSize > 1 ? stackSize.ToString() : string.Empty;
            _discardButton.onClick.RemoveAllListeners();
            _discardButton.onClick.AddListener(() => onDiscard?.Invoke());
            if (isPart)
            {
                _actionButton.gameObject.SetActive(true);
                _actionButtonText.text = "장착";
                _actionButton.onClick.AddListener(() => onAction?.Invoke());
            }
            else if (isConsumable)
            {
                _actionButton.gameObject.SetActive(true);
                _actionButtonText.text = "사용";
                _actionButton.onClick.AddListener(() => onAction?.Invoke());
            }
            else
            {
                _actionButton.gameObject.SetActive(false);
            }
        }
    }
}