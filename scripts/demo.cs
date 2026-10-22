#:property TreatWarningsAsErrors=false
#:property AnalysisLevel=none

// Runs http/kanimambo-journey.http from top to bottom against a running AppHost and narrates each step:
//
//   dotnet run --project src/Kanimambo.AppHost --launch-profile http     (in another terminal)
//   dotnet run scripts/demo.cs                                            (from the repository root)
//
// It understands the subset of the .http format the journey uses — file variables (@name = value), named requests
// (# @name x), response variables ({{x.response.body.$.path}}) and {{$guid}} — plus one comment of its own,
// "# demo: wait until $.path == "value"", which polls a request until the asynchronous work (payment callback,
// courier) has happened. A back-office token is created with `dotnet user-jwts` when the file still has the placeholder.

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

var file = args.FirstOrDefault(a => a.EndsWith(".http", StringComparison.Ordinal)) ?? Path.Combine("http", "kanimambo-journey.http");
var lines = File.ReadAllLines(file);
var variables = new Dictionary<string, string>(StringComparer.Ordinal);
var responses = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

foreach (var line in lines.TakeWhile(l => !l.StartsWith("###", StringComparison.Ordinal)))
{
    if (Regex.Match(line, @"^@(\w+)\s*=\s*(.*)$") is { Success: true } v)
    {
        variables[v.Groups[1].Value] = v.Groups[2].Value.Trim();
    }
}

variables["period"] = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(2)).ToString("yyyy/MM");
if (variables.GetValueOrDefault("backofficeToken", "").StartsWith("paste", StringComparison.Ordinal))
{
    variables["backofficeToken"] = Run("dotnet", "user-jwts create --project src/Kanimambo.Api --name ana.backoffice --role backoffice --output token").Trim();
}

var steps = Split(lines.SkipWhile(l => !l.StartsWith("###", StringComparison.Ordinal)).ToList());
var failures = 0;
foreach (var step in steps)
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine();
    Console.WriteLine(step.Title.Length > 0 ? step.Title : "(continued)");
    Console.ResetColor();
    foreach (var note in step.Notes)
    {
        Console.WriteLine($"   {note}");
    }

    var started = Stopwatch.StartNew();
    (int Status, string ContentType, byte[] Body) response;
    while (true)
    {
        response = await SendAsync(step);
        if (step.WaitPath is null || Matches(response.Body, step.WaitPath, step.WaitValue!) || started.Elapsed > TimeSpan.FromSeconds(45))
        {
            break;
        }

        await Task.Delay(1_000);
    }

    var ok = response.Status is >= 200 and < 300;
    failures += ok ? 0 : 1;
    Console.ForegroundColor = ok ? ConsoleColor.Green : ConsoleColor.Red;
    Console.WriteLine($"   {step.Method} {Substitute(step.Url)}  →  {response.Status}{(step.WaitPath is null ? "" : $"  (after {started.Elapsed.TotalSeconds:0} s)")}");
    Console.ResetColor();
    Console.WriteLine(Indent(Summarize(response.ContentType, response.Body)));

    if (step.Name is not null && response.ContentType.Contains("json", StringComparison.Ordinal))
    {
        responses[step.Name] = JsonDocument.Parse(response.Body).RootElement.Clone();
    }
}

Console.WriteLine();
Console.ForegroundColor = failures == 0 ? ConsoleColor.Green : ConsoleColor.Red;
Console.WriteLine(failures == 0 ? $"All {steps.Count} requests succeeded." : $"{failures} of {steps.Count} requests failed.");
Console.ResetColor();
return failures == 0 ? 0 : 1;

async Task<(int, string, byte[])> SendAsync(Step step)
{
    using var request = new HttpRequestMessage(new HttpMethod(step.Method), Substitute(step.Url));
    string? contentType = null;
    foreach (var (name, value) in step.Headers)
    {
        if (name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
        {
            contentType = Substitute(value);
        }
        else
        {
            request.Headers.TryAddWithoutValidation(name, Substitute(value));
        }
    }

    if (step.Body.Length > 0)
    {
        request.Content = new StringContent(Substitute(step.Body), Encoding.UTF8, contentType ?? "application/json");
    }

    using var response = await http.SendAsync(request);
    return ((int)response.StatusCode, response.Content.Headers.ContentType?.MediaType ?? "", await response.Content.ReadAsByteArrayAsync());
}

string Substitute(string text) => Regex.Replace(text, @"\{\{(.+?)\}\}", m =>
{
    var expression = m.Groups[1].Value.Trim();
    if (expression == "$guid")
    {
        return Guid.NewGuid().ToString();
    }

    if (Regex.Match(expression, @"^(\w+)\.response\.body\.(\$.*)$") is { Success: true } r)
    {
        return responses.TryGetValue(r.Groups[1].Value, out var json) && Select(json, r.Groups[2].Value) is { } value
            ? (value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText())
            : m.Value;
    }

    return variables.TryGetValue(expression, out var variable) ? Substitute(variable) : m.Value;
});

bool Matches(byte[] body, string path, string expected)
{
    try
    {
        return Select(JsonDocument.Parse(body).RootElement, path) is { ValueKind: JsonValueKind.String } value && value.GetString() == expected;
    }
    catch (JsonException)
    {
        return false;
    }
}

// The JSONPath subset the file uses: $.a.b, $[0].a, $.items[0].id.
static JsonElement? Select(JsonElement root, string path)
{
    var current = root;
    foreach (Match part in Regex.Matches(path[1..], @"\.(\w+)|\[(\d+)\]"))
    {
        if (part.Groups[1].Success)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(part.Groups[1].Value, out current))
            {
                return null;
            }
        }
        else
        {
            var index = int.Parse(part.Groups[2].Value);
            if (current.ValueKind != JsonValueKind.Array || current.GetArrayLength() <= index)
            {
                return null;
            }

            current = current[index];
        }
    }

    return current;
}

