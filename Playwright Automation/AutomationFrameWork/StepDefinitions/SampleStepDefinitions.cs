using AutomationFrameWork.Drivers;
using AutomationFrameWork.PageActions;
using AutomationFrameWork.Utilities;
using System.Threading.Tasks;

namespace AutomationFrameWork.StepDefinitions
{
    [Binding]
    public class SampleStepDefinitions : BaseSteps
    {

        // Lazy init — only created when first step runs, by then Page is not null
        private SamplePageAction _SearchPageAction => new SamplePageAction(Context, _driver.Page);

        public SampleStepDefinitions(PlaywrightDriver playwrightDriver, ScenarioContext scenarioContext) : base(playwrightDriver, scenarioContext)
        {
        }

        [Given("I navigate to the application url")]
        public async Task GivenINavigateToTheApplicationUrlAsync()
        {
            var configReader = Context.ScenarioContext.Get<ConfigReader>("ConfigReader");
            await Context.CommonActions.NavigateToURLAsync(configReader.Urls.ApplicationURL);
        }

        [When("I search some data")]
        public async Task WhenISearchSomeData()
        {
            await _SearchPageAction.searchData("Playwright Code");
        }

        [Then("I close the browser")]
        public async Task ThenICloseTheBrowser()
        {
            await Context.CommonActions.CloseBrowser();
        }




    }
}
