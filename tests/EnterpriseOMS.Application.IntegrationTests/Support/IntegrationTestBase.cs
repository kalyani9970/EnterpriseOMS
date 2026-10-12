using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.IntegrationTests.Support
{
    /// <summary>
    /// xUnit creates a new instance of the test class for every test, so each test gets
    /// its own TestHost (its own fresh database). That is the isolation guarantee.
    /// </summary>
    public abstract class IntegrationTestBase : IAsyncLifetime
    {
        protected TestHost App { get; private set; } = default!;
        public async Task InitializeAsync() => App = await TestHost.CreateAsync();
        public async Task DisposeAsync() => await App.DisposeAsync();
    }
}
