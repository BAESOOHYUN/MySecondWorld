using UnityEngine;
using Mirror;
using System.Collections.Generic;
using System.Collections;

/*
  전체 흐름

  클라이언트 연결
      ↓
  Mirror가 OnClientAuthenticate() 자동 호출   ← 클라이언트 인증 시작 진입점
      ↓
  내가 NetworkClient.Send(AuthRequestMessage) 작성
      ↓
  서버의 OnAuthRequestMessage(conn, msg) 실행
      ↓
  내가 ServerAccept(conn) 호출              ← 서버가 "통과"라고 Mirror에 알림
      ↓
  내가 conn.Send(AuthResponseMessage) 전송
      ↓
  클라이언트의 OnAuthResponseMessage(msg) 실행
      ↓
  내가 ClientAccept() 호출                  ← 클라이언트가 "완료"라고 Mirror에 알림
*/

namespace MySecondWorld.Network.Chat
{
    public class ChatAuthenticator : NetworkAuthenticator
    {
        // 이름 중복 검사에 사용할 컬렉션
        public static readonly HashSet<string> playerNames = new HashSet<string>();
        // 이름이 중복되어 요청이 거절된 사람 명단
        private readonly HashSet<NetworkConnectionToClient> connectionsPendingDisconnect = new HashSet<NetworkConnectionToClient>();

        // Client → Server : 이름 보냄
        public struct AuthRequestMessage : NetworkMessage 
        { 
            public string authUsername;
        }

        // Server → Client : 성공/실패 응답
        public struct AuthResponseMessage : NetworkMessage
        {
            public const byte Success = 100;
            public const byte Failure = 200;
            public byte code;
        }

        #region 서버
        public override void OnStartServer() //클라이언트가 보낼 AuthRequestMessage 핸들러 등록
        {
            // requireAuthentication = false의 의미 : 인증 전에도 이 메시지를 받겠다는 뜻
            // 인증 요청 메시지인데 "인증된 클라이언트만 받겠다"고 하면 닭이 먼저냐 달걀이 먼저냐 문제가 된다.
            // 인증하려면 메시지를 받아야 하고, 메시지를 받으려면 인증이 되어야 하고.
            // 반면 일반 게임 메시지들은 true(기본값)로 두어서 인증된 클라이언트만 처리한다.
            NetworkServer.RegisterHandler<AuthRequestMessage>(OnAuthRequestMessage, false);
        }

        public override void OnStopServer() //핸들러 해제
        {
            NetworkServer.UnregisterHandler<AuthRequestMessage>();
        }

        public override void OnServerAuthenticate(NetworkConnectionToClient conn)
        {
            //일단 빈 채로 (서버는 메시지가 올 때까지 기다리기만 함)
            Debug.Log($"[Auth] OnServerAuthenticate conn={conn.connectionId}");
        }

