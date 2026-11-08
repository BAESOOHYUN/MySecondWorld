using MySecondWorld.Customization;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace MySecondWorld.UI
{
    // 썸네일 스크롤뷰 안에 동적으로 생성되는 아이템 1개
    public class PartThumbnailButton : MonoBehaviour
    {
        [SerializeField] private Image _thumbnail; // 썸네일 스프라이트
        [SerializeField] private GameObject _selectedFrame; // 선택 강조 테두리 (껐다 켬)
        [SerializeField] private Button _button;
        [SerializeField] private Sprite _noneSprite;
        [SerializeField] private GameObject _lockIcon; // 미소유 표시 오버레이
        [SerializeField] private GenderBadge _genderBadge;

        [SerializeField] private TMPro.TextMeshProUGUI _label; // 카드 하단 라벨 (선택적)
        [SerializeField] private Image _rarityBorder; // 등급별 테두리 (선택적)
        [SerializeField] private Image _cardBg;

        public PartType Part { get; private set; }

        public int Index { get; private set; }

        public void Setup(PartType part, int index, Sprite sprite, Action<PartType, int> onClick)
        {
            Part = part;
            Index = index;
            _thumbnail.sprite = sprite != null ? sprite : _noneSprite; // null이면 X 아이콘
            _thumbnail.enabled = sprite != null; // 못 받은 슬롯은 빈칸 처리
            _thumbnail.enabled = _thumbnail.sprite != null; // 스프라이트가 배정되어 있으면 Image 활성화
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(() => onClick?.Invoke(Part, Index));
        }

        public void SetLabel(string text)
        {
            if (_label != null)
            {
                _label.text = text;
                _label.gameObject.SetActive(!string.IsNullOrEmpty(text));
            }
        }

        public void SetRarity(Color color)
        {
            if (_rarityBorder != null)
            {
                // 일반(흰색) 등급이나 투명색은 상단 띠를 표시하지 않음 (카드 전체가 하얗게 덮이는 현상 방지)
                bool hasSpecialRarity = color != Color.white && color != Color.clear &&
                                        !(color.r > 0.92f && color.g > 0.92f && color.b > 0.92f);

                if (hasSpecialRarity)
                {
                    _rarityBorder.color = color;
                    _rarityBorder.gameObject.SetActive(true);
                }
                else
                {
                    _rarityBorder.gameObject.SetActive(false);
                }
            }
        }

        public void SetSelected(bool selected)
        {
            if (_selectedFrame != null)
                _selectedFrame.SetActive(selected);
        }

        public void SetLocked(bool locked)
        {
            if (_lockIcon != null)
                _lockIcon.SetActive(locked);

            // 썸네일: 미소유는 어둡고 차분하게 딤드(0.55 alpha), 소유는 100% 선명하고 밝게
            if (_thumbnail != null)
                _thumbnail.color = locked ? new Color(0.28f, 0.30f, 0.35f, 0.55f) : Color.white;

            // 카드 배경: 미소유는 짙은 암회색, 소유는 정갈한 네이비 슬레이트
            if (_cardBg != null)
                _cardBg.color = locked ? new Color(0.06f, 0.08f, 0.11f, 0.98f) : new Color(0.11f, 0.14f, 0.20f, 0.98f);

            // 하단 텍스트 라벨: 미소유는 흐린 회청색, 소유는 또렷한 백청색
            if (_label != null)
                _label.color = locked ? new Color(0.45f, 0.52f, 0.62f, 0.85f) : new Color(0.88f, 0.93f, 1f, 0.98f);
        }

        // 성별 배지: Male/Female이면 해당 스프라이트로 바꿔 보이고, null(공용·없음)이면 숨긴다
        public void SetGenderBadge(Gender? gender) => _genderBadge?.Set(gender);
    }
}