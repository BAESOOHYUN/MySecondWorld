using System.Linq;
using UnityEngine;

namespace MySecondWorld.AI
{
    // 모든 NPC가 공유하는 프롬프트 규칙·샘플링 값
    [CreateAssetMenu(fileName = "NPCCommonRulesSO", menuName = "NPC/Common Rules")]
    public class NPCCommonRulesSO : ScriptableObject
    {
        [Header("언어")]
        [TextArea(2, 4)]
        [SerializeField]
        private string languageRule =
            "반드시 한글 한국어로만 답한다. 한자·중국어·영어 단어를 섞지 않는다.";

        [Header("상점 추천")]
        [TextArea(4, 10)]
        [SerializeField]
        private string shopRules =
            "위 목록은 이 손님의 성별에 맞는 상품만 추린 것이다. 손님이 다른 성별 전용을 찾으면 맞지 않아 보여 드릴 수 없다고 말하고 목록에서 맞는 상품을 권한다.\n" +
            "손님이 상품을 추천해 달라고 하면 목록에서 하나만 골라 이유와 함께 한 문장으로 권하고 응답 맨 끝에 반드시 [[추천:번호]]를 붙인다. 예) 방어력이 좋은 철 투구를 추천해요! [[추천:3]]\n" +
            "손님이 찾는 조건의 상품이 목록에 없으면 그런 상품은 없다고 짧게 말하고 '이 상점이 파는 부위' 중 무엇을 보여 드릴지 되묻는다. 이때는 [[추천:번호]]를 붙이지 않는다. 같은 부위의 다른 상품이 있을 때만 권하고 태그를 붙인다. 직전에 권한 상품을 계속 다시 권하지 말고 다른 상품을 먼저 권한다. 어떤 경우에도 빈 답은 하지 않는다.";
        [Tooltip("손님 말에 이 중 하나가 들어 있어야 추천 카드를 띄운다")]
        [SerializeField]
        private string[] recommendKeywords =
            { "추천", "골라", "어울", "뭐가 좋", "괜찮", "보여", "보고 싶", "살만", "다른 거",
            "사고 싶", "살게", "살래", "구매", "구입", "얼마", "가격" };

        [Header("샘플링 (Groq)")]
        [Range(0f, 1.5f)][SerializeField] private float temperature = 0.6f;
        [Range(0f, 1f)][SerializeField] private float topP = 0.8f;
        [SerializeField] private string model = "qwen/qwen3.8-27b"; // ★ 가장 안정적인 한국어 지원 대화 모델
        [Tooltip("none = 생각 모드 끔. <think>가 새면 이 값을 확인")]
        [SerializeField] private string reasoningEffort = "none";

        public string LanguageRule => languageRule;
        public string ShopRules => shopRules;
        public float Temperature => temperature;
        public float TopP => topP;
        public string Model => model;
        public string ReasoningEffort => reasoningEffort;
        public string SamplingJson =>
            $",\"reasoning_effort\":\"{reasoningEffort}\"" +
            $",\"temperature\":{temperature.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}" +
            $",\"top_p\":{topP.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}";
        // reasoning_effort: 생각을 얼마나 하게 할지 (<think>...</think>로 속으로 길게 고민할지 말지)
        // temperature: 다음 글자를 얼마나 무작위로 고를지. (값이 높으면 창의적이지만 엉뚱해지고, 낮으면 가장 그럴듯한 것만 골라서 안정적이지만 단조로워짐. 1.0은 보통, 0.6은 차분한 편, 0은 항상 같은 답)
        // top_p: 후보를 어디서 자를지. (확률이 높은 순으로 줄 세워서, 누적 확률이 이 값(0.8)에 닿을 때까지만 후보로 인정.확률이 낮은 이상한 글자들은 아예 뽑히지 않도록)
        public bool HasShopIntent(string userInput) =>
            !string.IsNullOrEmpty(userInput) && recommendKeywords.Any(k => userInput.Contains(k));
    }
}