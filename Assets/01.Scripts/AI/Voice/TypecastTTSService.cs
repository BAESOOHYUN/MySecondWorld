using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace MySecondWorld.AI.Voice
{
    public class TypecastTTSService : MonoBehaviour, ITTSService
    {
        private const string Url = "https://api.typecast.ai/v1/text-to-speech";
        private const string DefaultVoiceId = "tc_694395d43f2c8d9d43e9a897"; // SO에 보이스가 비어 있을 때의 기본값
                                                                             // 웹 (https://studio.typecast.ai/developers/api/voices)에서 복사
        private string _voiceId = DefaultVoiceId;

        // SemaphoreSlim(n): 동시에 n개의 작업만 통과시키는 문.
        // WaitAsync()로 입장, Release()로 퇴장하고, 자리가 없으면 await로 줄을 선다.
        // (CharacterCustomizerBase._appearanceLock은 n=1이라 "한 번에 하나"를 보장하는 락으로 쓰인다)
        private static readonly SemaphoreSlim _gate = new(2); // Typecast 무료 플랜은 API 키 하나당 동시 요청 2건까지만 허용한다.
                                                              // NPC마다 이 서비스 인스턴스가 따로 생기므로 static으로 모든 NPC가 같은 문(2자리)을 공유해야 한다.
                                                              // 유료 플랜으로 올리면 이 숫자도 플랜의 동시 호출 수에 맞춰 올린다.

        private static string _apiKey;

        [Serializable] private class Prompt { public string emotion_preset = "normal"; public float emotion_intensity = 1f; }
        [Serializable] private class Output { public string audio_format = "wav"; }
        [Serializable] private class Req
        {
            public string voice_id;
            public string text;
            public string model = "ssfm-v30";
            public string language = "kor";
            public Prompt prompt = new();
            public Output output = new();
        }

        public void Init(string voiceId)
        {
            if (!string.IsNullOrEmpty(voiceId)) _voiceId = voiceId;
        }

        public async UniTask<AudioClip> FetchAsync(string text, CancellationToken ct = default)
        {
            _apiKey ??= MySecondWorld.AI.SecretKeyLoader.Load("typecast_api_key");
            if (string.IsNullOrEmpty(_apiKey)) return null;

            string json = JsonUtility.ToJson(new Req { voice_id = _voiceId, text = text });
            await _gate.WaitAsync(ct);
            try
            {
                using var req = new UnityWebRequest(Url, "POST");
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.downloadHandler = new DownloadHandlerAudioClip("", AudioType.WAV);
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("X-API-KEY", _apiKey);

                await req.SendWebRequest().ToUniTask(cancellationToken: ct);

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[Typecast] 실패 {req.responseCode} : {req.error}");
                    return null; // NPCVoicePlayer가 말풍선만 표시
                }
                return DownloadHandlerAudioClip.GetContent(req);
            }
            finally { _gate.Release(); }
        }
    }
}
