using Microsoft.Playwright;

namespace AutomationFrameWork.PageElements
{
    public class SamplePageObjects
    {

        private readonly IPage _page;
        public SamplePageObjects(IPage page)
        {
            _page = page;
        }

        private string searchFieldLocator => "//textarea[@name='q']";

        public ILocator SearchField => _page.Locator(searchFieldLocator);



    }
}
