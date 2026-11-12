// 기능: 모듈 관리자에서 "위로/아래로"를 눌렀을 때 같은 자리 모듈들의 order를 어떻게 다시 매길지 계산한다.
// 사용법: var next = OrderPlan.Move([("a",100),("b",100),("c",300)], "c", -1);  → {a:100, c:200, b:300}  (null이면 움직일 수 없음: 맨 위/아래, 없는 id)
//   먼저 order 순(같으면 id 순)으로 세운 뒤 한 칸 바꾸고 100, 200, 300…으로 다시 매긴다(사이에 새 모듈이 끼어도 되게 간격 100).
// 의존: 없음(순수 계산).
namespace ClockOut.Hunsu.Core.Layout;

public static class OrderPlan
{
    public static IReadOnlyDictionary<string, int>? Move(IEnumerable<(string Id, int Order)> items, string id, int delta)
    {
        var list = items.OrderBy(i => i.Order).ThenBy(i => i.Id, StringComparer.Ordinal).Select(i => i.Id).ToList();
        int from = list.IndexOf(id), to = from + delta;
        if (from < 0 || to < 0 || to >= list.Count) return null;
        (list[from], list[to]) = (list[to], list[from]);
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int k = 0; k < list.Count; k++) result[list[k]] = (k + 1) * 100;
        return result;
    }
}
