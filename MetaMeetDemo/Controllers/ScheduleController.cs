using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MetaMeetDemo.Controllers
{
    [Authorize]
    public class ScheduleController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}