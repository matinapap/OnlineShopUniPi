using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OnlineShopUniPi.Models;
using OnlineShopUniPi.Helpers;
using Newtonsoft.Json;
using System.Text.Json;

namespace OnlineShopUniPi.Controllers
{
    public class ProductsController : Controller
    {
        private readonly OnlineStoreDBContext _context;
        private readonly ILogger<ProductsController> _logger;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IWebHostEnvironment _env;

        public ProductsController(IWebHostEnvironment env, OnlineStoreDBContext context, ILogger<ProductsController> logger, IWebHostEnvironment webHostEnvironment)
        {
            _env = env;
            _context = context;
            _logger = logger;
            _webHostEnvironment = webHostEnvironment;
        }

        [Authorize]
        [HttpPost]
        public IActionResult AddToCart(int id, int quantity)
        {
            // Retrieve the cart from session (stored as Dictionary<int, int>)
            var cartJson = HttpContext.Session.GetString("Cart");

            Dictionary<int, int> cart;
            try
            {
                // If the session is empty, create a new dictionary
                // Otherwise, deserialize the existing cart from JSON
                cart = string.IsNullOrEmpty(cartJson)
                    ? new Dictionary<int, int>()
                    : JsonConvert.DeserializeObject<Dictionary<int, int>>(cartJson);
            }
            catch
            {
                // If deserialization fails (corrupted session data), start with a new dictionary
                cart = new Dictionary<int, int>();
            }

            // Add the product or increase its quantity if it already exists in the cart
            if (cart.ContainsKey(id))
            {
                cart[id] += quantity;
            }
            else
            {
                cart[id] = quantity;
            }

            // Save the updated cart back into session as JSON
            HttpContext.Session.SetString("Cart", JsonConvert.SerializeObject(cart));

            return RedirectToAction("Details", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public IActionResult RemoveFromCart(int id)
        {
            // Retrieve the cart from session (Dictionary<ProductId, Quantity>)
            var cart = HttpContext.Session.GetObjectFromJson<Dictionary<int, int>>("Cart")
                       ?? new Dictionary<int, int>();

            // If the product exists in the cart, remove it
            if (cart.ContainsKey(id))
            {
                cart.Remove(id);
                // Save the updated cart back into the session
                HttpContext.Session.SetObjectAsJson("Cart", cart);
            }

            return RedirectToAction("Cart");
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Cart()
        {
            // Retrieve the cart from session (Dictionary<ProductId, Quantity>)
            var cart = HttpContext.Session.GetObjectFromJson<Dictionary<int, int>>("Cart")
                       ?? new Dictionary<int, int>();

            // Get the product IDs that exist in the cart
            var productIds = cart.Keys.ToList();

            // Fetch products from the database that are in the cart and have stock available (Quantity >= 1)
            var productsInCart = await _context.Products
                .Include(p => p.ProductImages)
                .Where(p => productIds.Contains(p.ProductId) && p.Quantity >= 1)
                .ToListAsync();

            // Pass the quantities to the ViewBag so Razor can use them
            ViewBag.CartQuantities = cart;

            return View(productsInCart);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public IActionResult UpdateCartQuantity([FromBody] Dictionary<string, object> model)
        {
            // Validate the incoming JSON body and ensure required keys exist
            if (model == null || !model.ContainsKey("ProductId") || !model.ContainsKey("Quantity"))
                return BadRequest();

            // Extract ProductId and Quantity from the JSON object
            int productId = ((JsonElement)model["ProductId"]).GetInt32();
            int quantity = ((JsonElement)model["Quantity"]).GetInt32();

            // Retrieve the cart from session (Dictionary<ProductId, Quantity>)
            var cart = HttpContext.Session.GetObjectFromJson<Dictionary<int, int>>("Cart")
                       ?? new Dictionary<int, int>();

            // If quantity is zero, remove the product from the cart
            if (quantity <= 0)
            {
                if (cart.ContainsKey(productId))
                    cart.Remove(productId);
            }
            else
            {
                // Otherwise, update or add the product with the given quantity
                cart[productId] = quantity;
            }

            // Save the updated cart back into the session
            HttpContext.Session.SetObjectAsJson("Cart", cart);

            // Calculate the total amount based on product prices and quantities
            var productsInCart = _context.Products
                .Where(p => cart.Keys.Contains(p.ProductId))
                .AsEnumerable() 
                .ToList();

            var total = productsInCart.Sum(p => p.Price * cart[p.ProductId]);


            return Json(new { success = true, total });
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Favorites([FromBody] Favorite model)
        {
            // Get the currently logged-in user's ID from claims
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            // Override the incoming UserId to ensure it's always the logged-in user
            model.UserId = userId;

            // Check if this product is already in the user's favorites
            var favoriteExists = await _context.Favorites
                .AnyAsync(f => f.UserId == userId && f.ProductId == model.ProductId);

            if (!favoriteExists)
            {
                // If not in favorites, add it with the current UTC timestamp
                model.AddedAt = DateTime.UtcNow;
                _context.Favorites.Add(model);
            }
            else
            {
                // If already exists, find it and remove it 
                var existing = await _context.Favorites
                    .FirstAsync(f => f.UserId == userId && f.ProductId == model.ProductId);

                _context.Favorites.Remove(existing);
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Favorites()
        {
            // Get the currently logged-in user's ID from claims
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            // Fetch favorite products for the user that are still in stock (Quantity >= 1)
            var favoriteProducts = await _context.Products
                .Include(p => p.ProductImages)
                .Where(p => p.Quantity >= 1 &&
                            _context.Favorites.Any(f => f.UserId == userId && f.ProductId == p.ProductId))
                .ToListAsync();

            var favoriteProductIds = favoriteProducts.Select(p => p.ProductId).ToList();

            ViewData["FavoriteProductIds"] = favoriteProductIds;

            return View(favoriteProducts);
        }


        //------------ Algorythm for personalized product recommendations ------------
        [HttpGet]
        public async Task<IActionResult> FilteredRecommendations(string? size, string? gender, string? category, decimal? minPrice, decimal? maxPrice)
        {
            // 1) If the user is not logged in, return an empty JSON array
            if (!User.Identity.IsAuthenticated)
                return Json(new List<object>());

            // 2) Extract the current logged-in user ID from the claims
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int currentUserId);

            // 3) Get product IDs that the user has already purchased
            var purchasedIds = await _context.OrderItems
                .Where(oi => oi.Order.UserId == currentUserId)
                .Select(oi => oi.ProductId)
                .ToListAsync();

            // 4) Get product IDs that the user has added to favorites
            var favoriteIds = await _context.Favorites
                .Where(f => f.UserId == currentUserId)
                .Select(f => f.ProductId)
                .ToListAsync();

            // --- Collect category preferences from purchased & favorite products ---

            // Helper function: normalize category strings (lowercase, trimmed, non-null)
            Func<string?, string> normalize = s => string.IsNullOrWhiteSpace(s) ? "" : s.Trim().ToLowerInvariant();

            // All categories from purchased products
            var purchasedCategoriesRaw = await _context.OrderItems
                .Where(oi => oi.Order.UserId == currentUserId)
                .Select(oi => oi.Product.Category)
                .Where(c => c != null)
                .ToListAsync();

            // All categories from favorite products
            var favoriteCategoriesRaw = await _context.Favorites
                .Where(f => f.UserId == currentUserId)
                .Select(f => f.Product.Category)
                .Where(c => c != null)
                .ToListAsync();

            // Count how many times each category appears in purchases
            var purchasedCategoryCounts = purchasedCategoriesRaw
                .Select(normalize)
                .Where(s => s != "")
                .GroupBy(s => s)
                .ToDictionary(g => g.Key, g => g.Count());

            // Count how many times each category appears in favorites
            var favoriteCategoryCounts = favoriteCategoriesRaw
                .Select(normalize)
                .Where(s => s != "")
                .GroupBy(s => s)
                .ToDictionary(g => g.Key, g => g.Count());

            // --- Fetch all available products (with images) ---
            var allProducts = await _context.Products
                .Include(p => p.ProductImages)
                .Where(p => p.Quantity > 0) // only products in stock
                .ToListAsync();

            // --- Normalize user filters (soft preferences, not strict filters) ---
            var sizeNorm = string.IsNullOrWhiteSpace(size) ? null : size.Trim();
            var genderNorm = string.IsNullOrWhiteSpace(gender) ? null : gender.Trim();
            var categoryNorm = string.IsNullOrWhiteSpace(category) ? null : category.Trim().ToLowerInvariant();

            // --- Function to compute a "relevance score" for each product ---
            Func<Product, double> computeScore = p =>
            {
                double score = 0;
                var pCat = normalize(p.Category);

                // (A) Score boost for matching categories with purchased/favorite history
                if (!string.IsNullOrEmpty(pCat))
                {
                    if (purchasedCategoryCounts.TryGetValue(pCat, out int pc))
                        score += pc * 3.0;  // Purchases weigh more (×3)
                    if (favoriteCategoryCounts.TryGetValue(pCat, out int fc))
                        score += fc * 1.0;  // Favorites weigh less (×1)
                }

                // (B) Bonus points if product matches user-selected filters (soft match)
                if (!string.IsNullOrEmpty(categoryNorm) && pCat == categoryNorm)
                    score += 2.0;

                if (!string.IsNullOrEmpty(sizeNorm) && !string.IsNullOrEmpty(p.Size) &&
                    string.Equals(p.Size.Trim(), sizeNorm, StringComparison.OrdinalIgnoreCase))
                    score += 1.0;

                if (!string.IsNullOrEmpty(genderNorm) && !string.IsNullOrEmpty(p.Gender) &&
                    string.Equals(p.Gender.Trim(), genderNorm, StringComparison.OrdinalIgnoreCase))
                    score += 0.8;

                // (C) Price range scoring (exact match > near match)
                if (minPrice.HasValue && maxPrice.HasValue)
                {
                    // Inside the given price range → full bonus
                    if (p.Price >= minPrice.Value && p.Price <= maxPrice.Value)
                        score += 1.2;
                    else
                    {
                        // Allow 20% tolerance outside of range → smaller bonus
                        var lower = minPrice.Value - (minPrice.Value * 0.20m);
                        var upper = maxPrice.Value + (maxPrice.Value * 0.20m);
                        if (p.Price >= lower && p.Price <= upper)
                            score += 0.6;
                    }
                }
                else if (minPrice.HasValue)
                {
                    if (p.Price >= minPrice.Value) score += 0.8;
                }
                else if (maxPrice.HasValue)
                {
                    if (p.Price <= maxPrice.Value) score += 0.8;
                }

                return score;
            };

            // --- STEP 1: Candidate products (exclude purchased & favorite ones first) ---
            var candidates = allProducts
                .Where(p => !purchasedIds.Contains(p.ProductId) && !favoriteIds.Contains(p.ProductId))
                .ToList();

            // --- STEP 2: Compute scores & select top 5 recommendations ---
            var recommended = candidates
                .Select(p => new
                {
                    Product = p,
                    ImageUrl = p.ProductImages.FirstOrDefault(img => img.IsMainImage == true)?.ImageUrl
                               ?? "/images/resources/no_image.png",
                    Score = computeScore(p)
                })
                .Where(x => x.Score > 0) // Only recommend products with positive score
                .OrderByDescending(x => x.Score) // Higher score first
                .ThenBy(_ => Guid.NewGuid())     // Randomize tie cases
                .Take(5)
                .ToList();

            // --- STEP 3: If fewer than 5, allow favorite products (fallback) ---
            if (recommended.Count < 5)
            {
                var moreFromFavorites = allProducts
                    .Where(p => !purchasedIds.Contains(p.ProductId)
                                && favoriteIds.Contains(p.ProductId)
                                && recommended.All(r => r.Product.ProductId != p.ProductId))
                    .Select(p => new
                    {
                        Product = p,
                        ImageUrl = p.ProductImages.FirstOrDefault(img => img.IsMainImage == true)?.ImageUrl
                                   ?? "/images/resources/no_image.png",
                        Score = computeScore(p) + 1.0 // Add small bonus for favorites
                    })
                    .OrderByDescending(x => x.Score)
                    .ThenBy(_ => Guid.NewGuid())
                    .Take(5 - recommended.Count)
                    .ToList();

                recommended.AddRange(moreFromFavorites);
            }

            // --- STEP 4: Final fallback → fill remaining slots with random products ---
            if (recommended.Count < 5)
            {
                var alreadyIds = recommended.Select(r => r.Product.ProductId).ToHashSet();
                var fallback = allProducts
                    .Where(p => !purchasedIds.Contains(p.ProductId) && !alreadyIds.Contains(p.ProductId))
                    .OrderBy(_ => Guid.NewGuid())
                    .Take(5 - recommended.Count)
                    .Select(p => new
                    {
                        Product = p,
                        ImageUrl = p.ProductImages.FirstOrDefault(img => img.IsMainImage == true)?.ImageUrl
                                   ?? "/images/resources/no_image.png",
                        Score = 0.0 // Fallback items have no score
                    })
                    .ToList();

                recommended.AddRange(fallback);
            }

            // --- STEP 5: Shape final JSON output (limit to 5 items) ---
            var final = recommended
                .Take(5)
                .Select(r => new
                {
                    r.Product.ProductId,
                    r.Product.Title,
                    r.Product.Price,
                    ImageUrl = r.ImageUrl,
                    Score = Math.Round(r.Score, 2)
                })
                .ToList();

            return Json(final);
        }


        // GET: Products
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Index(int? productId)
        {
            try
            {
                // Base query: fetch products including the related User entity
                IQueryable<Product> query = _context.Products
                    .Include(p => p.User);

                // If a specific ProductId is provided, filter by that ID
                if (productId.HasValue)
                {
                    query = query.Where(p => p.ProductId == productId.Value);
                }

                // Execute the query and return the result to the view
                var products = await query.ToListAsync();
                return View(products);
            }
            catch (Exception ex)
            {
                // In case of an exception, return an empty product list to avoid breaking the view
                return View(new List<Product>());
            }
        }

        [Authorize]
        public async Task<IActionResult> GetProducts()
        {
            try
            {
                // Get the current user's ID from claims
                var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userIdValue))
                    return Unauthorized();

                if (!int.TryParse(userIdValue, out int userId))
                    return Unauthorized();

                // Fetch products for the current user, including related User and ProductImages
                var products = await _context.Products
                    .Include(p => p.User)
                    .Include(p => p.ProductImages)
                    .Where(p => p.UserId == userId)
                    .ToListAsync();

                return View("ProductList", products);
            }
            catch (Exception ex)
            {
                // If an exception occurs, log it and return a 500 Internal Server Error
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        //------------ Algorythm for getting suggestions under a product ------------ 
        // GET: Products/Details/5
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            // 1) Get the current product (including its images and owner/user info)
            var product = await _context.Products
                .Include(p => p.ProductImages)
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.ProductId == id);

            if (product == null)
                return NotFound();

            // 2) Get the logged-in user ID from claims
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int currentUserId);

            // 3) Get the product IDs the user has already purchased
            var purchasedIds = await _context.OrderItems
                .Where(oi => oi.Order.UserId == currentUserId)
                .Select(oi => oi.ProductId)
                .ToListAsync();

            // 4) Get the product IDs the user has marked as favorites
            var favoriteIds = await _context.Favorites
                .Where(f => f.UserId == currentUserId)
                .Select(f => f.ProductId)
                .ToListAsync();

            // 5) Collect categories from purchased products (to detect user interests)
            var purchasedCategories = await _context.OrderItems
                .Where(oi => oi.Order.UserId == currentUserId)
                .Select(oi => oi.Product.Category)
                .Where(c => c != null)
                .ToListAsync();

            // 6) Collect categories from favorite products
            var favoriteCategories = await _context.Favorites
                .Where(f => f.UserId == currentUserId)
                .Select(f => f.Product.Category)
                .Where(c => c != null)
                .ToListAsync();

            // 7) Count how many times each category appears in purchased products
            var purchasedCategoryCounts = purchasedCategories
                .GroupBy(c => c)
                .ToDictionary(g => g.Key!, g => g.Count());

            // 8) Count how many times each category appears in favorite products
            var favoriteCategoryCounts = favoriteCategories
                .GroupBy(c => c)
                .ToDictionary(g => g.Key!, g => g.Count());

            // 9) Get all available products:
            // - not the current product
            // - in stock (Quantity > 0)
            // - not uploaded by the same user
            // - not already purchased by the current user
            var allProducts = await _context.Products
                .Include(p => p.ProductImages)
                .Where(p => p.ProductId != id
                            && p.Quantity > 0
                            && p.UserId != currentUserId
                            && !purchasedIds.Contains(p.ProductId))
                .ToListAsync();

            // 10) Score each product based on similarity and user history
            var scoredProducts = allProducts
                .Select(p =>
                {
                    int score = 0;

                    // +10 if product has the same category as the current one
                    if (p.Category == product.Category)
                        score += 10;

                    // +2 if product has the same gender as the current one
                    if (p.Gender == product.Gender)
                        score += 2;

                    // +2 × number of times user purchased products in this category
                    if (!string.IsNullOrEmpty(p.Category) && purchasedCategoryCounts.TryGetValue(p.Category, out int pc))
                        score += pc * 2;

                    // +1 × number of times user has this category in favorites
                    if (!string.IsNullOrEmpty(p.Category) && favoriteCategoryCounts.TryGetValue(p.Category, out int fc))
                        score += fc;

                    return new { Product = p, Score = score };
                })
                .Where(x => x.Score > 0)            // keep only products with a positive score
                .OrderByDescending(x => x.Score)    // sort by highest score first
                .ThenBy(x => Guid.NewGuid())        // add randomness for variety
                .Take(4)                            // take the top 4 products
                .Select(x => x.Product)
                .ToList();

            // 11) Pass favorite product IDs and recommended products to the view
            ViewData["FavoriteProductIds"] = favoriteIds;
            ViewData["RecommendedProducts"] = scoredProducts;

            // 12) Return the main product details view
            return View(product);
        }


        // GET: Products/Create
        [Authorize]
        public IActionResult Create()
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userIdValue))
                return Unauthorized();

            ViewData["UserId"] = userIdValue;

            return View();
        }

