using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using MySecondWorld.Customization;
using System;

namespace MySecondWorld.UI
{
    public class ColorChannelRow : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private Image _previewImage;
        [SerializeField] private Button _button;

        private static readonly Dictionary<ColorTarget, string> _koreanNames = new()
        {
            { ColorTarget.Primary,          "주 색상" },
            { ColorTarget.Secondary,        "보조 색상" },
            { ColorTarget.LeatherPrimary,   "가죽 주색" },
            { ColorTarget.LeatherSecondary, "가죽 보조" },
            { ColorTarget.MetalPrimary,     "금속 주색" },
            { ColorTarget.MetalSecondary,   "금속 보조" },
            { ColorTarget.MetalDark,        "금속 어둠" },
            { ColorTarget.Hair,             "머리카락" },
            { ColorTarget.Skin,             "피부" },
            { ColorTarget.Stubble,          "수염 잔털" },
            { ColorTarget.Scar,             "흉터" },
            { ColorTarget.Eyes,             "눈동자" },
            { ColorTarget.BodyArt,          "문신" },
        };

        public void Setup(ColorTarget target, Color initialColor, Action<ColorTarget> onSelected)
        {
            _label.text = _koreanNames.TryGetValue(target, out var name) ? name : target.ToString();
            _previewImage.color = initialColor;
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(() => onSelected?.Invoke(target));
        }
        public void UpdatePreview(Color color)
        {
            _previewImage.color = color;
        }
    }
}