using System;

namespace AutomationFrameWork.Utilities
{
    public class ConfigReader
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public UrlSettings Urls { get; set; }
        public AutomationSettings AutomationSettings { get; set; }
    }

    public class UrlSettings
    {
        public string ApplicationURL { get; set; }
    }

    public class AutomationSettings
    {
        public string Channel { get; set; }
        public string Browser { get; set; }
        public bool Headless { get; set; }
        public float Timeout { get; set; }
        public int SlowMo { get; set; }
        public int targetHeight { get; set; }
        public int targetWidth { get; set; }

    }

    public class JsonObjects
    {
        public string Name { get; set; }
        public int Age { get; set; }
        public string City { get; set; }
        public List<string> Skills { get; set; }
    }

}
