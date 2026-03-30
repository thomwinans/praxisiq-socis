using Snapp.TestHelpers;
using Xunit;

namespace Snapp.Service.User.Tests;

[CollectionDefinition(DockerTestCollection.Name)]
public class LocalDockerTestCollection : ICollectionFixture<DockerTestFixture>;
