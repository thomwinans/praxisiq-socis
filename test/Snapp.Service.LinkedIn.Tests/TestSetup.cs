using Snapp.TestHelpers;
using Xunit;

namespace Snapp.Service.LinkedIn.Tests;

[CollectionDefinition("Docker")]
public class LocalDockerTestCollection : ICollectionFixture<DockerTestFixture>;
