This is a simple .Net wrapper that lets you use the GoogleSuggest feature.

You can just download the dll here: https://github.com/eneifert/GoogleSuggest/blob/master/GoogleSuggest/bin/Debug/GoogleSuggest.dll

Example:

GoogleSuggest.GetSuggestion("miccrosoft") would return: "microsoft".
GoogleSuggest.GetSuggestions("miccrosoft") would return a list of CompletedSuggestions.

And here is what that class looks like:

public class CompleteSuggestion
{
	public string suggestion { get; set; }
	public string num_queries { get; set; }
}
    


Enjoy,