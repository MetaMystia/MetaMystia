using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace SgrYuki;

using KeyCommandQueue = ConcurrentDictionary<string, ConcurrentQueue<CommandScheduler.Command>>;

[MetaMystia.AutoLog]
public static partial class CommandScheduler
{
    public sealed class Command
    {
        public Func<bool> CanExecute;
        public Action Execute;
        public string ExecuteInfo;
        public Action BeforeExecute;
        public Action OnTimeout;

        public float TriggerTime; // 进入调度的时间
        public float ExpireTime;  // unscaled time
        public float TimeoutSeconds;  // 设定的超时时间

        // ================================
        // 周期任务
        // ================================
        public bool Repeat;
        public float IntervalSeconds;
        public string CommandId;
    }

    /// <summary><see cref="Now"/> 的计时源；单调，且不受游戏流速倍率影响。</summary>
    private static readonly Stopwatch s_clock = Stopwatch.StartNew();

    private static readonly ConcurrentQueue<Command> _pending = new();

    // 可执行 FIFO 队列（短任务 / 已到时间）
    private static readonly Queue<Command> _queue = new();

    // 延时队列（按 TriggerTime 排序）
    private static readonly PriorityQueue<Command, float> _delayed = new();

    // 关键字队列
    private static readonly KeyCommandQueue KeyQueue = new();
    private static readonly List<KeyCommandQueue> KeyQueueList = [];

    // 周期任务标记
    private static readonly ConcurrentDictionary<string, bool> _intervalTasks
        = new();

