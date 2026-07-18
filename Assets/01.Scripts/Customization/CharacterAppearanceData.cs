using System;
using UnityEngine;

namespace MySecondWorld.Customization
{
    // IEquatable = 객체가 동일한 타입의 다른 객체와 같은지(객체의 동등성(Value Equality)) 여부를
    // 효율적이고 안전하게 비교할 수 있도록 정의하는 제네릭 인터페이스.
    // 1. 박싱 및 언박싱 방지 : object.Equals()를 사용하면 값 형식(Struct)을 객체로 변환하는 과정에서 박싱이 발생함
    //    IEquatable<T>를 구현하면 컴파일 타임에 타입을 명확히 알기 때문에 박싱 없이 비교를 수행하여 성능이 향상됨
    // 2. 타입 안전성 : object.Equals()은 매개변수로 object를 받기 때문에 실수로 엉뚱한 타입(예: string과 int)을 비교해도 컴파일 에러를 발생시키지 않는다. 
    //    반면 IEquatable<T>는 다른 타입을 전달하면 컴파일 단계에서 오류를 잡아낼 수 있다.
    // 3. Dictionary<TKey, TValue>나 List<T>.Contains() 같은 제네릭 컬렉션 내부에서는 요소를 비교할 때
    //    IEquatable<T>가 구현되어 있는지 먼저 확인하고, 구현되어 있다면 이를 우선적으로 호출하여 성능을 최적화
    // 4. struct(값형)는 기본적으로 모든 필드를 리플렉션으로 하나하나 비교하는 Equals()를 자동 제공하는데, 이건 느리다. (리플렉션 = 런타임에 타입 정보 뒤지는 거라 비용 큼)
    //    Equatable<T>를 구현하면, "필드를 어떻게 비교할지" 직접 코드로 적어서 리플렉션 없이 빠르게 비교하게 만들 수 있다

    // 이 프로젝트에서 쓰는 이유 : SyncVar hook 중복 방지
    // Mirror의 [SyncVar(hook=...)]는 값이 "바뀌었을 때"만 hook 함수를 호출하는데, 이때 "바뀌었나?"를 판단하려고 내부적으로(리플렉션으로) Equals()를 호출한다.
    // IEquatable<T>를 구현 안 하면 기본 리플렉션 비교를 쓰게 되고, 매 변경마다(파츠 n개+색상 3개 필드) 느린 비교가 발생.
    // 직접 구현하면 필드 하나하나 ==로 빠르게 비교 가능.

    // JsonUtility.ToJson() 동작 +Inspector 노출
    [System.Serializable]
    public struct CharacterAppearanceData : IEquatable<CharacterAppearanceData>
    {
        // 파츠 인덱스 (PartType과 이름 일치)
        // AvatarPartKeyTableSO.GetCandidates(PartType.Head, gender)가 반환하는 리스트의 n번째 원소
        // CharacterAppearanceData.Head = 3이라는 값 자체는 그냥 숫자일 뿐이고, 아무 의미가 없다.
        // 이 숫자가 실제로 "어떤 머리 프리팹인지"를 가지려면 반드시 AvatarPartKeyTableSO를 거쳐서 해석해야 한다.
        public int Hair;
        public int Head;
        public int Eyebrows;
        public int FacialHair;
        public int Torso;
        public int ArmUpperRight;
        public int ArmUpperLeft;
        public int ArmLowerRight;
        public int ArmLowerLeft;
        public int HandLeft;
        public int HandRight;
        public int Hips;
        public int LegLeft;
        public int LegRight;

        public int Ear;
        public int HeadCoverings;
        public int BackAttachment;
        public int ShoulderAttachLeft;
        public int ShoulderAttachRight;
        public int ElbowAttachLeft;
        public int ElbowAttachRight;
        public int KneeAttachLeft;
        public int KneeAttachRight;
        public int HelmetAttachment;
        public int HipsAttachment;
        
        // 성별
        public Gender Gender;

