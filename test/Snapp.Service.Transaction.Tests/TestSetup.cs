using Snapp.TestHelpers;
using Xunit;

namespace Snapp.Service.Transaction.Tests;

[CollectionDefinition(DockerTestCollection.Name)]
public class LocalDockerTestCollection : ICollectionFixture<DockerTestFixture>;
