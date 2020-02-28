using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Collections.Specialized;
using System.Net;
using System.IO;
using HtmlAgilityPack;
using Newtonsoft.Json;
using System.Collections;

namespace FreesoundDownloader
{
    class Program
    {
        static string fsusername = "";
        static string fspassword = "";
        static string sessionid = "";
        static string apiUrl = "";

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("ERROR: No command line arguments found. Use 'help' to see available options");
                Console.Read();
                Environment.Exit(0);
            }

            UriBuilder builder = new UriBuilder("https://freesound.org/apiv2/search/text/");
            builder.Port = -1;
            NameValueCollection query = HttpUtility.ParseQueryString(builder.Query);
            //default sort, can be overridden
            query["sort"] = "created_asc";

            string filter = "";
            
            //check arguments
            foreach (var item in args)
            {
                if (item == "help")
                {
                    printHelp();
                }
                else if (item.Contains("user="))
                {
                    var split = item.Split('=');
                    string queryName = split[0];
                    string value = split[1];
                    fsusername = value;
                }
                else if (item.Contains("pass="))
                {
                    var split = item.Split('=');
                    string queryName = split[0];
                    string value = split[1];
                    fspassword = value;
                }
                else if (item.Contains("license="))
                {
                    var split = item.Split('=');
                    string queryName = split[0];
                    string value = split[1];

                    if (value == "atrib")
                    {
                        value = "Attribution";
                    }
                    else if (value == "atribnon")
                    {
                        value = "\"Attribution Noncommercial\"";
                    }
                    else if (value == "cc0")
                    {
                        value = "\"Creative Commons 0\"";
                    }
                    else
                    {
                        Console.WriteLine("ERROR: Filter License invalid");
                        Console.Read();
                        Environment.Exit(0);
                    }

                    filter = String.Concat(filter, queryName + ":" + value + " ");
                }
                else if (item.Contains("type="))
                {
                    string[] types = new string[] { "flac", "wav", "aiff", "m4a", "ogg", "mp3" };

                    var split = item.Split('=');
                    string queryName = split[0];
                    string value = split[1];

                    if (types.Any(value.Contains))
                    {

                        filter = String.Concat(filter, queryName + ":" + value + " ");
                    }
                    else
                    {
                        Console.WriteLine("ERROR: Filter Type invalid");
                        Console.Read();
                        Environment.Exit(0);
                    }
                }
                else if (item.Contains("samplerate="))
                {
                    var split = item.Split('=');
                    string queryName = split[0];
                    string value = split[1];
                    int valueout;

                    if (int.TryParse(value, out valueout))
                    {
                        filter = String.Concat(filter, queryName + ":" + valueout + " ");
                    }
                    else
                    {
                        Console.WriteLine("ERROR: Samplerate invalid");
                        Console.Read();
                        Environment.Exit(0);
                    }
                }
                else if (item.Contains("username="))
                {
                    var split = item.Split('=');
                    string queryName = split[0];
                    string value = split[1];

                    if (!String.IsNullOrWhiteSpace(value))
                    {
                        filter = String.Concat(filter, queryName + ":" + value + " ");
                    }
                    else
                    {
                        Console.WriteLine("ERROR: Username invalid");
                        Console.Read();
                        Environment.Exit(0);
                    }
                }
                else if (item.Contains("tag="))
                {
                    var split = item.Split('=');
                    string queryName = split[0];
                    string value = split[1];

                    if (!String.IsNullOrWhiteSpace(value))
                    {
                        filter = String.Concat(filter, queryName + ":" + value + " ");
                    }
                    else
                    {
                        Console.WriteLine("ERROR: Tag invalid");
                        Console.Read();
                        Environment.Exit(0);
                    }
                }
                else if (item.Contains("sort="))
                {
                    string[] sorttypes = new string[] { "score", "duration_desc", "duration_asc", "created_desc", "created_asc", "downloads_desc", "downloads_asc", "rating_desc", "rating_asc" };

                    var split = item.Split('=');
                    string queryName = split[0];
                    string value = split[1];

                    if (sorttypes.Any(value.Contains))
                    {
                        query[queryName] = value;
                    }
                    else
                    {
                        Console.WriteLine("ERROR: Sort type invalid");
                        Console.Read();
                        Environment.Exit(0);
                    }
                }
            }

