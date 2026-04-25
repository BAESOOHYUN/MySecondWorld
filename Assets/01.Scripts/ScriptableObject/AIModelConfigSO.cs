using UnityEngine;

[CreateAssetMenu(fileName = "AIModelConfigSO", menuName = "NPC/AIModelConfig")]
public class AIModelConfigSO : ScriptableObject
{
    [Tooltip("다운로드 URL (CDN 주소)")]
    public string modelUrl = "https://huggingface.co/bartowski/google_gemma-3-4b-it-GGUF/resolve/main/google_gemma-3-4b-it-Q4_K_M.gguf";

    [Tooltip("저장할 파일명 (예: gemma-3-4b-q4_k_m.gguf)")]
    public string modelFileName = "google_gemma-3-4b-it-Q4_K_M.gguf";

    [Tooltip("전체 파일 크기 (진행률 계산용)")]
    public long modelSizeBytes = 2490000000;

    [Header("소형 모델 (저 RAM 기기 - 4GB 미만)")]
    public string smallModelUrl = "https://huggingface.co/bartowski/google_gemma-3-1b-it-GGUF/resolve/main/google_gemma-3-1b-it-Q4_K_M.gguf";
    public string smallModelFileName = "google_gemma-3-1b-it-Q4_K_M.gguf";
    public long smallModelSizeBytes = 700000000; // 약 700MB
}