    private static void OnTimeoutDefault(string text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            Log.Error($"{text} timeout!");
        }
    }

    /// <summary>
    /// 调度器自持的单调时钟（秒），取代原先直读的引擎时间（不受流速倍率影响的运行时间）。
    /// <para>
    /// 本类的推进点是 <see cref="Tick"/>，由 <c>ModLoop.FixedUpdate</c> 调用且不带 delta；框架在那儿给的是固定步长
    /// <c>fixedDeltaTime</c>（随流速倍率快慢），不是真实时间，所以这里取自身的 <see cref="Stopwatch"/> 读数。
    /// 全部用法都只用相对差（<c>TriggerTime</c> ／ <c>ExpireTime</c> 的加减与比较），因此时钟原点（本类首次取值）
    /// 与原「自进程启动起算」不同不影响行为。
    /// </para>
    /// <para>
    /// 与原先的唯一差别：引擎时间只在每帧推进，而本实现在循环停摆期间也继续走；进程挂起恢复时两者都会跳变。
    /// </para>
    /// </summary>
    public static float Now => (float)s_clock.Elapsed.TotalSeconds;

    // ================================
    // Public API
    // ================================
    public static void Enqueue(
        Func<bool> executeWhen,
        Action execute,
        string executeInfo = "",
        Action beforeExecute = null,
        float timeoutSeconds = 90f,
        Action onTimeout = null)
    {
        if (executeWhen == null)
            throw new ArgumentNullException(nameof(executeWhen));
        if (execute == null)
            throw new ArgumentNullException(nameof(execute));

        onTimeout ??= () => OnTimeoutDefault(executeInfo);

        float now = Now;

        _pending.Enqueue(new Command
        {
            CanExecute = executeWhen,
            Execute = execute,
            ExecuteInfo = executeInfo,
            BeforeExecute = beforeExecute,
            OnTimeout = onTimeout,

            TriggerTime = now,                     // 立即可调度
            ExpireTime = now + timeoutSeconds
        });
    }

    public static void EnqueueWithNoCondition(Action execute) => Enqueue(() => true, execute);

    public static void NewThreadRunInBackGround(Action execute)
    {
        void act()
        {
            try
            {
                execute.Invoke();
            }
            catch (Exception ex)
            {
                Log.Error($"cannot execute in new thread background, {ex.Message}, {ex.StackTrace}");
            }
        }
        var t = new Thread(act)
        {
            IsBackground = true
        };
        t.Start();
    }

    public static void RunInBackGround(Action execute)
    {
        void act()
        {
            try
            {
                execute.Invoke();
            }
            catch (Exception ex)
            {
                Log.Error($"cannot execute in task background, {ex.Message}, {ex.StackTrace}");
            }
        }
        System.Threading.Tasks.Task.Run(act);
    }

    // ================================
    // KeyQueue
    // ================================
    public static void EnqueueKey(
        KeyCommandQueue keyCommandQueue,
        string key,
        Func<bool> executeWhen,
        Action execute,
        string executeInfo = "",
        Action beforeExecute = null,
        float timeoutSeconds = 60f,
        Action onTimeout = null)
    {
        if (executeWhen == null)
            throw new ArgumentNullException(nameof(executeWhen));
        if (execute == null)
            throw new ArgumentNullException(nameof(execute));

        onTimeout ??= () => OnTimeoutDefault(executeInfo);

        float now = Now;

        var queue = keyCommandQueue.GetOrAdd(
            key,
            _ => new ConcurrentQueue<Command>());

        queue.Enqueue(new Command
        {
            CanExecute = executeWhen,
            Execute = execute,
            ExecuteInfo = executeInfo,
            BeforeExecute = beforeExecute,
            OnTimeout = onTimeout,

            TriggerTime = now,
            ExpireTime = now + timeoutSeconds,
            TimeoutSeconds = timeoutSeconds
        });
    }

    public static void EnqueueKey(
        string key,
        Func<bool> executeWhen,
        Action execute,
        string executeInfo = "",
        Action beforeExecute = null,
        float timeoutSeconds = 60f,
        Action onTimeout = null) => EnqueueKey(KeyQueue, key, executeWhen, execute, executeInfo, beforeExecute, timeoutSeconds, onTimeout);

    public static void RegisterKeyQueue(KeyCommandQueue queue) => KeyQueueList.Add(queue);
    public static bool UnRegisterKeyQueue(KeyCommandQueue queue) => KeyQueueList.Remove(queue);

    /// <summary>
    /// 清空所有关键字队列
    /// </summary>
    /// <param name="logInfo">是否打印被移除命令的信息</param>
    public static void ClearKeyQueue(bool logInfo = true) => ClearKeyQueue(KeyQueue, logInfo);
    public static void ClearKeyQueue(KeyCommandQueue keyCommandQueue, bool logInfo = true)
    {
        foreach (var (k, commandQ) in keyCommandQueue)
        {
            while (commandQ.TryDequeue(out var command))
            {
                if (logInfo)
                {
                    Log.Message($"cleared key {k}, {command.ExecuteInfo}, trigger time {command.TriggerTime}");
                }
            }
        }
        keyCommandQueue.Clear();
    }

    /// <summary>
    /// 移除一个关键字队列
    /// </summary>
    /// <param name="key"></param>
    /// <returns>目标关键字队列未执行命令数</returns>
    public static int RemoveKeyFromKeyQueue(string key) => RemoveKeyFromKeyQueue(KeyQueue, key);
    public static int RemoveKeyFromKeyQueue(KeyCommandQueue keyCommandQueue, string key)
    {
        int count = 0;
        if (keyCommandQueue.TryRemove(key, out var commandQ))
        {
            while (commandQ.TryDequeue(out var command))
            {
                count++;
                Log.Message($"removed key {key}, {command.ExecuteInfo}, trigger time {command.TriggerTime}");
            }
        }
        return count;
    }

    // ================================
    // 延时执行 API
    // ================================
    public static void DelayExecute(
        float delaySeconds,
        Action execute,
        string executeInfo = "",
        Action beforeExecute = null,
        float timeoutSeconds = 60f,
        Action onTimeout = null)
    {
        if (execute == null)
            throw new ArgumentNullException(nameof(execute));

        onTimeout ??= () => OnTimeoutDefault(executeInfo);

        float now = Now;
        float triggerTime = now + delaySeconds;

        _pending.Enqueue(new Command
        {
            CanExecute = () => true,
            Execute = execute,
            ExecuteInfo = executeInfo,
            BeforeExecute = beforeExecute,
            OnTimeout = onTimeout,

            TriggerTime = triggerTime,
            ExpireTime = triggerTime + timeoutSeconds,
            TimeoutSeconds = timeoutSeconds
        });
    }

    // ================================
    // 周期执行 API
    // ================================
    public static void EnqueueInterval(
        string commandId,
        float intervalSeconds,
        Action execute,
        Func<bool> executeWhen = null,
        Action beforeExecute = null)
    {
        if (string.IsNullOrEmpty(commandId))
            throw new ArgumentNullException(nameof(commandId));
        if (intervalSeconds <= 0f)
            throw new ArgumentOutOfRangeException(nameof(intervalSeconds));
        if (execute == null)
            throw new ArgumentNullException(nameof(execute));
        if (_intervalTasks.TryGetValue(commandId, out var isRunning) && isRunning)
            return;

        executeWhen ??= () => true;

        _intervalTasks[commandId] = true;

        float now = Now;

        _pending.Enqueue(new Command
        {
            CommandId = commandId,
            CanExecute = executeWhen,
            Execute = execute,
            BeforeExecute = beforeExecute,
            ExecuteInfo = $"Interval:{commandId}",

            Repeat = true,
            IntervalSeconds = intervalSeconds,

            TriggerTime = now + intervalSeconds,
            ExpireTime = float.PositiveInfinity
        });
        Log.Message($"added interval task {commandId}");
    }

    public static void CancelInterval(string commandId)
    {
        if (!string.IsNullOrEmpty(commandId))
            _intervalTasks.TryRemove(commandId, out _);
        Log.Message($"cancelled interval task {commandId}");
    }

    // ================================
    // Main-thread tick
    // ================================
    public static void Tick()
    {
        float now = Now;

        // 接收后台提交，分流到 ready / delayed
        while (_pending.TryDequeue(out var cmd))
        {
            if (cmd.TriggerTime <= now)
                _queue.Enqueue(cmd);
            else
                _delayed.Enqueue(cmd, cmd.TriggerTime);
        }

        // 把“到时间”的延时任务转入 FIFO
        while (_delayed.Count > 0 &&
               _delayed.Peek().TriggerTime <= now)
        {
            _queue.Enqueue(_delayed.Dequeue());
        }

        // FIFO 扫描
        int count = _queue.Count;

        for (int i = 0; i < count; i++)
        {
            var cmd = _queue.Dequeue();

            // ===== 周期任务取消 =====
            if (cmd.Repeat && cmd.CommandId != null)
            {
                var repeating = _intervalTasks.TryGetValue(cmd.CommandId, out var active) && active;
                if (!repeating)
                    continue;
            }

            if (now > cmd.ExpireTime)
            {
                cmd.OnTimeout?.Invoke();
                continue;
            }

            bool canExecute;
            try
            {
                canExecute = cmd.CanExecute();
            }
            catch (Exception e)
            {
                Log.Error($"Error when checking action {cmd.ExecuteInfo}, reason: {e.Message}");
                continue;
            }

            if (!canExecute)
            {
                _queue.Enqueue(cmd);
                continue;
            }

            try
            {
                cmd.BeforeExecute?.Invoke();
                cmd.Execute();
            }
            catch (Exception e)
            {
                Log.Error(
                    $"Error when executing action {cmd.ExecuteInfo}, " +
                    $"reason: {e.Message}, stacktrace {e.StackTrace}");
            }

            // ===== 周期任务重新调度（进入 delayed）=====
            if (cmd.Repeat)
            {
                cmd.TriggerTime = now + cmd.IntervalSeconds;
                _delayed.Enqueue(cmd, cmd.TriggerTime);
            }
        }

        TickKeyQueue();
        KeyQueueList.ForEach(TickKeyQueue);
    }

    // ================================
    // KeyQueue Tick
    // ================================
    private static void TickKeyQueue() => TickKeyQueue(KeyQueue);
    private static void TickKeyQueue(KeyCommandQueue keyCommandQueue)
    {
        static void DequeueCommand(
            ConcurrentQueue<Command> commandQ)
        {
            if (commandQ.TryDequeue(out var last))
            {
                Log.Debug($"dequeued {last.ExecuteInfo}");
            }
            if (commandQ.TryPeek(out var cmd))
            {
                cmd.ExpireTime = Now + cmd.TimeoutSeconds;
            }
        }

        // 每个 keyCommandQueue 只处理队首
        foreach (var (k, commandQ) in keyCommandQueue)
        {
            if (!commandQ.TryPeek(out var cmd))
                continue;

            if (Now > cmd.ExpireTime)
            {
                cmd.OnTimeout?.Invoke();
                DequeueCommand(commandQ);
                continue;
            }

            bool canExecute;
            try
            {
                canExecute = cmd.CanExecute();
            }
            catch (Exception e)
            {
                Log.Error($"Error when checking action {cmd.ExecuteInfo}, reason: {e.Message}");
                DequeueCommand(commandQ);
                continue;
            }

            if (!canExecute)
                continue;

            try
            {
                cmd.BeforeExecute?.Invoke();
                cmd.Execute();
            }
            catch (Exception e)
            {
                Log.Error(
                    $"Error when executing action {cmd.ExecuteInfo}, " +
                    $"reason: {e.Message}, stacktrace {e.StackTrace}");
            }
            finally
            {
                DequeueCommand(commandQ);
            }
        }
    }

}
