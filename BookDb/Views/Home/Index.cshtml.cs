using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BookDb.Views.Home
{
    public class IndexModel
    {
        public string Title => "Home Page";
        public string WelcomeMessage => "Welcome";

        public IndexModel()
        {
        }

        public string GetApplicationName()
        {
            return "BookDb";
        }

        public string GetWelcomeText()
        {
            return WelcomeMessage;
        }
    }
}
