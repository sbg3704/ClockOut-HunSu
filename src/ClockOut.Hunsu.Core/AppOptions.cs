// 기능: 훈수꾼 실행 인자를 읽는다. 인자가 없으면 보통 모드(보이는 엑셀 창을 찾아 따라감).
// 사용법: var o = AppOptions.Parse(args); o.FollowHwnd, o.LogPath
//   --follow <창핸들(10진/0x16진)>  이 창을 따라감(측정 도구가 만든 시험 창 등)
//   --log <파일>                    측정용 기록을 남김(셀 값은 적지 않음)
//   --snapshot <폴더>               처음 그린 오버레이를 PNG로 저장(화면 확인용)
// 의존: 없음(순수 계산). 모르는 인자는 Errors에 모으고 무시한다.
using System.Globalization;

namespace ClockOut.Hunsu.Core;

public sealed record AppOptions(long FollowHwnd, string? LogPath, string? SnapshotDir, IReadOnlyList<string> Errors)
{
    public static AppOptions Parse(IReadOnlyList<string> args)
    {
        long follow = 0;
        string? log = null, snap = null;
        var errors = new List<string>();
        for (int i = 0; i < args.Count; i++)
        {
            string a = args[i];
            string? next = i + 1 < args.Count ? args[i + 1] : null;
            switch (a)
            {
                case "--follow" when next != null && TryParseHandle(next, out var h):
                    follow = h; i++; break;
                case "--log" when next != null:
                    log = next; i++; break;
                case "--snapshot" when next != null:
                    snap = next; i++; break;
                default:
                    errors.Add(a); break;
            }
        }
        return new AppOptions(follow, log, snap, errors);
    }

    public static bool TryParseHandle(string text, out long value)
    {
        text = text.Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return long.TryParse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value) && value != 0;
        return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value != 0;
    }
}
