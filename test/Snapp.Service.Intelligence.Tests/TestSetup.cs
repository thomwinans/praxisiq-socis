using Snapp.TestHelpers;
using Xunit;

namespace Snapp.Service.Intelligence.Tests;

[CollectionDefinition(DockerTestCollection.Name)]
public class LocalDockerTestCollection : ICollectionFixture<DockerTestFixture>;
