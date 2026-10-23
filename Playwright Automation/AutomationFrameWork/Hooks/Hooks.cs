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
        private static ConfigReader _configReader;

        private static ExtentReports _extentReports;
        private ExtentTest _extentScenario;

        private readonly MailHelper _mailHelper;

        private static int _executedScenarioCount = 0;

        // Tracks scenarios whose setup completed, so one failed setup
        private static int _successfulSetupCount = 0;

        private static string? _testRunException;

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
            var projectRoot = CommonActionsPage.GetProjectRoot();
            var configuration = new ConfigurationBuilder()
                .SetBasePath(projectRoot)
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            var configReader = configuration.GetSection("Credentials").Get<ConfigReader>();
            var automationSettings = configuration.GetSection("AutomationSettings").Get<AutomationSettings>();

            configReader.AutomationSettings = automationSettings;

            _configReader = configReader;

            _extentReports = ExtentReportManager.GetExtentReport("Document Generation", _configReader.AutomationSettings);
        }

        [AfterTestRun]
        public static void AfterTestRun()
        {
            // Only write the "no execution" report when no scenario got past setup.
            // Otherwise flush the normal report (failed setups are already marked Fail in it).
            if (_successfulSetupCount == 0 && _testRunException != null)
            {
                CreateNoExecutionReport(_testRunException);
            }
            else
            {
                _extentReports?.Flush();
                //_mailHelper.SendEmailReport();
            }
        }

        [BeforeFeature]
        public static void BeforeFeature(FeatureContext featureContext)
        {
            var feature = _extentReports.CreateTest(featureContext.FeatureInfo.Title);
            featureContext.Set(feature, "ExtentFeature");
        }

        // Order = 0 guarantees the browser is created before any other BeforeScenario hook runs
        [BeforeScenario(Order = 0)]
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

                Interlocked.Increment(ref _successfulSetupCount);
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
                // Use the execution status, not just TestError. Undefined, pending and
                // binding-error steps leave TestError null and were being reported as Passed.
                var status = _scenarioContext.ScenarioExecutionStatus;
                bool testFailed = status != ScenarioExecutionStatus.OK;
                bool isAuthFailure = testFailed && _playwrightDriver.IsOnLoginPage();

                if (_scenarioContext.TryGetValue("ExtentScenario", out ExtentTest extentScenario))
                {
                    if (testFailed)
                    {
                        var reason = _scenarioContext.TestError?.Message
                                     ?? $"Scenario ended with status: {status}";

                        extentScenario.Fail($"<b>Scenario Failed:</b> {reason}");
                    }
                    else
                    {
                        extentScenario.Pass("<b>Scenario Passed</b>");
                    }
                }

                // Collapsed duplicate branches — save session unless it was an auth failure
                if (isAuthFailure)
                {
                    PlaywrightDriver.InvalidateSession();
                }
                else
                {
                    await _playwrightDriver.SaveSessionAsync();
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

            var extentScenario = scenarioContext.TryGetValue("ExtentScenario", out ExtentTest storedScenario)
                ? storedScenario
                : ExtentTestManager.Test;

            if (extentScenario == null) return;

            var status = scenarioContext.ScenarioExecutionStatus;

            // Step passed only when the execution status is OK
            if (status == ScenarioExecutionStatus.OK)
            {
                extentScenario.Pass($"{stepType} {stepText}");
                return;
            }

            // Pending / undefined / binding-error steps have no TestError
            // but are not passes — log them as Fail instead of Pass
            if (scenarioContext.TestError == null)
            {
                extentScenario.Fail(
                    $"{stepType} {stepText}" +
                    $"<br>* Reason: Step did not pass (status: {status})");
                return;
            }

            // Removed the "Skipped" branch. AfterStep does not run for skipped steps,
            // and that branch was wrongly logging the actual failed step as Skipped.
            var base64Screenshot = await CaptureScreenshotAsBase64Async();
            var failMessage = $"{stepType} {stepText}" +
                              $"<br>* Reason: {scenarioContext.TestError.Message}";

            if (!string.IsNullOrEmpty(base64Screenshot))
            {
                extentScenario.Fail(
                    failMessage + "<br>* Failure Screenshot : ",
                    MediaEntityBuilder
                        .CreateScreenCaptureFromBase64String(base64Screenshot)
                        .Build());
            }
            else
            {
                extentScenario.Fail(
                    failMessage + "<br>* Screenshot: Could not be captured (headless/context closed)");
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
            return Encoding.UTF8.GetString(bytes);
        }

        private async Task<string?> CaptureScreenshotAsBase64Async()
        {
            try
            {
                if (_playwrightDriver.Page == null) return null;

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
            var extent = ExtentReportManager.GetExtentReport("Document Generation", _configReader.AutomationSettings);
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