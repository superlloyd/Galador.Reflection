using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Galador.Reflection.Utils
{
    /// <summary>
    /// Run action sequentially, but in a separate thread, so that they don't block the calling thread but their order is still preserved.
    /// </summary>
    class AsyncQueue : IDisposable
    {
        // REMARK: All work is done by the underlying RunData class, 
        // and all properties are there too
        // hence this class can be garbage collected...

        #region class QueueScheduler QueueContext

        class QueueScheduler : TaskScheduler
        {
            RunData data;

            public QueueScheduler(RunData data) { this.data = data; }

            protected override IEnumerable<Task> GetScheduledTasks()
            {
                return data.pending.Select(x => x.Task);
            }

            protected override void QueueTask(Task task)
            {
                data.Queue(() => base.TryExecuteTask(task));
            }

            protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
            {
                return base.TryExecuteTask(task);
            }
        }

        class QueueSynchronizationContext : SynchronizationContext
        {
            internal QueueSynchronizationContext(RunData queue)
            {
                this.Queue = queue;
            }
            internal RunData Queue { get; private set; }

            public override void Post(SendOrPostCallback d, object state)
            {
                Queue.Queue(() => d(state));
            }
            public override void Send(SendOrPostCallback d, object state)
            {
                throw new NotSupportedException();
            }
        }

        #endregion

        #region class Item

        interface IWorkItem
        {
            void Perform();
            Task Task { get; }
            DateTime? RunAfter { get; }
        }
        class Item<T> : IWorkItem
        {
            public Item(Func<T> a, DateTime? after)
            {
                if (a == null)
                    throw new ArgumentNullException("function");
                Action = a;
                TaskSource = new TaskCompletionSource<T>();
                RunAfter = after;
            }
            Func<T> Action;
            TaskCompletionSource<T> TaskSource;
            public DateTime? RunAfter { get; private set; }

            public void Perform()
            {
                try
                {
                    var result = Action();
                    TaskSource.SetResult(result);
                }
                catch (Exception ex) when (ShouldBreak(ex))
                {
                }
            }
            bool ShouldBreak(Exception ex)
            {
                TaskSource.SetException(ex);
                //Environment.FailFast("Unhandled Exception", ex); // it will be crashing anyway!
                return false;
            }
            Task IWorkItem.Task { get { return Task; } }
            public Task<T> Task { get { return TaskSource.Task; } }
        }

        #endregion

        // does all the work, contains all references
        class RunData
        {
            public RunData(string name)
            {
                Name = name;
                context = new QueueSynchronizationContext(this);
                scheduler = new QueueScheduler(this);
                Task.Factory.StartNew(RunLoop, null, TaskCreationOptions.LongRunning);
            }
            public string Name; // debugging help
            public ManualResetEvent waiter = new ManualResetEvent(false);
            public List<IWorkItem> pending = new List<IWorkItem>();
            public bool stopped;
            public QueueSynchronizationContext context;
            public QueueScheduler scheduler;

            #region EnsureAlive() Dispose()

            public void EnsureAlive() { if (stopped) throw new ObjectDisposedException(typeof(AsyncQueue).Name); }
            public void Dispose(bool disposing)
            {
                if (stopped)
                    return;
                if (disposing)
                {
                    // stop everything now
                    stopped = true;
                    waiter.Set();
                    waiter.Dispose();
                }
                else
                {
                    // let the tasks run their course and append a cleaning task
                    lock (pending)
                    {
                        if (pending.Count == 0)
                        {
                            stopped = true;
                            waiter.Set();
                            waiter.Dispose();
                        }
                        else
                        {
                            DateTime? next = null;
                            for (int i = 0; i < pending.Count; i++)
                            {
                                var it = pending[i];
                                if (it.RunAfter.HasValue)
                                {
                                    if (next == null || next.Value < it.RunAfter.Value)
                                    {
                                        next = it.RunAfter;
                                    }
                                }
                            }
                            if (next.HasValue)
                            {
                                next = next.Value.AddTicks(1);
                            }
                            pending.Add(new Item<bool>(() =>
                            {
                                stopped = true;
                                waiter.Set();
                                waiter.Dispose();
                                return true;
                            }, next));
                            waiter.Set();
                        }
                    }
                }
            }

            #endregion

            #region Queue()

            static Func<bool> ToFunc(Action a)
            {
                if (a == null)
                    throw new ArgumentNullException();
                return () =>
                {
                    a();
                    return true;
                };
            }

            public Task Queue(Action a, TimeSpan after) { return Queue<bool>(ToFunc(a), DateTime.Now + after); }
            public Task Queue(Action a, DateTime after) { return Queue<bool>(ToFunc(a), after); }
            public Task<T> Queue<T>(Func<T> a, TimeSpan after) { return Queue<T>(a, DateTime.Now + after); }
            public Task<T> Queue<T>(Func<T> a, DateTime after)
            {
                if (a == null)
                    throw new ArgumentNullException();
                EnsureAlive();
                var it = new Item<T>(a, after);
                Enqueue(it);
                return it.Task;
            }

            public Task Queue(Action a) { return Queue(ToFunc(a)); }
            public Task<T> Queue<T>(Func<T> a)
            {
                if (a == null)
                    throw new ArgumentNullException();
                EnsureAlive();
                var it = new Item<T>(a, null);
                Enqueue(it);
                return it.Task;
            }

            #endregion

            #region Flush()

            /// <summary>
            /// This will immediately run all current pending task in the current thread
            /// </summary>
            public void Flush(bool all)
            {
                EnsureAlive();
                List<IWorkItem> doNow = new List<IWorkItem>(pending.Count);
                lock (pending)
                {
                    if (all)
                    {
                        doNow = new List<IWorkItem>(pending);
                        pending.Clear();
                    }
                    else
                    {
                        doNow = new List<IWorkItem>(pending.Count);
                        int i = 0, N = pending.Count;
                        while (i < pending.Count)
                        {
                            var it = pending[i];
                            if (it.RunAfter == null || it.RunAfter.Value <= DateTime.Now)
                            {
                                pending.RemoveAt(i);
                                doNow.Add(it);
                            }
                            else
                            {
                                i++;
                            }
                        }
                    }
                }
                var ordered = doNow.OrderBy(x => x.RunAfter.HasValue ? x.RunAfter.Value.Ticks : 0);
                foreach (var item in ordered)
                    item.Perform();
            }

            #endregion

            #region RunLoop() Enqueue() GetNext() WaitOne()

            void RunLoop(object data)
            {
                IWorkItem it;
                TimeSpan? wait;
                System.Threading.SynchronizationContext.SetSynchronizationContext(context);
                while (true)
                {
                    if (stopped)
                        break;
                    if (GetNext(out it, out wait))
                    {
                        it.Perform();
                    }
                    else
                    {
                        WaitOne(wait);
                    }
                }
            }

            void Enqueue(IWorkItem item)
            {
                EnsureAlive();
                lock (pending)
                {
                    pending.Add(item);
                    waiter.Set();
                }
            }

            bool GetNext(out IWorkItem item, out TimeSpan? wait)
            {
                int nextIndex = -1;
                item = null;
                wait = null;
                lock (pending)
                {
                    DateTime? next = DateTime.MaxValue;
                    for (int i = 0; i < pending.Count; i++)
                    {
                        var it = pending[i];
                        if (!it.RunAfter.HasValue)
                        {
                            nextIndex = i;
                            item = it;
                            next = null;
                            break;
                        }
                        if (it.RunAfter.Value < next.Value)
                        {
                            next = it.RunAfter;
                            nextIndex = i;
                            item = it;
                        }
                    }
                    var now = DateTime.Now;
                    if (next == null || next.Value <= now)
                    {
                        pending.RemoveAt(nextIndex);
                        waiter.Set();
                        return true;
                    }
                    else if (Equals(next, DateTime.MaxValue))
                    {
                        wait = null;
                        waiter.Reset();
                        return false;
                    }
                    else
                    {
                        wait = next.Value - now;
                        waiter.Reset();
                        return false;
                    }
                }
            }

            void WaitOne(TimeSpan? waitTime)
            {
                if (waitTime.HasValue)
                    waiter.WaitOne(waitTime.Value);
                else
                    waiter.WaitOne();
            }

            #endregion
        }
        // all the work is done there! (hence AsyncQueue itself can be garbage collected!!!)
        RunData data;

        #region ctor() Dispose()

        public AsyncQueue(string name)
        {
            data = new RunData(name);
        }
        ~AsyncQueue() { Dispose(false); }
        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Dispose(true);
        }
        void Dispose(bool disposing)
        {
            data.Dispose(disposing);
        }

        #endregion

        #region SynchronizationContext Scheduler

        public SynchronizationContext SynchronizationContext
        {
            get
            {
                data.EnsureAlive();
                return data.context;
            }
        }
        public TaskScheduler Scheduler
        {
            get
            {
                data.EnsureAlive();
                return data.scheduler;
            }
        }

        #endregion

        #region wrap methods: Queue() Flush()

        public Task Queue(Action a) { return data.Queue(a); }
        public Task<T> Queue<T>(Func<T> a) { return data.Queue(a); }
        public Task Queue(Action a, TimeSpan after) { return data.Queue(a, after); }
        public Task<T> Queue<T>(Func<T> a, TimeSpan after) { return data.Queue(a, after); }
        public Task Queue(Action a, DateTime after) { return data.Queue(a, after); }
        public Task<T> Queue<T>(Func<T> a, DateTime after) { return data.Queue(a, after); }

        /// <summary>
        /// Will run all pending task which have no time limit right now
        /// </summary>
        public void Flush() { data.Flush(false); }
        public void Flush(bool all) { data.Flush(all); }

        #endregion
    }
}
