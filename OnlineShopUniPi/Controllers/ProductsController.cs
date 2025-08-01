using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OnlineShopUniPi.Models;

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

        // GET: Products
        public async Task<IActionResult> Index(int? productId)
        {
            try
            {
                IQueryable<Product> query = _context.Products
                    .Include(p => p.User);

                // Αν έχει δοθεί συγκεκριμένο ProductId, φιλτράρουμε
                if (productId.HasValue)
                {
                    query = query.Where(p => p.ProductId == productId.Value);
                }

                var products = await query.ToListAsync();
                return View(products);
            }
            catch (Exception ex)
            {
                // Logging ή αναφορά σφάλματος
                return View(new List<Product>());
            }
        }


        [Authorize]
        public async Task<IActionResult> GetProducts()
        {
            try
            {
                var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userIdValue))
                    return Unauthorized();

                if (!int.TryParse(userIdValue, out int userId))
                    return Unauthorized();

                var products = await _context.Products
                    .Include(p => p.User)
                    .Include(p => p.ProductImages)
                    .Where(p => p.UserId == userId)
                    .ToListAsync();

                return View("ProductList", products);
            }
            catch (Exception ex)
            {
                // Κάνε logging εδώ ή debugging για να δεις τι σφάλμα προέκυψε
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // GET: Products/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.Products == null)
            {
                return NotFound();
            }

            var product = await _context.Products
                .Include(p => p.User)             
                .Include(p => p.ProductImages)    
                .FirstOrDefaultAsync(m => m.ProductId == id);

            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        // GET: Products/Create
        public IActionResult Create()
        {
            // Παίρνουμε το UserId από τα claims του χρήστη
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Αν δεν υπάρχει UserId, επιστρέφουμε Unauthorized
            if (string.IsNullOrEmpty(userIdValue))
                return Unauthorized();
            

            // Βάζουμε το UserId στο ViewData για να το έχουμε στο View αν χρειαστεί
            ViewData["UserId"] = userIdValue;

            return View();
        }


        // POST: Products/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ProductId,Title,Description,Gender,Category,Price,Condition")] Product product, List<IFormFile> images)
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
                // Προσθήκη προϊόντος στη βάση
                _context.Products.Add(product);
                await _context.SaveChangesAsync(); // Χρειαζόμαστε το ProductId

                // Ανέβασμα φωτογραφιών
                if (images != null && images.Count > 0)
                {
                    var uploadPath = Path.Combine(_webHostEnvironment.WebRootPath, "images", "products");

                    if (!Directory.Exists(uploadPath))
                        Directory.CreateDirectory(uploadPath);

                    for (int i = 0; i < images.Count; i++)
                    {
                        var formFile = images[i];

                        if (formFile.Length > 0)
                        {
                            var fileName = Guid.NewGuid() + Path.GetExtension(formFile.FileName);
                            var filePath = Path.Combine(uploadPath, fileName);

                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                await formFile.CopyToAsync(stream);
                            }

                            var imageUrl = "/images/products/" + fileName;

                            var productImage = new ProductImage
                            {
                                ProductId = product.ProductId,
                                ImageUrl = imageUrl,
                                IsMainImage = (i == 0) // Η πρώτη εικόνα ως κύρια
                            };

                            _context.ProductImages.Add(productImage);
                        }
                    }

                    await _context.SaveChangesAsync();
                }

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
            ViewData["UserId"] = new SelectList(_context.Users, "UserId", "UserId", product.UserId);
            return View(product);
        }

        // POST: Products/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ProductId,Title,Description,Category,Price,Condition")] Product product, List<IFormFile> ProductImages)
        {
            if (id != product.ProductId)
            {
                return NotFound();
            }

            // Παίρνουμε το UserId από το claim του τρέχοντος χρήστη
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdValue) || !int.TryParse(userIdValue, out int userId))
            {
                return Unauthorized();
            }

            // Θέτουμε το UserId και CreatedAt
            product.UserId = userId;
            product.CreatedAt = DateTime.UtcNow;

            // Αφαιρούμε το validation για το User (αν υπάρχει)
            ModelState.Remove("User");

            if (!ModelState.IsValid)
            {
                return View(product);
            }

            try
            {
                var existingProduct = await _context.Products
                    .Include(p => p.ProductImages)
                    .FirstOrDefaultAsync(p => p.ProductId == id);

                if (existingProduct == null)
                {
                    return NotFound();
                }

                // Διαγραφή παλιών εικόνων (από βάση και αρχεία)
                if (existingProduct.ProductImages != null)
                {
                    foreach (var oldImage in existingProduct.ProductImages)
                    {
                        var oldImagePath = Path.Combine(_env.WebRootPath, oldImage.ImageUrl.TrimStart('/'));
                        if (System.IO.File.Exists(oldImagePath))
                        {
                            System.IO.File.Delete(oldImagePath);
                        }
                    }
                    _context.ProductImages.RemoveRange(existingProduct.ProductImages);
                }

                // Αποθήκευση νέων εικόνων
                var newImages = new List<ProductImage>();
                if (ProductImages != null && ProductImages.Count > 0)
                {
                    int index = 0;
                    foreach (var formFile in ProductImages)
                    {
                        if (formFile.Length > 0)
                        {
                            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(formFile.FileName)}";
                            var filePath = Path.Combine(_env.WebRootPath, "images", "products", fileName);

                            Directory.CreateDirectory(Path.GetDirectoryName(filePath));

                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                await formFile.CopyToAsync(stream);
                            }

                            newImages.Add(new ProductImage
                            {
                                ImageUrl = $"/images/products/{fileName}",
                                IsMainImage = index == 0
                            });
                            index++;
                        }
                    }
                }

                // Ενημερώνουμε τις ιδιότητες
                existingProduct.UserId = product.UserId;
                existingProduct.Title = product.Title;
                existingProduct.Description = product.Description;
                existingProduct.Category = product.Category;
                existingProduct.Price = product.Price;
                existingProduct.Condition = product.Condition;
                existingProduct.CreatedAt = product.CreatedAt;

                existingProduct.ProductImages = newImages;

                _context.Update(existingProduct);
                await _context.SaveChangesAsync();

                return RedirectToAction("GetProducts");
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ProductExists(product.ProductId))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
        }


        // GET: Products/Delete/5
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

            return View(product);
        }

        // POST: Products/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            // Φόρτωσε το προϊόν μαζί με τις εικόνες του
            var product = await _context.Products
                .Include(p => p.ProductImages)
                .FirstOrDefaultAsync(p => p.ProductId == id);

            if (product != null)
            {
                // Διαγραφή των εικόνων από το δίσκο
                if (product.ProductImages != null)
                {
                    foreach (var image in product.ProductImages)
                    {
                        var imagePath = Path.Combine(_env.WebRootPath, image.ImageUrl.TrimStart('/'));
                        if (System.IO.File.Exists(imagePath))
                        {
                            System.IO.File.Delete(imagePath);
                        }
                    }

                    // Διαγραφή εγγραφών εικόνων από τη βάση
                    _context.ProductImages.RemoveRange(product.ProductImages);
                }

                // Διαγραφή του προϊόντος
                _context.Products.Remove(product);

                await _context.SaveChangesAsync();
            }

            return RedirectToAction("GetProducts");
        }


        private bool ProductExists(int id)
        {
            return _context.Products.Any(e => e.ProductId == id);
        }
    }
}
