// 기능: 자리 코드와 그 자리에 놓을 수 있는 모양(docs/01 5장 표)을 한 곳에 둔다.
// 사용법: Places.CanPlace("edge-right", ["tab","panel"]) → true;  Places.IsDock(place)
// 의존: 없음(순수 값). 자리 목록을 다른 파일에 다시 적지 않는다.
namespace ClockOut.Hunsu.Core.Layout;

public static class Places
{
    public const string EdgeLeft = "edge-left";
    public const string EdgeRight = "edge-right";
    public const string Pin = "pin";
    public const string Strip = "strip";
    public const string Mark = "mark";
    public const string Summon = "summon";
    public const string Second = "second";

    private static readonly Dictionary<string, string[]> ShapesFor = new()
    {
        [Mark] = ["mark"],
        [Strip] = ["chip"],
        [EdgeLeft] = ["tab", "panel"],
        [EdgeRight] = ["tab", "panel"],
        [Summon] = ["window", "panel"],
        [Pin] = ["window", "panel"],
        [Second] = ["window", "panel"],
    };

    public static bool IsDock(string place) => place is EdgeLeft or EdgeRight;

    /// <summary>그 자리에 놓을 모양이 선언에 하나라도 있는가. pin/summon/second는 window가 없으면 panel을 창에 넣는다(docs/01 5장).</summary>
    public static bool CanPlace(string place, IEnumerable<string> shapes) =>
        ShapesFor.TryGetValue(place, out var need) && shapes.Any(need.Contains);
}
