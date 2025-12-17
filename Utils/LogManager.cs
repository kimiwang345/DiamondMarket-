using System.Collections.Concurrent;

namespace DiamondMarket.Utils
{
    /// <summary>
    /// 日志组件，内部维护了一个静态日志记录类
    /// </summary>
    public static class LogManager
    {
        /// <summary>
        /// 日志等级
        /// </summary>
        public enum LogLevel
        { Debug, Info, Warn, Error, Fatal }

        /// <summary>
        /// 日志数据
        /// </summary>
        public class LogItem
        {
            public LogItem(LogLevel level, string message)
            {
                Level = level;
                Message = message;
                Time = DateTime.Now;
                ThreadId = Thread.CurrentThread.ManagedThreadId;
            }

            public DateTime Time { get; private set; }
            public LogLevel Level { get; private set; }
            public int ThreadId { get; private set; }
            public string Message { get; private set; }

            public override string ToString()
            {
                return $"[{Time:yyyy-MM-dd HH:mm:ss ffff}] [{Level.ToString().ToUpper()}] [{ThreadId}] {Message}";
            }
        }

        /// <summary>
        /// 线程安全异步日志基础类，默认缓存10000条日志，超出时日志会阻塞
        /// </summary>
        public class Logger
        {
            /// <summary>
            /// 日志文件存储路径的委托
            /// </summary>
            public Func<string> CustomLogPath { get; set; }

            /// <summary>
            /// 日志文件名的委托，文件扩展名必须是log，否则会影响日志文件的自动清理（可以自定义清理的方法）
            /// </summary>
            public Func<string> CustomLogFileName { get; set; }

            /// <summary>
            /// 日志文件保存时间
            /// </summary>
            public int SaveDays { get; set; } = 3;

            /// <summary>
            /// 日志格式化委托实例
            /// </summary>
            public Func<LogItem, string> LogFormatter { get; set; }

            /// <summary>
            /// 写日志事件
            /// </summary>
            public Action<LogItem> OnWriteLog { get; set; }

            /// <summary>
            /// 日志清理委托实例，传入日志保存时间
            /// </summary>
            public Action<int> LogCleanup { get; set; }

            /// <summary>
            /// 最后一次异常（仅调试时用，不用于正常业务流程）
            /// </summary>
            public Exception LastException { get; set; }

            // 线程安全的日志队列
            private BlockingCollection<string> logQueue = new BlockingCollection<string>(10000);

            // 标识是否允许写入新日志
            private bool allowNewLogs = true;

            public Logger()
            {
                Task.Factory.StartNew(WriteToFile, TaskCreationOptions.LongRunning);
            }

            // 添加日志至队列方法
            public void EnqueueLog(LogLevel level, string message)
            {
                if (!allowNewLogs) return;
                LogItem item = new LogItem(level, message);

                string logMessage;
                if (LogFormatter != null)
                {
                    logMessage = LogFormatter(item);
                }
                else
                {
                    logMessage = item.ToString();
                }
                if (!string.IsNullOrWhiteSpace(logMessage))
                {
                    logQueue.Add(logMessage);
                }
                OnWriteLog?.Invoke(item);
            }

            // 循环写入写日志到文件
            private void WriteToFile()
            {
                string logPath = CustomLogPath?.Invoke()?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Log");

                DirectoryInfo logDir = Directory.CreateDirectory(logPath);

                while (true)
                {
                    try
                    {
                        if (!allowNewLogs && logQueue.Count == 0) break;

                        string logMessage;
                        if (logQueue.TryTake(out logMessage, TimeSpan.FromSeconds(1)))
                        {
                            string fileName = CustomLogFileName?.Invoke() ?? DateTime.Now.ToString("yyyy-MM-dd HH") + ".log";
                            if (!File.Exists(fileName))
                            {
                                // 清理旧日志
                                if (LogCleanup != null)
                                {
                                    LogCleanup(SaveDays);
                                }
                                else
                                {
                                    CleanUpOldLogs(logDir, SaveDays);
                                }
                            }
                            File.AppendAllText(Path.Combine(logPath, fileName), logMessage + Environment.NewLine);
                        }
                    }
                    catch (Exception ex)
                    {
                        LastException = ex;
                        Console.WriteLine("Logger Exception - WriteToFile : " + ex.Message);
                    }
                }


            }

            /// <summary>
            /// 关闭日志器方法，指定超时时间（秒）
            /// </summary>
            /// <param name="waitTimeInSeconds">等待时间</param>
            public void Close(int waitTimeInSeconds = 3)
            {
                allowNewLogs = false;
                CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(waitTimeInSeconds));
                try
                {
                    CancellationToken token = cts.Token;
                    // 标识队列已完成添加
                    logQueue.CompleteAdding();

                    while (!token.IsCancellationRequested)
                    {
                        if (logQueue.Count == 0) break; // 提前退出

                        // 短暂休眠以降低 CPU 占用
                        Task.Delay(100, token).Wait();
                    }
                }
                catch (OperationCanceledException)
                {
                    // 等待时间到，退出方法，不传播异常
                }
                catch (Exception ex)
                {
                    Console.WriteLine("An unexpected exception occurred in the Close method: " + ex.Message);
                }
            }

            /// <summary>
            /// 默认的清理过期日志的方法
            /// </summary>
            /// <param name="logDir"></param>
            /// <param name="saveDays"></param>
            public static void CleanUpOldLogs(DirectoryInfo logDir, int saveDays)
            {
                FileInfo[] logFiles = logDir.GetFiles("*.log");
                foreach (FileInfo file in logFiles)
                {
                    if (DateTime.Now - file.CreationTime >= TimeSpan.FromDays(saveDays))
                    {
                        file.Delete();
                    }
                }
            }