static string Summarize(string contentType, byte[] body)
{
    if (body.Length == 0)
    {
        return "(no content)";
    }

    if (!contentType.Contains("json", StringComparison.Ordinal) && !contentType.StartsWith("text", StringComparison.Ordinal))
    {
        return $"[{contentType}, {body.Length:N0} bytes{(Encoding.ASCII.GetString(body, 0, Math.Min(8, body.Length)) is var head && head.StartsWith("%PDF") ? $", starts with {head.Trim()}" : "")}]";
    }

    var text = contentType.Contains("json", StringComparison.Ordinal) ? Pretty(body) : Encoding.UTF8.GetString(body);
    var shown = text.Split('\n').Take(14).ToList();
    return string.Join('\n', shown) + (text.Split('\n').Length > shown.Count ? "\n…" : "");
}

// File-based apps are trimming-friendly (no reflection serialization), so indent through the writer.
static string Pretty(byte[] json)
{
    using var stream = new MemoryStream();
    using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
    {
        JsonDocument.Parse(json).RootElement.WriteTo(writer);
    }

    return Encoding.UTF8.GetString(stream.ToArray());
}

static string Indent(string text) => string.Join('\n', text.Split('\n').Select(l => "     " + l.TrimEnd('\r')));

static string Run(string command, string arguments)
{
    using var process = Process.Start(new ProcessStartInfo(command, arguments) { RedirectStandardOutput = true, UseShellExecute = false })!;
    var output = process.StandardOutput.ReadToEnd();
    process.WaitForExit();
    return output;
}

static List<Step> Split(List<string> lines)
{
    var steps = new List<Step>();
    Step? current = null;
    var inBody = false;
    foreach (var raw in lines)
    {
        var line = raw.TrimEnd();
        if (line.StartsWith("###", StringComparison.Ordinal))
        {
            current = new Step { Title = line.TrimStart('#').Trim() };
            steps.Add(current);
            inBody = false;
            continue;
        }

        if (current is null)
        {
            continue;
        }

        if (current.Method.Length == 0)
        {
            if (Regex.Match(line, @"^#\s*@name\s+(\w+)") is { Success: true } name)
            {
                current.Name = name.Groups[1].Value;
            }
            else if (Regex.Match(line, @"^#\s*demo:\s*wait until\s+(\S+)\s*==\s*""(.*)""") is { Success: true } wait)
            {
                (current.WaitPath, current.WaitValue) = (wait.Groups[1].Value, wait.Groups[2].Value);
            }
            else if (line.StartsWith('#'))
            {
                current.Notes.Add(line.TrimStart('#').Trim());
            }
            else if (Regex.Match(line, @"^(GET|POST|PUT|DELETE|PATCH)\s+(\S+)") is { Success: true } request)
            {
                (current.Method, current.Url) = (request.Groups[1].Value, request.Groups[2].Value);
            }

            continue;
        }

        if (!inBody && line.Length == 0)
        {
            inBody = true;
        }
        else if (!inBody && line.IndexOf(':') is > 0 and var colon)
        {
            current.Headers.Add((line[..colon].Trim(), line[(colon + 1)..].Trim()));
        }
        else if (inBody)
        {
            current.Body += (current.Body.Length > 0 ? "\n" : "") + raw;
        }
    }

    foreach (var step in steps)
    {
        step.Body = step.Body.Trim();
    }

    return steps.Where(s => s.Method.Length > 0 && !s.Title.StartsWith("──", StringComparison.Ordinal)).ToList();
}

sealed class Step
{
    public string Title { get; set; } = "";
    public List<string> Notes { get; } = [];
    public string? Name { get; set; }
    public string? WaitPath { get; set; }
    public string? WaitValue { get; set; }
    public string Method { get; set; } = "";
    public string Url { get; set; } = "";
    public List<(string Name, string Value)> Headers { get; } = [];
    public string Body { get; set; } = "";
}
