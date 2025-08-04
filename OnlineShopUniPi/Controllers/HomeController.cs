using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
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

        public async Task<IActionResult> Index()
        {
            var topFavoritedProducts = await _context.Products
                .Where(p => p.Favorites.Any())
                .OrderByDescending(p => p.Favorites.Count)
                .Take(10)
                .Include(p => p.ProductImages)
                .ToListAsync();

            ViewBag.TopFavorited = topFavoritedProducts;

            return View();
        }


        [HttpGet]
        public async Task<IActionResult> ClothingPage(string gender = "Women", string category = null)
        {
            var productsQuery = _context.Products
                .Include(p => p.ProductImages) // Για να φορτώνονται και οι εικόνες
                .AsQueryable();

            if (!string.IsNullOrEmpty(gender))
                productsQuery = productsQuery.Where(p => p.Gender == gender);

            if (!string.IsNullOrEmpty(category))
                productsQuery = productsQuery.Where(p => p.Category == category);

            var products = await productsQuery.ToListAsync();

            ViewBag.SelectedGender = gender;
            ViewBag.SelectedCategory = category;

            if (User.Identity.IsAuthenticated)
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
                var favoriteProductIds = await _context.Favorites
                    .Where(f => f.UserId == userId)
                    .Select(f => f.ProductId)
                    .ToListAsync();

                ViewBag.FavoriteProductIds = favoriteProductIds;
            }
            else
            {
                ViewBag.FavoriteProductIds = new List<int>();
            }

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