        // 색상
        public Color PrimaryColor;
        public Color SecondaryColor;
        public Color LeatherPrimaryColor;
        public Color LeatherSecondaryColor;
        public Color MetalPrimaryColor;
        public Color MetalSecondaryColor;
        public Color MetalDarkColor;
        public Color HairColor;
        public Color SkinColor;
        public Color StubbleColor;
        public Color ScarColor;
        public Color EyesColor;
        public Color BodyArtColor;

        public bool Equals(CharacterAppearanceData other)
        {
            return Hair == other.Hair
                && Head == other.Head
                && Eyebrows == other.Eyebrows
                && FacialHair == other.FacialHair
                && Torso == other.Torso
                && ArmUpperRight == other.ArmUpperRight
                && ArmUpperLeft == other.ArmUpperLeft
                && ArmLowerRight == other.ArmLowerRight
                && ArmLowerLeft == other.ArmLowerLeft
                && HandLeft == other.HandLeft
                && HandRight == other.HandRight
                && Hips == other.Hips
                && LegLeft == other.LegLeft
                && LegRight == other.LegRight
                && Ear == other.Ear
                && HeadCoverings == other.HeadCoverings
                && BackAttachment == other.BackAttachment
                && ShoulderAttachLeft == other.ShoulderAttachLeft
                && ShoulderAttachRight == other.ShoulderAttachRight
                && ElbowAttachLeft == other.ElbowAttachLeft
                && ElbowAttachRight == other.ElbowAttachRight
                && KneeAttachLeft == other.KneeAttachLeft
                && KneeAttachRight == other.KneeAttachRight
                && HelmetAttachment == other.HelmetAttachment
                && HipsAttachment == other.HipsAttachment
                && Gender == other.Gender
                && PrimaryColor == other.PrimaryColor
                && SecondaryColor == other.SecondaryColor
                && LeatherPrimaryColor == other.LeatherPrimaryColor
                && LeatherSecondaryColor == other.LeatherSecondaryColor
                && MetalPrimaryColor == other.MetalPrimaryColor
                && MetalSecondaryColor == other.MetalSecondaryColor
                && MetalDarkColor == other.MetalDarkColor
                && HairColor == other.HairColor
                && SkinColor == other.SkinColor
                && StubbleColor == other.StubbleColor
                && ScarColor == other.ScarColor
                && EyesColor == other.EyesColor
                && BodyArtColor == other.BodyArtColor;
        }

        public override bool Equals(object obj)
        {
            // is 패턴 매칭.
            // 1. obj가 CharacterAppearanceData인가?
            // 2. 맞으면, 그 값을 CharacterAppearanceData 타입으로 캐스팅해서 other라는 새 변수에 즉시 할당

            // Equals(other)는 위의 bool Equals(CharacterAppearanceData other)를 가리킴
            return obj is CharacterAppearanceData other && Equals(other);
        }

        // Color는 GetHashCode에서 제외. (Color 정밀도 이슈)
        // Color는 내부적으로 r, g, b, a 4개의 float으로 저장.
        // float은 정확한 값이 아니라 근사값이라서, 같은 색상이라도 계산 경로가 다르면 비트 단위로 살짝 다른 값이 나올 수 있다.
        // 미세한 float 차이 때문에, 0.50000001과 0.49999999처럼 "사실상 같은 색"인데 해시코드가 달라지는 경우가 생김.
        // GetHashCode 계약과의 충돌 = NET 규칙: "Equals()가 true를 반환하는 두 값은 반드시 같은 해시코드를 내야 한다."
        public override int GetHashCode()
        {
            // 해시값 계산은 int 오버플로우가 나는 게 정상 동작
            // unchecked로 감싸두면 그 설정과 무관하게 항상 안전하게 오버플로우를 허용함
            // HashCode 구조체 자체도 내부적으로 unchecked를 쓰고 있어서 사실 이중 방어이긴 하지만, 명시적으로 감싸두는 게 안전한 습관
            unchecked
            {
                var hash = new HashCode();
                hash.Add(Hair);
                hash.Add(Head);
                hash.Add(Eyebrows);
                hash.Add(FacialHair);
                hash.Add(Torso);
                hash.Add(ArmUpperRight);
                hash.Add(ArmUpperLeft);
                hash.Add(ArmLowerRight);
                hash.Add(ArmLowerLeft);
                hash.Add(HandLeft);
                hash.Add(HandRight);
                hash.Add(Hips);
                hash.Add(LegLeft);
                hash.Add(LegRight);
                hash.Add(Ear);
                hash.Add(HeadCoverings);
                hash.Add(BackAttachment);
                hash.Add(ShoulderAttachLeft);
                hash.Add(ShoulderAttachRight);
                hash.Add(ElbowAttachLeft);
                hash.Add(ElbowAttachRight);
                hash.Add(KneeAttachLeft);
                hash.Add(KneeAttachRight);
                hash.Add(HelmetAttachment);
                hash.Add(HipsAttachment);
                hash.Add(Gender);
                return hash.ToHashCode();
            }
        }

