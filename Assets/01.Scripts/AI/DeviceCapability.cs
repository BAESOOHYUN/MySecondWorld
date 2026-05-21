using UnityEngine;

namespace MySecondWorld.AI
{
    public enum ModelSize { Large, Small }

    public interface IDeviceCapability
    {
        public bool IsCapable { get; }
        public string Reason { get; }
        public ModelSize RecommendedModelSize { get; }
        public bool UseServerMode { get; } // 서버 AI 모드(Groq) 사용 여부. true:서버, false:로컬
        public void Evaluate();
    }

    public class LocalDeviceCapability : IDeviceCapability
    {
        public bool IsCapable { get; private set; }  // 이 기기에서 로컬 AI 추론 가능한가?
        public string Reason { get; private set; } // 왜 AI 추론이 불가능한지
        public ModelSize RecommendedModelSize { get; private set; }
        public bool UseServerMode => false;  // 로컬 모드 전용 — 항상 false
        public void Evaluate()
        {
            int memoryMB = SystemInfo.systemMemorySize;

            if (memoryMB < 3500)
            {
                IsCapable = false;
                Reason = $"RAM 부족: {memoryMB}MB (최소 3.5GB 필요)";
                return;
            }
            
            if (memoryMB < 6000)
            {
                // Gemma 3 4B 모델을 CPU로 추론할 때 필요한 최소 RAM이 약 5~6GB이기 때문.
                // 모델 파일 자체: ~2.5GB(Q4_K_M 양자화), 추론 중 워킹 메모리: ~3GB, OS + 게임 실행: ~2GB
                // 기기 총 RAM이 6GB 미만이면 추론 중 OOM(메모리 부족)으로 크래시날 가능성이 높다.
                IsCapable = true;
                RecommendedModelSize = ModelSize.Small;
                Reason = $"RAM {memoryMB}MB — 소형 모델(1B) 사용";
                return;
            }

            IsCapable = true;
            RecommendedModelSize = ModelSize.Large;
            Reason = $"RAM {memoryMB}MB — 대형 모델(4B) 사용";
        }
    }

    // TODO
    public class NetworkDeviceCapability : IDeviceCapability
    {
        public bool IsCapable { get; private set; }
        public string Reason { get; private set; }
        public ModelSize RecommendedModelSize { get; private set; }
        public bool UseServerMode => true;  // 서버 모드 전용 — 항상 true
        public void Evaluate()
        {
            // 네트워크 연결이 있으면 Groq API 사용 가능으로 판정
            IsCapable = true;
            Reason = "서버 AI 모드 (Groq API)";
            // 서버 모드에서는 모델 크기 개념이 없음 — 기본값 유지
        }
    }
}