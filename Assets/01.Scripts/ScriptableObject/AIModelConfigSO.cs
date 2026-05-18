using UnityEngine;

[CreateAssetMenu(fileName = "AIModelConfigSO", menuName = "NPC/AIModelConfig")]
public class AIModelConfigSO : ScriptableObject
{
    [Tooltip("다운로드 URL (CDN 주소)")]
    public string modelUrl = "https://huggingface.co/bartowski/google_gemma-3-4b-it-GGUF/resolve/main/google_gemma-3-4b-it-Q4_K_M.gguf";

    [Tooltip("저장할 파일명 (예: gemma-3-4b-q4_k_m.gguf)")]
    public string modelFileName = "google_gemma-3-4b-it-Q4_K_M.gguf";

    [Header("소형 모델 (저 RAM 기기 - 3.5GB 미만)")]
    public string smallModelUrl = "https://huggingface.co/bartowski/google_gemma-3-1b-it-GGUF/resolve/main/google_gemma-3-1b-it-Q4_K_M.gguf";
    public string smallModelFileName = "google_gemma-3-1b-it-Q4_K_M.gguf";

    [Header("인증")]
    [Tooltip("HuggingFace Access Token — 속도 제한 완화용. 비워두면 무인증.")]
    public string huggingFaceToken = "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";

    [Header("STT 모델")]
    public string sttModelUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.bin";
    public string sttModelFileName = "ggml-tiny.bin";
}
