using System;

namespace WorkflowCore.Exceptions
{
    public class WorkflowNotRegisteredException : Exception
    {
        public WorkflowNotRegisteredException(string workflowId, int? tenantId, int? version)
            : base($"Workflow {workflowId} version {version} tenant {tenantId} is not registered")
        {
        }
    }
}
