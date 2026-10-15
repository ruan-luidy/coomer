namespace Coomer;

// Log minimo em %APPDATA%\coomer\coomer.log. O processo e residente e sem
// console, entao sem isso qualquer excecao engolida some sem rastro. Passou
// de 1MB, gira pra coomer.log.old.
internal static class Log
{
  private const long MaxBytes = 1024 * 1024;
  private static readonly object Gate = new();

  public static string FilePath { get; } = Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
      "coomer", "coomer.log");

  public static void Info(string message) => Write("INFO", message);

  public static void Error(string context, Exception ex)
      => Write("ERRO", $"{context}: {ex}");

  public static void Error(string message) => Write("ERRO", message);

  private static void Write(string level, string message)
  {
    try
    {
      lock (Gate)
      {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var info = new FileInfo(FilePath);
        if (info.Exists && info.Length > MaxBytes)
          File.Move(FilePath, FilePath + ".old", overwrite: true);
        File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {level} {message}{Environment.NewLine}");
      }
    }
    catch
    {
      // log nunca derruba o app
    }
  }
}