            /// <summary>
            /// 记录Info等级日志
            /// </summary>
            /// <param name="message"></param>
            /// <param name="args"></param>
            public void Info(string message, params object[] args)
            {
                EnqueueLog(LogLevel.Info, string.Format(message, args));
            }

            /// <summary>
            /// 记录Debug等级日志
            /// </summary>
            /// <param name="message"></param>
            /// <param name="args"></param>
            public void Debug(string message, params object[] args)
            {
                EnqueueLog(LogLevel.Debug, string.Format(message, args));
            }

            /// <summary>
            /// 记录Warning等级日志
            /// </summary>
            /// <param name="message"></param>
            /// <param name="args"></param>
            public void Warn(string message, params object[] args)
            {
                EnqueueLog(LogLevel.Warn, string.Format(message, args));
            }

            /// <summary>
            /// 记录Error等级日志
            /// </summary>
            /// <param name="message"></param>
            /// <param name="args"></param>
            public void Error(string message, params object[] args)
            {
                EnqueueLog(LogLevel.Error, string.Format(message, args));
            }

            /// <summary>
            /// 记录Fatal等级日志
            /// </summary>
            /// <param name="message"></param>
            /// <param name="args"></param>
            public void Fatal(string message, params object[] args)
            {
                EnqueueLog(LogLevel.Fatal, string.Format(message, args));
            }
        }


        private static Logger logger = new Logger();
        private static ConcurrentDictionary<string, Logger> logDic = new ConcurrentDictionary<string, Logger>();

        /// <summary>
        /// 获取自定义的日志记录类
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static Logger GetLogger(string name)
        {
            return logDic.GetOrAdd(name, key =>
            {
                var log = new Logger();
                log.CustomLogPath = () => AppDomain.CurrentDomain.BaseDirectory + "\\Log\\" + key;
                return log;
            });
        }

        /// <summary>
        /// 日志文件存储路径的委托
        /// </summary>
        public static Func<string> CustomLogPath
        {
            get => logger.CustomLogPath;
            set => logger.CustomLogPath = value;
        }

        /// <summary>
        /// 日志文件名的委托，文件扩展名必须是log，否则会影响日志文件的自动清理（可以自定义清理的方法）
        /// </summary>
        public static Func<string> CustomLogFileName
        {
            get => logger.CustomLogFileName;
            set => logger.CustomLogFileName = value;
        }

        /// <summary>
        /// 日志文件保存时间
        /// </summary>
        public static int SaveDays
        {
            get => logger.SaveDays;
            set => logger.SaveDays = value;
        }

        /// <summary>
        /// 日志格式化委托实例
        /// </summary>
        public static Func<LogItem, string> LogFormatter
        {
            get => logger.LogFormatter;
            set => logger.LogFormatter = value;
        }

        /// <summary>
        /// 写日志事件
        /// </summary>
        public static Action<LogItem> OnWriteLog
        {
            get => logger.OnWriteLog;
            set => logger.OnWriteLog = value;
        }

        /// <summary>
        /// 日志清理委托实例，传入日志保存时间
        /// </summary>
        public static Action<int> LogCleanup
        {
            get => logger.LogCleanup;
            set => logger.LogCleanup = value;
        }

        /// <summary>
        /// 最后一次异常（仅调试时用，不用于正常业务流程）
        /// </summary>
        public static Exception LastException
        {
            get => logger.LastException;
            set => logger.LastException = value;
        }

        /// <summary>
        /// 关闭所有日志记录器，指定超时时间（秒），日志记录器较多时可能耗时较久
        /// </summary>
        /// <param name="waitTimeInSeconds">等待时间</param>
        public static void Close(int waitTimeInSeconds = 3)
        {
            logger.Close(waitTimeInSeconds);
            foreach (var item in logDic.Values)
            {
                item.Close(waitTimeInSeconds);
            }
        }

        /// <summary>
        /// 记录Info等级日志
        /// </summary>
        /// <param name="message"></param>
        /// <param name="args"></param>
        public static void Info(string message, params object[] args)
        {
            logger.EnqueueLog(LogLevel.Info, string.Format(message, args));
        }

        public static void Info(string message)
        {
            logger.EnqueueLog(LogLevel.Info, message);
        }

        /// <summary>
        /// 记录Debug等级日志
        /// </summary>
        /// <param name="message"></param>
        /// <param name="args"></param>
        public static void Debug(string message, params object[] args)
        {
            logger.EnqueueLog(LogLevel.Debug, string.Format(message, args));
        }

        /// <summary>
        /// 记录Warning等级日志
        /// </summary>
        /// <param name="message"></param>
        /// <param name="args"></param>
        public static void Warn(string message, params object[] args)
        {
            logger.EnqueueLog(LogLevel.Warn, string.Format(message, args));
        }

        /// <summary>
        /// 记录Error等级日志
        /// </summary>
        /// <param name="message"></param>
        /// <param name="args"></param>
        public static void Error(string message, params object[] args)
        {
            logger.EnqueueLog(LogLevel.Error, string.Format(message, args));
        }

        /// <summary>
        /// 记录Fatal等级日志
        /// </summary>
        /// <param name="message"></param>
        /// <param name="args"></param>
        public static void Fatal(string message, params object[] args)
        {
            logger.EnqueueLog(LogLevel.Fatal, string.Format(message, args));
        }
    }
}