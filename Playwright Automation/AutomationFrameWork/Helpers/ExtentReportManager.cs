using AutomationFrameWork.Pages;
using AventStack.ExtentReports;
using AventStack.ExtentReports.Reporter;

namespace AutomationFrameWork.Utilities
{
    public class ExtentReportManager
    {
        private static ExtentReports _extentReports;

        private static readonly object SyncLock = new();
        public static string ExtentReportName { get; private set; }
        public static string ReportsDirectory { get; private set; }

        public static ExtentReports GetExtentReport()
        {
            if (_extentReports == null)
            {
                lock (SyncLock)
                {
                    if (_extentReports == null)
                    {
                        var projectRoot = CommonActionsPage.GetProjectRoot();

                        ReportsDirectory = Path.Combine(projectRoot, "Reports");
                        Directory.CreateDirectory(ReportsDirectory);

                        ExtentReportName = $"ExtentReport_{DateTime.Now:yyyyMMdd_HHmmss}.html";

                        var reportFilePath = Path.Combine(
                            ReportsDirectory,
                            ExtentReportName
                        );

                        Console.WriteLine("Report Name : " + ExtentReportName);

                        var sparkReporter = new ExtentSparkReporter(reportFilePath);

                        sparkReporter.Config.ReportName = "Reqnroll Automation Report";
                        sparkReporter.Config.DocumentTitle = "Test Execution Report";
                        sparkReporter.Config.Theme =
                            AventStack.ExtentReports.Reporter.Config.Theme.Standard;

                        _extentReports = new ExtentReports();
                        _extentReports.AttachReporter(sparkReporter);
                    }
                }
            }

            return _extentReports;
        }

    }

    public static class ExtentTestManager
    {
        [ThreadStatic]
        private static ExtentTest _test;

        public static ExtentTest Test
        {
            get => _test;
            set => _test = value;
        }

    }
}
