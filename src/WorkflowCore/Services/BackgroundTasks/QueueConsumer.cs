using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace WorkflowCore.Services.BackgroundTasks
{
    internal abstract class QueueConsumer : IBackgroundTask
    {
        protected abstract QueueType Queue { get; }
        protected virtual int MaxConcurrentItems => Math.Max(Environment.ProcessorCount, 2);

        protected readonly IQueueProvider QueueProvider;
        protected readonly ILogger Logger;
        protected readonly WorkflowOptions Options;
        protected Task DispatchTask;
        private CancellationTokenSource _cancellationTokenSource;

        protected QueueConsumer(IQueueProvider queueProvider, ILoggerFactory loggerFactory, WorkflowOptions options)
        {
            QueueProvider = queueProvider;
            Options = options;
            Logger = loggerFactory.CreateLogger(GetType());
        }

        protected abstract Task ProcessItem(string itemId, CancellationToken cancellationToken);

        public virtual void Start()
        {
            if (DispatchTask != null)
            {
                throw new InvalidOperationException();
            }

            _cancellationTokenSource = new CancellationTokenSource();

            DispatchTask = Task.Factory.StartNew(()=>Execute());
           
        }

        public virtual void Stop()
        {
            _cancellationTokenSource.Cancel();
            if (DispatchTask != null)
                DispatchTask.Wait();
            DispatchTask = null;
        }

        private async Task Execute()
        {
            try
            {
                var cancelToken = _cancellationTokenSource.Token;
                var opts = new ExecutionDataflowBlockOptions()
                {
                    MaxDegreeOfParallelism = MaxConcurrentItems,
                    BoundedCapacity = MaxConcurrentItems + 1
                };

                var actionBlock = new ActionBlock<string>(ExecuteItem, opts);

                while (!cancelToken.IsCancellationRequested)
                {
                    try
                    {
                        if (!SpinWait.SpinUntil(() => actionBlock.InputCount == 0, Options.IdleTime))
                        {
                            continue;
                        }

                        var item = await QueueProvider.DequeueWork(Queue, cancelToken);

                        if (item == null)
                        {
                            if (!QueueProvider.IsDequeueBlocking)
                                await Task.Delay(Options.IdleTime, cancelToken);
                            continue;
                        }

                        if (!actionBlock.Post(item))
                        {
                            await QueueProvider.QueueWork(item, Queue);
                        }
                    }
                    catch (OperationCanceledException ce)
                    {
                        Logger.LogError(ce.Message, ce);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex.Message, ex);
                    }
                }

                actionBlock.Complete();
                await actionBlock.Completion;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.Message, ex);
                //This is to prevent async void crashing the program. 
                //Should never reach this line
                //TODO remove async void
            }
        }

        private async Task ExecuteItem(string itemId)
        {
            try
            {
                await ProcessItem(itemId, _cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                Logger.LogInformation($"Operation cancelled while processing {itemId}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error executing item {itemId} - {ex.Message}");
            }
        }
    }
}
