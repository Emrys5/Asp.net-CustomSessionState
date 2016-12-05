using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace CustomSessionState.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            Session["TT"] = "TT";
            return View();
        }

        public ActionResult About()
        {
            ViewBag.Message = Convert.ToString(Session["TT"]);
            return View();
        }

        public ActionResult Abandon()
        {
            Session.Abandon();
            return RedirectToAction("About");
        }

    }
}