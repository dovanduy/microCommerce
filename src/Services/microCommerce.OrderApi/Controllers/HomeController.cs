﻿using microCommerce.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace microCommerce.OrderApi.Controllers
{
    [Route("/")]
    public class HomeController : ServiceBaseController
    {
        [HttpGet]
        public IActionResult Get()
        {
            return Content("OrderApi is a live", "text/plain", Encoding.UTF8);
        }
    }
}