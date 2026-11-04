namespace LabApp.Core;

public static class InputParser
{
    public static Result<IReadOnlyList<Command>> Parse(string input)
    {
        if (input is null) return Fail("input is null");

        var tokens = input.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var commands = new List<Command>(tokens.Length);

        foreach (var t in tokens)
        {
            if (t.Contains(','))
            {
                var parts = t.Split(',', 2);
                if (parts[0] != "1")
                    return Fail($"команда с аргументом должна быть 1,<value>, получено: {t}");
                if (!ScalarValue.TryParse(parts[1], out var sv))
                    return Fail($"некорректное значение: {parts[1]}");
                commands.Add(new Command(CommandKind.PushOrEnqueue, sv));
                continue;
            }
            if (!int.TryParse(t, out var code) || code < 1 || code > 5)
                return Fail($"неизвестная команда: {t}");
            commands.Add(new Command((CommandKind)code));
        }
        return Result<IReadOnlyList<Command>>.Ok(commands);
    }

    private static Result<IReadOnlyList<Command>> Fail(string msg) =>
        Result<IReadOnlyList<Command>>.Fail(msg);
}