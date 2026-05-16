using ClosedXML.Excel;
using System.Text.Json;

namespace AutomationFrameWork.Utilities
{
    public class FileDataReaders
    {
        public void ReadDetailsFromExcel(String excelFilePathToRead)
        {
            string filePath = excelFilePathToRead;

            using (XLWorkbook workbook = new XLWorkbook(filePath))
            {
                IXLWorksheet worksheet = workbook.Worksheet(1); // First sheet

                int rowCount = worksheet.LastRowUsed().RowNumber();

                for (int row = 2; row <= rowCount; row++) // Skip header
                {
                    string username = worksheet.Cell(row, 1).GetString();
                    string password = worksheet.Cell(row, 2).GetString();
                    string role = worksheet.Cell(row, 3).GetString();

                    Console.WriteLine($"Username: {username}");
                    Console.WriteLine($"Password: {password}");
                    Console.WriteLine($"Role: {role}");
                    Console.WriteLine("-----------------------");

                    // You can now use these variables in Playwright login
                }
            }
        }

        public void ReadDataFromJson(string jsonFilePath)
        {
            Console.WriteLine(jsonFilePath);

            string jsonString = File.ReadAllText(jsonFilePath);

            JsonObjects jsonObjects = JsonSerializer.Deserialize<JsonObjects>(jsonString);

            Console.WriteLine($"Name: {jsonObjects.Name}");
            Console.WriteLine($"Age: {jsonObjects.Age}");
            Console.WriteLine($"City: {jsonObjects.City}");

            Console.WriteLine("Skills:");
            foreach (var skill in jsonObjects.Skills)
            {
                Console.WriteLine($"- {skill}");
            }
        }

    }

}
