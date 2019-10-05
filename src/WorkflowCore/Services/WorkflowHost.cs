using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace WorkflowCore.Services
{
    public class WorkflowHost : IWorkflowHost, IDisposable
    {
        protected bool _shutdown = true;
        protected readonly IServiceProvider _serviceProvider;

        private readonly IEnumerable<IBackgroundTask> _backgroundTasks;
        private readonly IWorkflowController _workflowController;

        public event StepErrorEventHandler OnStepError;

        // Public dependencies to allow for extension method access.
        public IPersistenceProvider PersistenceStore { get; private set; }
        public IDistributedLockProvider LockProvider { get; private set; }
        public IWorkflowRegistry Registry { get; private set; }
        public WorkflowOptions Options { get; private set; }
        public IQueueProvider QueueProvider { get; private set; }
        public ILogger Logger { get; private set; }

        public WorkflowHost(IPersistenceProvider persistenceStore, IQueueProvider queueProvider, WorkflowOptions options, ILoggerFactory loggerFactory, IServiceProvider serviceProvider, IWorkflowRegistry registry, IDistributedLockProvider lockProvider, IEnumerable<IBackgroundTask> backgroundTasks, IWorkflowController workflowController)
        {
            PersistenceStore = persistenceStore;
            QueueProvider = queueProvider;
            Options = options;
            Logger = loggerFactory.CreateLogger<WorkflowHost>();
            _serviceProvider = serviceProvider;
            Registry = registry;
            LockProvider = lockProvider;
            _backgroundTasks = backgroundTasks;
            _workflowController = workflowController;
            persistenceStore.EnsureStoreExists();
        }

        public Task<string> StartWorkflow(string workflowId, int? tenantId, object data = null)
        {
            return _workflowController.StartWorkflow(workflowId, tenantId, data);
        }

        public Task<string> StartWorkflow(string workflowId, int? tenantId, int? version, object data = null)
        {
            return _workflowController.StartWorkflow<object>(workflowId, tenantId, version, data);
        }

        public Task<string> StartWorkflow<TData>(string workflowId, int? tenantId, TData data = null)
            where TData : class
        {
            return _workflowController.StartWorkflow<TData>(workflowId, tenantId, null, data);
        }

        public Task<string> StartWorkflow<TData>(string workflowId, int? tenantId, int? version, TData data = null)
            where TData : class
        {
            return _workflowController.StartWorkflow(workflowId, tenantId, version, data);
        }

        public Task PublishEvent(string eventName, string eventKey, object eventData, DateTime? effectiveDate = null)
        {
            return _workflowController.PublishEvent(eventName, eventKey, eventData, effectiveDate);
        }

        public async Task Start()
        {
            _shutdown = false;
            PersistenceStore.EnsureStoreExists();
            await QueueProvider.Start();
            await LockProvider.Start();

            Logger.LogInformation("Starting backgroud tasks");

            foreach (var task in _backgroundTasks)
                task.Start();
        }

        public async Task Stop()
        {
            _shutdown = true;

            Logger.LogInformation("Stopping background tasks");
            foreach (var th in _backgroundTasks)
                th.Stop();

            Logger.LogInformation("Worker tasks stopped");

            await QueueProvider.Stop();
            await LockProvider.Stop();
        }

        public void RegisterWorkflow<TWorkflow>()
            where TWorkflow : IWorkflow, new()
        {
            TWorkflow wf = new TWorkflow();
            Registry.RegisterWorkflow(wf);
        }

        public void RegisterWorkflow<TWorkflow, TData>()
            where TWorkflow : IWorkflow<TData>, new()
            where TData : new()
        {
            TWorkflow wf = new TWorkflow();
            Registry.RegisterWorkflow<TData>(wf);
        }

        public Task<bool> SuspendWorkflow(string workflowId)
        {
            return _workflowController.SuspendWorkflow(workflowId);
        }

        public Task<bool> ResumeWorkflow(string workflowId)
        {
            return _workflowController.ResumeWorkflow(workflowId);
        }

        public Task<bool> TerminateWorkflow(string workflowId)
        {
            return _workflowController.TerminateWorkflow(workflowId);
        }

        public void ReportStepError(WorkflowInstance workflow, WorkflowStep step, Exception exception)
        {
            OnStepError?.Invoke(workflow, step, exception);
        }

        public void Dispose()
        {
            if (!_shutdown)
                Abp.Threading.AsyncHelper.RunSync(async () => await Stop());
        }
    }
}