        // 인덱서 추가
        // 인덱서 : 배열 int[], string[] 등에서 쓰는 [] 문법을 내가 만든 class/struct에도 쓸 수 있게 해줌
        // this = CharacterAppearanceData struct 자기자신
        // ex) _currentAppearance[part] 와 같이 사용한다.
        public int this[PartType part]
        {
            get => part switch
            {

                PartType.Hair => Hair,
                PartType.Head => Head,
                PartType.Eyebrows => Eyebrows,
                PartType.FacialHair => FacialHair,
                PartType.Torso => Torso,
                PartType.ArmUpperRight => ArmUpperRight,
                PartType.ArmUpperLeft => ArmUpperLeft,
                PartType.ArmLowerRight => ArmLowerRight,
                PartType.ArmLowerLeft => ArmLowerLeft,
                PartType.HandLeft => HandLeft,
                PartType.HandRight => HandRight,
                PartType.Hips => Hips,
                PartType.LegLeft => LegLeft,
                PartType.LegRight => LegRight,
                PartType.Ear => Ear,
                PartType.HeadCoverings => HeadCoverings,
                PartType.BackAttachment => BackAttachment,
                PartType.ShoulderAttachLeft => ShoulderAttachLeft,
                PartType.ShoulderAttachRight => ShoulderAttachRight,
                PartType.ElbowAttachLeft => ElbowAttachLeft,
                PartType.ElbowAttachRight => ElbowAttachRight,
                PartType.KneeAttachLeft => KneeAttachLeft,
                PartType.KneeAttachRight => KneeAttachRight,
                PartType.HelmetAttachment => HelmetAttachment,
                PartType.HipsAttachment => HipsAttachment,
                _ => 0,
            };
            set 
            {
                switch (part)
                {
                    case PartType.Hair: Hair = value; break;
                    case PartType.Head: Head = value; break;
                    case PartType.Eyebrows: Eyebrows = value; break;
                    case PartType.FacialHair: FacialHair = value; break;
                    case PartType.Torso: Torso = value; break;
                    case PartType.ArmUpperRight: ArmUpperRight = value; break;
                    case PartType.ArmUpperLeft: ArmUpperLeft = value; break;
                    case PartType.ArmLowerRight: ArmLowerRight = value; break;
                    case PartType.ArmLowerLeft: ArmLowerLeft = value; break;
                    case PartType.HandLeft: HandLeft = value; break;
                    case PartType.HandRight: HandRight = value; break;
                    case PartType.Hips: Hips = value; break;
                    case PartType.LegLeft: LegLeft = value; break;
                    case PartType.LegRight: LegRight = value; break;
                    case PartType.Ear: Ear = value; break;
                    case PartType.HeadCoverings: HeadCoverings = value; break;
                    case PartType.BackAttachment: BackAttachment = value; break;
                    case PartType.ShoulderAttachLeft: ShoulderAttachLeft = value; break;
                    case PartType.ShoulderAttachRight: ShoulderAttachRight = value; break;
                    case PartType.ElbowAttachLeft: ElbowAttachLeft = value; break;
                    case PartType.ElbowAttachRight: ElbowAttachRight = value; break;
                    case PartType.KneeAttachLeft: KneeAttachLeft = value; break;
                    case PartType.KneeAttachRight: KneeAttachRight = value; break;
                    case PartType.HelmetAttachment: HelmetAttachment = value; break;
                    case PartType.HipsAttachment: HipsAttachment = value; break;
                }
            }
        }
    }
}
