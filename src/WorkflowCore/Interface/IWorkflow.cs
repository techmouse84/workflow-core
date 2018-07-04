﻿namespace WorkflowCore.Interface
{
    public interface IWorkflow<TData>
        where TData : new()
    {
        string Id { get; }
        int Version { get; }
        int? TenantId { get; }
        void Build(IWorkflowBuilder<TData> builder);
    }

    public interface IWorkflow : IWorkflow<object>
    {
    }
}
