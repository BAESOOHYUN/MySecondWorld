using System;
using System.Security.Cryptography;
using System.Text;

namespace MySecondWorld.Network
{
    public class SceneMatchId
    {

        /// <summary>
        /// 씬 경로(문자열)를 기반으로 고정된 Guid를 생성한다.
        /// 씬별 matchId (씬마다 고유 Guid)
        /// 같은 씬에 있는 오브젝트끼리 MatchInterestManagement observer로 묶기 위해 사용.
        /// 같은 씬에 있는 플레이어(+ NPC, 몬스터, 퀘스트 아이템 등)끼리만 매칭
        /// 나중에 대륙이 늘어도 자연스러움
        /// </summary>
        public static Guid GetMatchIdFromScene(string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath))
                return Guid.Empty;

            // 문자열을 바이트 배열로 변환
            byte[] inputBytes = Encoding.UTF8.GetBytes(scenePath);

            // MD5 해시 생성 (항상 16바이트 결과물을 반환함)
            using (MD5 md5 = MD5.Create())
            {
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                // 16바이트 해시를 Guid로 변환
                return new Guid(hashBytes);
            }
        }
    }
}