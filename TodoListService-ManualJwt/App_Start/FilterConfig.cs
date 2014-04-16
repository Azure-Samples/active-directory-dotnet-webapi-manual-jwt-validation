using System.Web;
using System.Web.Mvc;

namespace TodoListService_ManualJwt
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
        }
    }
}
