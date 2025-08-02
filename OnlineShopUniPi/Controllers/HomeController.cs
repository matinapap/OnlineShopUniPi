using System.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnlineShopUniPi.Models;

namespace OnlineShopUniPi.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly OnlineStoreDBContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public HomeController(ILogger<HomeController> logger, OnlineStoreDBContext context, IWebHostEnvironment webHostEnvironment)
        {
            _logger = logger;
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public IActionResult ClothingPage(string gender = "Women", string category = null)
        {
            var productsQuery = _context.Products
                .Include(p => p.ProductImages) // Για να φορτώνονται και οι εικόνες
                .AsQueryable();

            if (!string.IsNullOrEmpty(gender))
                productsQuery = productsQuery.Where(p => p.Gender == gender);

            if (!string.IsNullOrEmpty(category))
                productsQuery = productsQuery.Where(p => p.Category == category);

            var products = productsQuery.ToList();

            ViewBag.SelectedGender = gender;
            ViewBag.SelectedCategory = category;

            return View(products);
        }


        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
