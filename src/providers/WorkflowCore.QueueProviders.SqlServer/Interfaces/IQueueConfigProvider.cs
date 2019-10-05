#region using

using WorkflowCore.Interface;
using WorkflowCore.QueueProviders.SqlServer.Models;

#endregion

namespace WorkflowCore.QueueProviders.SqlServer.Interfaces
{
    public interface IQueueConfigProvider
    {
        QueueConfig GetByQueue(QueueType queue);
    }
}