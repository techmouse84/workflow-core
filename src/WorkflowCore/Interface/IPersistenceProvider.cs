﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WorkflowCore.Models;

namespace WorkflowCore.Interface
{
    /// <remarks>
    /// The implemention of this interface will be responsible for
    /// persisiting running workflow instances to a durable store    
    /// </remarks>
    public interface IPersistenceProvider
    {
        Task<string> CreateNewWorkflow(WorkflowInstance workflow);

        Task PersistWorkflow(WorkflowInstance workflow);

        Task<IEnumerable<string>> GetRunnableInstances(DateTime asAt);

        Task<IEnumerable<WorkflowInstance>> GetWorkflowInstances(WorkflowStatus? status, string type, DateTime? createdFrom, DateTime? createdTo, int skip, int take);

        Task<WorkflowInstance> GetWorkflowInstance(string Id);

        Task<string> CreateEventSubscription(EventSubscription subscription);

        Task<IEnumerable<EventSubscription>> GetSubcriptions(string eventName, string eventKey, DateTime asOf);

        Task TerminateSubscription(string eventSubscriptionId);

        Task<string> CreateEvent(Event newEvent);

        Task<Event> GetEvent(string id);

        Task<IEnumerable<string>> GetWorkflowInstanceIdsByUserId(long id, int? tenantId);

        IEnumerable<dynamic> GetListOfWorkflowEvent(List<string> listOfWorkflowIds);

        Task<IEnumerable<string>> GetRunnableEvents(DateTime asAt);

        Task<IEnumerable<string>> GetEvents(string eventName, string eventKey, DateTime? asOf, bool? runnable = null);

        Task MarkEventProcessed(string id);

        Task MarkEventUnprocessed(string id);

        Task PersistErrors(IEnumerable<ExecutionError> errors);

        void EnsureStoreExists();

    }
}
