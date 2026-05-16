using AutomationFrameWork.PageElements;
using AutomationFrameWork.Utilities;
using Microsoft.Playwright;

namespace AutomationFrameWork.PageActions
{
    public class SamplePageAction : SamplePageObjects
    {
        private readonly TestContext _testContext;

        public SamplePageAction(TestContext context, IPage page) : base(page)
        {
            _testContext = context;
        }

        public async Task searchData(string searchText)
        {
            await SearchField.FillAsync(searchText);
            await SearchField.PressAsync("Enter");
            await _testContext.CommonActions.WaitAsync(3);
        }

    }
}
