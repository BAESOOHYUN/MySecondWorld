using System;
using System.Collections.Generic;

namespace MySecondWorld.Network
{
    [Serializable]
    public class CredentialEntry
    {
        public string username;
        public string saltBase64; // 해싱하기 전에 비밀번호에 섞는 무작위 값.
                                  // 솔트 없이 그냥 해시만 하면 "1234"를 쓰는 두 사람의 해시값이 완전히 똑같이 나온다.
                                  // 사람마다 다른 솔트를 섞으면 같은 비밀번호라도 해시 결과가 전부 달라짐
        public string hashBase64; // 비밀번호를 원래대로 되돌릴 수 없는 뒤죽박죽 문자열로 바꿈
        public int iterations;    // 반복횟수 — 해싱을 한 번만 하는 게 아니라 지정한 횟수(코드에서 100,000번)만큼 반복 적용
                                  // 한 번 해싱은 컴퓨터가 눈 깜짝할 새 끝나서, 공격자가 비밀번호 후보를 초당 수백만 개씩 대입해볼 수 있다.
                                  // 일부러 100,000번 반복해서 "해시 하나 계산하는 데 시간이 걸리게" 만들면, 무차별 대입 공격 속도를 그만큼 늦출 수 있음.
    }

    // JsonUtility가 최상위 List/Dictionary를 직렬화 못 해서 두는 래퍼 (SaveData.cs와 동일 패턴)
    [Serializable]
    public class CredentialFile
    {
        public List<CredentialEntry> entries = new();
    }
}