using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.IO;
using System.Threading;
using System.Globalization;
using HtmlAgilityPack;
using ScrapySharp.Extensions;
using ScrapySharp.Network;

namespace WebScraper
{
    public static class StringExtensions
    {
        public static bool Contains(this string source, string toCheck, StringComparison comp)
        {
            return source.IndexOf(toCheck, comp) >= 0;
        }
    }

    class Program
    {
        static IDictionary<string, int> itemCount = new Dictionary<string, int>();

        static string CreateLink(string page)
        {
            return string.Format(
                "http://listings.spafinder.com/search?keywords=Medical+Spa&spatypes=Medical+Spa&page={0}", 
                page);
        }

        static string GetName(string data)
        {
            foreach (Match match in Regex.Matches(data, "<span>(.+)<\\/span>\n<\\/h1>"))
            {
                return match.Groups[1].Value;
            }
            return string.Empty;
        }

        static string GetAddress(string data)
        {
            foreach (Match match in Regex.Matches(data, "<address>((.|\\n)+?)<\\/address>"))
            {
                return match.Groups[1].Value;
            }
            return string.Empty;
        }

        static string GetPhone(string data)
        {
            var pattern = "<div id=property-phone>(.+)<\\/div>";
            foreach (Match match in Regex.Matches(data, pattern))
            {
                return match.Groups[1].Value;
            }
            return string.Empty;
        }

        static string GetStreet(string data)
        {
            var pattern = "street-address>(.+?)<\\/span>";
            foreach (Match match in Regex.Matches(data, pattern))
            {
                return match.Groups[1].Value;
            }
            return string.Empty;
        }

        static string GetLocality(string data)
        {
            var pattern = "locality>(.+?)<\\/span>";
            foreach (Match match in Regex.Matches(data, pattern))
            {
                return match.Groups[1].Value;
            }
            return string.Empty;
        }

        static string GetRegion(string data)
        {
            var pattern = "region>(.+?)<\\/span>";
            foreach (Match match in Regex.Matches(data, pattern))
            {
                return match.Groups[1].Value;
            }
            return string.Empty;
        }

        static string GetPostalCode(string data)
        {
            var pattern = "postal-code>(.+?)<\\/span>";
            foreach (Match match in Regex.Matches(data, pattern))
            {
                return match.Groups[1].Value;
            }
            return string.Empty;
        }

        static string GetCountryName(string data)
        {
            var pattern = "country-name>(\\S|.+?)<\\/span>";
            foreach (Match match in Regex.Matches(data, pattern))
            {
                return match.Groups[1].Value;
            }
            return string.Empty;
        }

        static string GetMail(string data)
        {
            var pattern = "<div class=property-email data1=(.+) data2=(.+) data3=(.+) data4";
            foreach (Match match in Regex.Matches(data, pattern))
            {
                var data1 = match.Groups[1].Value.Trim(' ', '"');
                var data2 = match.Groups[2].Value.Trim(' ', '"');
                if (string.IsNullOrWhiteSpace(data1)|| string.IsNullOrWhiteSpace(data2))
                {
                    continue;
                }
                return data1 + "@" + data2;
            }
            return string.Empty;
        }

        static string GetCacheName(string url)
        {
            var cacheName = url;
            foreach (var symvol in Path.GetInvalidPathChars())
            {
                cacheName = cacheName.Replace(symvol + "", "");
            }
            return cacheName.Replace("/", "").Replace(":", "").Replace("?", "");
        }

        static string DownloadPageWithCache(string url)
        {
            var name = Path.GetFileName(url);
            var filePath = Path.Combine("cache", GetCacheName(url));
            if (File.Exists(filePath))
            {
                return File.OpenText(filePath).ReadToEnd();
            }
            string data = string.Empty;
            //var browser = new ScrapingBrowser();
            //var page = browser.NavigateToPage(new Uri(url));
            //data = browser.AjaxDownloadString(new Uri(url));
            data = new WebClient().DownloadString(url);
            Directory.CreateDirectory("cache");
            var writer = File.CreateText(filePath);
            writer.Write(data);
            writer.Close();
            return data;
        }

        static string DownloadPage(string url)
        {
            try
            {
                var data = DownloadPageWithCache(url);
                var name = GetName(data);
                var street = GetStreet(data);
                var locality = GetLocality(data);
                var region = GetRegion(data);
                var countryname = GetCountryName(data);
                var postalcode = GetPostalCode(data);
                var phone = GetPhone(data);
                var email = GetMail(data);

                //"Name,Street,Locality,Region,PostalCode,Country,Phone,Email";
                var template = "\"{0}\",\"{1}\",\"{2}\",\"{3}\",\"{4}\",\"{5}\",\"{6}\",\"{7}\"";
                return string.Format(template,
                    name, street, locality, region, postalcode, countryname, phone, email);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            return string.Empty;
        }

        static IList<string> GetItems(string url)
        {
            var items = new List<string>();
            var data = DownloadPageWithCache(url);

            foreach (Match match in Regex.Matches(data, "<a href=\\\"(.+?)\\\" class=spa-name"))
            {
                items.Add(match.Groups[1].Value);
            }

            return items;
        }

        static IList<string> GetItemsMultipage()
        {
            var items = new List<string>();
            IList<string> nextItems;
            var page = 0;
            do
            {
                ++page;
                nextItems = GetItems(CreateLink(page.ToString()));
                items.AddRange(nextItems);
            }
            while (nextItems.Count > 0 && page < 18);

            return items;
        }
        
        static StreamWriter CreateImportCSVFile(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var header = "\"Name\",\"Street\",\"Locality\",\"Region\",\"PostalCode\",\"Country\",\"Phone\",\"Email\"";
            var file = File.CreateText(path);
            file.WriteLine(header);
            return file;
        }

        static async Task<string> DownloadItemsAsync(IList<string> items)
        {
            Console.WriteLine("Start download items. Size: {0}", items.Count);
            return string.Join("\n", await Task.WhenAll(
                items.Select(item => 
                    Task.Run(() =>
                        DownloadPage(item)
                ))));
        }

        static void Load(string to)
        {
            var file = CreateImportCSVFile(Path.Combine(to, "spafinder.csv"));
            var items = GetItemsMultipage();
            Console.WriteLine("Start download: Size: {0}", items.Count);
            file.Write(DownloadItemsAsync(items).Result);
            file.Close();
            Console.WriteLine("Download ended. Downloaded {0} items.", items.Count);
        }

        static void DisplayHelp()
        {
            Console.WriteLine(@"
Web Scraper For zhats.com v1.0.0  released: September 29, 2016
Copyright (C) 2016 Konstantin S.
https://www.upwork.com/fl/havendv

Usage:
    webscraper.exe <pathtooutputdir>
    - pathtooutputdir - Directory for save output csv files. Example: C:\\zhats.com\\

");
        }
        private static bool HelpRequired(string param)
        {
            return param == "-h" || param == "--help" || param == "/?";
        }

        static void Main(string[] args)
        {
            try
            {
                if (args.Length < 1 || HelpRequired(args[0]))
                {
                    DisplayHelp();
                    Console.ReadKey();
                    return;
                }

                var outputDir = args[0];
                Console.WriteLine("Download started.");
                Load(outputDir);
                Console.WriteLine("Download ended.");
            }
            catch(Exception e)
            {
                Console.WriteLine("Exception: {0}", e.Message);
            }
            Console.ReadKey();
        }
    }
}
