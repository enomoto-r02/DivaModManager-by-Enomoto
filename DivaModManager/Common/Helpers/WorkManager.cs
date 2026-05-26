using System;
using System.Threading;
using System.Threading.Tasks;

public static class WorkManager
{
    private static int _count = 0;

    /// <summary>現在動作中のバックグラウンド作業数</summary>
    public static int WorkCount => _count;

    /// <summary>1つでも動作中なら true</summary>
    public static bool IsBusy => _count > 0;

    /// <summary>
    /// 非同期処理をラップして Begin/End を自動管理
    /// </summary>
    public static async Task RunAsync(Func<Task> action)
    {
        Interlocked.Increment(ref _count);
        try
        {
            await action();
        }
        finally
        {
            Interlocked.Decrement(ref _count);
            if (_count < 0) _count = 0; // 念のための保険
        }
    }
}
