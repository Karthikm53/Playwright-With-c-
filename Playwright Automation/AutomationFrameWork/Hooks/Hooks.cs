using AutomationFrameWork.Drivers;
using AutomationFrameWork.Pages;
using AutomationFrameWork.Utilities;
using AventStack.ExtentReports;
using Microsoft.Extensions.Configuration;
using System.Text;

namespace AutomationFrameWork.Hooks
{
    [Binding]
    public class Hooks
    {
        private readonly PlaywrightDriver _playwrightDriver;
        private readonly ScenarioContext _scenarioContext;
        private readonly FeatureContext _featureContext;

        private static ExtentReports _extentReports;
        private ExtentTest _extentScenario;

        private readonly MailHelper _mailHelper;

        private static int _executedScenarioCount = 0;
        private static String? _testRunException;


        public Hooks(PlaywrightDriver playwrightDriver, ScenarioContext scenarioContext, FeatureContext featureContext)
        {
            _playwrightDriver = playwrightDriver;
            _scenarioContext = scenarioContext;
            _featureContext = featureContext;
            _mailHelper = new MailHelper();
        }

        [BeforeTestRun]
        public static void BeforeTestRun()
        {
            _extentReports = ExtentReportManager.GetExtentReport();
        }

        [AfterTestRun]
        public static void AfterTestRun()
        {
            if (_testRunException != null)
            {
                CreateNoExecutionReport(_testRunException);
            }
            else
            {
                _extentReports.Flush();
                //_mailHelper.SendEmailReport();
            }
        }

        [BeforeFeature]
        public static void BeforeFeature(FeatureContext featureContext)
        {
            var feature = _extentReports.CreateTest(featureContext.FeatureInfo.Title);
            featureContext.Set(feature, "ExtentFeature");
        }

        [BeforeScenario]
        public async Task BeforeScenarioAsync()
        {
            ExtentTest? extentFeature = null;

            try
            {
                LoadConfiguration();

                Interlocked.Increment(ref _executedScenarioCount);

                extentFeature = _featureContext.Get<ExtentTest>("ExtentFeature");
                _extentScenario = extentFeature.CreateNode(_scenarioContext.ScenarioInfo.Title);

                ExtentTestManager.Test = _extentScenario;
                _scenarioContext["ExtentScenario"] = _extentScenario;

                await _playwrightDriver.InitializeAsync();
            }
            catch (Exception ex)
            {
                _testRunException = ex.Message;

                _extentScenario?.Fail(
                    "<b>Scenario setup failed.</b><br>" +
                    $"<b>Message:</b> {ex.Message}"
                );

                extentFeature?.Fail("Feature failed during scenario setup.");

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Scenario setup failed:");
                Console.WriteLine(ex);
                Console.ResetColor();

                throw;
            }
        }

        [AfterScenario]
        public async Task AfterScenarioAsync()
        {
            try
            {
                bool testFailed = _scenarioContext.TestError != null;
                bool isAuthFailure = testFailed && _playwrightDriver.IsOnLoginPage();

                if (isAuthFailure)
                {
                    // The browser ended up on the login page — session is definitely broken.
                    // Wipe it so next run does a fresh login.
                    PlaywrightDriver.InvalidateSession();
                    Console.WriteLine("[Session] Auth failure detected (redirected to login) — session invalidated.");
                }
                else if (testFailed)
                {
                    // Test failed for a NON-auth reason (bad selector, data issue, etc.)
                    // Session is still valid — keep it and refresh the timestamp so it doesn't expire.
                    await _playwrightDriver.SaveSessionAsync();
                    Console.WriteLine("[Session] Test failed (non-auth reason) — session preserved and timestamp refreshed.");
                }
                else
                {
                    // Test passed — always refresh the session & timestamp so the 24hr clock resets.
                    await _playwrightDriver.SaveSessionAsync();
                    Console.WriteLine("[Session] Test passed — session saved/refreshed.");
                }
            }
            finally
            {
                await _playwrightDriver.CleanupAsync();
            }
        }

        [AfterStep]
        public async Task AfterStep(ScenarioContext scenarioContext)
        {
            var stepText = scenarioContext.StepContext.StepInfo.Text;
            var stepType = scenarioContext.StepContext.StepInfo.StepDefinitionType;

            ExtentTest extentScenario;

            if (scenarioContext.TryGetValue("ExtentScenario", out ExtentTest storedScenario))
            {
                extentScenario = storedScenario;
            }
            else
            {
                extentScenario = ExtentTestManager.Test;
            }

            if (extentScenario == null)
                return;

            if (scenarioContext.TestError == null)
            {
                extentScenario.Pass($"{stepType} {stepText}");
            }
            else
            {
                var base64Screenshot = await CaptureScreenshotAsBase64Async();
                if (!string.IsNullOrEmpty(base64Screenshot))
                {
                    extentScenario.Fail($"{stepType} {stepText} " +
                    $"<br>* Reason: {_scenarioContext.TestError.Message} " +
                    $"<br>* Failure Screenshot : ", MediaEntityBuilder
                            .CreateScreenCaptureFromBase64String(base64Screenshot)
                            .Build());
                }
            }
        }

        private void LoadConfiguration()
        {
            var projectRoot = CommonActionsPage.GetProjectRoot();
            Console.WriteLine("Project Root : " + projectRoot);

            var configuration = new ConfigurationBuilder()
                .SetBasePath(projectRoot)
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            var configReader = configuration.GetSection("Credentials").Get<ConfigReader>();
            var urls = configuration.GetSection("Urls").Get<UrlSettings>();
            var automationSettings = configuration.GetSection("AutomationSettings").Get<AutomationSettings>();

            configReader.Password = DecodeBase64(configReader.Password);
            configReader.Urls = urls;
            configReader.AutomationSettings = automationSettings;

            _scenarioContext["ConfigReader"] = configReader;
        }

        private static string DecodeBase64(string base64Text)
        {
            var bytes = Convert.FromBase64String(base64Text);
            var decodedValue = Encoding.UTF8.GetString(bytes);

            return decodedValue;
        }

        private async Task<string?> CaptureScreenshotAsBase64Async()
        {
            try
            {
                var screenshotBytes = await _playwrightDriver.Page.ScreenshotAsync(new()
                {
                    FullPage = true
                });

                return Convert.ToBase64String(screenshotBytes);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Base64 screenshot capture failed: " + ex.Message);
                return null;
            }
        }

        private static void CreateNoExecutionReport(string exception)
        {
            var extent = ExtentReportManager.GetExtentReport();
            var test = extent.CreateTest("Test Execution Status");

            test.Fail(
                "<b>No scenarios were executed in this test run.</b><br>" +
                "<span style='color:red;'><b>Execution failed due to error:</b></span><br>" +
                $"<b>Message:</b> {exception}<br>"
            );

            extent.Flush();
        }
    }
}