            //check to see if username or pass exists
            if (String.IsNullOrWhiteSpace(fspassword) || String.IsNullOrWhiteSpace(fsusername))
            {
                Console.WriteLine("ERROR: Freesound.org Username or Password not specified");
                Console.Read();
                Environment.Exit(0);
            }

            //if any filters are used then add the query here
            if (!String.IsNullOrWhiteSpace(filter))
            {
                query["filter"] = filter;
            }

            query["format"] = "json";
            query["page"] = "1";
            query["page_size"] = "150";
            query["fields"] = "name,username,id,type,url";
            query["token"] = "FiiBj60WhFDf7IxRetbspC6z694CH0qsKeDzjnPS";
            builder.Query = query.ToString();
            apiUrl = builder.ToString();
            
            login();
            processJson();

            Console.WriteLine("All downloads finished!");
            Console.Read();
        }
        
        static void processJson()
        {
            string data = "";

            //download json to string
            try
            {
                WebClient wc = new WebClient();
                data = wc.DownloadString(apiUrl);
            }
            catch (WebException ex)
            {
                Console.WriteLine(ex);
            }

            //deserialize json to object to itterate through them
            RootApiJson json = JsonConvert.DeserializeObject<RootApiJson>(data);

            //check to see if there is anything to download 
            if (json.count == 0)
            {
                Console.WriteLine("No results found or arguments are incorrect");
                Console.Read();
                Environment.Exit(0);
            }

            //known valid upload extensions, used to contruct final filename. 
            string[] extensions = new string[] { ".flac", ".wav", ".aif", ".aiff", ".m4a", ".ogg", ".mp3" };

            //itterate through each item in the json results (150 results per page)
            foreach (var item in json.results)
            {
                string filename = "";

                //not all sound names are free from file extensions, so if it contains one, remove it, just for cosmetic purposes,
                //then construct a real filename based on sound id, username and sound name with the type as extension.
                if (extensions.Any(item.name.ToLowerInvariant().Contains))
                {
                    foreach (var extension in extensions)
                    {
                        if (item.name.Contains(extension))
                        {
                            filename = item.id + "-" + item.username + "-" + item.name.Replace(extension, "") + "." + item.type;
                            break;
                        }
                    }
                }
                else
                {
                    filename = item.id + "-" + item.username + "-" + item.name + "." + item.type;
                }

                //santize filename just incase its not windows path compatible
                filename = MakeValidFileName(filename);

                //no need for full url of file, just tack download/ onto the end of the sound url
                string url = item.url + "download/";

                downloadAudio(url, filename);
            }

            //check to see if there is a next page, if so, process it
            if (json.next != null)
            {
                apiUrl = json.next + "&token=FiiBj60WhFDf7IxRetbspC6z694CH0qsKeDzjnPS";
                processJson();
            }
        }
        
        static void login()
        {
            string url = "https://freesound.org/home/login/";
            string csrftoken = "";
            
            try
            {
                //Request the login page to fetch the csrfmiddletoken and csrftoken
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.CookieContainer = new CookieContainer();
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();

                //get csrftoken from cookies
                foreach (Cookie cook in response.Cookies)
                {
                    if (cook.Name == "csrftoken")
                    {
                        csrftoken = cook.Value;
                    }
                }

                //get the response page source code 
                Stream receiveStream = response.GetResponseStream();
                StreamReader readStream = new StreamReader(receiveStream); ;
                string data = readStream.ReadToEnd();
                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(data);

                //parse the source code and get the csrfmiddlewaretoken from the form input field
                HtmlNode forminputid = doc.DocumentNode.SelectSingleNode("//input[@name='csrfmiddlewaretoken']");
                string csrfmidtoken = forminputid.GetAttributeValue("value", "");

                //send login request using the previously fetched tokens
                request = (HttpWebRequest)WebRequest.Create(url);
                request.AllowAutoRedirect = false;
                request.Headers.Add("Origin", @"https://freesound.org");
                request.ContentType = "application/x-www-form-urlencoded";
                request.UserAgent = "Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/82.0.4070.0 Safari/537.36";
                request.Referer = "https://freesound.org/home/login/";
                request.Headers.Set(HttpRequestHeader.Cookie, @"csrftoken=" + csrftoken + ";");
                request.Method = "POST";

                string postdata = @"csrfmiddlewaretoken=" + csrfmidtoken + "&username=" + fsusername + "&password=" + fspassword + "&next=";
                byte[] postBytes = Encoding.UTF8.GetBytes(postdata);
                request.ContentLength = postBytes.Length;

                Stream stream = request.GetRequestStream();
                stream.Write(postBytes, 0, postBytes.Length);
                stream.Close();

                //parse login response and grab sessionid from set cookie header
                response = (HttpWebResponse)request.GetResponse();
                for (int i = 0; i < response.Headers.Count; i++)
                {
                    string name = response.Headers.GetKey(i);
                    if (name != "Set-Cookie")
                    {
                        continue;
                    }
                    string value = response.Headers.Get(i);
                    foreach (var singleCookie in value.Split(','))
                    {
                        if (singleCookie.Contains("sessionid"))
                        {
                            sessionid = singleCookie.Split(';')[0].ToString().Split('=')[1].ToString();
                        }
                    }
                }
            }
            catch (WebException ex)
            {
                Console.WriteLine(ex);
            }
        }

        static void downloadAudio(string url, string filename)
        {
            Console.Write("Downloading: " + filename + "... ");
            try
            {
                //if file already downloaded, skip it
                if (File.Exists(filename))
                {
                    Console.WriteLine("File exists, skipping.. ");
                }
                else
                {
                    WebClient wc = new WebClient();
                    wc.Headers.Add(HttpRequestHeader.Cookie, "sessionid=" + sessionid);
                    wc.DownloadFile(url, filename);
                    Console.WriteLine("Download Complete.");
                }
            }
            catch (WebException ex)
            {
                Console.WriteLine(ex);
            }
        }

        static void printHelp()
        {
            Console.Clear();
            Console.WriteLine("================== Freesound Downloader by PrawnCocktail ==================");
            Console.WriteLine("================================ Help  Meh ================================");
            Console.WriteLine();
            Console.WriteLine("Required arguments are \"user\" and \"pass\" for your freesound account");
            Console.WriteLine("For example, FreesoundDownloader.exe user=myusername pass=mypassword");
            Console.WriteLine();
            Console.WriteLine("Optional arguments are:");
            Console.WriteLine("===========================================================================");

            Console.WriteLine("Audio type, options are wav, aiff, ogg, mp3, m4a, flac. Example: type=wav");
            Console.WriteLine("===========================================================================");

            Console.WriteLine("License type, options are atrib, atribnon, cc0. Example: license=cc0");
            Console.WriteLine("===========================================================================");

            Console.WriteLine("Samplerate, must be a whole number. Example: sameplerate=48000");
            Console.WriteLine("===========================================================================");

            Console.WriteLine("Username, used to filter files by specific user. Example: username=bilbo");
            Console.WriteLine("===========================================================================");

            Console.WriteLine("Tag, used to filter files by specific tags. Example: tag=siren");
            Console.WriteLine("===========================================================================");

            Console.WriteLine("Sort, sort download order, Default created_asc. Example: sort=created_asc");
            Console.WriteLine("Sort options are: score, duration_desc, duration_asc, created_desc, ");
            Console.WriteLine("created_asc, downloads_desc, downloads_asc, rating_desc, rating_asc ");
            Console.WriteLine("===========================================================================");

            Console.WriteLine();
            Console.WriteLine("===========================================================================");
            Console.WriteLine("If none of that made any sense, good luck.");
            Console.WriteLine("===========================================================================");
            Console.Read();
            Environment.Exit(0);

        }

        private static string MakeValidFileName(string name)
        {
            string invalidChars = System.Text.RegularExpressions.Regex.Escape(new string(System.IO.Path.GetInvalidFileNameChars()));
            string invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidChars);

            return System.Text.RegularExpressions.Regex.Replace(name, invalidRegStr, "_");
        }
    }
}
