#region using

using System.Data;
using System.Data.Common;

#endregion

namespace WorkflowCore.QueueProviders.SqlServer.Interfaces
{
    public interface ISqlCommandExecutor
    {
        TResult ExecuteScalar<TResult>(IDbConnection cn, IDbTransaction tx, string cmdtext, params DbParameter[] parameters);
        int ExecuteCommand(IDbConnection cn, IDbTransaction tx, string cmdtext, params DbParameter[] parameters);
    }
}