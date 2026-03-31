using Snapp.TestHelpers;
using Xunit;

namespace Snapp.Service.Notification.Tests;

[CollectionDefinition(DockerTestCollection.Name)]
public class LocalDockerTestCollection : ICollectionFixture<DockerTestFixture>;
