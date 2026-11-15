using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MySecondWorld.Customization;

namespace MySecondWorld.UI
{
    public class InventorySlotButton : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _stackSizeText;
        [SerializeField] private Button _discardButton;
        [SerializeField] private Button _actionButton; // 파츠면 "장착", 소모품이면 "사용"
        [SerializeField] private TMP_Text _actionButtonText;
        [SerializeField] private GenderBadge _genderBadge;

        public void SetGenderBadge(Gender? gender) => _genderBadge?.Set(gender);

        public void Setup(Sprite icon, int stackSize,
                        bool isPart, bool isConsumable, bool isEquipped, 
                        Action onDiscard, Action onAction, bool isShopMode = false)
        {
            _icon.sprite = icon;
            _icon.enabled = icon != null;
            _stackSizeText.text = stackSize > 1 ? stackSize.ToString() : string.Empty;
            _discardButton.onClick.RemoveAllListeners();
            _discardButton.onClick.AddListener(() => onDiscard?.Invoke());

            var discardImg = _discardButton.GetComponent<Image>();
            if (discardImg != null)
            {
                discardImg.color = isShopMode 
                    ? new Color(1.0f, 0.82f, 0.2f, 1f)   // 상점 판매 모드: 골드 틴트
                    : new Color(0.85f, 0.25f, 0.25f, 0.9f); // 일반 모드: 레드 틴트
            }
            _actionButton.onClick.RemoveAllListeners();
            var actionImage = _actionButton.GetComponent<Image>();
            if (isPart)
            {
                _actionButton.gameObject.SetActive(true);
                _actionButtonText.text = isEquipped ? "해제" : "장착";
                if (actionImage != null)
                {
                    actionImage.color = isEquipped
                        ? new Color(0.95f, 0.45f, 0.25f, 1f)  // 해제: 코랄/오렌지
                        : new Color(0.18f, 0.68f, 0.95f, 1f); // 장착: 스카이블루
                }
                _actionButton.onClick.AddListener(() => onAction?.Invoke());
            }
            else if (isConsumable)
            {
                _actionButton.gameObject.SetActive(true);
                _actionButtonText.text = "사용";
                if (actionImage != null)
                {
                    actionImage.color = new Color(0.2f, 0.82f, 0.5f, 1f); // 사용: 에메랄드 그린
                }
                _actionButton.onClick.AddListener(() => onAction?.Invoke());
            }
            else
            {
                _actionButton.gameObject.SetActive(false);
            }
        }
    }
}