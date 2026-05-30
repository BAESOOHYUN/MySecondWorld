namespace MySecondWorld.Bundle
{
    public struct AddressablesProgressEvent
    {
        public float Progress;
        public bool IsError;
        public string ErrorMessage;
        public bool NeedsRetry; // UI가 서비스에게 보내는 신호
    }
}

//[서비스 → UI 방향]
//AddressablesService
//  └→ IPublisher.Publish(new AddressablesProgressEvent { IsError = true })
//       └→ LoadingUI가 받아서 에러 팝업 + 재시도 버튼 표시

//[UI → 서비스 방향]
//LoadingUI (재시도 버튼 클릭)
//  └→ IPublisher.Publish(new AddressablesProgressEvent { NeedsRetry = true })
//       └→ AddressablesService가 받아서 재시도 로직 실행