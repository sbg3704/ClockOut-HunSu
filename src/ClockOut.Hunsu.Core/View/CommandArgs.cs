// 기능: 단추·목록 줄을 누를 때 다리에 보낼 명령 인자(JSON)를 만든다. 선언의 "arg.<이름>": "{item.addr}" 줄들을 글 틀로 채워 객체 하나로 묶는다.
// 사용법: string json = CommandArgs.Build(item.Props, rowScope);   → {"addr":"B2","sheet":"매출"}
//   값은 늘 글자로 넣고, 글자는 JSON 규칙대로 이스케이프한다(선언이 JSON 문장을 직접 만들지 못함). 모듈은 명령 이름만 고르고 코드를 실행하지 못한다.
// 의존: Template, DataScope. 명령 이름이 모듈 선언의 commands 목록에 있는지는 앱(ShellController)이 확인한다.
using System.Text;
using System.Text.Json;

namespace ClockOut.Hunsu.Core.View;

public static class CommandArgs
{
    public const string Prefix = "arg.";

    public static string Build(IReadOnlyDictionary<string, string> props, DataScope scope)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartObject();
            foreach (var kv in props.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                if (!kv.Key.StartsWith(Prefix, StringComparison.Ordinal) || kv.Key.Length == Prefix.Length) continue;
                w.WriteString(kv.Key[Prefix.Length..], Template.Render(kv.Value, scope));
            }
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>목록의 한 줄(item)만 보이는 범위: {item.경로}로 쓴다.</summary>
    public static DataScope RowScope(JsonElement item)
    {
        var scope = new DataScope();
        scope.Set("item", item);
        return scope;
    }
}
