using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace MyAiAssistant.Services;

public class EverythingService : IEverythingService
{
    private const string DllName = "Everything64.dll";

    #region P/Invoke

    [DllImport(DllName, CharSet = CharSet.Unicode)]
    private static extern void Everything_SetSearchW(string lpString);

    [DllImport(DllName)]
    private static extern void Everything_SetRequestFlags(uint dwRequestFlags);

    [DllImport(DllName)]
    private static extern void Everything_SetMax(uint dwMax);

    [DllImport(DllName)]
    private static extern bool Everything_QueryW(bool bWait);

    [DllImport(DllName)]
    private static extern uint Everything_GetNumResults();

    [DllImport(DllName, CharSet = CharSet.Unicode)]
    private static extern void Everything_GetResultFullPathNameW(uint nIndex, StringBuilder lpString, uint nMaxCount);

    [DllImport(DllName)]
    private static extern bool Everything_IsDBLoaded();

    [DllImport(DllName)]
    private static extern uint Everything_GetLastError();

    private const uint EVERYTHING_REQUEST_FULL_PATH_AND_FILE_NAME = 0x00000004;

    #endregion

    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");

    public bool IsAvailable
    {
        get
        {
            try
            {
                return Everything_IsDBLoaded();
            }
            catch
            {
                return false;
            }
        }
    }

    public List<string> SearchFiles(string query, int maxResults = 10)
    {
        var results = new List<string>();
        if (string.IsNullOrWhiteSpace(query)) return results;

        lock (this)
        {
            try
            {
                Everything_SetSearchW(query);
                Everything_SetRequestFlags(EVERYTHING_REQUEST_FULL_PATH_AND_FILE_NAME);
                Everything_SetMax((uint)maxResults);

                if (!Everything_QueryW(true))
                {
                    Log($"Everything query failed: error {Everything_GetLastError()}");
                    return results;
                }

                uint numResults = Everything_GetNumResults();
                var buffer = new StringBuilder(260);

                for (uint i = 0; i < numResults; i++)
                {
                    buffer.Clear();
                    Everything_GetResultFullPathNameW(i, buffer, (uint)buffer.Capacity);
                    results.Add(buffer.ToString());
                }

                Log($"Everything search '{query}': {results.Count} results");
            }
            catch (DllNotFoundException)
            {
                Log("Everything64.dll not found");
            }
            catch (Exception ex)
            {
                Log($"Everything error: {ex.Message}");
            }
        }

        return results;
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] {msg}\n"); } catch { }
    }
}
