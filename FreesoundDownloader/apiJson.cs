using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FreesoundDownloader
{
    public class Result
    {
        public int id { get; set; }
        public string url { get; set; }
        public string name { get; set; }
        public DateTime created { get; set; }
        public string type { get; set; }
        public string username { get; set; }
    }

    public class RootApiJson
    {
        public int count { get; set; }
        public string next { get; set; }
        public List<Result> results { get; set; }
        public object previous { get; set; }
    }
}
