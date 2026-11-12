// 기능: 다리가 보내는 신호(창 메시지)의 lParam을 신호 번호와 순번으로 풀고 묶는다(docs/02 4장).
// 사용법: var (code, seq) = SignalCodec.Decode(lParam); SignalCodec.Encode(SignalCodec.Sel, 7)
// 의존: 없음(순수 계산). lParam 하위 8비트 = 신호 번호, 그다음 8비트 = 순번.
namespace ClockOut.Hunsu.Core;

public static class SignalCodec
{
    /// <summary>등록 메시지 이름. 다리(CoHunsuMail.bas)와 글자 하나까지 같아야 한다.</summary>
    public const string MessageName = "ClockOut.Hunsu.Signal";

    /// <summary>틀이 만드는 숨은 창의 클래스 이름. 다리가 FindWindow로 찾는다.</summary>
    public const string WindowClass = "ClockOutHunsuMsg";

    public const int Sel = 1;
    public const int Change = 2;
    public const int Sheet = 3;
    public const int Book = 4;
    public const int Calc = 5;
    public const int Mode = 6;

    public static long Encode(int code, int seq) => (long)(uint)((code & 0xFF) | ((seq & 0xFF) << 8));

    public static (int Code, int Seq) Decode(long lParam) => ((int)(lParam & 0xFF), (int)((lParam >> 8) & 0xFF));

    /// <summary>신호 번호를 규격의 이름으로. 모르는 번호는 "unknown".</summary>
    public static string NameOf(int code) => code switch
    {
        Sel => "sel",
        Change => "change",
        Sheet => "sheet",
        Book => "book",
        Calc => "calc",
        Mode => "mode",
        _ => "unknown",
    };
}
