using Snapp.TestHelpers;
using Xunit;

namespace Snapp.Service.DigestJob.Tests;

[CollectionDefinition(DockerTestCollection.Name)]
public class LocalDockerTestCollection : ICollectionFixture<DockerTestFixture>;
