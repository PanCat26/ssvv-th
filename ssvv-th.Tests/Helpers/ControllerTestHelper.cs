using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace ssvv_th.Tests.Helpers
{
    public sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }

    public static class ControllerTestHelper
    {
        public static void AttachTempData(Controller controller)
        {
            controller.TempData = new TempDataDictionary(new DefaultHttpContext(), new NullTempDataProvider());
        }
    }
}
