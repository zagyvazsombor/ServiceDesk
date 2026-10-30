namespace ServiceDesk.Web.Tests.Http;

// The test classes on the ServiceDesk_WebTests database share one fixture and run one after the other, so the database
// is recreated once and never while another class uses it.
[CollectionDefinition(nameof(ProductionDatabaseCollection))]
public sealed class ProductionDatabaseCollection : ICollectionFixture<ProductionDatabaseFixture>;
