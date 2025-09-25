using System.Diagnostics;
using System.Security.Claims;
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
            //------------ Algorythm for getting top 10 most favorited products ------------ 
            // Fetch top 10 most favorited products
            var topFavoritedProducts = await _context.Products
                .Include(p => p.ProductImages) // Load product images
                .Where(p => p.Favorites.Any()) // Only products with at least one favorite
                .OrderByDescending(p => p.Favorites.Count) // Most favorited first
                .Take(10)
                .ToListAsync();

            ViewBag.TopFavorited = topFavoritedProducts;

            // Fetch latest 10 products (from all categories)
            var latestProducts = await _context.Products
                .Include(p => p.ProductImages)
                .OrderByDescending(p => p.CreatedAt) 
                .Take(10)
                .ToListAsync();

            ViewBag.LatestProducts = latestProducts;

            return View();
        }

        // Returns clothing page filtered by gender and category
        [HttpGet]
        public async Task<IActionResult> ClothingPage(string gender = "Women", string category = null)
        {
            var productsQuery = _context.Products
                .Include(p => p.ProductImages)
                .Where(p => p.Quantity >= 1) // Only include products with quantity >= 1
                .AsQueryable();

            if (!string.IsNullOrEmpty(gender))
                productsQuery = productsQuery.Where(p => p.Gender == gender);

            if (!string.IsNullOrEmpty(category))
                productsQuery = productsQuery.Where(p => p.Category == category);

            var products = await productsQuery.ToListAsync();

            ViewBag.SelectedGender = gender;
            ViewBag.SelectedCategory = category;

            // Get favorite product IDs for the logged-in user
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
