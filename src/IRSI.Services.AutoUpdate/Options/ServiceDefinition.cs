namespace IRSI.Services.AutoUpdate.Options
{
    public class ServiceDefinition
    {
        public string Owner { get; set; }
        public string RepositoryName { get; set; }
        public string InstallationPath { get; set; }
        public string ServiceExecutable { get; set; }
        public bool SetupStoreId { get; set; }
        public bool SetupServiceBus { get; set; }
    }
}