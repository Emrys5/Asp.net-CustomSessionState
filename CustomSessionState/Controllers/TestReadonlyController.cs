using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace CustomSessionState.Controllers
{
    [SessionState(System.Web.SessionState.SessionStateBehavior.ReadOnly)]
    public class TestReadonlyController : Controller
    {
        // GET: TestReadonly
        public ActionResult Index()
        {
            return Content(Convert.ToString(Session["TT"]));

        }
    }
}