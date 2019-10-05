﻿using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using WorkflowCore.Models;

namespace WorkflowCore.Interface
{
    public interface IWorkflowHost : IWorkflowController
    {
        /// <summary>
        /// Start the workflow host, this enable execution of workflows
        /// </summary>
        Task Start();

        /// <summary>
        /// Stop the workflow host
        /// </summary>
        Task Stop();


        event StepErrorEventHandler OnStepError;
        void ReportStepError(WorkflowInstance workflow, WorkflowStep step, Exception exception);

        //public dependencies to allow for extension method access
        IPersistenceProvider PersistenceStore { get; }
        IDistributedLockProvider LockProvider { get; }
        IWorkflowRegistry Registry { get; }
        WorkflowOptions Options { get; }
        IQueueProvider QueueProvider { get; }
        ILogger Logger { get; }

    }

    public delegate void StepErrorEventHandler(WorkflowInstance workflow, WorkflowStep step, Exception exception);
}
