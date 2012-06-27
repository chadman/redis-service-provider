using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Web2.Controllers {
    public class HomeController : Controller {
        public ActionResult Index() {
            ViewBag.Message = "Modify this template to jump-start your ASP.NET MVC application.";

            Session["key"] = "thekey";

            List<KeyValuePair<string, string>> sessionObjects = new List<KeyValuePair<string, string>>();

            for (int i = 0; i < Session.Keys.Count; i++) {
                KeyValuePair<string, string> keyValue = new KeyValuePair<string,string>(Session.Keys.Get(i), Session[i].ToString());
                sessionObjects.Add(keyValue);
            }

            return View("Index", sessionObjects);
        }

        [HttpPost]
        [ActionName("Index")]
        public PartialViewResult CreateSessionKey() {

            Session[Request.Form["key"]] = Request.Form["value"];

            List<KeyValuePair<string, string>> sessionObjects = new List<KeyValuePair<string, string>>();

            for (int i = 0; i < Session.Keys.Count; i++) {
                KeyValuePair<string, string> keyValue = new KeyValuePair<string, string>(Session.Keys.Get(i), Session[i].ToString());
                sessionObjects.Add(keyValue);
            }

            return PartialView("_sessionItems", sessionObjects);
        }

        public ActionResult About() {
            ViewBag.Message = "Your quintessential app description page.";

            return View();
        }

        public ActionResult Contact() {
            ViewBag.Message = "Your quintessential contact page.";

            return View();
        }
    }
}
