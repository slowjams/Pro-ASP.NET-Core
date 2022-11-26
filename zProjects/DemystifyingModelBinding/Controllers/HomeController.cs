using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using DemystifyingModelBinding.Models;

namespace DemystifyingModelBinding.Controllers
{
   public class HomeController : Controller
   {
      private IRepository repository;
      public HomeController(IRepository repo)
      {
         repository = repo;
      }
      public IActionResult Index(int id)
      {
         return View(repository[id]);
      }

      public IActionResult Create()
      {
         return View();
      }

      [HttpPost]
      public IActionResult Create(Employee model)
      {
         string houseNo = Request.Form["HomeAddress.HouseNumber"];
         //model.Name = "";
         //return View(model);
         return View("Index", model);
      }

      [HttpPost]
      public IActionResult DisplayMainAddress([Bind(Prefix = nameof(Employee.HomeAddress))] MainAddress mainAddress)
      {
         return View(mainAddress);
      }

      //public IActionResult Places(string[] places)
      //{
      //   return View(places);
      //}

      public IActionResult Places(List<string> places)
      {
         return View(places);
      }

      [HttpGet("/users/{ids}")]
      public IActionResult GetUsersByIds([FromRoute] string idsz)
      {
         return null;
      }
   }
}