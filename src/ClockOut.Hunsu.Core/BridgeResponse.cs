// 기능: 다리가 돌려준 JSON 응답({"ok":..,"proto":..,"data":..} 또는 오류)을 읽는다(docs/02 5장 공통 형식).
// 사용법: var r = BridgeResponse.Parse(json); if (r.Ok) r.Data.GetProperty("addr") ...
// 의존: System.Text.Json. 읽을 수 없는 글은 Ok=false, Code="query_failed"로 바꾼다(예외를 던지지 않음).
using System.Text.Json;

namespace ClockOut.Hunsu.Core;

public sealed record BridgeResponse(bool Ok, int Proto, string? Code, string? Error, JsonElement Data)
{
    public static BridgeResponse Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Failed("query_failed", "빈 응답");
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return Failed("query_failed", "응답이 객체가 아님");
            bool ok = root.TryGetProperty("ok", out var okEl) && okEl.ValueKind == JsonValueKind.True;
            int proto = root.TryGetProperty("proto", out var p) && p.TryGetInt32(out var pv) ? pv : -1;
            string? code = root.TryGetProperty("code", out var c) ? c.GetString() : null;
            string? error = root.TryGetProperty("error", out var e) ? e.GetString() : null;
            JsonElement data = root.TryGetProperty("data", out var d) ? d.Clone() : default;
            return new BridgeResponse(ok, proto, code, error, data);
        }
        catch (JsonException ex)
        {
            return Failed("query_failed", "JSON을 읽지 못함: " + ex.Message);
        }
    }

    public static BridgeResponse Failed(string code, string error) => new(false, -1, code, error, default);
}
