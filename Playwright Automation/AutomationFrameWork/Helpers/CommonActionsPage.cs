using System.Globalization;
using AutomationFrameWork.Drivers;
using Microsoft.Playwright;

namespace AutomationFrameWork.Pages
{
    public class CommonActionsPage
    {
        private readonly IPage _page;
        public string ProjectRoot;

        public CommonActionsPage(IPage page)
        {
            _page = page ?? throw new ArgumentNullException(nameof(page));
            ProjectRoot = GetProjectRoot();
        }

        public async Task NavigateToURLAsync(String url)
        {
            if (!PlaywrightDriver.HasSavedSession())
            {
                await ClearBrowserCache();
                Console.WriteLine("[Session] No saved session - cache cleared for fresh login.");
            }
            else
            {
                Console.WriteLine("[Session] Saved session detected - skipping cache clear.");
            }

            await _page.GotoAsync(url);

            if (!PlaywrightDriver.HasSavedSession())
                await WaitAsync(25);
        }

        public async Task WaitAsync(float seconds)
        {
            if (seconds > 0)
            {
                await _page.WaitForTimeoutAsync(seconds * 1000);
            }
        }

        public void LogCurrentPageUrl()
        {
            var currentUrl = _page.Url;
            Console.WriteLine("** Navigated to the URL : " + currentUrl);
        }

        public static string GetProjectRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory != null && !directory.GetFiles("*.csproj").Any())
            {
                directory = directory.Parent;
            }

            if (directory == null)
                throw new DirectoryNotFoundException("Could not find project root (.csproj)");

