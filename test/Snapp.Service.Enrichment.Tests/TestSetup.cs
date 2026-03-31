using Snapp.TestHelpers;
using Xunit;

namespace Snapp.Service.Enrichment.Tests;

[CollectionDefinition(DockerTestCollection.Name)]
public class LocalDockerTestCollection : ICollectionFixture<DockerTestFixture>;
