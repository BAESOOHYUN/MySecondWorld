using UnityEngine;

public interface IDeviceCapability
{
    public bool IsCapable { get; }
    public string Reason { get; }
    public void Evaluate();
}
public class LocalDeviceCapability : IDeviceCapability
{
    public bool IsCapable { get; private set; }  // 이 기기에서 로컬 AI 추론 가능한가?
    public string Reason { get; private set; } // 왜 AI 추론이 불가능한지

    public void Evaluate()
    {
        int memoryMB = SystemInfo.systemMemorySize;
        // Gemma 3 4B 모델을 CPU로 추론할 때 필요한 최소 RAM이 약 5~6GB이기 때문.
        // 모델 파일 자체: ~2.5GB(Q4_K_M 양자화), 추론 중 워킹 메모리: ~3GB, OS + 게임 실행: ~2GB
        // 기기 총 RAM이 6GB 미만이면 추론 중 OOM(메모리 부족)으로 크래시날 가능성이 높다.
        if (memoryMB < 6000)
        {
            IsCapable = false;
            Reason = $"RAM 부족: {memoryMB}MB";
            return;
        }
        IsCapable = true;
        Reason = "OK";
    }
}

// TODO
public class NetworkDeviceCapability : IDeviceCapability
{
    public bool IsCapable { get; private set; }
    public string Reason { get; private set; }

    public void Evaluate()
    {
        IsCapable = false;
        Reason = "미구현 (클라우드 AI 도입 시 구현 예정)";
    }
}