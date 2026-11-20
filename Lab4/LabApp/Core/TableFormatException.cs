namespace LabApp.Core;

/// <summary>Файл не удалось разобрать как таблицу.</summary>
public sealed class TableFormatException(string message) : Exception(message);
