using ssvv_th.Tests.Helpers;
using Xunit;

namespace ssvv_th.Tests.GuiTests
{
    [CollectionDefinition("GuiWebCollection")]
    public class GuiWebCollection : ICollectionFixture<CustomWebApplicationFactory<Program>>
    {
    }
}
