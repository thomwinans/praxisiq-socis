using Snapp.TestHelpers;
using Xunit;

namespace Snapp.Service.Auth.Tests;

[CollectionDefinition(DockerTestCollection.Name)]
public class LocalDockerTestCollection : ICollectionFixture<DockerTestFixture>;
