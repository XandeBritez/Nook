namespace Nook;

/// <summary>Leituras de CPU/RAM via Win32 — só leitura, sem admin, sem pacotes.</summary>
internal static class Monitor
{
    private static ulong _prevIdle;
    private static ulong _prevTotal;
    private static bool _hasPrev;

    /// <summary>Uso da CPU 0–100. A 1ª chamada retorna 0 (precisa de 2 amostras).</summary>
    public static double SampleCpu()
    {
        try
        {
            if (!Native.GetSystemTimes(out var idle, out var kernel, out var user))
                return 0;
            ulong idleU = idle.ToUInt64();
            // kernel inclui o idle (documentação Win32) → busy = total - idle.
            ulong totalU = kernel.ToUInt64() + user.ToUInt64();

            double result = 0;
            if (_hasPrev && totalU > _prevTotal)
            {
                ulong totalDelta = totalU - _prevTotal;
                ulong idleDelta = idleU >= _prevIdle ? idleU - _prevIdle : 0;
                result = totalDelta == 0 ? 0
                    : 100.0 * (totalDelta - idleDelta) / totalDelta;
            }
            _prevIdle = idleU;
            _prevTotal = totalU;
            _hasPrev = true;
            return Math.Clamp(result, 0, 100);
        }
        catch { return 0; }
    }

    /// <summary>% da RAM em uso (0–100).</summary>
    public static double RamPercent()
    {
        try
        {
            var mem = new Native.MEMORYSTATUSEX { dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Native.MEMORYSTATUSEX>() };
            return Native.GlobalMemoryStatusEx(ref mem) ? mem.dwMemoryLoad : 0;
        }
        catch { return 0; }
    }

    /// <summary>RAM livre em GB (para o relatório do Turbo).</summary>
    public static double FreeGb()
    {
        try
        {
            var mem = new Native.MEMORYSTATUSEX { dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Native.MEMORYSTATUSEX>() };
            return Native.GlobalMemoryStatusEx(ref mem)
                ? mem.ullAvailPhys / 1024.0 / 1024.0 / 1024.0
                : 0;
        }
        catch { return 0; }
    }
}
