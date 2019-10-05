namespace WorkflowCore.QueueProviders.SqlServer.Interfaces
{
    public interface ISqlServerQueueProviderMigrator
    {
        void MigrateDb();
        void CreateDb();
        void EnableBroker();
    }
}
