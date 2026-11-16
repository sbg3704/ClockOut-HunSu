// 기능: 지금 실행 중인 훈수꾼의 판 표시를 한 곳에서 준다. 판 번호(Directory.Build.props의 Version) 뒤에 "이 PR의 몇 번째 작업"을 글자(a, b, c...)로, 이어서 co 꼬리표(pr20)와 짧은 커밋을 붙인다.
// 사용법: BuildInfo.Text → "v0.0.020c pr20 · abc1234" (main과 같으면 글자 없음: "v0.0.020 · abc1234"),  BuildInfo.Version → "0.0.020"
// 의존: 어셈블리 InformationalVersion(csproj의 HunsuStamp가 "판+[꼬리표.]개수.커밋"으로 채움). 파일을 읽거나 엑셀을 부르지 않는다.
using System.Reflection;

namespace ClockOut.Hunsu.Shell;

internal static class BuildInfo
{
    private static readonly string Full =
        typeof(BuildInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(BuildInfo).Assembly.GetName().Version?.ToString(3) ?? "?";

    /// <summary>판 번호만(0.0.020).</summary>
    public static string Version => Full.Split('+')[0];

    /// <summary>사람에게 보일 표시.</summary>
    public static string Text => Core.BuildLabel.Format(Full);
}
