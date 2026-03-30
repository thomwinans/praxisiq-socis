using Snapp.TestHelpers;
using Xunit;

namespace Snapp.Service.Content.Tests;

[CollectionDefinition(DockerTestCollection.Name)]
public class LocalDockerTestCollection : ICollectionFixture<DockerTestFixture>;