        // 실제 인증 처리 메세지
        // 이름 중복 확인 후 ServerAccept(conn) 또는 거부
        public void OnAuthRequestMessage(NetworkConnectionToClient conn, AuthRequestMessage msg)
        {
            Debug.Log($"[Auth] OnAuthRequestMessage conn={conn.connectionId}, name='{msg.authUsername}'");
            // 이름 중복으로, 거절된 사람 명단에 있으면 무시
            if (connectionsPendingDisconnect.Contains(conn))
                return;

            // 인증요청을 보낸 사람의 이름이 playerNames에 포함되지 않았다면 추가.
            if (!playerNames.Contains(msg.authUsername))
            {
                playerNames.Add(msg.authUsername);


                // authenticationData는 object type.
                // 지금은 이름만 필요하니까 string 하나지만,
                // 실제 게임이라면 username, userId, sessionToken, permissionLevel 등이 담길수 있다.
                conn.authenticationData = msg.authUsername;

                // 성공 응답 생성
                AuthResponseMessage authResponseMessage = new AuthResponseMessage
                {
                    code = AuthResponseMessage.Success
                };

                //클라이언트에게 성공 응답 보내기
                conn.Send(authResponseMessage);

                // 서버가 "이 클라이언트 인증 통과"라고 Mirror에 알림
                // NetworkAuthenticator 상속 클래스
                Debug.Log($"[Auth] ServerAccept: '{msg.authUsername}'");
                ServerAccept(conn);
            }
            else //이름이 이미 사용중인 경우 
            {
                // Add하여 (이 클라이언트의 연결동안) '이 conn은 거절 처리 중'임을 알림
                connectionsPendingDisconnect.Add(conn);

                // 실패 응답 생성
                AuthResponseMessage authResponseMessage = new AuthResponseMessage
                {
                    code = AuthResponseMessage.Failure
                };

                // 클라이언트에게 실패 응답 보내기
                conn.Send(authResponseMessage);

                // Mirror는 인증 성공 시 conn.isAuthenticated = true로 자동 설정한다.
                // else (거부)쪽에서 명시적으로 false로 되돌려
                // ServerReject 전에 확실하게 "이 연결은 인증 안 됨" 상태로 표시해두는 안전장치
                conn.isAuthenticated = false;

                // 클라이언트에게 연결 종료(거부) 이유 알려주기 위해 1초 딜레이
                StartCoroutine(DelayedDisconnect(conn, 1f));
            }
        }

        private IEnumerator DelayedDisconnect(NetworkConnectionToClient conn, float waitTime)
        {
            yield return new WaitForSeconds(waitTime);

            // 대기 중 클라이언트가 먼저 연결을 끊었을 경우 conn이 무효화될 수 있다
            if (conn == null) 
                yield break;

            // NetworkAuthenticator 상속 클래스
            ServerReject(conn);
            // 실제 연결 해제는 그 프레임이 끝날 때 Mirror가 네트워크 루프를 돌면서 처리하기 때문에
            // ServerReject(conn) 호출 직후는 "연결 해제 예약됨" 상태지, 아직 완전히 끊긴 게 아니다.
            // 그래서 yield return null로 한 프레임을 넘겨서,
            // Mirror가 disconnect를 완전히 처리한 뒤에 다음 줄로 넘어가는 것
            yield return null;

            //이제 완전히 끊겼으니 'conn의 거절 처리' 추적 필요 없음
            connectionsPendingDisconnect.Remove(conn);
        }
        #endregion

        #region 클라이언트
        public override void OnStartClient()
        {
            NetworkClient.RegisterHandler<AuthResponseMessage>(OnAuthResponseMessage, false);
        }

        public override void OnStopClient()
        {
            NetworkClient.UnregisterHandler<AuthResponseMessage>();
        }

        // 서버 연결되자마자 호출됨. Mirror 자동호출
        public override void OnClientAuthenticate()
        {
            Debug.Log($"[Auth] OnClientAuthenticate localPlayerName='{ChatUI.instance.localPlayerName}'");
            // 이름 없이 연결 시도하면 null이 서버 playerNames에 등록될 수 있으므로 사전 차단
            if (string.IsNullOrWhiteSpace(ChatUI.instance.localPlayerName))
            {
                Debug.LogWarning("[Auth] localPlayerName 비어있음 → 인증 메시지 미전송");
                return;
            }

            NetworkClient.Send(new AuthRequestMessage { authUsername = ChatUI.instance.localPlayerName });
        }

        public void OnAuthResponseMessage(AuthResponseMessage msg)
        {
            Debug.Log($"[Auth] OnAuthResponseMessage code={msg.code}");
            if (msg.code == AuthResponseMessage.Success)
            {
                // Mirror에 "클라이언트 인증 완료" 알림
                ClientAccept();
            }
            else
            {
                // Mirror에 "클라이언트 인증 실패" 알림
                ClientReject();
            }
        }
        #endregion    
    }
}