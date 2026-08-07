using LLMUnity;

namespace MySecondWorld.AI
{
    // 서버 모드에서는 실제 대화가 클라우드LLM서비스(GroqLLMService)로 처리되므로 로컬 LLM Awake 시도 자체를 건너뛴다.
    // LLMInitializer가 서버 모드에서 LLM GameObject를 활성화하지 않아 AssignLLM()이
    // 항상 실패하고 에러를 던지던 문제를 근본적으로 차단.
    public class ServerAwareLLMAgent : LLMAgent
    {
        public override void Awake()
        {
            if (ServerModeFlag.UseServerMode)
            {
                enabled = false;
                return;
            }
            base.Awake();
        }
    }
}