        // POST: Products/Create
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ProductId,Title,Description,Gender,Category,Quantity,Size,Price,Condition")] Product product, IFormFile ?MainImage, List<IFormFile> ?AdditionalImages)
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userIdValue) || !int.TryParse(userIdValue, out int userId))
            {
                return Unauthorized();
            }

            product.UserId = userId;
            product.CreatedAt = DateTime.UtcNow;

            ModelState.Remove("User");

            if (!ModelState.IsValid)
            {
                return View(product);
            }

            try
            {
                _context.Products.Add(product);
                await _context.SaveChangesAsync();

                var uploadPath = Path.Combine(_webHostEnvironment.WebRootPath, "images", "products");
                if (!Directory.Exists(uploadPath))
                    Directory.CreateDirectory(uploadPath);

                // Main Image
                if (MainImage != null && MainImage.Length > 0)
                {
                    var fileName = Guid.NewGuid() + Path.GetExtension(MainImage.FileName);
                    var filePath = Path.Combine(uploadPath, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await MainImage.CopyToAsync(stream);
                    }

                    var mainProductImage = new ProductImage
                    {
                        ProductId = product.ProductId,
                        ImageUrl = "/images/products/" + fileName,
                        IsMainImage = true
                    };

                    _context.ProductImages.Add(mainProductImage);
                }

                // Other Images
                if (AdditionalImages != null && AdditionalImages.Count > 0)
                {
                    foreach (var formFile in AdditionalImages)
                    {
                        if (formFile.Length > 0)
                        {
                            var fileName = Guid.NewGuid() + Path.GetExtension(formFile.FileName);
                            var filePath = Path.Combine(uploadPath, fileName);

                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                await formFile.CopyToAsync(stream);
                            }

                            var productImage = new ProductImage
                            {
                                ProductId = product.ProductId,
                                ImageUrl = "/images/products/" + fileName,
                                IsMainImage = false
                            };

                            _context.ProductImages.Add(productImage);
                        }
                    }
                }

                await _context.SaveChangesAsync();
                return RedirectToAction("GetProducts");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save product");
                ModelState.AddModelError("", "Απέτυχε η αποθήκευση του προϊόντος. Δοκιμάστε ξανά.");
                return View(product);
            }
        }

        // GET: Products/Edit/5
        [Authorize]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdValue) || !int.TryParse(userIdValue, out int userId))
                return Unauthorized();

            // Check if the logged-in user is not an Admin
            // and is trying to edit a product that doesn't belong to them
            if (!User.IsInRole("Admin") && product.UserId != userId)
                return Forbid(); // 403 Forbidden

            ViewData["UserId"] = new SelectList(_context.Users, "UserId", "UserId", product.UserId);

            return View(product);
        }

        // POST: Products/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ProductId,Title,Description,Gender,Category,Quantity,Size,Price,Condition")] Product product, IFormFile? MainImage, List<IFormFile>? AdditionalImages)
        {
            if (id != product.ProductId)
                return NotFound();

            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdValue) || !int.TryParse(userIdValue, out int userId))
                return Unauthorized();

            ModelState.Remove("User");

            var existingProduct = await _context.Products
                .Include(p => p.ProductImages)
                .FirstOrDefaultAsync(p => p.ProductId == id);

            if (existingProduct == null)
                return NotFound();

            // Authorization check: only Admins can edit any product
            // Regular users can only edit their own products
            if (!User.IsInRole("Admin") && existingProduct.UserId != userId)
                return Forbid();

            if (!ModelState.IsValid)
                return View(existingProduct);

            // Update product properties
            existingProduct.Title = product.Title;
            existingProduct.Description = product.Description;
            existingProduct.Gender = product.Gender;
            existingProduct.Category = product.Category;
            existingProduct.Quantity = product.Quantity;
            existingProduct.Size = product.Size;
            existingProduct.Price = product.Price;
            existingProduct.Condition = product.Condition;

            // Only set UserId to the current user if not Admin
            if (!User.IsInRole("Admin"))
                existingProduct.UserId = userId;

            existingProduct.CreatedAt = DateTime.UtcNow;

            // Handle images
            bool hasNewImages = (MainImage != null && MainImage.Length > 0) ||
                                (AdditionalImages != null && AdditionalImages.Any(f => f.Length > 0));

            if (hasNewImages)
            {
                // Delete old images from disk
                foreach (var oldImage in existingProduct.ProductImages)
                {
                    var oldImagePath = Path.Combine(_env.WebRootPath, oldImage.ImageUrl.TrimStart('/'));
                    if (System.IO.File.Exists(oldImagePath))
                        System.IO.File.Delete(oldImagePath);
                }

                _context.ProductImages.RemoveRange(existingProduct.ProductImages);

                var newImages = new List<ProductImage>();

                // Main image
                if (MainImage != null && MainImage.Length > 0)
                {
                    var mainFileName = $"{Guid.NewGuid()}{Path.GetExtension(MainImage.FileName)}";
                    var mainFilePath = Path.Combine(_env.WebRootPath, "images", "products", mainFileName);
                    Directory.CreateDirectory(Path.GetDirectoryName(mainFilePath)!);

                    using (var stream = new FileStream(mainFilePath, FileMode.Create))
                    {
                        await MainImage.CopyToAsync(stream);
                    }

                    newImages.Add(new ProductImage
                    {
                        ImageUrl = $"/images/products/{mainFileName}",
                        IsMainImage = true
                    });
                }

                // Additional images
                if (AdditionalImages != null)
                {
                    foreach (var img in AdditionalImages)
                    {
                        if (img.Length > 0)
                        {
                            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(img.FileName)}";
                            var filePath = Path.Combine(_env.WebRootPath, "images", "products", fileName);

                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                await img.CopyToAsync(stream);
                            }

                            newImages.Add(new ProductImage
                            {
                                ImageUrl = $"/images/products/{fileName}",
                                IsMainImage = false
                            });
                        }
                    }
                }

                existingProduct.ProductImages = newImages;
            }

            _context.Update(existingProduct);
            await _context.SaveChangesAsync();

            if (User.IsInRole("Admin"))
                return RedirectToAction("Index", "Products");

            return RedirectToAction("GetProducts");
        }

        // GET: Products/Delete/5
        [Authorize]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products
                .Include(p => p.User)
                .FirstOrDefaultAsync(m => m.ProductId == id);

            if (product == null)
            {
                return NotFound();
            }

            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdValue) || !int.TryParse(userIdValue, out int userId))
                return Unauthorized();

            // Authorization check: Admin can delete any product
            // Regular users can only delete their own products
            if (!User.IsInRole("Admin") && product.UserId != userId)
                return Forbid(); // 403 Forbidden

            return View(product);
        }

        // POST: Products/Delete/5
        [Authorize]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var product = await _context.Products
                .Include(p => p.ProductImages)
                .Include(p => p.Favorites)
                .Include(p => p.OrderItems)
                .FirstOrDefaultAsync(p => p.ProductId == id);

            if (product == null)
                return NotFound();

            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdValue) || !int.TryParse(userIdValue, out int userId))
                return Unauthorized();

            // Authorization: Admin can delete any product, user only their own
            if (!User.IsInRole("Admin") && product.UserId != userId)
                return Forbid(); // 403 Forbidden

            // Delete images from disk
            if (product.ProductImages != null)
            {
                foreach (var image in product.ProductImages)
                {
                    var imagePath = Path.Combine(_env.WebRootPath, image.ImageUrl.TrimStart('/'));
                    if (System.IO.File.Exists(imagePath))
                        System.IO.File.Delete(imagePath);
                }
                _context.ProductImages.RemoveRange(product.ProductImages);
            }

            // Delete related Favorites
            if (product.Favorites != null)
                _context.Favorites.RemoveRange(product.Favorites);

            // Delete related OrderItems
            if (product.OrderItems != null)
                _context.OrderItems.RemoveRange(product.OrderItems);

            // Finally, delete the product itself
            _context.Products.Remove(product);

            await _context.SaveChangesAsync();

            return RedirectToAction("Index", "Products");
        }

    }
}