            return directory.FullName;
        }

        public string GetProjectRootPath()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory != null && !directory.GetFiles("*.csproj").Any())
            {
                directory = directory.Parent;
            }

            if (directory == null)
                throw new DirectoryNotFoundException("Could not find project root (.csproj)");

            return directory.FullName;
        }

        public async Task ClearBrowserCache()
        {
            // Clear storage (localStorage, sessionStorage, IndexedDB)
            await _page.EvaluateAsync(@"() => {
            try {
                localStorage.clear();
                sessionStorage.clear();
                
                // Clear IndexedDB databases
                if (window.indexedDB) {
                    window.indexedDB.databases().then(databases => {
                        databases.forEach(db => {
                            window.indexedDB.deleteDatabase(db.name);
                        });
                    });
                }
                
                // Clear cache storage if available
                if ('caches' in window) {
                    caches.keys().then(keyList => {
                        return Promise.all(keyList.map(key => {
                            return caches.delete(key);
                        }));
                    });
                }
            } catch(e) {
                console.error('Error clearing storage:', e);
            }
        }");

            // Optional: Clear service workers
            await _page.EvaluateAsync(@"() => {
            if ('serviceWorker' in navigator) {
                navigator.serviceWorker.getRegistrations().then(registrations => {
                    registrations.forEach(registration => {
                        registration.unregister();
                    });
                });
            }
        }");
        }

        public void DeleteExistingFilesInFileDownloadsFolder()
        {
            // Use GetProjectRoot() to set download folder at project root
            var downloadDirectory = Path.Combine(GetProjectRootPath(), "FileDownloads");
            Directory.CreateDirectory(downloadDirectory);

            // Clear any existing files in the folder before downloading
            foreach (var existingFile in Directory.GetFiles(downloadDirectory))
            {
                File.Delete(existingFile);
                Console.WriteLine($" Deleted existing file: {Path.GetFileName(existingFile)}");
            }
        }

        public async Task VerifyFileDownloadFunctionality(string fileName, string fileType, ILocator downloadLinkLocator)
        {
            var downloadDirectory = Path.Combine(GetProjectRootPath(), "FileDownloads");
            Directory.CreateDirectory(downloadDirectory);

            // ✅ Sets up the listener BEFORE the click, atomically
            var download = await _page.RunAndWaitForDownloadAsync(async () =>
            {
                await downloadLinkLocator.ClickAsync();
            }, new PageRunAndWaitForDownloadOptions
            {
                Timeout = 60000 // optional: increase if file is large or server is slow
            });

            var downloadedFilePath = Path.Combine(downloadDirectory, download.SuggestedFilename);
            await download.SaveAsAsync(downloadedFilePath);

            try
            {
                if (!File.Exists(downloadedFilePath))
                    throw new Exception($"Download failed: File not found at '{downloadedFilePath}'.");

                if (!Path.GetExtension(downloadedFilePath).Equals("." + fileType, StringComparison.OrdinalIgnoreCase))
                    throw new Exception($"Expected a .{fileType} file but got: '{Path.GetExtension(downloadedFilePath)}'.");

                if (new FileInfo(downloadedFilePath).Length == 0)
                    throw new Exception($"Downloaded {fileType} is empty or corrupt.");

                if (!download.SuggestedFilename.Contains(fileName, StringComparison.OrdinalIgnoreCase))
                    throw new Exception($"Downloaded file '{download.SuggestedFilename}' does not contain expected name '{fileName}'.");

                Console.WriteLine($"✅ Filename check passed: {download.SuggestedFilename} contains '{fileName}'.");
                Console.WriteLine($"✅ Download verified: {download.SuggestedFilename}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Download verification failed: {ex.Message}");
                throw;
            }
            finally
            {
                if (File.Exists(downloadedFilePath))
                {
                    File.Delete(downloadedFilePath);
                    Console.WriteLine($"🧹 Cleaned up: {downloadedFilePath}");
                }
            }
        }

        public bool IsAscending(List<int> numbers)
        {
            for (int i = 0; i < numbers.Count - 1; i++)
            {
                if (numbers[i] > numbers[i + 1])
                {
                    return false;
                }
            }
            return true;
        }

        public bool IsDescending(List<int> numbers)
        {
            for (int i = 0; i < numbers.Count - 1; i++)
            {
                if (numbers[i] < numbers[i + 1])
                {
                    return false;
                }
            }
            return true;
        }

        public bool IsAscendingDates(List<DateTime> dates)
        {
            for (int i = 0; i < dates.Count - 1; i++)
            {
                if (dates[i] > dates[i + 1])
                {
                    return false;
                }
            }
            return true;
        }

        public bool IsDescendingDates(List<DateTime> dates)
        {
            for (int i = 0; i < dates.Count - 1; i++)
            {
                if (dates[i] < dates[i + 1])
                {
                    return false;
                }
            }
            return true;
        }

        public async Task<List<int>> GetElementIntValuesAsync(ILocator locator)
        {
            var elements = await locator.AllTextContentsAsync();

            var result = new List<int>();

            foreach (var text in elements)
            {
                if (int.TryParse(text.Trim().Replace(",", ""), out int value))
                {
                    result.Add(value);
                }
            }

            return result;
        }

        public async Task<List<DateTime>> GetElementDateTimeValuesAsync(ILocator locator)
        {
            var elements = await locator.AllTextContentsAsync();

            var result = new List<DateTime>();

            foreach (var text in elements)
            {
                if (DateTime.TryParseExact(
                        text.Trim(),
                        "M/d/yyyy h:mm:ss tt",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out DateTime value))
                {
                    result.Add(value);
                }
            }

            return result;
        }

        public async Task ValidatePageTitle()
        {
            string title = await _page.TitleAsync();
            if (title.Trim().Contains("Document Generation - "))
            {
                Console.WriteLine($"Page title is displayed as Expected : {title}");
            }
            else
            {
                throw new Exception($"Page title is not as expected. Actual page title is : {title}");
            }
        }

        public int GetRandomLoanNumber()
        {

            Random random = new Random();
            int number = 0;

            number = random.Next(0000005, 999999999);

            Console.WriteLine("Generated Loannumber : " + number);

            return number;
        }

        public async Task UploadFile(ILocator button, string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Upload file not found: {filePath}");

            var fileChooser = await _page.RunAndWaitForFileChooserAsync(async () =>
            {
                await button.ClickAsync();
            }, new PageRunAndWaitForFileChooserOptions
            {
                Timeout = 10000 // optional: increase if file dialog takes time to appear
            });

            await fileChooser.SetFilesAsync(filePath);
            Console.WriteLine($"✅ File uploaded: {Path.GetFileName(filePath)}");
        }

        public async Task EnterTextInTheField(ILocator fieldLocator, string textToEnter)
        {
            await fieldLocator.ClearAsync();
            await fieldLocator.FillAsync(textToEnter);
            Console.WriteLine($"Entered text '{textToEnter}' in the field.");
        }

        public async Task TypeTextInTheField(ILocator fieldLocator, string textToEnter)
        {
            await fieldLocator.ClearAsync();
            await fieldLocator.TypeAsync(textToEnter, new LocatorTypeOptions
            {
                Delay = 150 // milliseconds between keystrokes
            });
            Console.WriteLine($"Entered text '{textToEnter}' in the field.");
        }

        public async Task ClickOnTheElement(ILocator elementLocator)
        {
            await elementLocator.ClickAsync();
            Console.WriteLine("Clicked on the element.");
        }

        public async Task SelectOptionFromDropdown(ILocator dropdownLocator, string optionToSelect)
        {
            await dropdownLocator.SelectOptionAsync(new SelectOptionValue { Label = optionToSelect });
            Console.WriteLine($"Selected '{optionToSelect}' from the dropdown.");
        }

        public async Task SelectOptionFromDropdownByIndex(ILocator dropdownLocator, int optionIndex)
        {
            // Playwright uses 0-based index
            await dropdownLocator.SelectOptionAsync(new SelectOptionValue
            {
                Index = optionIndex
            });

            Console.WriteLine($"Selected option at index '{optionIndex}' from the dropdown.");
        }

        public async Task CloseBrowser()
        {
            await _page.CloseAsync();
        }


    }
}
