using Snapp.TestHelpers;
using Xunit;

namespace Snapp.Service.Network.Tests;

[CollectionDefinition(DockerTestCollection.Name)]
public class LocalDockerTestCollection : ICollectionFixture<DockerTestFixture>;
