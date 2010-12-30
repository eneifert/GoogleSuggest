using System.Collections.Generic;
using RestSharp;

namespace GoogleSuggest
{
    public class GoogleSuggest
    {
        public static string GetSuggestion(string text)
        {
            return GetSuggestions(text)[0].suggestion;            
        }

        public static List<CompleteSuggestion> GetSuggestions(string text)
        {
            RestClient r = new RestClient("http://google.com/complete/search");
            RestRequest req = new RestRequest();
            req.AddParameter("q", text);
            req.AddParameter("output", "toolbar");
            req.RequestFormat = DataFormat.Xml;
            r.AddHandler("text/xml", new GoogleXmlDeserializer());

            return r.Execute<List<CompleteSuggestion>>(req).Data;   
        }        
    }

    public class CompleteSuggestion
    {
        public string suggestion { get; set; }
        public string num_queries { get; set; }
    }  
}
