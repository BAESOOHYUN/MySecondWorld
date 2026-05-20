using UnityEngine;
using TMPro;

namespace MySecondWorld.UI
{
    public class ChatBubble : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _text;

        public void SetText(string  text) => _text.text = text;
        public void AppendText(string text) => _text.text += text;
    }
}